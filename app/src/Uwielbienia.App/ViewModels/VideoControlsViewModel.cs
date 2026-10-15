using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Threading;
using Uwielbienia.App.Services;
using Uwielbienia.Core.Videos;

namespace Uwielbienia.App.ViewModels;

/// <summary>Film w kolumnie „Na ekranie” i na rzutniku: podgląd, pauza, ±15 s, przewijanie, czas i obraz względem dźwięku.</summary>
public sealed partial class VideoControlsViewModel : ObservableObject
{
    private readonly IVideoPlayer _player;
    private readonly FilmRemote _film;
    private readonly DispatcherTimer _promptTimer = new() { Interval = FilmRemote.PromptTime };

    public VideoControlsViewModel(IVideoPlayer player, VideoFrames frames, FilmRemote film)
    {
        _player = player;
        _film = film;
        Frames = frames;
        player.StatusChanged += (_, _) => Refresh();
        // Podpowiedź pilota (długo „do tyłu”) znika sama po kilku sekundach bez drugiego naciśnięcia.
        _promptTimer.Tick += (_, _) => film.CancelPrompt();
        film.PromptChanged += (_, _) =>
        {
            _promptTimer.Stop();
            if (film.IsPrompting)
                _promptTimer.Start();
            OnPropertyChanged(nameof(IsPromptVisible));
        };
    }

    /// <summary>Po długim „do tyłu” na pilocie: co zrobi drugie długie naciśnięcie.</summary>
    public bool IsPromptVisible => _film.IsPrompting;

    public VideoFrames Frames { get; }

    public bool IsPlaying => _player.Status.IsPlaying;

    public string? Error => _player.Status.Error;

    public double DurationSeconds => Math.Max(1, _player.Status.Duration.TotalSeconds);

    /// <summary>Suwak: zmiana przez operatora przewija film (stan odtwarzacza tylko go przesuwa).</summary>
    public double PositionSeconds
    {
        get => _player.Status.Position.TotalSeconds;
        set
        {
            // Dopóki VLC nie poda długości, suwak ma maksimum 1 i przycina wartość — to nie jest przewinięcie
            // (inaczej powrót do filmu przerwanego w 2:00 zaczynałby od 0:01).
            if (_player.Status.Duration > TimeSpan.Zero && Math.Abs(value - PositionSeconds) > 0.5)
                _player.Seek(TimeSpan.FromSeconds(value));
        }
    }

    public string TimeText => $"{PositionText} / {DurationText}";

    public string PositionText => Format(_player.Status.Position);

    public string DurationText => Format(_player.Status.Duration);

    [RelayCommand]
    private void TogglePause() => _player.TogglePause();

    public static readonly TimeSpan SkipStep = TimeSpan.FromSeconds(15);

    [RelayCommand]
    private void SkipBack() => _player.Skip(-SkipStep);

    [RelayCommand]
    private void SkipForward() => _player.Skip(SkipStep);

    /// <summary>Obraz względem dźwięku, np. „+1,6 s” (obraz później), „−0,3 s”, „0,0 s”.</summary>
    public string PictureOffsetText => FormatOffset(_player.PictureOffsetMs());

    public bool HasPictureOffset => _player.PictureOffset != TimeSpan.Zero;

    [RelayCommand]
    private void PictureEarlier() => _film.ShiftPicture(-VideoPlayerExtensions.PictureStep);

    [RelayCommand]
    private void PictureLater() => _film.ShiftPicture(VideoPlayerExtensions.PictureStep);

    /// <summary>Kliknięcie w wartość: obraz znów razem z dźwiękiem.</summary>
    [RelayCommand]
    private void ResetPictureOffset() => _player.PictureOffset = TimeSpan.Zero;

    public static string FormatOffset(int ms)
    {
        var seconds = (Math.Abs(ms) / 1000.0).ToString("0.0", PolishNumbers);
        return ms switch { > 0 => $"+{seconds} s", < 0 => $"−{seconds} s", _ => $"{seconds} s" };
    }

    private static readonly System.Globalization.CultureInfo PolishNumbers = System.Globalization.CultureInfo.GetCultureInfo("pl-PL");

    private static string Format(TimeSpan time) =>
        time.TotalHours >= 1 ? time.ToString(@"h\:mm\:ss") : time.ToString(@"m\:ss");

    private void Refresh()
    {
        OnPropertyChanged(nameof(IsPlaying));
        OnPropertyChanged(nameof(Error));
        OnPropertyChanged(nameof(DurationSeconds));
        OnPropertyChanged(nameof(PositionSeconds));
        OnPropertyChanged(nameof(TimeText));
        OnPropertyChanged(nameof(PositionText));
        OnPropertyChanged(nameof(DurationText));
        OnPropertyChanged(nameof(PictureOffsetText));
        OnPropertyChanged(nameof(HasPictureOffset));
    }
}
