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

    public string Title => State.Item is { } item ? item.Title : "Ekran jest pusty";

    public string? Number => State.Item?.Number.ToString();

    public bool IsBlank => State.IsBlank;

    public bool HasItem => State.Item is not null;

    public string? NextText => State.Next is { } next ? $"Dalej: {next}" : null;

    public ObservableCollection<SlideTabViewModel> Slides { get; } = [];

    /// <summary>Wersy bieżącego slajdu z akordami — dla operatora i muzyków.</summary>
    public IReadOnlyList<SlideLine> ChordLines => State.Slide?.Lines ?? [];

    [RelayCommand]
    private void Next() => _control.Next();

    [RelayCommand]
    private void Previous() => _control.Previous();

    [RelayCommand]
    private void ToggleBlank() => _control.ToggleBlank();

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
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Number));
        OnPropertyChanged(nameof(IsBlank));
        OnPropertyChanged(nameof(HasItem));
        OnPropertyChanged(nameof(NextText));
        OnPropertyChanged(nameof(ChordLines));
    }
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
