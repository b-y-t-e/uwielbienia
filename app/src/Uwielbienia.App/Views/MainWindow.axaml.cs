using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Uwielbienia.App.Services;
using Uwielbienia.App.ViewModels;

namespace Uwielbienia.App.Views;

public partial class MainWindow : Window
{
    private static readonly DataFormat<PlanItemViewModel> PlanItemDataFormat =
        DataFormat.CreateInProcessFormat<PlanItemViewModel>("uwielbienia-plan-item");
    private PlanItemViewModel? _planDragCandidate;
    private PointerPressedEventArgs? _planDragPress;
    private Avalonia.Point _planDragStart;
    private bool _planDragStarted;

    public MainWindow()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDownTunnel, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, OnKeyUpTunnel, RoutingStrategies.Tunnel);
        AddHandler(TextInputEvent, OnTextInputTunnel, RoutingStrategies.Tunnel);
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

        if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None)
        {
            vm.Preview.ShowLiveCommand.Execute(null);
            e.Handled = true;
            return;
        }
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
                vm.Search.ShowNowCommand.Execute(null);
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

    private void OnPlanItemPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control { DataContext: PlanItemViewModel item } row ||
            !e.GetCurrentPoint(row).Properties.IsLeftButtonPressed)
            return;

        _planDragCandidate = item;
        _planDragPress = e;
        _planDragStart = e.GetPosition(PlanList);
        _planDragStarted = false;
    }

    private async void OnPlanItemPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_planDragCandidate is not { } item || _planDragPress is not { } press || _planDragStarted ||
            sender is not Control row || !e.GetCurrentPoint(row).Properties.IsLeftButtonPressed)
            return;

        var position = e.GetPosition(PlanList);
        if (Math.Abs(position.X - _planDragStart.X) < 6 && Math.Abs(position.Y - _planDragStart.Y) < 6)
            return;

        _planDragStarted = true;
        e.Handled = true;
        using var transfer = new DataTransfer();
        transfer.Add(DataTransferItem.Create(PlanItemDataFormat, item));
        await DragDrop.DoDragDropAsync(press, transfer, DragDropEffects.Move);
        _planDragCandidate = null;
        _planDragPress = null;
        _planDragStarted = false;
    }

    private void OnPlanItemPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_planDragStarted)
        {
            _planDragCandidate = null;
            _planDragPress = null;
        }
    }

    private void OnPlanDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Contains(PlanItemDataFormat)
            ? DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnPlanDrop(object? sender, DragEventArgs e)
    {
        var dragged = e.DataTransfer.TryGetValue(PlanItemDataFormat);
        if (dragged is null)
            return;

        var sourceIndex = ViewModel.Plan.Items.IndexOf(dragged);
        if (sourceIndex < 0)
            return;

        var target = (e.Source as Control)?.DataContext as PlanItemViewModel;
        var insertionIndex = target is null ? ViewModel.Plan.Items.Count : ViewModel.Plan.Items.IndexOf(target);
        if (target is not null && PlanList.ContainerFromItem(target) is Control container &&
            e.GetPosition(container).Y > container.Bounds.Height / 2)
            insertionIndex++;
        if (sourceIndex < insertionIndex)
            insertionIndex--;

        if (sourceIndex != insertionIndex)
            ViewModel.Plan.Reorder(dragged, insertionIndex);
        e.Handled = true;
    }

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
