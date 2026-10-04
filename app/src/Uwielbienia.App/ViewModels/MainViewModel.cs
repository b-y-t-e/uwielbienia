using Avalonia;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Uwielbienia.App.Services;
using Uwielbienia.Core.Plans;
using Uwielbienia.Core.Presentation;
using Uwielbienia.Core.Songs;
using Uwielbienia.Core.Updates;

namespace Uwielbienia.App.ViewModels;

/// <summary>Okno operatora: trzy kolumny — Plan, Pieśń (wyszukiwarka i wybrana pieśń), Na ekranie (to, co widzi sala).</summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly ISettingsStore _settings;
    private readonly IUpdateService _updates;

    public MainViewModel(
        LiveViewModel live,
        PlanViewModel plan,
        PreviewViewModel preview,
        PlansViewModel plans,
        RemoteViewModel remote,
        ISettingsStore settings,
        IUpdateService updates,
        IProjectionController projection,
        ILiveControl control,
        ISongLibrary library,
        PlanAddViewModel planAdd,
        PlanActions planActions,
        ProjectionCalibration calibration)
    {
        PlanAdd = planAdd;
        Live = live;
        Plan = plan;
        Preview = preview;
        Plans = plans;
        Remote = remote;
        Projection = projection;
        Control = control;
        _settings = settings;
        _updates = updates;
        LibraryWarning = library.Errors.Count > 0 ? $"Nie udało się odczytać pieśni: {library.Errors.Count}" : null;

        plan.ItemSelected += (_, item) => ShowDetails(item, library, preview);
        plan.SelectionCleared += (_, _) => preview.Clear();
        // „Nowa pieśń / Nowy tekst” z okna dodawania: edytor w kolumnie „Pieśń”, po zapisie — w wybrane miejsce planu.
        planAdd.NewRequested += (_, request) =>
            preview.StartNew(request.Kind, song => planActions.InsertAt(song, request.Index));
        projection.Changed += (_, _) =>
        {
            OnPropertyChanged(nameof(ProjectionStatus));
            OnPropertyChanged(nameof(ProjectionHint));
            // po zamknięciu projekcji tryb kalibracji się kończy
            if (projection.Mode == ProjectionMode.Off)
                calibration.IsActive = false;
        };
        settings.Changed += (_, _) => OnSettingsChanged();
        _updates.UpdateAvailable += OnUpdateAvailable;
        ApplyTheme();
    }

    public LiveViewModel Live { get; }

    /// <summary>
    /// Kolumna „Pieśń” dla zaznaczonej pozycji planu. Nowy rodzaj elementu prezentacji dostaje tu
    /// własny model widoku (i szablon w <c>MainWindow</c>); nieznany rodzaj czyści kolumnę.
    /// </summary>
    private static void ShowDetails(PlanItemViewModel item, ISongLibrary library, PreviewViewModel preview)
    {
        switch (item.Item)
        {
            case SongPlanItem songItem when library.Find(songItem.SongId) is { } song:
                preview.ShowPlanItem(songItem, song);
                break;
            case PresentationPlanItem presentation:
                preview.ShowPresentation(presentation);
                break;
            default:
                preview.Clear();
                break;
        }
    }

    public PlanViewModel Plan { get; }

    public PlanAddViewModel PlanAdd { get; }

    public PreviewViewModel Preview { get; }

    public PlansViewModel Plans { get; }

    public RemoteViewModel Remote { get; }

    public IProjectionController Projection { get; }

    public ILiveControl Control { get; }

    public string? LibraryWarning { get; }

    public string ProjectionStatus => Projection.Status;

    public string ProjectionHint => Projection.Hint;

    public bool ShowChords => _settings.Current.ShowChords;

    public bool IsDarkTheme => _settings.Current.Theme == AppTheme.Dark;

    public bool IsProjectionDark => _settings.Current.ProjectionTheme == AppTheme.Dark;

    public bool IsUpdateAvailable => _updates.HasUpdate;

    public string? UpdateVersion => _updates.NewVersion;

    public double PlanColumnRatio => _settings.Current.PlanColumnRatio;

    public double PreviewColumnRatio => _settings.Current.PreviewColumnRatio;

    public double LiveColumnRatio => _settings.Current.LiveColumnRatio;

    public void SaveColumnRatios(double plan, double preview, double live) =>
        _settings.Update(s => s with
        {
            PlanColumnRatio = plan,
            PreviewColumnRatio = preview,
            LiveColumnRatio = live,
        });

    [RelayCommand]
    private void ToggleProjection() => Projection.Toggle();

    [RelayCommand]
    private void ShowOnThisScreen() => Projection.ShowOnSameScreen();

    [RelayCommand]
    private void ToggleChords() => _settings.Update(s => s with { ShowChords = !s.ShowChords });

    [RelayCommand]
    private void ToggleTheme() =>
        _settings.Update(s => s with { Theme = s.Theme == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark });

    [RelayCommand]
    private void ToggleProjectionTheme() =>
        _settings.Update(s => s with { ProjectionTheme = s.ProjectionTheme == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark });

    [ObservableProperty]
    private string? _updateError;

    [RelayCommand]
    private void ApplyUpdate() => UpdateError = _updates.ApplyUpdate();

    private void OnUpdateAvailable()
    {
        OnPropertyChanged(nameof(IsUpdateAvailable));
        OnPropertyChanged(nameof(UpdateVersion));
    }

    private void OnSettingsChanged()
    {
        ApplyTheme();
        OnPropertyChanged(nameof(ShowChords));
        OnPropertyChanged(nameof(IsDarkTheme));
        OnPropertyChanged(nameof(IsProjectionDark));
        OnPropertyChanged(nameof(PlanColumnRatio));
        OnPropertyChanged(nameof(PreviewColumnRatio));
        OnPropertyChanged(nameof(LiveColumnRatio));
    }

    private void ApplyTheme()
    {
        if (Application.Current is { } app)
            app.RequestedThemeVariant = _settings.Current.Theme == AppTheme.Dark ? ThemeVariant.Dark : ThemeVariant.Light;
    }
}
