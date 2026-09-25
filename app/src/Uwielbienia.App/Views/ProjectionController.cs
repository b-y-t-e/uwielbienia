using Avalonia.Controls;
using Avalonia.Platform;
using Uwielbienia.App.Services;

namespace Uwielbienia.App.Views;

/// <summary>
/// Pilnuje okna projekcji: otwiera je na monitorze innym niż okno operatora, przenosi przy podłączeniu
/// rzutnika lub przesunięciu okna operatora, zamyka, gdy monitor zniknie.
/// </summary>
public sealed class ProjectionController(Func<ProjectionWindow> createWindow) : IProjectionController
{
    private Window? _operator;
    private ProjectionWindow? _window;
    private Screen? _screen;
    private bool _closedByUser;

    public ProjectionMode Mode { get; private set; } = ProjectionMode.Off;

    public bool HasSecondScreen => OtherScreen() is not null;

    public string Status => Mode switch
    {
        ProjectionMode.SecondScreen => $"Projekcja: {Describe(_screen)}",
        ProjectionMode.SameScreen => "Projekcja na tym ekranie — Esc wraca",
        _ when HasSecondScreen => "Projekcja wyłączona — F5 włącza",
        _ => "Brak drugiego ekranu — F5 pokazuje na tym",
    };

    public event EventHandler? Changed;

    /// <summary>Wywoływane raz, gdy okno operatora jest już widoczne.</summary>
    public void Attach(Window operatorWindow)
    {
        _operator = operatorWindow;
        operatorWindow.Screens.Changed += (_, _) => Reevaluate();
        operatorWindow.PositionChanged += (_, _) => Reevaluate();
        operatorWindow.Closed += (_, _) => _window?.Close();
        Reevaluate();
    }

    public void Toggle()
    {
        if (Mode != ProjectionMode.Off)
        {
            _closedByUser = true;
            Close();
            return;
        }
        _closedByUser = false;
        if (OtherScreen() is { } other)
            OpenOn(other, ProjectionMode.SecondScreen);
        else
            ShowOnSameScreen();
    }

    public void ShowOnSameScreen()
    {
        if (_operator is null)
            return;
        OpenOn(_operator.Screens.ScreenFromWindow(_operator) ?? _operator.Screens.Primary, ProjectionMode.SameScreen);
        _window?.Activate();
    }

    public void Close()
    {
        var window = _window;
        _window = null;
        _screen = null;
        window?.Close();
        SetMode(ProjectionMode.Off);
        _operator?.Activate();
    }

    private void Reevaluate()
    {
        var other = OtherScreen();
        switch (Mode)
        {
            case ProjectionMode.Off when other is not null && !_closedByUser:
                OpenOn(other, ProjectionMode.SecondScreen);
                break;
            case ProjectionMode.SecondScreen when other is null:
                Close();
                break;
            case ProjectionMode.SecondScreen when !Equals(other, _screen):
                OpenOn(other!, ProjectionMode.SecondScreen);
                break;
            default:
                Changed?.Invoke(this, EventArgs.Empty);
                break;
        }
    }

    private Screen? OtherScreen()
    {
        if (_operator?.Screens is not { } screens)
            return null;
        var current = screens.ScreenFromWindow(_operator);
        return screens.All.Where(s => !Equals(s, current))
            .OrderByDescending(s => s.Bounds.Width * s.Bounds.Height)
            .FirstOrDefault();
    }

    private void OpenOn(Screen? screen, ProjectionMode mode)
    {
        if (screen is null)
            return;
        if (_window is null)
        {
            _window = createWindow();
            _window.Closed += (_, _) =>
            {
                if (_window is not null)
                {
                    _window = null;
                    _screen = null;
                    SetMode(ProjectionMode.Off);
                }
            };
        }

        _window.WindowState = WindowState.Normal;
        _window.Position = screen.WorkingArea.Position;
        if (!_window.IsVisible)
            _window.Show();
        _window.WindowState = WindowState.FullScreen;
        _window.IsSameScreen = mode == ProjectionMode.SameScreen;
        _screen = screen;
        SetMode(mode);
        if (mode == ProjectionMode.SecondScreen)
            _operator?.Activate();
    }

    private void SetMode(ProjectionMode mode)
    {
        Mode = mode;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static string Describe(Screen? screen) =>
        screen is null ? "" : string.IsNullOrWhiteSpace(screen.DisplayName)
            ? $"{screen.Bounds.Width}×{screen.Bounds.Height}"
            : screen.DisplayName;
}
