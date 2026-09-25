namespace Uwielbienia.Core.Songs;

/// <summary>Surowy plik pieśni: identyfikator (nazwa folderu) i treść.</summary>
public sealed record SongDocument(string Id, string Markdown);

/// <summary>Skąd pochodzą pliki pieśni (folder na dysku, zasoby wbudowane…).</summary>
public interface ISongSource
{
    IEnumerable<SongDocument> Load();
}

public interface ISongLibrary
{
    IReadOnlyList<Song> Songs { get; }

    /// <summary>Pliki, których nie udało się odczytać — do pokazania operatorowi.</summary>
    IReadOnlyList<SongFormatException> Errors { get; }

    Song? Find(string id);
}

public sealed class SongLibrary : ISongLibrary
{
    private readonly Dictionary<string, Song> _byId;

    public SongLibrary(ISongSource source, ISongParser parser)
    {
        var songs = new List<Song>();
        var errors = new List<SongFormatException>();
        foreach (var document in source.Load())
        {
            try
            {
                songs.Add(parser.Parse(document.Id, document.Markdown));
            }
            catch (SongFormatException ex)
            {
                errors.Add(ex);
            }
        }
        Songs = songs.OrderBy(s => s.Number).ThenBy(s => s.Id, StringComparer.Ordinal).ToList();
        Errors = errors;
        _byId = Songs.ToDictionary(s => s.Id, StringComparer.Ordinal);
    }

    public IReadOnlyList<Song> Songs { get; }

    public IReadOnlyList<SongFormatException> Errors { get; }

    public Song? Find(string id) => _byId.GetValueOrDefault(id);
}
