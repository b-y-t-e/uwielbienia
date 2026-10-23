using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Uwielbienia.Core.AppWindows;
using Uwielbienia.Core;

namespace Uwielbienia.App.Services;

/// <summary>
/// Obraz okna innego programu na rzutniku: klatki z <see cref="WindowCaptureSession"/> trafiają do <see cref="Frames"/>,
/// które rysuje <see cref="Views.VideoSurface"/> (jak film — z „Dopasuj obraz” i przenikaniem). Osobny wątek szuka okna
/// co sekundę: nie ma programu — czarny ekran, pojawi się (albo zostanie uruchomiony ponownie) — od razu jego obraz.
/// Zminimalizowane okno nie rysuje, więc pokazywane okno wraca na pulpit, pod inne okna. Na ekran trafia sama treść okna
/// (bez paska tytułu i ramek — <see cref="NativeWindows.Content"/>).
/// </summary>
public sealed class AppWindowCapture(IErrorLog errors) : IAppWindowCapture, IDisposable
{
    private static readonly TimeSpan SearchInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan FrameWait = TimeSpan.FromMilliseconds(8);

    /// <summary>
    /// Ostatni obraz zostaje po zejściu na czas przejścia do następnego slajdu: przejście robi zrzut starej strony dopiero,
    /// gdy nowa jest gotowa — obraz wczytuje się do 2 s (<c>IWaitablePage</c>), na słabszym komputerze dłużej, a efekt
    /// trwa swoje. Zapas nic nie kosztuje (jedna bitmapa w pamięci); ponowne <see cref="Show"/> i tak czyści obraz od razu.
    /// </summary>
    private static readonly TimeSpan ClearAfterStop = TimeSpan.FromSeconds(5);

    /// <summary>Szersze okno (np. 4K) — co drugi piksel: rzutnik i tak ma zwykle 1920 px, a kopia jest 4× mniejsza.</summary>
    private const int MaxWidth = 2880;

    private readonly object _gate = new();
    private AppWindowTarget? _target;
    private CancellationTokenSource? _running;
    private int _generation; // zmienia się przy każdym Show / Stop; stare pokazywanie nie rusza już obrazu ani stanu
    private WriteableBitmap? _bitmap;

    /// <summary>Klatki pokazywanego okna — wspólne dla rzutnika i kolumny „Na ekranie”.</summary>
    public VideoFrames Frames { get; } = new();

