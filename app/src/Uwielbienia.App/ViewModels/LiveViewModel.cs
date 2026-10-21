using System.Collections.ObjectModel;
using Avalonia.Animation;
using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.Input;
using Uwielbienia.App.Services;
using Uwielbienia.App.Transitions;
using Uwielbienia.Core.Kiosks;
using Uwielbienia.Core.Presentation;

namespace Uwielbienia.App.ViewModels;

/// <summary>Panel „Na ekranie”: dokładnie to, co widzi sala, plus akordy i skróty do slajdów bieżącej pieśni.</summary>
public sealed partial class LiveViewModel : ObservableObject
{
    private readonly ILiveControl _control;

    public LiveViewModel(
        ILiveStateSource source, ILiveControl control, ProjectionAppearance appearance, VideoControlsViewModel video, KioskPlayback kiosk)
    {
        _control = control;
        Appearance = appearance;
        Video = video;
        Kiosk = kiosk;
        kiosk.PropertyChanged += (_, _) => OnPropertyChanged(nameof(CurrentLabel));
        source.StateChanged += (_, state) => Apply(state);
        Apply(source.State);
    }

    public ProjectionAppearance Appearance { get; }

    /// <summary>Film na ekranie: podgląd i sterowanie odtwarzaniem.</summary>
    public VideoControlsViewModel Video { get; }

    /// <summary>Kiosk na ekranie: bieżący element (zmienia się sam).</summary>
    public KioskPlayback Kiosk { get; }

    [ObservableProperty]
    public partial LiveState State { get; private set; } = LiveState.Empty;

    public Slide? VisibleSlide => State.VisibleSlide;

    /// <summary>Strona rzutnika: slajd i — na pierwszym slajdzie pieśni — jej tytuł; slajd prezentacji — obraz; film.</summary>
    public ProjectedPage? Page => State.VisibleSlide switch
    {
        ImageSlide image => new ProjectedImage(image),
        VideoSlide video => new ProjectedVideo(video, Video.Frames),
        KioskSlide kiosk => new ProjectedKiosk(kiosk, Kiosk),
        { } slide when State.Item is { } item => new ProjectedPage(slide, State.SlideIndex == 0 && item.ShowTitle ? item.Title : null),
        _ => null,
    };

    /// <summary>Ten sam slajd, czarny ekran, pieśń spoza planu i wybrane samo przenikanie: krótkie przenikanie.</summary>
    public static readonly IPageTransition SlideChange = new DirectCrossFade(TimeSpan.FromSeconds(0.15));

    /// <summary>
    /// Przejście do strony <see cref="Page"/>: na inną pozycję planu — jej efekty (<see cref="LiveItem.Transitions"/>), między slajdami
    /// jednej pozycji — efekty „Slajdy: …” (<see cref="LiveItem.InnerTransitions"/>), poza tym <see cref="SlideChange"/>. Ustawiane przed zmianą <see cref="Page"/>.
    /// </summary>
    [ObservableProperty]
    public partial IPageTransition PageTransition { get; private set; } = SlideChange;

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

    /// <summary>Slajd prezentacji (miniatura) — zamiast wersów.</summary>
    /// <summary>Slajd prezentacji w kolumnie „Na ekranie” — ta sama strona co na rzutniku (także przy czarnym ekranie).</summary>
    public ProjectedImage? CurrentImage => CurrentTab?.Slide is ImageSlide image ? new ProjectedImage(image) : null;

    public Task<Bitmap?>? NextImage => NextTab?.Slide is ImageSlide image ? SlideImages.LoadThumbnailAsync(image.ImageFile) : null;

    public bool HasCurrentImage => CurrentTab?.Slide is ImageSlide;

    public bool HasNextImage => NextTab?.Slide is ImageSlide;

    public bool HasCurrentVideo => CurrentTab?.Slide is VideoSlide;

    /// <summary>Kiosk w kolumnie „Na ekranie” — tak jak na rzutniku, także pod czarnym ekranem (zmiana wtedy stoi).</summary>
    public ProjectedKiosk? CurrentKiosk => CurrentTab?.Slide is KioskSlide kiosk ? new ProjectedKiosk(kiosk, Kiosk) : null;

    public bool HasCurrentKiosk => CurrentTab?.Slide is KioskSlide;

