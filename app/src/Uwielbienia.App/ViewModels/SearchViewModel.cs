using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Uwielbienia.Core.Plans;
using Uwielbienia.Core.Presentation;
using Uwielbienia.Core.Songs;

namespace Uwielbienia.App.ViewModels;

/// <summary>
/// Szybki wybór pieśni: numer albo słowa. Ctrl+Enter = dodaj jako następną w planie. W oknie operatora Enter
/// tylko wybiera pieśń do kolumny „Pieśń”; w nakładce na projekcji (tryb jednego ekranu) <see cref="ShowNowCommand"/>
/// pokazuje ją od razu i dodaje do planu za bieżącą.
/// Ten sam model działa w oknie operatora i w nakładce na ekranie projekcji (tryb jednego urządzenia).
/// </summary>
public sealed partial class SearchViewModel : ObservableObject
{
    private readonly ISongSearch _search;
    private readonly ISongPresenter _songs;
    private readonly ILiveControl _control;
    private readonly PlanActions _planActions;

    public SearchViewModel(ISongSearch search, ISongPresenter songs, ILiveControl control, PlanActions planActions)
    {
        _search = search;
        _songs = songs;
        _control = control;
        _planActions = planActions;
    }

    [ObservableProperty]
    public partial string Query { get; set; } = "";

    [ObservableProperty]
    public partial SongResultViewModel? Selected { get; set; }

    public ObservableCollection<SongResultViewModel> Results { get; } = [];

    public bool HasQuery => Query.Length > 0;

    public event EventHandler<Song>? SongSelected;

    partial void OnQueryChanged(string value)
    {
        Results.Clear();
        if (value.Trim().Length > 0)
        {
            foreach (var song in _search.Search(value, 40))
                Results.Add(new SongResultViewModel(song));
        }
        Selected = Results.FirstOrDefault();
        OnPropertyChanged(nameof(HasQuery));
    }

    partial void OnSelectedChanged(SongResultViewModel? value)
    {
        if (value is not null)
            SongSelected?.Invoke(this, value.Song);
    }

    [RelayCommand]
    private void ShowNow(SongResultViewModel? result = null)
    {
        if ((result ?? Selected) is not { } target)
            return;
        // Pieśń dobrana „na szybko” trafia do planu zaraz za bieżącą — plan zostaje zapisem tego,
        // co śpiewano, a „Dalej” prowadzi z powrotem do dalszej części planu.
        var item = _planActions.AddAfterLive(target.Song);
        _control.Show(_planActions.LiveItemFor(item) ?? _songs.Present(target.Song));
        Clear();
    }

    [RelayCommand]
    private void AddAsNext(SongResultViewModel? result = null)
    {
        if ((result ?? Selected) is not { } target)
            return;
        _planActions.AddAfterLive(target.Song);
        Clear();
    }

    [RelayCommand]
    private void AddToEnd(SongResultViewModel? result = null)
    {
        if ((result ?? Selected) is not { } target)
            return;
        _planActions.AddToEnd(target.Song);
        Clear();
    }

    [RelayCommand]
    public void Clear() => Query = "";

    public void MoveSelection(int step)
    {
        if (Results.Count == 0)
            return;
        var index = Selected is null ? -1 : Results.IndexOf(Selected);
        Selected = Results[Math.Clamp(index + step, 0, Results.Count - 1)];
    }
}

public sealed class SongResultViewModel(Song song)
{
    public Song Song { get; } = song;

    public int? Number => Song.Number;

    public bool IsText => Song.IsText;

    public string Title => Song.DisplayTitle;

    /// <summary>Pierwszy wers, gdy różni się od tytułu (tytuł to zwykle incipit).</summary>
    public string? FirstLine =>
        SongSearch.Normalize(Song.FirstLine) == SongSearch.Normalize(Song.DisplayTitle) ? null : Song.FirstLine;
}
