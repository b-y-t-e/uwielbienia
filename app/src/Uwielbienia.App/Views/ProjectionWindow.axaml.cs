using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Uwielbienia.App.Services;
using Uwielbienia.App.ViewModels;
using Uwielbienia.Core.Presentation;

namespace Uwielbienia.App.Views;

public partial class ProjectionWindow : Window
{
    /// <summary>Przesunięcie mniejsze niż to = kliknięcie (w trybie jednego ekranu: dalej / wstecz), nie przeciąganie.</summary>
    private const double ClickSlop = 6;

    private readonly DispatcherTimer _hideHandles = new() { Interval = TimeSpan.FromSeconds(3) };
    private Point? _lastPointer;
    private bool _handlesShown;

    public ProjectionWindow()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDownTunnel, RoutingStrategies.Tunnel);
        AddHandler(TextInputEvent, OnTextInputTunnel, RoutingStrategies.Tunnel);
        Warped.SizeChanged += (_, _) => UpdateWarp();
        _hideHandles.Tick += (_, _) =>
        {
            _hideHandles.Stop();
            if (!IsDragging)
                SetHandlesShown(false);
        };
        UpdateCursor();
    }

    public ProjectionWindow(ProjectionViewModel viewModel) : this()
    {
        DataContext = viewModel;
        // Dopasowanie jest wspólne dla kolejnych okien projekcji — zamknięte okno musi się odpiąć.
        viewModel.Calibration.PropertyChanged += OnCalibrationChanged;
        Closed += (_, _) =>
        {
            viewModel.Calibration.PropertyChanged -= OnCalibrationChanged;
            _hideHandles.Stop();
        };
        UpdateWarp();
        UpdateHandles();
    }

    private void OnCalibrationChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ProjectionCalibration.Corners))
            UpdateWarp();
        else if (e.PropertyName is nameof(ProjectionCalibration.IsActive))
            UpdateHandles();
    }

    private ProjectionViewModel ViewModel => (ProjectionViewModel)DataContext!;

    private ProjectionCalibration Calibration => ViewModel.Calibration;

    /// <summary>Jedno urządzenie: widać przyciski i kursor; na drugim ekranie — tylko tekst.</summary>
    public bool IsSameScreen
    {
        get => SameScreenBar.IsVisible;
        set
        {
            SameScreenBar.IsVisible = value;
            UpdateCursor();
        }
    }

    /// <summary>Tryb kalibracji (K): siatka i uchwyty widoczne stale, aż do Esc albo ponownego K.</summary>
    private bool IsCalibrating => DataContext is ProjectionViewModel { Calibration.IsActive: true };

    /// <summary>Ramkę i narożniki widać: po ruchu myszą (przez 3 s), w trakcie przeciągania albo w trybie kalibracji.</summary>
    private bool HandlesVisible => _handlesShown || IsCalibrating || IsDragging;

    private void UpdateCursor() =>
        Cursor = IsSameScreen || HandlesVisible ? Cursor.Default : new Cursor(StandardCursorType.None);

    private void SetHandlesShown(bool shown)
    {
        _handlesShown = shown;
        UpdateHandles();
    }

    private void UpdateHandles()
    {
        Handles.IsVisible = HandlesVisible;
        UpdateCursor();
    }

    /// <summary>Jak w pps_viewer: ruch myszą nad obrazem pokazuje uchwyty i kursor na 3 s.</summary>
    private void ShowHandlesTemporarily()
    {
        SetHandlesShown(true);
        _hideHandles.Stop();
        _hideHandles.Start();
    }

    /// <summary>
    /// Cały obraz projekcji rozpięty perspektywicznie na 4 narożnikach (macierz z <see cref="Keystone"/>);
    /// poza obrazem — czerń, żeby rzutnik nie świecił obok.
    /// </summary>
    private void UpdateWarp()
    {
        if (DataContext is not ProjectionViewModel vm)
            return;
        var corners = vm.Calibration.Corners;
        double w = Warped.Bounds.Width, h = Warped.Bounds.Height;
        if (Keystone.IsFullScreen(corners) || w <= 0 || h <= 0)
        {
            Warped.RenderTransform = null;
            Screen.Background = null;
            return;
        }
        var quad = corners.Select(c => new CornerPoint(c.X * w, c.Y * h)).ToList();
        var m = Keystone.RectToQuad(w, h, quad);
        Warped.RenderTransform = new MatrixTransform(new Matrix(m[0], m[3], m[6], m[1], m[4], m[7], m[2], m[5], m[8]));
        Screen.Background = Brushes.Black;
    }

    // Dopasowanie obrazu myszą, wprost na ekranie projekcji (jak w pps_viewer). Narożniki bywają poza
    // ekranem w sali, więc chwyta się w dowolnym miejscu: przesuwa się najbliższy narożnik (z Shift —
    // cały obraz); kółko: wielkość, z Shift — szerokość, z Ctrl — wysokość.
    private int _dragCorner = -1;
    private bool _dragAll;
    private bool _dragMoved;
    private Point _dragStart;
    private IReadOnlyList<CornerPoint> _dragOrigin = Keystone.FullScreen;

    private bool IsDragging => _dragCorner >= 0 || _dragAll;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (ViewModel.IsQuickPickOpen || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;
        var p = e.GetPosition(Handles);
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            _dragAll = true;
        else
            Calibration.SelectedCorner = _dragCorner = NearestCorner(p);
        _dragMoved = false;
        _dragStart = p;
        _dragOrigin = Calibration.Corners;
        ShowHandlesTemporarily();
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var p = e.GetPosition(Handles);
        if (!IsDragging)
        {
            // „Ruchy” bez zmiany położenia (np. po przełączeniu trybu okna) nie pokazują uchwytów.
            if (_lastPointer is { } last && last != p && !ViewModel.IsQuickPickOpen)
                ShowHandlesTemporarily();
            _lastPointer = p;
            // wypełniony punkt = narożnik, który przesunie przeciąganie
            if (HandlesVisible)
                Calibration.SelectedCorner = NearestCorner(p);
            return;
        }
        if (!_dragMoved && Point.Distance(p, _dragStart) < ClickSlop)
            return;
        _dragMoved = true;
        var dx = (p.X - _dragStart.X) / Handles.Bounds.Width;
        var dy = (p.Y - _dragStart.Y) / Handles.Bounds.Height;
        if (_dragAll)
        {
            Calibration.TrySet(Keystone.Move(_dragOrigin, dx, dy));
            return;
        }
        var next = _dragOrigin.ToList();
        next[_dragCorner] = Keystone.Clamp(new CornerPoint(next[_dragCorner].X + dx, next[_dragCorner].Y + dy));
        Calibration.TrySet(next);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!IsDragging)
            return;
        var moved = _dragMoved;
        _dragCorner = -1;
        _dragAll = false;
        _dragMoved = false;
        e.Pointer.Capture(null);
        e.Handled = true;
        ShowHandlesTemporarily();
        if (moved)
            Calibration.Save();
        else if (IsSameScreen && !IsCalibrating)
            Tap(e.GetPosition(this).X);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (ViewModel.IsQuickPickOpen)
            return;
        // Shift+kółko bywa zamieniane przez system na przewijanie w poziomie.
        var delta = e.Delta.Y != 0 ? e.Delta.Y : e.Delta.X;
        if (delta == 0)
            return;
        var factor = delta > 0 ? 1.01 : 1 / 1.01;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            Calibration.Scale(factor, 1);
        else if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
            Calibration.Scale(1, factor);
        else
            Calibration.Scale(factor, factor);
        ShowHandlesTemporarily();
        e.Handled = true;
    }

    private int NearestCorner(Point p)
    {
        var corners = Handles.CornersInPixels();
        return Enumerable.Range(0, 4).MinBy(i => Point.Distance(corners[i], p));
    }

    /// <summary>Tryb jednego ekranu: kliknięcie w prawą połowę = dalej, w lewą = wstecz.</summary>
    private void Tap(double x)
    {
        if (x > Bounds.Width / 2)
            ViewModel.Control.Next();
        else
            ViewModel.Control.Previous();
    }

    private void OnKeyDownTunnel(object? sender, KeyEventArgs e)
    {
        var vm = ViewModel;
        if (vm.IsQuickPickOpen)
        {
            HandleQuickPickKey(vm, e);
            return;
        }
        if (HandleCalibrationKey(e))
            return;
        if (e.Key == Key.Escape && IsSameScreen)
        {
            vm.Projection.Close();
            e.Handled = true;
            return;
        }
        e.Handled = LiveKeyboard.Handle(e, vm.Control, vm.Projection);
    }

    /// <summary>
    /// K (przy widocznych uchwytach) włącza tryb kalibracji (jak w pps_viewer). Dopiero w nim działają:
    /// G — siatka, R — pełny obraz, Tab — następny narożnik, Ctrl+strzałki — narożnik o piksel (z Shift o 10),
    /// Esc / K — koniec. Poza trybem kalibracji litery służą szukaniu pieśni, więc np. „R” wpisane przy
    /// szukaniu „Ruah” nie skasuje dopasowania.
    /// </summary>
    private bool HandleCalibrationKey(KeyEventArgs e)
    {
        var calibration = Calibration;
        var plain = e.KeyModifiers == KeyModifiers.None;
        switch (e.Key)
        {
            case Key.K when plain && (IsCalibrating || CanStartCalibration):
                calibration.IsActive = !calibration.IsActive;
                break;
            case Key.Escape when IsCalibrating:
                calibration.IsActive = false;
                break;
            case Key.G when plain && IsCalibrating:
                calibration.ShowGrid = !calibration.ShowGrid;
                break;
            case Key.R when plain && IsCalibrating:
                calibration.ResetCommand.Execute(null);
                break;
            case Key.Tab when plain && IsCalibrating:
                calibration.SelectNextCorner();
                break;
            case Key.Left or Key.Right or Key.Up or Key.Down when IsCalibrating && e.KeyModifiers.HasFlag(KeyModifiers.Control):
                var step = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 10 : 1;
                var dx = e.Key == Key.Left ? -step : e.Key == Key.Right ? step : 0;
                var dy = e.Key == Key.Up ? -step : e.Key == Key.Down ? step : 0;
                calibration.Nudge(dx / Math.Max(1, Bounds.Width), dy / Math.Max(1, Bounds.Height));
                break;
            default:
                return false;
        }
        ShowHandlesTemporarily();
        e.Handled = true;
        return true;
    }

    private void HandleQuickPickKey(ProjectionViewModel vm, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                vm.CloseQuickPick();
                e.Handled = true;
                break;
            case Key.Enter when e.KeyModifiers == KeyModifiers.Control:
                vm.QuickPick.AddAsNextCommand.Execute(null);
                vm.CloseQuickPick();
                e.Handled = true;
                break;
            case Key.Enter:
                vm.QuickPick.ShowNowCommand.Execute(null);
                vm.CloseQuickPick();
                e.Handled = true;
                break;
            case Key.Down or Key.Up:
                vm.QuickPick.MoveSelection(e.Key == Key.Down ? 1 : -1);
                e.Handled = true;
                break;
        }
    }

    /// <summary>K zaczyna kalibrację tylko na drugim ekranie po ruchu myszą — w trybie jednego ekranu litery szukają pieśni.</summary>
    private bool CanStartCalibration => HandlesVisible && !IsSameScreen;

    private void OnTextInputTunnel(object? sender, TextInputEventArgs e)
    {
        // Litery obsłużone jako klawisze kalibracji nie otwierają szukania pieśni.
        var letter = e.Text?.ToUpperInvariant();
        if ((IsCalibrating && letter is "G" or "K" or "R") || (CanStartCalibration && letter is "K"))
        {
            e.Handled = true;
            return;
        }
        if (ViewModel.IsQuickPickOpen || LiveKeyboard.SearchStart(e) is not { } start)
            return;
        ViewModel.OpenQuickPick(start);
        e.Handled = true;
        FocusQuickPick();
    }

    private void OnSearchClick(object? sender, RoutedEventArgs e)
    {
        ViewModel.OpenQuickPick(null);
        FocusQuickPick();
    }

    private void OnExitClick(object? sender, RoutedEventArgs e) => ViewModel.Projection.Close();

    private void OnResultDoubleTapped(object? sender, TappedEventArgs e)
    {
        ViewModel.QuickPick.ShowNowCommand.Execute(null);
        ViewModel.CloseQuickPick();
    }

    private void FocusQuickPick()
    {
        QuickPickBox.Focus();
        QuickPickBox.CaretIndex = QuickPickBox.Text?.Length ?? 0;
    }
}
