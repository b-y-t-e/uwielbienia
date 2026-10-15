using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using LibVLCSharp.Shared;
using Uwielbienia.Core.Videos;

namespace Uwielbienia.App.Services;

/// <summary>
/// Odtwarzacz filmów na LibVLC: każdy format, który zna VLC (mp4, mkv, avi, mov…), dźwięk na domyślne
/// urządzenie. Obraz nie trafia do natywnego okna, tylko klatka po klatce do <see cref="VideoFrames"/>
/// (BGRA, najwyżej 1920 px szerokości). VLC wczytuje się dopiero przy pierwszym filmie.
/// </summary>
public sealed class VlcVideoPlayer : IVideoPlayer, IDisposable
{
    private const uint MaxWidth = 1920;

    private readonly VideoFrames _frames;
    // Wywołania zwrotne trzymane w polach — VLC woła je z kodu natywnego, nie mogą zniknąć przez GC.
    private readonly MediaPlayer.LibVLCVideoFormatCb _format;
    private readonly MediaPlayer.LibVLCVideoCleanupCb _cleanup;
    private readonly MediaPlayer.LibVLCVideoLockCb _lock;
    private readonly MediaPlayer.LibVLCVideoUnlockCb _unlock;
    private readonly MediaPlayer.LibVLCVideoDisplayCb _display;
    private LibVLC? _libVlc;
    private MediaPlayer? _player;
    private string? _initError;
    private WriteableBitmap? _bitmap;
    private ILockedFramebuffer? _locked;
    private IntPtr _scratch;
    private int _generation;

    // Stan zamówiony przez aplikację (wątek UI). Zdarzenia VLC przychodzą z opóźnieniem — „Playing” po pauzie
    // ustawionej zaraz po starcie (np. czarny ekran) nie może jej cofnąć, a czas i koniec liczą się dopiero od
    // faktycznego startu bieżącego odtworzenia (nie od spóźnionych zdarzeń poprzedniego).
    private bool _wantPlaying;
    private bool _started;

    public VlcVideoPlayer(VideoFrames frames)
    {
        _frames = frames;
        _format = OnFormat;
        _cleanup = OnCleanup;
        _lock = OnLock;
        _unlock = OnUnlock;
        _display = OnDisplay;
    }

    public VideoStatus Status { get; private set; } = VideoStatus.Stopped;

    public string? Unavailable => Player() is null ? _initError : null;

    public bool IsHeld { get; set; }

