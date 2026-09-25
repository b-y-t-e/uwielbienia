using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Uwielbienia.App.Services;
using Uwielbienia.App.ViewModels;

namespace Uwielbienia.App.Views;

public partial class ProjectionWindow : Window
{
    public ProjectionWindow()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDownTunnel, RoutingStrategies.Tunnel);
        AddHandler(TextInputEvent, OnTextInputTunnel, RoutingStrategies.Tunnel);
        Slide.PointerReleased += OnSlideTapped;
        Cursor = new Cursor(StandardCursorType.None);
    }

    public ProjectionWindow(ProjectionViewModel viewModel) : this() => DataContext = viewModel;

    private ProjectionViewModel ViewModel => (ProjectionViewModel)DataContext!;

    /// <summary>Jedno urządzenie: widać przyciski i kursor; na drugim ekranie — tylko tekst.</summary>
    public bool IsSameScreen
    {
        get => SameScreenBar.IsVisible;
        set
        {
            SameScreenBar.IsVisible = value;
            Cursor = value ? Cursor.Default : new Cursor(StandardCursorType.None);
        }
    }

    private void OnKeyDownTunnel(object? sender, KeyEventArgs e)
    {
        var vm = ViewModel;
        if (vm.IsQuickPickOpen)
        {
            HandleQuickPickKey(vm, e);
            return;
        }
        if (e.Key == Key.Escape && IsSameScreen)
        {
            vm.Projection.Close();
            e.Handled = true;
            return;
        }
        e.Handled = LiveKeyboard.Handle(e, vm.Control, vm.Projection);
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

    private void OnTextInputTunnel(object? sender, TextInputEventArgs e)
    {
        if (ViewModel.IsQuickPickOpen || LiveKeyboard.SearchStart(e) is not { } start)
            return;
        ViewModel.OpenQuickPick(start);
        e.Handled = true;
        FocusQuickPick();
    }

    private void OnSlideTapped(object? sender, PointerReleasedEventArgs e)
    {
        if (!IsSameScreen || e.InitialPressMouseButton == MouseButton.Right)
            return;
        var x = e.GetPosition(Slide).X;
        if (x > Slide.Bounds.Width / 2)
            ViewModel.Control.Next();
        else
            ViewModel.Control.Previous();
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
