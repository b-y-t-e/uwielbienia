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
/// Na ekran pieśń trafia tylko dwuklikiem w planie. Tu też edytuje się pieśni i teksty (<see cref="Editor"/>).
/// </summary>
public sealed partial class PreviewViewModel : ObservableObject
{
    private readonly ISlideBuilder _slides;
    private readonly ActivePlan _plan;
    private readonly PlanActions _planActions;
    private readonly ISongLibrary _library;
    private readonly ISongEditor _editor;
    private SongPlanItem? _planItem;
    private Action<Song>? _created;

    public PreviewViewModel(ISlideBuilder slides, ActivePlan plan, PlanActions planActions, ISongLibrary library, ISongEditor editor)
    {
        Parts.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasRemovedSections));
        _slides = slides;
        _plan = plan;
        _planActions = planActions;
        _library = library;
        _editor = editor;
        // Pieśń spoza planu po edycji: wczytaj nową wersję (pozycje planu odświeża plan).
        library.Changed += (_, _) =>
        {
            if (Song is { } song && _planItem is null)
                Reload(song.Id);
        };
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSong), nameof(Title), nameof(Number), nameof(Category), nameof(OriginText), nameof(IsText), nameof(ColumnTitle))]
    public partial Song? Song { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditing), nameof(IsViewing), nameof(ColumnTitle))]
    public partial SongEditorViewModel? Editor { get; private set; }

    public bool IsEditing => Editor is not null;

    /// <summary>Nagłówek kolumny: rodzaj wybranej pozycji, a w trakcie edycji — co jest edytowane.</summary>
    public string ColumnTitle => Editor?.Heading ?? (IsText ? "Tekst" : "Pieśń");

    public bool IsViewing => Editor is null;

    public bool IsText => Song?.IsText == true;

    /// <summary>Znacznik przy tytule: własny tekst albo pieśń śpiewnika zmieniona na tym komputerze.</summary>
    public string? OriginText => Song?.Origin switch
    {
        SongOrigin.Local => Song.IsText ? "własny tekst" : "własna pieśń",
        SongOrigin.Modified => "zmieniona na tym komputerze",
        _ => null,
    };

    [ObservableProperty]
    public partial bool IsFromPlan { get; private set; }

    public bool HasSong => Song is not null;

    public string? Title => Song?.DisplayTitle;

    public string? Number => Song?.Number?.ToString();

    public string? Category => Song?.Category;

    public ObservableCollection<SongPartViewModel> Parts { get; } = [];

    /// <summary>Części pieśni usunięte z układu — do „Przywróć usuniętą część”.</summary>
    public IReadOnlyList<Section> RemovedSections =>
        Song?.Sections.Where(s => Parts.All(p => p.Code != s.Code)).ToList() ?? [];

    public bool HasRemovedSections => RemovedSections.Count > 0;

    public void ShowPlanItem(PlanItemViewModel item)
    {
        // Plan przebudowuje się po każdym zapisie (np. zmianie układu) — ta sama pozycja z tym samym
        // układem nie wymaga ponownego wczytania, które skasowałoby listę części w trakcie jej zmiany.
        if (_planItem?.Id == item.Item.Id && ReferenceEquals(Song, item.Song) && SameLayout(_planItem, item.Item))
        {
            _planItem = item.Item;
            return;
        }
        _planItem = item.Item;
        IsFromPlan = true;
        Load(item.Song, item.Item);
    }

    public void ShowSong(Song song)
    {
        _planItem = null;
        IsFromPlan = false;
        Load(song, null);
    }

    // Układ pieśni w planie: kolejność (przeciąganie), powtórzenie, usunięcie i dodanie części.

    public void MovePart(SongPartViewModel part, int newIndex)
    {
        var index = Parts.IndexOf(part);
        if (index < 0 || newIndex == index)
            return;
        Parts.Move(index, Math.Clamp(newIndex, 0, Parts.Count - 1));
        SaveLayout();
    }

    [RelayCommand]
    private void DuplicatePart(SongPartViewModel part)
    {
        var index = Parts.IndexOf(part);
        if (index < 0 || Song is not { } song)
            return;
        Parts.Insert(index + 1, CreatePart(song, new LayoutEntry(part.Code, part.IsIncluded)));
        SaveLayout();
    }

    [RelayCommand]
    private void RemovePart(SongPartViewModel part)
    {
        if (Parts.Remove(part))
            SaveLayout();
    }

    /// <summary>Dodaje część pieśni na koniec układu (np. przywraca usuniętą).</summary>
    public void AddPart(string code)
    {
        if (Song is not { } song)
            return;
        Parts.Add(CreatePart(song, new LayoutEntry(code)));
        SaveLayout();
    }

    [RelayCommand]
    private void Edit()
    {
        if (Song is { } song)
            OpenEditor(SongDraft.From(song));
    }

    /// <summary>Nowa pieśń lub tekst; <paramref name="created"/> dostaje zapisaną (np. żeby wstawić ją do planu).</summary>
    public void StartNew(SongKind kind, Action<Song>? created = null)
    {
        _created = created;
        OpenEditor(SongDraft.New(kind));
    }

    private void OpenEditor(SongDraft draft) =>
        Editor = new SongEditorViewModel(draft, _editor, OnEditorClosed);

    /// <summary>
    /// Zmiany i usunięcia odświeża już przeładowanie biblioteki (plan i <see cref="Reload"/>);
    /// tu zostaje tylko pokazanie nowo utworzonej pieśni lub tekstu.
    /// </summary>
    private void OnEditorClosed(EditorOutcome outcome, Song? saved)
    {
        var isNew = Editor?.IsNew == true;
        var created = _created;
        _created = null;
        Editor = null;
        if (outcome != EditorOutcome.Saved || !isNew || saved is null)
            return;
        if (created is not null)
            created(saved);
        else
            ShowSong(saved);
    }

    private void Reload(string id)
    {
        if (_library.Find(id) is { } song)
            Load(song, null);
        else
            Clear();
    }

    public void Clear()
    {
        Editor = null;
        _planItem = null;
        IsFromPlan = false;
        Song = null;
        Parts.Clear();
    }

    private void Load(Song song, SongPlanItem? item)
    {
        Song = song;
        Parts.Clear();
        var layout = item?.LayoutFor(song) ?? song.Arrangement.Select(code => new LayoutEntry(code)).ToList();
        foreach (var entry in layout)
            Parts.Add(CreatePart(song, entry));
    }

    private SongPartViewModel CreatePart(Song song, LayoutEntry entry)
    {
        var lines = _slides.Build(song, [entry.Code]).SelectMany(s => s.Lines).ToList();
        var part = new SongPartViewModel(entry.Code, song.FindSection(entry.Code)?.Name ?? entry.Code, lines, entry.Sung);
        part.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SongPartViewModel.IsIncluded))
                SaveLayout();
        };
        return part;
    }

    /// <summary>Zapisuje układ w pozycji planu; dla granej pieśni zmiana działa od razu na ekranie.</summary>
    private void SaveLayout()
    {
        if (_planItem is null || Song is not { } song)
            return;
        _planItem = _planItem.WithLayout(song, Parts.Select(p => new LayoutEntry(p.Code, p.IsIncluded)).ToList());
        _plan.Update(p => p.Replace(_planItem));
    }

    private static bool SameLayout(SongPlanItem a, SongPlanItem b) =>
        Same(a.Arrangement, b.Arrangement) && Same(a.Layout, b.Layout);

    private static bool Same<T>(IReadOnlyList<T>? a, IReadOnlyList<T>? b) =>
        a is null || b is null ? a is null && b is null : a.SequenceEqual(b);
}

/// <summary>Jedno wystąpienie części pieśni w kolejności wykonania (np. drugi „Refren”).</summary>
public sealed partial class SongPartViewModel(string code, string name, IReadOnlyList<SlideLine> lines, bool included)
    : ObservableObject, IReorderableItem
{
    [ObservableProperty]
    public partial bool IsDragged { get; set; }

    [ObservableProperty]
    public partial bool IsDropBefore { get; set; }

    [ObservableProperty]
    public partial bool IsDropAfter { get; set; }

    public string Code { get; } = code;

    public string Name { get; } = name;

    public IReadOnlyList<SlideLine> Lines { get; } = lines;

    [ObservableProperty]
    public partial bool IsIncluded { get; set; } = included;
}
