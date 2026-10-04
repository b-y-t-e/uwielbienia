using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Uwielbienia.Core.Songs;

namespace Uwielbienia.App.ViewModels;

/// <summary>
/// „Dodaj do planu” — okno z wyszukiwarką pieśni i tekstów otwierane z planu („+”, „Dodaj przed / po”).
/// Wybrana pozycja trafia w zapamiętane miejsce; „Nowa pieśń / Nowy tekst” tworzy ją i wstawia tam samo.
/// </summary>
public sealed partial class PlanAddViewModel(ISongSearch search, ISongLibrary library, PlanActions actions) : ObservableObject
{
    private int _index;

    [ObservableProperty]
    public partial bool IsOpen { get; private set; }

    [ObservableProperty]
    public partial string Query { get; set; } = "";

    [ObservableProperty]
    public partial SongResultViewModel? Selected { get; set; }

    public ObservableCollection<SongResultViewModel> Results { get; } = [];

    /// <summary>Utworzenie nowej pozycji — obsługuje kolumna „Pieśń” (edytor), potem wstawienie w <c>Index</c>.</summary>
    public event EventHandler<(SongKind Kind, int Index)>? NewRequested;

    /// <param name="index">Miejsce w planie, w które trafi wybrana pozycja.</param>
    /// <param name="query">Początek wpisany „na szybko” (pisanie gdziekolwiek w oknie).</param>
    public void Open(int index, string query = "")
    {
        _index = index;
        Query = query;
        Fill();
        IsOpen = true;
    }

    public bool HasSelection => Selected is not null;

    partial void OnSelectedChanged(SongResultViewModel? value) => OnPropertyChanged(nameof(HasSelection));

    partial void OnQueryChanged(string value) => Fill();

    /// <summary>Bez zapytania: najpierw własne teksty (części Mszy, modlitwy), potem śpiewnik.</summary>
    private void Fill()
    {
        Results.Clear();
        var songs = Query.Trim().Length == 0
            ? library.Songs.Where(s => s.IsText).Concat(library.Songs.Where(s => !s.IsText))
            : search.Search(Query, 60);
        foreach (var song in songs.Take(300))
            Results.Add(new SongResultViewModel(song));
        Selected = Results.FirstOrDefault();
    }

    public void MoveSelection(int step)
    {
        if (Results.Count == 0)
            return;
        var index = Selected is null ? -1 : Results.IndexOf(Selected);
        Selected = Results[Math.Clamp(index + step, 0, Results.Count - 1)];
    }

    [RelayCommand]
    private void Add(SongResultViewModel? result)
    {
        if ((result ?? Selected) is not { } target)
            return;
        actions.InsertAt(target.Song, _index);
        IsOpen = false;
    }

    [RelayCommand]
    private void NewSong() => RequestNew(SongKind.Song);

    [RelayCommand]
    private void NewText() => RequestNew(SongKind.Text);

    [RelayCommand]
    private void Close() => IsOpen = false;

    private void RequestNew(SongKind kind)
    {
        IsOpen = false;
        NewRequested?.Invoke(this, (kind, _index));
    }
}
