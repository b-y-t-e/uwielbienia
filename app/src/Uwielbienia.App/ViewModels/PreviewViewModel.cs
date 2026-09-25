using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Uwielbienia.Core.Plans;
using Uwielbienia.Core.Presentation;
using Uwielbienia.Core.Songs;

namespace Uwielbienia.App.ViewModels;

/// <summary>
/// Środkowa kolumna: podgląd wybranej pieśni. Tu wybiera się części do zaśpiewania (chipy)
/// i świadomie wysyła pieśń lub konkretny slajd na ekran. Sam podgląd nigdy nie zmienia ekranu.
/// </summary>
public sealed partial class PreviewViewModel : ObservableObject
{
    private readonly ISlideBuilder _slides;
    private readonly ActivePlan _plan;
    private readonly ILiveControl _control;
    private readonly ILiveItemFactory _items;
    private readonly PlanActions _planActions;
    private SongPlanItem? _planItem;

    public PreviewViewModel(ISlideBuilder slides, ActivePlan plan, ILiveControl control, ILiveItemFactory items, PlanActions planActions)
    {
        _slides = slides;
        _plan = plan;
        _control = control;
        _items = items;
        _planActions = planActions;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSong), nameof(Title), nameof(Number), nameof(Category))]
    public partial Song? Song { get; private set; }

    [ObservableProperty]
    public partial bool IsFromPlan { get; private set; }

    public bool HasSong => Song is not null;

    public string? Title => Song?.Title;

    public string? Number => Song?.Number.ToString();

    public string? Category => Song?.Category;

    public ObservableCollection<ArrangementChipViewModel> Chips { get; } = [];

    public ObservableCollection<SlideTabViewModel> Slides { get; } = [];

    public void ShowPlanItem(PlanItemViewModel item)
    {
        _planItem = item.Item;
        IsFromPlan = true;
        Load(item.Song, item.Item.Arrangement);
    }

    public void ShowSong(Song song)
    {
        _planItem = null;
        IsFromPlan = false;
        Load(song, null);
    }

    [RelayCommand]
    private void ShowLive() => ShowSlide(0);

    [RelayCommand]
    private void ShowSlideLive(SlideTabViewModel tab) => ShowSlide(tab.Index);

    [RelayCommand]
    private void AddAsNext()
    {
        if (Song is null)
            return;
        var item = _planActions.AddAfterLive(Song) with { Arrangement = SelectedArrangement() };
        _plan.Update(p => p.Replace(item));
    }

    [RelayCommand]
    private void AddToEnd()
    {
        if (Song is null)
            return;
        var item = _planActions.AddToEnd(Song) with { Arrangement = SelectedArrangement() };
        _plan.Update(p => p.Replace(item));
    }

    [RelayCommand]
    private void ResetArrangement()
    {
        foreach (var chip in Chips)
            chip.IsIncluded = true;
    }

    private void ShowSlide(int index)
    {
        if (Song is null)
            return;
        var live = _planItem is not null
            ? _plan.FindLiveItem(_planItem.Id)
            : _items.Create(Song) with { Slides = BuildSlides() };
        if (live is not null)
            _control.Show(live, index);
    }

    private void Load(Song song, IReadOnlyList<string>? chosen)
    {
        Song = song;
        Chips.Clear();
        var remaining = chosen?.ToList();
        foreach (var code in song.Arrangement)
        {
            var included = remaining is null || remaining.Remove(code);
            var chip = new ArrangementChipViewModel(code, song.FindSection(code)?.Name ?? code, included);
            chip.PropertyChanged += (_, _) => OnArrangementChanged();
            Chips.Add(chip);
        }
        RebuildSlides();
    }

    private void OnArrangementChanged()
    {
        RebuildSlides();
        if (_planItem is not null)
        {
            var arrangement = SelectedArrangement();
            _planItem = _planItem with { Arrangement = arrangement };
            _plan.Update(p => p.Replace(_planItem));
        }
    }

    /// <summary><c>null</c>, gdy zaznaczone są wszystkie części — plan podąża wtedy za plikiem pieśni.</summary>
    private IReadOnlyList<string>? SelectedArrangement() =>
        Chips.All(c => c.IsIncluded) ? null : Chips.Where(c => c.IsIncluded).Select(c => c.Code).ToList();

    private IReadOnlyList<Slide> BuildSlides() =>
        Song is null ? [] : _slides.Build(Song, Chips.Where(c => c.IsIncluded).Select(c => c.Code).ToList());

    private void RebuildSlides()
    {
        Slides.Clear();
        foreach (var (slide, index) in BuildSlides().Select((s, i) => (s, i)))
            Slides.Add(new SlideTabViewModel(index, slide));
    }
}

public sealed partial class ArrangementChipViewModel(string code, string name, bool included) : ObservableObject
{
    public string Code { get; } = code;

    public string Name { get; } = name;

    public string ShortName => Code;

    [ObservableProperty]
    public partial bool IsIncluded { get; set; } = included;
}