    public string? Unavailable =>
        OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041) ? null : "Pokazywanie aplikacji działa tylko w Windows 10 (2004) i nowszym.";

    public AppWindowInfo? Shown { get; private set; }

    /// <summary>Dlaczego okna nie da się pokazać, choć program może działać (np. brak sprzętowej grafiki); <c>null</c> = brak błędu.</summary>
    public string? Failure { get; private set; }

    public event EventHandler? ShownChanged;

    public IReadOnlyList<AppWindowInfo> Windows() => Unavailable is null ? NativeWindows.List() : [];

    public void Show(AppWindowTarget target)
    {
        if (Unavailable is not null)
            return;
        lock (_gate)
        {
            if (_target == target && _running is not null)
                return;
            var generation = Restart(target);
            var running = _running = new CancellationTokenSource();
            new Thread(() => Run(target, generation, running.Token)) { IsBackground = true, Name = "Przechwytywanie okna" }.Start();
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            var generation = Restart(null, clearFrames: false);
            _running = null;
            // Ostatnia klatka zostaje na czas przenikania do następnego slajdu (inaczej przenikanie szłoby przez czerń).
            DispatcherTimer.RunOnce(() => ClearIfStill(generation), ClearAfterStop);
        }
    }

    private void ClearIfStill(int generation)
    {
        lock (_gate)
            if (generation == _generation)
                ClearFrames();
    }

    public void Dispose() => Stop();

    /// <summary>Kończy bieżące pokazywanie: czarny ekran i brak programu, dopóki nowe nie znajdzie okna (wywołanie pod <see cref="_gate"/>).</summary>
    private int Restart(AppWindowTarget? target, bool clearFrames = true)
    {
        _running?.Cancel();
        _target = target;
        var generation = ++_generation;
        if (clearFrames)
            ClearFrames();
        Publish(null, null, generation);
        return generation;
    }

    private void Run(AppWindowTarget target, int generation, CancellationToken stop)
    {
        WindowCaptureSession? session = null;
        AppWindowInfo? window = null;
        ContentArea? content = null;
        var checkedAt = Stopwatch.StartNew();
        string? lastFailure = null;
        var failureShown = false;
        // Położenie treści okna w pikselach ekranu, jak obraz z przechwytywania (niezależnie od skalowania programu).
        NativeWindows.UsePhysicalPixels();
        try
        {
            while (!stop.IsCancellationRequested)
            {
                try
                {
                    Step();
                }
                catch (Exception ex)
                {
                    // Wątek tła nie może zamknąć aplikacji: zwalnia sesję, pokazuje przyczynę i próbuje znowu za sekundę.
                    session?.Dispose();
                    session = null;
                    window = null;
                    if (ex.Message != lastFailure)
                        errors.Report(ex);
                    lastFailure = ex.Message;
                    failureShown = true;
                    ShowWindow(null, generation, $"Nie udało się pokazać okna: {ex.Message}");
                    stop.WaitHandle.WaitOne(SearchInterval);
                }
            }
        }
        finally
        {
            session?.Dispose();
        }

        void Step()
        {
            if (session is null || checkedAt.Elapsed >= SearchInterval)
            {
                checkedAt.Restart();
                if (window is not null && !NativeWindows.Exists(window.Handle))
                {
                    session?.Dispose();
                    session = null;
                    window = null;
                    ShowWindow(null, generation);
                }
                if (window is null)
                {
                    if (target.FirstMatch(NativeWindows.List()) is { } found)
                    {
                        session = TryStart(found);
                        window = found;
                        ShowWindow(found, generation);
                    }
                    else if (failureShown)
                        ShowWindow(null, generation); // program zamknięty po błędzie — „nie jest uruchomiony”, nie stary błąd
                    failureShown = false;
                }
                if (window is not null)
                {
                    NativeWindows.RestoreBehind(window.Handle);
                    content = NativeWindows.Content(window.Handle);
                }
            }

            if (session is null || !TryRead(session, content, generation))
                stop.WaitHandle.WaitOne(session is null ? SearchInterval : FrameWait);
        }
    }

    private static WindowCaptureSession? TryStart(AppWindowInfo window)
    {
        NativeWindows.RestoreBehind(window.Handle);
        // Błąd (np. brak sprzętowego Direct3D 11) obsługuje Run: zapis w dzienniku, przyczyna dla operatora, próba za sekundę.
        return new WindowCaptureSession(window.Handle);
    }

    /// <remarks>Błąd odczytu (np. utrata urządzenia) obsługuje Run: zwalnia sesję i za sekundę tworzy nową.</remarks>
    private unsafe bool TryRead(WindowCaptureSession session, ContentArea? content, int generation) =>
        session.TryRead((pixels, pitch, width, height) => Write(pixels, pitch, width, height, content, generation));

    /// <summary>Kopiuje klatkę do wspólnej bitmapy — pod <see cref="_gate"/> i tylko dla bieżącego pokazywania.</summary>
    private unsafe void Write(byte* pixels, int pitch, int width, int height, ContentArea? content, int generation)
    {
        if (content is { } area)
        {
            var left = (int)Math.Round(area.Left * width);
            var top = (int)Math.Round(area.Top * height);
            var right = (int)Math.Round(area.Right * width);
            var bottom = (int)Math.Round(area.Bottom * height);
            if (right - left >= 16 && bottom - top >= 16)
            {
                pixels += (long)top * pitch + left * 4L;
                (width, height) = (right - left, bottom - top);
            }
        }
        var step = width > MaxWidth ? 2 : 1;
        var size = new PixelSize(width / step, height / step);
        lock (_gate)
        {
            if (generation != _generation)
                return;
            if (_bitmap is null || _bitmap.PixelSize != size)
            {
                _bitmap = new WriteableBitmap(size, new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
                Frames.Reset(_bitmap);
            }
            using (var buffer = _bitmap.Lock())
            {
                for (var y = 0; y < size.Height; y++)
                {
                    var source = pixels + (long)y * step * pitch;
                    var target = (byte*)buffer.Address + (long)y * buffer.RowBytes;
                    if (step == 1)
                        Buffer.MemoryCopy(source, target, buffer.RowBytes, size.Width * 4L);
                    else
                        for (var x = 0; x < size.Width; x++)
                            ((uint*)target)[x] = ((uint*)source)[x * step];
                }
            }
        }
        Frames.NotifyFrame();
    }

    /// <summary>Nowe pokazywane okno (<c>null</c> = czarny ekran) — tylko gdy to wciąż bieżące pokazywanie.</summary>
    private void ShowWindow(AppWindowInfo? window, int generation, string? failure = null)
    {
        lock (_gate)
        {
            if (generation != _generation)
                return;
            if (window is null)
                ClearFrames();
            Publish(window, failure, generation);
        }
    }

    private void ClearFrames()
    {
        _bitmap = null;
        Frames.Reset(null);
    }

    /// <summary>Stan dla widoków (wątek interfejsu); sprawdzany tam ponownie — późniejsze pokazywanie wygrywa.</summary>
    private void Publish(AppWindowInfo? window, string? failure, int generation) => Dispatcher.UIThread.Post(() =>
    {
        if (generation != Volatile.Read(ref _generation) || (Shown == window && Failure == failure))
            return;
        Shown = window;
        Failure = failure;
        ShownChanged?.Invoke(this, EventArgs.Empty);
    });
}

