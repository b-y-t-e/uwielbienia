using System.Globalization;
using System.Text;

namespace Uwielbienia.Core.Songs;

/// <summary>
/// Zapis własnych pieśni i tekstów oraz lokalnych poprawek śpiewnika — tylko na tym komputerze.
/// Śpiewnik wspólny (<c>Teksty/</c>) nigdy nie jest zmieniany.
/// </summary>
public interface ISongEditor
{
    /// <summary>Zapisuje i przeładowuje bibliotekę; zwraca zapisaną pieśń (nowa dostaje numer i identyfikator).</summary>
    Song Save(SongDraft draft);

    /// <summary>Usuwa lokalną poprawkę pieśni śpiewnika — wraca oryginał.</summary>
    void RevertToShared(string id);

    /// <summary>Usuwa własną pieśń lub tekst.</summary>
    void Delete(string id);

    /// <summary>
    /// Niezależna kopia pieśni lub tekstu (cała treść: części, akordy, kolejność, tonacja) jako własna —
    /// pieśń dostaje nowy numer, tekst nowy identyfikator; tytuł bez zmian.
    /// </summary>
    Song Copy(Song song);
}

/// <summary>Pliki w układzie repozytorium: <c>{folder}/{id}/piesn.md</c>.</summary>
public sealed class LocalSongEditor(string directory, ISongLibrary library) : ISongEditor
{
    /// <summary>Własne pieśni numerujemy od 1000, żeby nie kolidowały ze śpiewnikiem.</summary>
    public const int FirstLocalNumber = 1000;

    public Song Save(SongDraft draft)
    {
        var (id, number) = draft.Original is { } original
            ? (original.Id, original.Number)
            : draft.Kind == SongKind.Text
                ? (NewId("tekst", draft.DisplayTitle), (int?)null)
                : NewSongIdentity(draft.Title);

        return Write(draft.Build(id, number));
    }

    public Song Copy(Song song)
    {
        var (id, number) = song.IsText
            ? (NewId("tekst", song.DisplayTitle), (int?)null)
            : NewSongIdentity(song.Title);
        return Write(song with { Id = id, Number = number });
    }

    private Song Write(Song song)
    {
        var folder = Path.Combine(directory, song.Id);
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, DirectorySongSource.FileName);
        var temp = path + ".tmp";
        File.WriteAllText(temp, MarkdownSongWriter.Write(song), new UTF8Encoding(false));
        File.Move(temp, path, overwrite: true);

        library.Reload();
        return library.Find(song.Id) ?? song;
    }

    public void RevertToShared(string id)
    {
        if (library.Find(id)?.Origin == SongOrigin.Modified)
            RemoveFolder(id);
    }

    public void Delete(string id)
    {
        if (library.Find(id)?.Origin == SongOrigin.Local)
            RemoveFolder(id);
    }

    private void RemoveFolder(string id)
    {
        var folder = Path.Combine(directory, id);
        if (Directory.Exists(folder))
            Directory.Delete(folder, recursive: true);
        library.Reload();
    }

    private (string Id, int? Number) NewSongIdentity(string title)
    {
        var number = Math.Max(FirstLocalNumber, library.Songs.Where(s => !s.IsText).Max(s => s.Number ?? 0) + 1);
        return (NewId(number.ToString(CultureInfo.InvariantCulture), title), number);
    }

    private string NewId(string prefix, string title)
    {
        var baseId = $"{prefix}-{Slug(title)}";
        var id = baseId;
        for (var n = 2; library.Find(id) is not null || Directory.Exists(Path.Combine(directory, id)); n++)
            id = $"{baseId}-{n}";
        return id;
    }

    /// <summary>Tytuł bez polskich znaków, małymi literami, słowa rozdzielone <c>-</c> (jak nazwy folderów śpiewnika).</summary>
    public static string Slug(string title)
    {
        var slug = SongSearch.Normalize(title).Replace(' ', '-');
        if (slug.Length > 60)
            slug = slug[..60].TrimEnd('-');
        return slug.Length > 0 ? slug : "bez-tytulu";
    }
}