    public string? CurrentLabel => (HasCurrentKiosk ? Kiosk.PositionText : null) ?? CurrentTab?.Label ?? (HasItem && Slides.Count == 0 ? State.Item?.Note ?? "Brak slajdów" : null);

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
        PageTransition = TransitionTo(State, state);
        State = state;
        if (itemChanged)
        {
            Slides.Clear();
            foreach (var (slide, index) in (state.Item?.Slides ?? []).Select((s, i) => (s, i)))
                Slides.Add(new SlideTabViewModel(index, slide));
        }
        foreach (var tab in Slides)
            tab.IsCurrent = tab.Index == state.SlideIndex;
        // Następny slajd prezentacji wczytany zawczasu — „Dalej” bez czekania na obraz.
        if (NextTab?.Slide is ImageSlide next)
            SlideImages.Preload(next.ImageFile);

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
        OnPropertyChanged(nameof(CurrentImage));
        OnPropertyChanged(nameof(NextImage));
        OnPropertyChanged(nameof(HasCurrentImage));
        OnPropertyChanged(nameof(HasNextImage));
        OnPropertyChanged(nameof(HasCurrentVideo));
        OnPropertyChanged(nameof(CurrentKiosk));
        OnPropertyChanged(nameof(HasCurrentKiosk));
        OnPropertyChanged(nameof(CurrentLabel));
        OnPropertyChanged(nameof(UpcomingLabel));
        OnPropertyChanged(nameof(HasUpcoming));
        OnPropertyChanged(nameof(ProgressValue));
        OnPropertyChanged(nameof(ProgressText));
    }

    private IPageTransition TransitionTo(LiveState before, LiveState after)
    {
        var effects = after.TransitionsFrom(before);
        LogScreenChange(before, after, effects);
        return effects switch
        {
            [] => SlideChange,
            _ when PageTransition is ShaderPageTransition current && current.Effects.SequenceEqual(effects) => current,
            _ => ShaderPageTransition.For(effects),
        };
    }

    /// <summary>Zmiana slajdu, pozycji albo czarnego ekranu — do dziennika przejść z tym, czy dostała efekt, czy krótkie przenikanie.</summary>
    private static void LogScreenChange(LiveState before, LiveState after, IReadOnlyList<SlideTransition> effects)
    {
        var within = after.IsWithinItem(before) && before.IsBlank == after.IsBlank;
        if (within && before.SlideIndex == after.SlideIndex)
            return;
        var blank = before.IsBlank || after.IsBlank ? $" (czarny ekran {before.IsBlank} → {after.IsBlank})" : "";
        var chosen = string.Join(", ", ((within ? after.Item?.InnerTransitions : after.Item?.Transitions) ?? []).Select(SlideTransitions.Name));
        var result = effects.Count == 0 ? $"krótkie przenikanie — {after.NoTransitionsReason(before)}" : within ? "efekt slajdów (pozycji albo planu)" : "efekt planu";
        if (within)
        {
            TransitionLog.Write($"Slajd {before.SlideIndex + 1} → {after.SlideIndex + 1} w „{after.Item?.Title}”; slajdy mają [{chosen}] → {result}");
            return;
        }
        TransitionLog.Write($"Ekran: „{before.Item?.Title}” → „{after.Item?.Title}”{blank}; plan ma [{chosen}] → {result}");
    }
}

/// <summary>
/// To, co rysuje rzutnik. Jeden obiekt (a nie osobne właściwości), żeby tytuł pojawiał się
/// i znikał w tym samym przejściu co tekst slajdu.
/// </summary>
public record ProjectedPage(Slide Slide, string? Title)
{
    public bool HasTitle => Title is not null;
}

/// <summary>Slajd prezentacji na rzutniku: obraz na cały ekran (wczytywany w tle).</summary>
public sealed record ProjectedImage(ImageSlide Image) : ProjectedPage(Image, null), IWaitablePage
{
    public Task<Bitmap?> Picture => SlideImages.LoadAsync(Image.ImageFile);

    public Task Ready => Picture;
}

/// <summary>Film na rzutniku: klatki na cały ekran na czarnym tle.</summary>
public sealed record ProjectedVideo(VideoSlide Video, VideoFrames Frames) : ProjectedPage(Video, null);

public sealed partial class SlideTabViewModel(int index, Slide slide) : ObservableObject
{
    public int Index { get; } = index;

    public Slide Slide { get; } = slide;

    public string Label => Slide.Label;

    public string FirstLine => Slide.Lines.Count > 0 ? Slide.Lines[0].Text : "";

    [ObservableProperty]
    public partial bool IsCurrent { get; set; }
}