    public TimeSpan PictureOffset
    {
        get => _pictureOffset;
        set
        {
            if (_pictureOffset == value)
                return;
            _pictureOffset = value;
            ApplyPictureOffset();
            StatusChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private TimeSpan _pictureOffset;

    /// <summary>
    /// VLC umie opóźnić tylko dźwięk, więc obraz później o X = dźwięk wcześniej o X (opóźnienie dźwięku −X, w µs).
    /// VLC zeruje je przy każdym nowym pliku — stąd ponowne ustawienie po starcie odtwarzania.
    /// </summary>
    private void ApplyPictureOffset() => _player?.SetAudioDelay(-(long)(_pictureOffset.TotalMilliseconds * 1000));

    public event EventHandler? StatusChanged;

    public void Open(string file, TimeSpan start = default)
    {
        if (!File.Exists(file))
        {
            // Przejście film → film nie zatrzymuje poprzedniego (nowy go zastępuje) — tu nowego nie będzie, więc
            // poprzedni musi zamilknąć, inaczej grałby pod slajdem nowego bez możliwości zatrzymania.
            Interlocked.Increment(ref _generation);
            _wantPlaying = false;
            _player?.Stop();
            _frames.Reset(null);
            SetStatus(new VideoStatus(file, false, TimeSpan.Zero, TimeSpan.Zero, Error: $"Nie ma pliku: {file}"));
            return;
        }
        if (Player() is not { } player)
        {
            SetStatus(new VideoStatus(file, false, TimeSpan.Zero, TimeSpan.Zero, Error: _initError));
            return;
        }
        PlayFrom(player, file, start);
        SetStatus(new VideoStatus(file, true, start, TimeSpan.Zero));
    }

    /// <summary>
    /// Nowe odtworzenie pliku od <paramref name="start"/> (opcja <c>:start-time</c> dotyczy tylko tego odtworzenia,
    /// więc „od nowa” po końcu filmu naprawdę zaczyna od 0:00). Zdarzenia poprzedniego odtworzenia są od tej chwili
    /// odrzucane (<see cref="_generation"/>).
    /// </summary>
    private void PlayFrom(MediaPlayer player, string file, TimeSpan start)
    {
        Interlocked.Increment(ref _generation);
        _wantPlaying = true;
        _started = false;
        using var media = new Media(_libVlc!, file, FromType.FromPath);
        if (start > TimeSpan.Zero)
            media.AddOption(":start-time=" + start.TotalSeconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
        player.Play(media);
    }

    public void Stop()
    {
        Interlocked.Increment(ref _generation);
        _wantPlaying = false;
        _player?.Stop();
        SetStatus(VideoStatus.Stopped);
        // Ostatnia klatka zostaje na czas przenikania do następnego slajdu (inaczej przenikanie szłoby przez czerń);
        // potem znika — chyba że w tym czasie ruszył inny film i ma już własną klatkę.
        var last = _frames.Bitmap;
        DispatcherTimer.RunOnce(() =>
        {
            if (Status.File is null && ReferenceEquals(_frames.Bitmap, last))
                _frames.Reset(null);
        }, ClearAfterStop);
    }

    /// <summary>Dłużej niż przenikanie slajdów (0,15 s w <c>SlideView</c>).</summary>
    private static readonly TimeSpan ClearAfterStop = TimeSpan.FromMilliseconds(400);

    public void Pause()
    {
        if (_player is null || Status.File is null)
            return;
        _wantPlaying = false;
        _player.SetPause(true);
        SetStatus(Status with { IsPlaying = false });
    }

    public void Resume()
    {
        if (_player is null || IsHeld || Status is not { File: not null, Error: null })
            return;
        if (Status.HasEnded)
        {
            // Po końcu filmu — od początku (nowe odtworzenie, bez dawnego miejsca startu).
            PlayFrom(_player, Status.File, TimeSpan.Zero);
            SetStatus(Status with { IsPlaying = true, HasEnded = false, Position = TimeSpan.Zero });
            return;
        }
        _wantPlaying = true;
        _player.SetPause(false);
        SetStatus(Status with { IsPlaying = true, HasEnded = false });
    }

    public void Seek(TimeSpan position)
    {
        if (_player is null || Status is not { File: not null, Error: null })
            return;
        if (Status.HasEnded && IsHeld)
        {
            // czarny ekran — tylko zapamiętaj miejsce, odtwarzanie ruszy po powrocie obrazu
            SetStatus(Status with { Position = position });
            return;
        }
        if (Status.HasEnded)
        {
            // po końcu filmu VLC nie przewija — nowe odtworzenie od wskazanego miejsca
            PlayFrom(_player, Status.File, position);
            SetStatus(Status with { IsPlaying = true, HasEnded = false, Position = position });
            return;
        }
        _player.Time = (long)position.TotalMilliseconds;
        SetStatus(Status with { Position = position });
    }

    public void Dispose()
    {
        _player?.Stop();
        _player?.Dispose();
        _libVlc?.Dispose();
        if (_scratch != IntPtr.Zero)
            Marshal.FreeHGlobal(_scratch);
    }

    private MediaPlayer? Player()
    {
        if (_player is not null || _initError is not null)
            return _player;
        try
        {
            // Folder VLC z instalacji wskazany wprost — w .exe z jednego pliku biblioteka nie ma swojej ścieżki.
            var bundled = Path.Combine(AppContext.BaseDirectory, "libvlc", "win-x64");
            if (OperatingSystem.IsWindows() && Directory.Exists(bundled))
                LibVLCSharp.Shared.Core.Initialize(bundled);
            else
                LibVLCSharp.Shared.Core.Initialize();
            _libVlc = new LibVLC("--no-osd", "--no-video-title-show", "--quiet");
            var player = new MediaPlayer(_libVlc);
            player.SetVideoFormatCallbacks(_format, _cleanup);
            player.SetVideoCallbacks(_lock, _unlock, _display);
            // Zdarzenia VLC przychodzą z jego wątków; nie wolno z nich wołać odtwarzacza — tylko stan na wątek UI.
            player.LengthChanged += (_, e) => Post(s => s with { Duration = TimeSpan.FromMilliseconds(e.Length) });
            player.TimeChanged += (_, e) => Post(s => _started ? s with { Position = TimeSpan.FromMilliseconds(e.Time) } : s);
            player.Playing += (_, _) => Post(s =>
            {
                _started = true;
                ApplyPictureOffset();
                if (_wantPlaying)
                    return s with { IsPlaying = true, HasEnded = false };
                // VLC ruszył po pauzie zamówionej przed startem — zatrzymaj go znowu, stan bez zmian
                _player?.SetPause(true);
                return s;
            });
            player.Paused += (_, _) => Post(s => _wantPlaying ? s : s with { IsPlaying = false });
            player.EndReached += (_, _) => Post(s => _started ? s with { IsPlaying = false, HasEnded = true, Position = s.Duration } : s);
            player.EncounteredError += (_, _) => Post(s => s with { IsPlaying = false, Error = "Nie udało się odtworzyć filmu." });
            _player = player;
        }
        catch (Exception ex) when (ex is VLCException or DllNotFoundException or BadImageFormatException or InvalidOperationException)
        {
            _initError = OperatingSystem.IsWindows()
                ? "Brak odtwarzacza VLC w instalacji programu."
                : "Zainstaluj VLC (np. sudo apt install vlc), żeby odtwarzać filmy.";
        }
        return _player;
    }

    private void Post(Func<VideoStatus, VideoStatus> change)
    {
        // Numer odtworzenia z chwili zdarzenia (wątek VLC): spóźnione zdarzenie poprzedniego filmu (albo odtworzenia
        // sprzed „od nowa”) nie zmieni pozycji ani końca bieżącego.
        var generation = Volatile.Read(ref _generation);
        Dispatcher.UIThread.Post(() =>
        {
            if (Status.File is not null && generation == Volatile.Read(ref _generation))
                SetStatus(change(Status));
        });
    }

    private void SetStatus(VideoStatus status)
    {
        Status = status;
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>VLC podaje wymiary filmu; zamawiamy BGRA (RV32), zmniejszone do 1920 px szerokości.</summary>
    private uint OnFormat(ref IntPtr opaque, IntPtr chroma, ref uint width, ref uint height, ref uint pitches, ref uint lines)
    {
        Marshal.Copy("RV32"u8.ToArray(), 0, chroma, 4);
        if (width > MaxWidth)
        {
            height = Math.Max(2, height * MaxWidth / width) & ~1u;
            width = MaxWidth;
        }
        var bitmap = new WriteableBitmap(new PixelSize((int)width, (int)height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
        using (var buffer = bitmap.Lock())
            pitches = (uint)buffer.RowBytes;
        lines = height;
        _bitmap = bitmap;
        _frames.Reset(bitmap);
        return 1;
    }

    private void OnCleanup(ref IntPtr opaque) => _bitmap = null;

    /// <summary>VLC dekoduje klatkę wprost do pamięci bitmapy — zablokowanej, żeby rzutnik nie narysował połowy klatki.</summary>
    private IntPtr OnLock(IntPtr opaque, IntPtr planes)
    {
        if (_bitmap is { } bitmap)
        {
            _locked = bitmap.Lock();
            Marshal.WriteIntPtr(planes, _locked.Address);
        }
        else
        {
            // Klatka po wyczyszczeniu obrazu — VLC musi gdzieś pisać.
            if (_scratch == IntPtr.Zero)
                _scratch = Marshal.AllocHGlobal(4 * 1920 * 1088);
            Marshal.WriteIntPtr(planes, _scratch);
        }
        return IntPtr.Zero;
    }

    private void OnUnlock(IntPtr opaque, IntPtr picture, IntPtr planes)
    {
        _locked?.Dispose();
        _locked = null;
    }

    private void OnDisplay(IntPtr opaque, IntPtr picture) => _frames.NotifyFrame();
}
