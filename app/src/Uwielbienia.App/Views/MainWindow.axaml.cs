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
    }

    public MainWindow(MainViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.PropertyChanged += OnViewModelChanged;
        UpdateLiveColumn();
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
            UpdateLiveColumn();
    }

    private void UpdateLiveColumn()
    {
        var visible = ViewModel.IsLivePanelVisible;
        Columns.ColumnDefinitions[3].Width = new GridLength(visible ? 16 : 0);
        Columns.ColumnDefinitions[4].Width = new GridLength(visible ? 460 : 0);
    }
}
