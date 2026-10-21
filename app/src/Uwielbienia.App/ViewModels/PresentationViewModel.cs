using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Uwielbienia.App.Services;
using Uwielbienia.Core.Plans;
using Uwielbienia.Core.Presentation;
using Uwielbienia.Core.Slideshows;

namespace Uwielbienia.App.ViewModels;

/// <summary>
/// Kolumna „Pieśń” dla prezentacji: miniatury slajdów, stan ich przygotowania i przejścia — na prezentację (jak przy
/// pieśni: jak w planie albo własne) i między jej slajdami (domyślnie przenikanie).
/// </summary>
public sealed partial class PresentationViewModel : ObservableObject, IDisposable
{
    private readonly ISlideshowLibrary _library;
    private readonly ActivePlan _plan;

    public PresentationViewModel(PresentationPlanItem item, ISlideshowLibrary library, ActivePlan plan)
    {
        Item = item;
        _library = library;
        _plan = plan;
        Transitions = new TransitionOverrideViewModel(own => Save(Item with { Transitions = own }));
        SlideChoice.Chosen += (_, chosen) =>
        {
            OnPropertyChanged(nameof(SlidesButtonText));
            Save(Item with { SlideEffects = chosen.Count == 0 ? null : chosen });
        };
        library.Changed += OnChanged;
        library.Progress += OnProgress;
        Show(item);
        Refresh();
    }

    public PresentationPlanItem Item { get; private set; }

    /// <summary>Przejście na prezentację z innej pozycji planu.</summary>
    public TransitionOverrideViewModel Transitions { get; }

    /// <summary>Przejście między slajdami prezentacji: żaden efekt = przenikanie.</summary>
    public TransitionChoiceViewModel SlideChoice { get; } = new();

    /// <summary>Np. „Slajdy: przenikanie”, „Slajdy: Akwarela”.</summary>
    public string SlidesButtonText => "Slajdy: " + SlideChoice.Description;

    /// <summary>Ta sama pozycja po zapisie albo zmianie przejścia planu — odśwież wybór bez wczytywania slajdów od nowa.</summary>
    public void Show(PresentationPlanItem item)
    {
        Item = item;
        Transitions.Show(item.Transitions, _plan.Plan?.ChosenTransitions ?? Plan.DefaultTransitions);
        SlideChoice.Show(item.SlideEffects ?? []);
        OnPropertyChanged(nameof(SlidesButtonText));
    }

    private void Save(PresentationPlanItem item)
    {
        Item = item;
        _plan.Update(p => p.Replace(item));
    }

    public string Title => Item.Title;

    public ObservableCollection<PresentationSlideViewModel> Slides { get; } = [];

    /// <summary>Przygotowywanie albo błąd; <c>null</c> = slajdy gotowe.</summary>
    [ObservableProperty]
    public partial string? Note { get; private set; }

    [ObservableProperty]
    public partial bool IsFailed { get; private set; }

    public string? Summary => Slides.Count switch
    {
        0 => null,
        1 => "1 slajd",
        var n when n % 10 is >= 2 and <= 4 && n % 100 is < 12 or > 14 => $"{n} slajdy",
        var n => $"{n} slajdów",
    };

    [RelayCommand]
    private void Retry() => _library.Retry(Item.Folder);

    public void Dispose()
    {
        _library.Changed -= OnChanged;
        _library.Progress -= OnProgress;
    }

    private void OnChanged(object? sender, string folder)
    {
        if (folder == Item.Folder)
            Refresh();
    }

    private void OnProgress(object? sender, SlideshowProgress progress)
    {
        if (progress.Folder == Item.Folder)
            Note = progress.Text;
    }

    private void Refresh()
    {
        var state = _library.Peek(Item.Folder);
        Note = state.Note;
        IsFailed = state.IsFailed;
        Slides.Clear();
        foreach (var (file, i) in state.Slides.Select((f, i) => (f, i)))
            Slides.Add(new PresentationSlideViewModel(i + 1, file));
        OnPropertyChanged(nameof(Summary));
    }
}

public sealed class PresentationSlideViewModel(int number, string file)
{
    public string Label { get; } = $"Slajd {number}";

    public Task<Bitmap?> Thumbnail => SlideImages.LoadThumbnailAsync(file);
}
