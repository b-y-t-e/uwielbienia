using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Uwielbienia.App.Services;
using Uwielbienia.App.ViewModels;
using Uwielbienia.Core.Slideshows;

namespace Uwielbienia.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDownTunnel, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, OnKeyUpTunnel, RoutingStrategies.Tunnel);
        AddHandler(TextInputEvent, OnTextInputTunnel, RoutingStrategies.Tunnel);
        // Kolejność przeciąganiem: pozycje planu (cały wiersz poza ✕) i części pieśni (za nagłówek części).
        _ = new ListReorderDrag(PlanList, source => FindRow<PlanItemViewModel>(source, "planItem"),
            (item, index) => ViewModel.Plan.Reorder((PlanItemViewModel)item, index));
        _ = new ListReorderDrag(PartsList, source => FindRow<SongPartViewModel>(source, "partHandle"),
            (item, index) => ViewModel.Preview.MovePart((SongPartViewModel)item, index));
        PlanList.AddHandler(DragDrop.DragOverEvent, OnPlanDragOver);
        PlanList.AddHandler(DragDrop.DropEvent, OnPlanDrop);
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
        viewModel.PlanAdd.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PlanAddViewModel.IsOpen) && viewModel.PlanAdd.IsOpen)
                Dispatcher.UIThread.Post(() =>
                {
                    PlanAddBox.Focus();
                    PlanAddBox.CaretIndex = PlanAddBox.Text?.Length ?? 0;
                });
        };
    }

    private MainViewModel ViewModel => (MainViewModel)DataContext!;

    private bool IsOverlayOpen =>
        ViewModel.Plans.IsOpen || ViewModel.Remote.IsOpen || ViewModel.Plan.IsRemoveConfirmationOpen || ViewModel.PlanAdd.IsOpen;

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
        if (vm.PlanAdd.IsOpen && HandlePlanAddKey(vm.PlanAdd, e))
            return;
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

        if (e.Source is TextBox || IsInEditor(e.Source))
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

    /// <summary>Klawiatura w oknie „Dodaj do planu”: strzałki wybierają, Enter dodaje, Esc zamyka.</summary>
    private bool HandlePlanAddKey(PlanAddViewModel add, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.PageDown or Key.PageUp or Key.F5:
                // piloty do prezentacji działają także przy otwartym oknie
                return e.Handled = LiveKeyboard.Handle(e, ViewModel.Control, ViewModel.Projection);
            case Key.Escape:
                add.CloseCommand.Execute(null);
                break;
            case Key.Enter:
                add.AddCommand.Execute(null);
                break;
            case Key.Down or Key.Up:
                add.MoveSelection(e.Key == Key.Down ? 1 : -1);
                break;
            default:
                return false;
        }
        e.Handled = true;
        return true;
    }

    // Dodawanie do planu: „+” w nagłówku, przycisk w pustym planie i menu pod prawym przyciskiem.
    private void OnPlanAddClick(object? sender, RoutedEventArgs e) =>
        ViewModel.PlanAdd.Open(ViewModel.Plan.Items.Count);

    private void OnPlanMenuShow(object? sender, RoutedEventArgs e)
    {
        if (MenuItemTarget(sender) is { } item)
            ViewModel.Plan.ShowLiveCommand.Execute(item);
    }

    private void OnPlanMenuAddBefore(object? sender, RoutedEventArgs e)
    {
        if (MenuItemTarget(sender) is { } item)
            ViewModel.PlanAdd.Open(ViewModel.Plan.Items.IndexOf(item));
    }

    private void OnPlanMenuAddAfter(object? sender, RoutedEventArgs e)
    {
        if (MenuItemTarget(sender) is { } item)
            ViewModel.PlanAdd.Open(ViewModel.Plan.Items.IndexOf(item) + 1);
    }

    private void OnPlanMenuDuplicate(object? sender, RoutedEventArgs e)
    {
        if (MenuItemTarget(sender) is { } item)
            ViewModel.Plan.DuplicateCommand.Execute(item);
    }

    private void OnPlanMenuRemove(object? sender, RoutedEventArgs e)
    {
        if (MenuItemTarget(sender) is { } item)
            ViewModel.Plan.RequestRemoveCommand.Execute(item);
    }

    private static PlanItemViewModel? MenuItemTarget(object? sender) => (sender as Control)?.DataContext as PlanItemViewModel;

    /// <summary>„+ Prezentacja”: plik PowerPoint albo obrazy slajdów (kilka naraz = jedna prezentacja).</summary>
    private async void OnAddPresentationClick(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Prezentacja albo obrazy slajdów",
            AllowMultiple = true,
            FileTypeFilter =
            [
                new FilePickerFileType("Prezentacje i obrazy")
                {
                    Patterns = [.. SlideshowLibrary.PresentationExtensions.Concat(SlideshowLibrary.ImageExtensions).Select(ext => "*" + ext)],
                },
            ],
        });
        var paths = files.Select(f => f.TryGetLocalPath()).OfType<string>().ToList();
        if (paths.Count > 0)
            await ViewModel.PlanAdd.AddPresentationAsync(paths);
    }

    private static List<string> DroppedFiles(DragEventArgs e) =>
        (e.DataTransfer.TryGetFiles() ?? []).Select(f => f.TryGetLocalPath()).OfType<string>()
            .Where(SlideshowLibrary.IsSupported).ToList();

    private void OnPlanDragOver(object? sender, DragEventArgs e) =>
        e.DragEffects = DroppedFiles(e).Count > 0 ? DragDropEffects.Copy : DragDropEffects.None;

    /// <summary>Upuszczona prezentacja (albo obrazy) trafia za wiersz pod kursorem albo na koniec planu.</summary>
    private async void OnPlanDrop(object? sender, DragEventArgs e)
    {
        var files = DroppedFiles(e);
        if (files.Count == 0)
            return;
        var plan = ViewModel.Plan.Items;
        var row = (e.Source as Visual)?.GetSelfAndVisualAncestors().OfType<Control>()
            .Select(c => c.DataContext).OfType<PlanItemViewModel>().FirstOrDefault();
        await ViewModel.PlanAdd.AddFilesAsync(files, row is null ? plan.Count : plan.IndexOf(row) + 1);
    }

    private void OnPlanAddResultDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: SongResultViewModel result })
            ViewModel.PlanAdd.AddCommand.Execute(result);
    }

    /// <summary>Klik obok okna „Dodaj do planu” zamyka je.</summary>
    private void OnPlanAddScrimPressed(object? sender, PointerPressedEventArgs e)
    {
        if (ReferenceEquals(e.Source, PlanAddScrim))
            ViewModel.PlanAdd.CloseCommand.Execute(null);
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

    // Układ części pieśni w planie: „⋯” / prawy klik na części i „+ Dodaj część”.
    private void OnPartMenuClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: SongPartViewModel part } button)
            return;
        var menu = new MenuFlyout { Placement = PlacementMode.BottomEdgeAlignedRight };
        menu.Items.Add(new MenuItem { Header = "Powtórz", Command = ViewModel.Preview.DuplicatePartCommand, CommandParameter = part });
        menu.Items.Add(new MenuItem { Header = "Usuń", Command = ViewModel.Preview.RemovePartCommand, CommandParameter = part });
        menu.ShowAt(button);
    }

    private void OnPartDuplicate(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.DataContext is SongPartViewModel part)
            ViewModel.Preview.DuplicatePartCommand.Execute(part);
    }

    private void OnPartRemove(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.DataContext is SongPartViewModel part)
            ViewModel.Preview.RemovePartCommand.Execute(part);
    }

    /// <summary>Części usunięte z układu (z pierwszym wersem) — wybrana wraca na koniec układu.</summary>
    private void OnAddPartClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control button)
            return;
        var menu = new MenuFlyout { Placement = PlacementMode.TopEdgeAlignedLeft };
        foreach (var section in ViewModel.Preview.RemovedSections)
        {
            var code = section.Code;
            var firstLine = section.Lines.FirstOrDefault(l => l.IsSung)?.Text;
            var item = new MenuItem { Header = firstLine is null ? section.Name : $"{section.Name} — {firstLine}" };
            item.Click += (_, _) => ViewModel.Preview.AddPart(code);
            menu.Items.Add(item);
        }
        menu.ShowAt(button);
    }

    /// <summary>Wiersz listy pod wskaźnikiem — tylko z elementu oznaczonego klasą (np. bez przycisku ✕).</summary>
    private static T? FindRow<T>(object? source, string handleClass) where T : class =>
        (source as Visual)?.GetSelfAndVisualAncestors().OfType<Control>().FirstOrDefault(c => c.Classes.Contains(handleClass))
            ?.DataContext as T;

    /// <summary>W edytorze pieśni klawisze (spacja, litery) należą do edytora, a nie do sterowania ekranem.</summary>
    private static bool IsInEditor(object? source) =>
        (source as Visual)?.FindAncestorOfType<SongEditorView>(includeSelf: true) is not null;

    /// <summary>
    /// Pisanie gdziekolwiek w oknie (np. „47”) otwiera „Dodaj do planu” z tym tekstem; wybrana pieśń
    /// trafia zaraz za tę, która jest na ekranie (albo na koniec planu).
    /// </summary>
    private void OnTextInputTunnel(object? sender, TextInputEventArgs e)
    {
        if (IsOverlayOpen || e.Source is TextBox || IsInEditor(e.Source) || LiveKeyboard.SearchStart(e) is not { } start)
            return;
        var plan = ViewModel.Plan.Items;
        var live = plan.FirstOrDefault(i => i.IsLive);
        ViewModel.PlanAdd.Open(live is null ? plan.Count : plan.IndexOf(live) + 1, start);
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
