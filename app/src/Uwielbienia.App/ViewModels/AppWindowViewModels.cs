using CommunityToolkit.Mvvm.ComponentModel;
using Uwielbienia.App.Services;
using Uwielbienia.Core.AppWindows;
using Uwielbienia.Core.Plans;

namespace Uwielbienia.App.ViewModels;

/// <summary>Aplikacja na ekranie (kolumna „Na ekranie”): ten sam obraz co na rzutniku i czy program jest uruchomiony.</summary>
public sealed partial class AppWindowLiveViewModel : ObservableObject
{
    private readonly AppWindowCapture _capture;

    public AppWindowLiveViewModel(AppWindowCapture capture)
    {
        _capture = capture;
        capture.ShownChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(IsWaiting));
        };
    }

    public VideoFrames Frames => _capture.Frames;

    public bool IsWaiting => _capture.Shown is null;

    /// <summary>Program nie jest uruchomiony albo jego okna nie da się pokazać — sala widzi czarny ekran.</summary>
    public string Status => _capture.Unavailable ?? _capture.Failure ?? (_capture.Shown is null ? "Program nie jest uruchomiony — ekran jest czarny" : "");
}

/// <summary>Kolumna „Pieśń” dla aplikacji: który program będzie pokazywany.</summary>
public sealed class AppWindowDetailsViewModel(AppWindowPlanItem item, IAppWindowCapture capture)
{
    public AppWindowPlanItem Item { get; } = item;

    public string Title => Item.Title;

    /// <summary>Aplikacja ze Sklepu — po tytule okna, zwykły program — po nazwie (dowolne jego okno).</summary>
    public string Source => Item.WindowTitle is { } window ? $"Okno „{window}”" : $"Program {Item.Process}";

    public string? Unavailable { get; } = capture.Unavailable;
}

/// <summary>Okno do wyboru w „Dodaj do planu” → „Aplikacja”.</summary>
public sealed class AppWindowChoiceViewModel(AppWindowInfo window)
{
    public AppWindowInfo Window { get; } = window;

    public string Title => Window.Title;

    public string Process => Window.Process;
}
