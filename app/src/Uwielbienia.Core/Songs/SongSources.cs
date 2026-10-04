using System.Reflection;

namespace Uwielbienia.Core.Songs;

/// <summary>Folder w układzie repozytorium: <c>Teksty/NNN-slug/piesn.md</c>.</summary>
public sealed class DirectorySongSource(string directory) : ISongSource
{
    public const string FileName = "piesn.md";

    public IEnumerable<SongDocument> Load() =>
        !Directory.Exists(directory)
            ? []
            : Directory.EnumerateDirectories(directory)
                .Select(dir => (Id: Path.GetFileName(dir), File: Path.Combine(dir, FileName)))
                .Where(x => File.Exists(x.File))
                .Select(x => new SongDocument(x.Id, File.ReadAllText(x.File)));
}

/// <summary>
/// Pieśni wbudowane w zestaw jako zasoby o nazwach logicznych <c>Teksty/NNN-slug/piesn.md</c>.
/// </summary>
public sealed class EmbeddedSongSource(Assembly assembly) : ISongSource
{
    private const string Prefix = "Teksty/";
    private const string Suffix = "/" + DirectorySongSource.FileName;

    public IEnumerable<SongDocument> Load()
    {
        foreach (var resource in assembly.GetManifestResourceNames())
        {
            var name = resource.Replace('\\', '/');
            if (!name.StartsWith(Prefix, StringComparison.Ordinal) || !name.EndsWith(Suffix, StringComparison.Ordinal))
                continue;
            using var stream = assembly.GetManifestResourceStream(resource)!;
            using var reader = new StreamReader(stream);
            yield return new SongDocument(name[Prefix.Length..^Suffix.Length], reader.ReadToEnd());
        }
    }
}

/// <summary>
/// Śpiewnik wspólny z nałożonymi plikami lokalnymi: plik lokalny o tym samym identyfikatorze
/// zastępuje pieśń śpiewnika (<see cref="SongOrigin.Modified"/>), pozostałe to własne teksty.
/// </summary>
public sealed class LayeredSongSource(ISongSource shared, ISongSource local) : ISongSource
{
    public IEnumerable<SongDocument> Load()
    {
        var documents = shared.Load().ToDictionary(d => d.Id, StringComparer.Ordinal);
        foreach (var document in local.Load())
        {
            var origin = documents.ContainsKey(document.Id) ? SongOrigin.Modified : SongOrigin.Local;
            documents[document.Id] = document with { Origin = origin };
        }
        return documents.Values;
    }
}

/// <summary>Pierwsze źródło, które ma jakiekolwiek pieśni (np. folder użytkownika, potem wbudowane).</summary>
public sealed class FallbackSongSource(params ISongSource[] sources) : ISongSource
{
    public IEnumerable<SongDocument> Load()
    {
        foreach (var source in sources)
        {
            var documents = source.Load().ToList();
            if (documents.Count > 0)
                return documents;
        }
        return [];
    }
}
