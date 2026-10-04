namespace Uwielbienia.Core.Songs;

/// <summary>Surowy plik pieśni: identyfikator (nazwa folderu), treść i pochodzenie.</summary>
public sealed record SongDocument(string Id, string Markdown, SongOrigin Origin = SongOrigin.Shared);

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

    /// <summary>Wczytuje pliki od nowa (po zapisie własnej pieśni lub tekstu).</summary>
    void Reload();

    /// <summary>Po <see cref="Reload"/> — plan, wyszukiwarka i ekran przeliczają się z nowymi tekstami.</summary>
    event EventHandler? Changed;
}

public sealed class SongLibrary : ISongLibrary
{
    private readonly ISongSource _source;
    private readonly ISongParser _parser;
    private Dictionary<string, Song> _byId = [];

    public SongLibrary(ISongSource source, ISongParser parser)
    {
        _source = source;
        _parser = parser;
        Load();
    }

    public IReadOnlyList<Song> Songs { get; private set; } = [];

    public IReadOnlyList<SongFormatException> Errors { get; private set; } = [];

    public event EventHandler? Changed;

    public void Reload()
    {
        Load();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Pieśni wg numeru, potem teksty wg tytułu.</summary>
    private void Load()
    {
        var songs = new List<Song>();
        var errors = new List<SongFormatException>();
        foreach (var document in _source.Load())
        {
            try
            {
                songs.Add(_parser.Parse(document.Id, document.Markdown) with { Origin = document.Origin });
            }
            catch (SongFormatException ex)
            {
                errors.Add(ex);
            }
        }
        Songs = songs
            .OrderBy(s => s.IsText)
            .ThenBy(s => s.Number ?? int.MaxValue)
            .ThenBy(s => s.Title, StringComparer.CurrentCulture)
            .ThenBy(s => s.Id, StringComparer.Ordinal)
            .ToList();
        Errors = errors;
        _byId = Songs.ToDictionary(s => s.Id, StringComparer.Ordinal);
    }

    public Song? Find(string id) => _byId.GetValueOrDefault(id);
}