/// <summary>Treść okna jako ułamki szerokości i wysokości jego obrazu (0..1).</summary>
internal readonly record struct ContentArea(double Left, double Top, double Right, double Bottom);

/// <summary>Okna pulpitu (Win32): lista do wyboru, sprawdzenie istnienia, przywrócenie zminimalizowanego.</summary>
internal static class NativeWindows
{
    public static IReadOnlyList<AppWindowInfo> List()
    {
        var own = Environment.ProcessId;
        var names = new Dictionary<int, string?>();
        var windows = new List<AppWindowInfo>();
        EnumWindows((handle, _) =>
        {
            if (IsCandidate(handle) && GetWindowThreadProcessId(handle, out var pid) != 0 && pid != own)
            {
                if (!names.TryGetValue(pid, out var process))
                    names[pid] = process = ProcessName(pid);
                if (process is not null)
                    windows.Add(new AppWindowInfo(handle, process, Title(handle)));
            }
            return true;
        }, 0);
        return windows;
    }

    public static bool Exists(nint handle) => IsWindow(handle) && IsWindowVisible(handle);

    /// <summary>Współrzędne w pikselach ekranu (jak obraz z przechwytywania) dla bieżącego wątku.</summary>
    public static void UsePhysicalPixels() => SetThreadDpiAwarenessContext(PerMonitorAwareV2);

