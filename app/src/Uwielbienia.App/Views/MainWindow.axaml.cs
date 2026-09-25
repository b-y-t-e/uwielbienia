using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Uwielbienia.App.Services;
using Uwielbienia.App.ViewModels;

namespace Uwielbienia.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDownTunnel, RoutingStrategies.Tunnel);
        AddHandler(TextInputEvent, OnTextInputTunnel, RoutingStrategies.Tunnel);
        PropertyChanged += (_, e) =>
        {
            if (e.Property == WindowStateProperty && e.OldValue is WindowState.FullScreen && WindowState == WindowState.Normal)
                FitToScreen();
        };
        SizeChanged += (_, _) => UpdateColumnWidths();
    }

    public MainWindow(MainViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.PropertyChanged += OnViewModelChanged;
        UpdateColumnWidths();
    }

    private MainViewModel ViewModel => (MainViewModel)DataContext!;

    private bool IsOverlayOpen => ViewModel.Plans.IsOpen || ViewModel.Remote.IsOpen;

    private void OnKeyDownTunnel(object? sender, KeyEventArgs e)
    {
        var vm = ViewModel;
        if (e.Key == Key.Escape && IsOverlayOpen)
        {
            vm.Plans.IsOpen = false;
            vm.Remote.IsOpen = false;
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

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsLivePanelVisible))
            UpdateColumnWidths();
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

    private const double PlanColumnShare = 0.22, PlanColumnMin = 200, PlanColumnMax = 340;
    private const double LiveColumnShare = 0.28, LiveColumnMin = 240, LiveColumnMax = 460;
    private const double ColumnGap = 16;

    /// <summary>Kolumny planu i podglądu ekranu rosną i maleją z oknem, żeby środek zawsze miał miejsce.</summary>
    private void UpdateColumnWidths()
    {
        if (DataContext is not MainViewModel vm)
            return;
        var width = Columns.Bounds.Width > 0 ? Columns.Bounds.Width : Width;
        var visible = vm.IsLivePanelVisible;
        Columns.ColumnDefinitions[0].Width = new GridLength(ColumnWidth(width, PlanColumnShare, PlanColumnMin, PlanColumnMax));
        Columns.ColumnDefinitions[3].Width = new GridLength(visible ? ColumnGap : 0);
        Columns.ColumnDefinitions[4].Width = new GridLength(visible ? ColumnWidth(width, LiveColumnShare, LiveColumnMin, LiveColumnMax) : 0);
    }

    private static double ColumnWidth(double availableWidth, double share, double min, double max) =>
        Math.Clamp(availableWidth * share, min, max);
}
