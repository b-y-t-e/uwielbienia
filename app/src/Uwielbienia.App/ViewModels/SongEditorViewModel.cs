using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Uwielbienia.Core.Songs;

namespace Uwielbienia.App.ViewModels;

/// <summary>Jak zakończyła się edycja — kolumna „Pieśń” pokazuje wtedy zapisaną pieśń albo nic.</summary>
public enum EditorOutcome
{
    Cancelled,
    Saved,
    Deleted,
}

/// <summary>
/// Edycja pieśni albo tekstu w kolumnie „Pieśń”. Zapis zawsze lokalny (na tym komputerze);
/// pieśń śpiewnika dostaje lokalną poprawkę, którą można cofnąć.
/// </summary>
public sealed partial class SongEditorViewModel : ObservableObject
{
    private readonly ISongEditor _editor;
    private readonly SongDraft _draft;
    private readonly Action<EditorOutcome, Song?> _closed;

    public SongEditorViewModel(SongDraft draft, ISongEditor editor, Action<EditorOutcome, Song?> closed)
    {
        _draft = draft;
        _editor = editor;
        _closed = closed;
        Title = draft.Title;
        foreach (var part in draft.Parts)
            Parts.Add(PartEditorViewModel.From(part));
    }

    public bool IsText => _draft.Kind == SongKind.Text;

    public bool IsNew => _draft.Original is null;

    public bool IsSong => !IsText;

    public string Heading => _draft.Original is null
        ? IsText ? "Nowy tekst" : "Nowa pieśń"
        : IsText ? "Edycja tekstu" : "Edycja pieśni";

    public string TitlePlaceholder => IsText ? "Tytuł (opcjonalnie)" : "Tytuł";

    public string LinesPlaceholder => IsText ? "Treść" : "Treść, akordy w nawiasie: Panie [G C D]";

    public IReadOnlyList<SongPartType> PartTypes => SongPartType.All;

    public ObservableCollection<PartEditorViewModel> Parts { get; } = [];

    [ObservableProperty]
    public partial string Title { get; set; }


    [ObservableProperty]
    public partial string? Error { get; private set; }

    [ObservableProperty]
    public partial bool IsDeleteConfirmationOpen { get; set; }

    /// <summary>Pieśń śpiewnika zmieniona na tym komputerze.</summary>
    public bool CanRevert => _draft.Original?.Origin == SongOrigin.Modified;

    /// <summary>Własna pieśń lub tekst.</summary>
    public bool CanDelete => _draft.Original?.Origin == SongOrigin.Local;

    /// <param name="kind">Kod rodzaju części pieśni (<c>V</c>, <c>C</c>…); tekst ma zawsze <c>O</c>.</param>
    [RelayCommand]
    private void AddPart(string? kind) =>
        Parts.Add(new PartEditorViewModel(SongPartType.ForKind(IsText ? "O" : kind ?? "V"), "", ""));

    [RelayCommand]
    private void RemovePart(PartEditorViewModel part) => Parts.Remove(part);

    [RelayCommand]
    private void Save()
    {
        var draft = _draft with
        {
            Title = Title,
            Parts = Parts.Select(p => p.ToDraft()).ToList(),
        };
        Error = draft.Validate();
        if (Error is not null)
            return;
        try
        {
            _closed(EditorOutcome.Saved, _editor.Save(draft));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Error = $"Nie udało się zapisać: {ex.Message}";
        }
    }

    [RelayCommand]
    private void Cancel() => _closed(EditorOutcome.Cancelled, null);

    [RelayCommand]
    private void Revert()
    {
        if (_draft.Original is not { } original)
            return;
        _editor.RevertToShared(original.Id);
        _closed(EditorOutcome.Saved, null);
    }

    [RelayCommand]
    private void RequestDelete() => IsDeleteConfirmationOpen = true;

    [RelayCommand]
    private void CancelDelete() => IsDeleteConfirmationOpen = false;

    [RelayCommand]
    private void ConfirmDelete()
    {
        if (_draft.Original is not { } original)
            return;
        _editor.Delete(original.Id);
        _closed(EditorOutcome.Deleted, null);
    }
}

/// <summary>Część w edytorze: rodzaj (pieśń) albo nazwa (tekst) i wersy — jeden wers w jednej linii.</summary>
public sealed partial class PartEditorViewModel(SongPartType type, string name, string text, string? originalCode = null, int repeat = 1)
    : ObservableObject
{
    [ObservableProperty]
    public partial SongPartType Type { get; set; } = type;

    [ObservableProperty]
    public partial string Name { get; set; } = name;

    [ObservableProperty]
    public partial string Text { get; set; } = text;

    public static PartEditorViewModel From(DraftPart part) =>
        new(SongPartType.ForKind(part.Kind), part.Name, part.Text, part.OriginalCode, part.Repeat);

    public DraftPart ToDraft() => new(Type.Kind, Name, Text, originalCode, repeat);
}
