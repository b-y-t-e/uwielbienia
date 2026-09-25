using Avalonia;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Uwielbienia.App.Services;
using Uwielbienia.Core.Presentation;
using Uwielbienia.Core.Songs;

namespace Uwielbienia.App.ViewModels;

/// <summary>Okno operatora: po lewej zarządzanie (plan, wyszukiwarka, podgląd), po prawej to, co jest na ekranie.</summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly ISettingsStore _settings;

    public MainViewModel(
        LiveViewModel live,
        PlanViewModel plan,
        SearchViewModel search,
        PreviewViewModel preview,
        PlansViewModel plans,
        RemoteViewModel remote,
        ISettingsStore settings,
        IProjectionController projection,
        ILiveControl control,
        ISongLibrary library)
    {
        Live = live;
        Plan = plan;
        Search = search;
        Preview = preview;
        Plans = plans;
        Remote = remote;
        Projection = projection;
        Control = control;
        _settings = settings;
        LibraryWarning = library.Errors.Count > 0 ? $"Nie udało się odczytać pieśni: {library.Errors.Count}" : null;

        plan.ItemSelected += (_, item) => preview.ShowPlanItem(item);
        search.SongSelected += (_, song) => preview.ShowSong(song);
        projection.Changed += (_, _) => OnPropertyChanged(nameof(ProjectionStatus));
        settings.Changed += (_, _) => OnSettingsChanged();
        ApplyTheme();
    }

    public LiveViewModel Live { get; }

    public PlanViewModel Plan { get; }

    public SearchViewModel Search { get; }

    public PreviewViewModel Preview { get; }

    public PlansViewModel Plans { get; }

    public RemoteViewModel Remote { get; }

    public IProjectionController Projection { get; }

    public ILiveControl Control { get; }

    public string? LibraryWarning { get; }

    public string ProjectionStatus => Projection.Status;

    public bool IsLivePanelVisible => _settings.Current.ShowLivePanel;

    public bool ShowChords => _settings.Current.ShowChords;

    public bool IsDarkTheme => _settings.Current.Theme == AppTheme.Dark;

    public bool IsProjectionDark => _settings.Current.ProjectionTheme == AppTheme.Dark;

    [RelayCommand]
    private void ToggleProjection() => Projection.Toggle();

    [RelayCommand]
    private void ShowOnThisScreen() => Projection.ShowOnSameScreen();

    [RelayCommand]
    private void ToggleLivePanel() => _settings.Update(s => s with { ShowLivePanel = !s.ShowLivePanel });

    [RelayCommand]
    private void ToggleChords() => _settings.Update(s => s with { ShowChords = !s.ShowChords });

    [RelayCommand]
    private void ToggleTheme() =>
        _settings.Update(s => s with { Theme = s.Theme == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark });

    [RelayCommand]
    private void ToggleProjectionTheme() =>
        _settings.Update(s => s with { ProjectionTheme = s.ProjectionTheme == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark });

    private void OnSettingsChanged()
    {
        ApplyTheme();
        OnPropertyChanged(nameof(IsLivePanelVisible));
        OnPropertyChanged(nameof(ShowChords));
        OnPropertyChanged(nameof(IsDarkTheme));
        OnPropertyChanged(nameof(IsProjectionDark));
    }

    private void ApplyTheme()
    {
        if (Application.Current is { } app)
            app.RequestedThemeVariant = _settings.Current.Theme == AppTheme.Dark ? ThemeVariant.Dark : ThemeVariant.Light;
    }
}