    /// <summary>
    /// Gdzie w obrazie okna (granice widocznej ramki — to, co daje przechwytywanie) leży jego treść: obszar klienta, a dla
    /// aplikacji ze Sklepu okno z jej treścią (pasek tytułu rysuje wokół niej <c>ApplicationFrameHost</c>). <c>null</c> = całe okno.
    /// </summary>
    public static ContentArea? Content(nint handle)
    {
        if (DwmGetWindowAttribute(handle, ExtendedFrameBounds, out Rect frame, Marshal.SizeOf<Rect>()) != 0)
            return null;
        Rect content;
        var store = FindWindowEx(handle, 0, "Windows.UI.Core.CoreWindow", null);
        if (store != 0)
        {
            if (!GetWindowRect(store, out content))
                return null;
            // Aplikacja ze Sklepu rysuje pod paskiem tytułu — treść zaczyna się pod nim (pełny ekran: pasek ukryty).
            for (var bar = FindWindowEx(handle, 0, "ApplicationFrameTitleBarWindow", null); bar != 0;
                 bar = FindWindowEx(handle, bar, "ApplicationFrameTitleBarWindow", null))
                if (IsWindowVisible(bar) && GetWindowRect(bar, out var title) && title.Right > title.Left && title.Bottom > title.Top)
                    content = content with { Top = Math.Max(content.Top, title.Bottom) };
        }
        else
        {
            var origin = new Point();
            if (!GetClientRect(handle, out var client) || !ClientToScreen(handle, ref origin))
                return null;
            content = new Rect(origin.X, origin.Y, origin.X + client.Right, origin.Y + client.Bottom);
        }
        double width = frame.Right - frame.Left, height = frame.Bottom - frame.Top;
        if (width <= 0 || height <= 0)
            return null;
        static double Clamp(double value) => Math.Clamp(value, 0, 1);
        return new ContentArea(
            Clamp((content.Left - frame.Left) / width),
            Clamp((content.Top - frame.Top) / height),
            Clamp((content.Right - frame.Left) / width),
            Clamp((content.Bottom - frame.Top) / height));
    }

    /// <summary>Zminimalizowane okno nie rysuje — wraca na pulpit bez aktywacji, pod inne okna (operator go nie zobaczy).</summary>
    public static void RestoreBehind(nint handle)
    {
        if (!IsIconic(handle))
            return;
        ShowWindow(handle, ShowNoActivate);
        SetWindowPos(handle, Bottom, 0, 0, 0, 0, NoSize | NoMove | NoActivate);
    }

    /// <summary>Zwykłe okno programu: widoczne, z tytułem, bez właściciela, nie narzędziowe i nie ukryte przez system.</summary>
    private static bool IsCandidate(nint handle)
    {
        if (!IsWindowVisible(handle) || GetWindow(handle, Owner) != 0 || GetWindowTextLength(handle) == 0)
            return false;
        if ((GetWindowLongPtr(handle, ExStyle) & ToolWindow) != 0)
            return false;
        if (DwmGetWindowAttribute(handle, Cloaked, out int cloaked, sizeof(int)) == 0 && cloaked != 0)
            return false;
        var name = new StringBuilder(64);
        GetClassName(handle, name, name.Capacity);
        return name.ToString() != "Progman";
    }

    private static string Title(nint handle)
    {
        var text = new StringBuilder(GetWindowTextLength(handle) + 1);
        GetWindowText(handle, text, text.Capacity);
        return text.ToString();
    }

    private static string? ProcessName(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return process.ProcessName;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    private const int ExtendedFrameBounds = 9;
    private static readonly nint PerMonitorAwareV2 = -4;
    private const uint Owner = 4;
    private const int ExStyle = -20;
    private const long ToolWindow = 0x80;
    private const int Cloaked = 14;
    private const int ShowNoActivate = 4;
    private static readonly nint Bottom = 1;
    private const uint NoSize = 0x1;
    private const uint NoMove = 0x2;
    private const uint NoActivate = 0x10;

    private delegate bool EnumWindowsProc(nint handle, nint parameter);

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct Rect(int Left, int Top, int Right, int Bottom);

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern nint SetThreadDpiAwarenessContext(nint context);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint FindWindowEx(nint parent, nint after, string? className, string? title);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(nint handle, out Rect rect);

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(nint handle, out Rect rect);

    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(nint handle, ref Point point);

    [DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")]
    private static extern int DwmGetWindowAttribute(nint handle, int attribute, out Rect value, int size);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, nint parameter);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(nint handle);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(nint handle);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(nint handle);

    [DllImport("user32.dll")]
    private static extern nint GetWindow(nint handle, uint command);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern long GetWindowLongPtr(nint handle, int index);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(nint handle);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(nint handle, StringBuilder text, int maxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(nint handle, StringBuilder name, int maxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint handle, out int processId);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint handle, int command);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(nint handle, nint after, int x, int y, int width, int height, uint flags);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(nint handle, int attribute, out int value, int size);
}
