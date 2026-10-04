using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Uwielbienia.Core.Presentation;

namespace Uwielbienia.App.ViewModels;

/// <summary>Panel „Na ekranie”: dokładnie to, co widzi sala, plus akordy i skróty do slajdów bieżącej pieśni.</summary>
public sealed partial class LiveViewModel : ObservableObject
{
    private readonly ILiveControl _control;

    public LiveViewModel(ILiveStateSource source, ILiveControl control, ProjectionAppearance appearance)
    {
        _control = control;
        Appearance = appearance;
        source.StateChanged += (_, state) => Apply(state);
        Apply(source.State);
    }

    public ProjectionAppearance Appearance { get; }

    [ObservableProperty]
    public partial LiveState State { get; private set; } = LiveState.Empty;

    public Slide? VisibleSlide => State.VisibleSlide;

    /// <summary>Strona rzutnika: slajd i — na pierwszym slajdzie pieśni — jej tytuł.</summary>
    public ProjectedPage? Page => State.VisibleSlide is { } slide && State.Item is { } item
        ? new ProjectedPage(slide, State.SlideIndex == 0 && item.HasOwnTitle ? item.Title : null)
        : null;

    public string Title => State.Item is { } item ? item.Title : "Ekran jest pusty";

    public string? Number => State.Item?.Number?.ToString();

    public bool IsBlank => State.IsBlank;

    public bool HasItem => State.Item is not null;

    public string? NextText => State.Next is { } next ? $"Dalej: {next}" : null;

    public ObservableCollection<SlideTabViewModel> Slides { get; } = [];

    public SlideTabViewModel? CurrentTab =>
        State.Item is not null && State.SlideIndex >= 0 && State.SlideIndex < Slides.Count
            ? Slides[State.SlideIndex]
            : null;

    public SlideTabViewModel? NextTab =>
        State.Item is not null && State.SlideIndex >= 0 && State.SlideIndex + 1 < Slides.Count
            ? Slides[State.SlideIndex + 1]
            : null;

    public IReadOnlyList<SlideLine> CurrentLines => CurrentTab?.Slide.Lines ?? [];

    public IReadOnlyList<SlideLine> NextLines => NextTab?.Slide.Lines ?? [];

    public string? CurrentLabel => CurrentTab?.Label ?? (HasItem && Slides.Count == 0 ? "Wszystkie części pominięte" : null);

    public string? UpcomingLabel => NextTab?.Label ?? NextText;

    public bool HasUpcoming => UpcomingLabel is not null;

    public double ProgressValue => Slides.Count == 0 ? 0 : (State.SlideIndex + 1d) / Slides.Count * 100;

    public string ProgressText => Slides.Count == 0
        ? ""
        : $"{State.SlideIndex + 1} z {Slides.Count} · {ProgressValue:0}%";

    [RelayCommand]
    private void Next() => _control.Next();

    [RelayCommand]
    private void Previous() => _control.Previous();

    [RelayCommand]
    private void Clear() => _control.Clear();

    [RelayCommand]
    private void GoTo(SlideTabViewModel tab) => _control.GoToSlide(tab.Index);

    private void Apply(LiveState state)
    {
        var itemChanged = !ReferenceEquals(State.Item, state.Item);
        State = state;
        if (itemChanged)
        {
            Slides.Clear();
            foreach (var (slide, index) in (state.Item?.Slides ?? []).Select((s, i) => (s, i)))
                Slides.Add(new SlideTabViewModel(index, slide));
        }
        foreach (var tab in Slides)
            tab.IsCurrent = tab.Index == state.SlideIndex;

        OnPropertyChanged(nameof(VisibleSlide));
        OnPropertyChanged(nameof(Page));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Number));
        OnPropertyChanged(nameof(IsBlank));
        OnPropertyChanged(nameof(HasItem));
        OnPropertyChanged(nameof(NextText));
        OnPropertyChanged(nameof(CurrentTab));
        OnPropertyChanged(nameof(NextTab));
        OnPropertyChanged(nameof(CurrentLines));
        OnPropertyChanged(nameof(NextLines));
        OnPropertyChanged(nameof(CurrentLabel));
        OnPropertyChanged(nameof(UpcomingLabel));
        OnPropertyChanged(nameof(HasUpcoming));
        OnPropertyChanged(nameof(ProgressValue));
        OnPropertyChanged(nameof(ProgressText));
    }
}

/// <summary>
/// To, co rysuje rzutnik. Jeden obiekt (a nie osobne właściwości), żeby tytuł pojawiał się
/// i znikał w tym samym przejściu co tekst slajdu.
/// </summary>
public sealed record ProjectedPage(Slide Slide, string? Title)
{
    public bool HasTitle => Title is not null;
}

public sealed partial class SlideTabViewModel(int index, Slide slide) : ObservableObject
{
    public int Index { get; } = index;

    public Slide Slide { get; } = slide;

    public string Label => Slide.Label;

    public string FirstLine => Slide.Lines.Count > 0 ? Slide.Lines[0].Text : "";

    [ObservableProperty]
    public partial bool IsCurrent { get; set; }
}
