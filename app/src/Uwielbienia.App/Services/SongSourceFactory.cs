using Uwielbienia.Core.Songs;

namespace Uwielbienia.App.Services;

/// <summary>
/// Kolejność: folder wskazany w ustawieniach → <c>Teksty/</c> nad katalogiem programu (uruchomienie z repozytorium)
/// → pieśni wbudowane w aplikację.
/// </summary>
public static class SongSourceFactory
{
    public static ISongSource Create(AppSettings settings)
    {
        var sources = new List<ISongSource>();
        if (settings.SongsFolder is { } folder && Directory.Exists(folder))
            sources.Add(new DirectorySongSource(folder));
        if (FindRepositoryFolder() is { } repo)
            sources.Add(new DirectorySongSource(repo));
        sources.Add(new EmbeddedSongSource(typeof(SongSourceFactory).Assembly));
        return new FallbackSongSource([.. sources]);
    }

    private static string? FindRepositoryFolder()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "Teksty");
            if (Directory.Exists(candidate))
                return candidate;
        }
        return null;
    }
}
