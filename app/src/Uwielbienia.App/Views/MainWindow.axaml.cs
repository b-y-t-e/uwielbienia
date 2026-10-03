using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Uwielbienia.App.Services;
using Uwielbienia.App.ViewModels;

namespace Uwielbienia.App.Views;

public partial class MainWindow : Window
{
    private const double PlanDragThreshold = 6;
    private static readonly Cursor PlanDragCursor = new(StandardCursorType.SizeNorthSouth);
    private const double PlanAutoScrollEdge = 48;
    private const double PlanAutoScrollStep = 14;
    private readonly DispatcherTimer _planAutoScroll = new() { Interval = TimeSpan.FromMilliseconds(30) };
    private Point _planDragPosition;
    private PlanItemViewModel? _planDragCandidate;
    private Point _planDragStart;
    private bool _planDragStarted;
    private int _planDropIndex = -1;

    public MainWindow()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDownTunnel, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, OnKeyUpTunnel, RoutingStrategies.Tunnel);
        AddHandler(TextInputEvent, OnTextInputTunnel, RoutingStrategies.Tunnel);
        // Wiersz planu to Button, który sam oznacza wciśnięcie jako obsłużone — przeciąganie musi słuchać mimo to.
        PlanList.AddHandler(PointerPressedEvent, OnPlanItemPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        PlanList.AddHandler(PointerMovedEvent, OnPlanItemPointerMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
        PlanList.AddHandler(PointerReleasedEvent, OnPlanItemPointerReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
        PlanList.AddHandler(PointerCaptureLostEvent, (_, _) => EndPlanDrag(), RoutingStrategies.Bubble, handledEventsToo: true);
        _planAutoScroll.Tick += (_, _) => AutoScrollPlan();
        PropertyChanged += (_, e) =>
        {
            if (e.Property == WindowStateProperty && e.OldValue is WindowState.FullScreen && WindowState == WindowState.Normal)
                FitToScreen();
        };
    }

    public MainWindow(MainViewModel viewModel) : this()
    {
        DataContext = viewModel;
        ApplySavedColumnWidths();
    }

    private MainViewModel ViewModel => (MainViewModel)DataContext!;

    private bool IsOverlayOpen =>
        ViewModel.Plans.IsOpen || ViewModel.Remote.IsOpen || ViewModel.Plan.IsRemoveConfirmationOpen;

    private void OnKeyDownTunnel(object? sender, KeyEventArgs e)
    {
        var vm = ViewModel;
        if (e.Key == Key.F11 && e.KeyModifiers == KeyModifiers.None)
        {
            WindowState = WindowState == WindowState.FullScreen
                ? WindowState.Maximized
                : WindowState.FullScreen;
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Escape && vm.Plans.IsDeleteConfirmationOpen)
        {
            vm.Plans.CancelDeleteCommand.Execute(null);
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Escape && IsOverlayOpen)
        {
            vm.Plans.IsOpen = false;
            vm.Remote.IsOpen = false;
            vm.Plan.CancelRemoveCommand.Execute(null);
            e.Handled = true;
            return;
        }
        if (IsOverlayOpen)
            return;

        if (e.Source is TextBox box && ReferenceEquals(box, SearchBox))
        {
            HandleSearchKey(vm, e);
            return;
        }
        if (e.Source is TextBox)
            return;

        e.Handled = LiveKeyboard.Handle(e, vm.Control, vm.Projection);
    }

    private void OnKeyUpTunnel(object? sender, KeyEventArgs e)
    {
        // KeyDown spacji steruje prezentacją. Nie pozwól, by jej KeyUp dodatkowo
        // uruchomił przycisk, który zachował fokus po kliknięciu (np. „Wstecz”).
        if (!IsOverlayOpen && e.Source is not TextBox && e.Key == Key.Space &&
            e.KeyModifiers is KeyModifiers.None or KeyModifiers.Shift)
            e.Handled = true;
    }

    private void HandleSearchKey(MainViewModel vm, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter when e.KeyModifiers == KeyModifiers.Control:
                vm.Search.AddAsNextCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Enter:
                // Enter tylko wybiera pieśń do kolumny „Pieśń” — na ekran trafia wyłącznie dwuklikiem w planie.
                SearchPopup.IsOpen = false;
                Focus();
                e.Handled = true;
                break;
            case Key.Escape:
                vm.Search.Clear();
                Focus();
                e.Handled = true;
                break;
            case Key.Down or Key.Up:
                vm.Search.MoveSelection(e.Key == Key.Down ? 1 : -1);
                e.Handled = true;
                break;
            case Key.PageDown or Key.PageUp or Key.F5:
                // piloty do prezentacji działają także wtedy, gdy kursor stoi w wyszukiwarce
                e.Handled = LiveKeyboard.Handle(e, vm.Control, vm.Projection);
                break;
        }
    }

    private void OnSearchFocus(object? sender, FocusChangedEventArgs e) => UpdateSearchPopup();

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e) => UpdateSearchPopup();

    private void UpdateSearchPopup() =>
        SearchPopup.IsOpen = !string.IsNullOrWhiteSpace(SearchBox.Text);

    private void OnSearchResultPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        SearchPopup.IsOpen = false;
        Focus();
    }

    private void OnPlanItemClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: PlanItemViewModel item })
            ViewModel.Plan.Selected = item;
    }

    private void OnPlanItemDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Control { DataContext: PlanItemViewModel item })
            return;
        ViewModel.Plan.ShowLiveCommand.Execute(item);
        e.Handled = true;
    }

    // Zmiana kolejności w planie: zwykłe przeciąganie myszą wewnątrz listy (bez systemowego drag & drop),
    // ze znacznikiem miejsca upuszczenia między wierszami.
    private void OnPlanItemPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DraggablePlanItem(e.Source) is not { } item || !e.GetCurrentPoint(PlanList).Properties.IsLeftButtonPressed)
            return;

        _planDragCandidate = item;
        _planDragStart = e.GetPosition(PlanList);
        _planDragStarted = false;
    }

    private void OnPlanItemPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_planDragCandidate is not { } item || !e.GetCurrentPoint(PlanList).Properties.IsLeftButtonPressed)
            return;

        var position = e.GetPosition(PlanList);
        if (!_planDragStarted)
        {
            if (Math.Abs(position.Y - _planDragStart.Y) < PlanDragThreshold &&
                Math.Abs(position.X - _planDragStart.X) < PlanDragThreshold)
                return;
            _planDragStarted = true;
            item.IsDragged = true;
            PlanList.Cursor = PlanDragCursor;
            _planAutoScroll.Start();
        }

        _planDragPosition = position;
        ShowPlanDropMarker(PlanInsertionIndex(position.Y));
        AutoScrollPlan();
        e.Handled = true;
    }

    private void OnPlanItemPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_planDragStarted && _planDragCandidate is { } item)
        {
            var source = ViewModel.Plan.Items.IndexOf(item);
            var target = _planDropIndex > source ? _planDropIndex - 1 : _planDropIndex;
            EndPlanDrag();
            if (source >= 0 && target >= 0 && target != source)
                ViewModel.Plan.Reorder(item, target);
            // Upuszczenie nie jest kliknięciem — nie wybieraj wiersza.
            e.Handled = true;
            return;
        }
        EndPlanDrag();
    }

    /// <summary>Przeciąganie przy górnej/dolnej krawędzi listy przewija plan (także bez ruchu myszą).</summary>
    private void AutoScrollPlan()
    {
        if (!_planDragStarted || PlanList.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault() is not { } scroll)
            return;
        var y = _planDragPosition.Y;
        var height = PlanList.Bounds.Height;
        var step = y < PlanAutoScrollEdge ? -PlanAutoScrollStep
            : y > height - PlanAutoScrollEdge ? PlanAutoScrollStep
            : 0;
        var maxOffset = Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height);
        var offset = Math.Clamp(scroll.Offset.Y + step, 0, maxOffset);
        if (step == 0 || offset == scroll.Offset.Y)
            return;
        scroll.Offset = scroll.Offset.WithY(offset);
        ShowPlanDropMarker(PlanInsertionIndex(y));
    }

    /// <summary>Indeks, przed którym wstawić przeciągany wiersz (liczba wierszy = na koniec).</summary>
    private int PlanInsertionIndex(double y)
    {
        // Lista tworzy tylko widoczne wiersze — poza nimi wynik nie może „przeskoczyć” niewidocznych pieśni.
        var items = ViewModel.Plan.Items;
        var afterLastVisible = 0;
        for (var i = 0; i < items.Count; i++)
        {
            if (PlanList.ContainerFromIndex(i) is not Control row || row.TranslatePoint(default, PlanList) is not { } top)
                continue;
            if (y < top.Y + row.Bounds.Height / 2)
                return i;
            afterLastVisible = i + 1;
        }
        return afterLastVisible;
    }

    private void ShowPlanDropMarker(int insertionIndex)
    {
        var items = ViewModel.Plan.Items;
        var source = _planDragCandidate is { } dragged ? items.IndexOf(dragged) : -1;
        // Upuszczenie tuż nad albo tuż pod sobą niczego nie zmienia — bez znacznika.
        var noOp = insertionIndex == source || insertionIndex == source + 1;
        _planDropIndex = insertionIndex;
        for (var i = 0; i < items.Count; i++)
        {
            items[i].IsDropBefore = !noOp && i == insertionIndex;
            items[i].IsDropAfter = !noOp && i == items.Count - 1 && insertionIndex == items.Count;
        }
    }

    private void EndPlanDrag()
    {
        foreach (var item in ViewModel.Plan.Items)
        {
            item.IsDragged = false;
            item.IsDropBefore = false;
            item.IsDropAfter = false;
        }
        _planAutoScroll.Stop();
        _planDragCandidate = null;
        _planDragStarted = false;
        _planDropIndex = -1;
        PlanList.Cursor = null;
    }

    /// <summary>Pozycja planu pod wskaźnikiem — tylko z obszaru wiersza, nie z przycisku usuwania.</summary>
    private static PlanItemViewModel? DraggablePlanItem(object? source) =>
        (source as Visual)?.FindAncestorOfType<Button>(includeSelf: true) is { } button && button.Classes.Contains("planItem")
            ? button.DataContext as PlanItemViewModel
            : null;

    /// <summary>Pisanie gdziekolwiek w oknie zaczyna wyszukiwanie pieśni.</summary>
    private void OnTextInputTunnel(object? sender, TextInputEventArgs e)
    {
        if (IsOverlayOpen || e.Source is TextBox || LiveKeyboard.SearchStart(e) is not { } start)
            return;
        ViewModel.Search.Query = start;
        SearchBox.Focus();
        SearchBox.CaretIndex = start.Length;
        e.Handled = true;
    }

    /// <summary>
    /// Zwykłe okno (po wyjściu z pełnego ekranu) ma się mieścić na ekranie, na którym jest —
    /// także na małym laptopie: najwyżej 1440×900, ale nie więcej niż 92% obszaru roboczego.
    /// </summary>
    private void FitToScreen()
    {
        if (Screens.ScreenFromWindow(this) is not { } screen)
            return;
        var area = screen.WorkingArea;
        var scale = screen.Scaling;
        Width = Math.Max(MinWidth, Math.Min(1440, area.Width / scale * 0.92));
        Height = Math.Max(MinHeight, Math.Min(900, area.Height / scale * 0.92));
        Position = new Avalonia.PixelPoint(
            area.X + (int)((area.Width - Width * scale) / 2),
            area.Y + (int)((area.Height - Height * scale) / 2));
    }

    private void ApplySavedColumnWidths()
    {
        var (plan, preview, live) = NormalizeRatios(
            ViewModel.PlanColumnRatio, ViewModel.PreviewColumnRatio, ViewModel.LiveColumnRatio);
        Columns.ColumnDefinitions[0].Width = new GridLength(plan, GridUnitType.Star);
        Columns.ColumnDefinitions[2].Width = new GridLength(preview, GridUnitType.Star);
        Columns.ColumnDefinitions[4].Width = new GridLength(live, GridUnitType.Star);
    }

    private void OnColumnSplitterDragCompleted(object? sender, VectorEventArgs e)
    {
        var plan = Columns.ColumnDefinitions[0].ActualWidth;
        var preview = Columns.ColumnDefinitions[2].ActualWidth;
        var live = Columns.ColumnDefinitions[4].ActualWidth;
        var total = plan + preview + live;
        if (total <= 0)
            return;
        ViewModel.SaveColumnRatios(plan / total, preview / total, live / total);
    }

    private static (double Plan, double Preview, double Live) NormalizeRatios(double plan, double preview, double live)
    {
        if (!double.IsFinite(plan) || !double.IsFinite(preview) || !double.IsFinite(live) ||
            plan <= 0 || preview <= 0 || live <= 0)
            return (0.16, 0.42, 0.42);
        var total = plan + preview + live;
        return (plan / total, preview / total, live / total);
    }
}
