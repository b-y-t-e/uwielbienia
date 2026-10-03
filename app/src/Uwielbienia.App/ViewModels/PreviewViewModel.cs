using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Uwielbienia.Core.Plans;
using Uwielbienia.Core.Presentation;
using Uwielbienia.Core.Songs;

namespace Uwielbienia.App.ViewModels;

/// <summary>
/// Kolumna „Pieśń”: wybrana pieśń, części jedna pod drugą. Pole wyboru przy części decyduje,
/// czy jest śpiewana (wyświetlana); wybór zapisuje się w planie, a dla pieśni na ekranie działa na żywo.
/// Na ekran pieśń trafia tylko dwuklikiem w planie.
/// </summary>
public sealed partial class PreviewViewModel : ObservableObject
{
    private readonly ISlideBuilder _slides;
    private readonly ActivePlan _plan;
    private readonly PlanActions _planActions;
    private SongPlanItem? _planItem;

    public PreviewViewModel(ISlideBuilder slides, ActivePlan plan, PlanActions planActions)
    {
        _slides = slides;
        _plan = plan;
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

    public ObservableCollection<SongPartViewModel> Parts { get; } = [];

    public void ShowPlanItem(PlanItemViewModel item)
    {
        // Plan przebudowuje się po każdym zapisie (np. zaznaczeniu części) — ta sama pozycja z tym samym
        // układem nie wymaga ponownego wczytania, które skasowałoby pola wyboru w trakcie ich zmiany.
        if (_planItem?.Id == item.Item.Id && ReferenceEquals(Song, item.Song) &&
            SameArrangement(_planItem.Arrangement, item.Item.Arrangement))
        {
            _planItem = item.Item;
            return;
        }
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
    private void AddAsNext()
    {
        if (Song is null)
            return;
        _planActions.AddAfterLive(Song, SelectedArrangement());
    }

    [RelayCommand]
    private void AddToEnd()
    {
        if (Song is null)
            return;
        _planActions.AddToEnd(Song, SelectedArrangement());
    }

    public void Clear()
    {
        _planItem = null;
        IsFromPlan = false;
        Song = null;
        Parts.Clear();
    }

    private void Load(Song song, IReadOnlyList<string>? chosen)
    {
        Song = song;
        Parts.Clear();
        var remaining = chosen?.ToList();
        foreach (var code in song.Arrangement)
        {
            var included = remaining is null || remaining.Remove(code);
            var lines = _slides.Build(song, [code]).SelectMany(s => s.Lines).ToList();
            var part = new SongPartViewModel(code, song.FindSection(code)?.Name ?? code, lines, included);
            part.PropertyChanged += (_, _) => OnArrangementChanged();
            Parts.Add(part);
        }
    }

    private void OnArrangementChanged()
    {
        if (_planItem is not null)
        {
            var arrangement = SelectedArrangement();
            _planItem = _planItem with { Arrangement = arrangement };
            _plan.Update(p => p.Replace(_planItem));
        }
    }

    private static bool SameArrangement(IReadOnlyList<string>? a, IReadOnlyList<string>? b) =>
        a is null || b is null ? a is null && b is null : a.SequenceEqual(b);

    /// <summary><c>null</c>, gdy zaznaczone są wszystkie części — plan podąża wtedy za plikiem pieśni.</summary>
    private IReadOnlyList<string>? SelectedArrangement() =>
        Parts.All(p => p.IsIncluded) ? null : Parts.Where(p => p.IsIncluded).Select(p => p.Code).ToList();
}

/// <summary>Jedno wystąpienie części pieśni w kolejności wykonania (np. drugi „Refren”).</summary>
public sealed partial class SongPartViewModel(string code, string name, IReadOnlyList<SlideLine> lines, bool included)
    : ObservableObject
{
    public string Code { get; } = code;

    public string Name { get; } = name;

    public IReadOnlyList<SlideLine> Lines { get; } = lines;

    [ObservableProperty]
    public partial bool IsIncluded { get; set; } = included;
}
