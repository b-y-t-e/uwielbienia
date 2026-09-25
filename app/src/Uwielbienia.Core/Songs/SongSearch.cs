using System.Globalization;
using System.Text;

namespace Uwielbienia.Core.Songs;

public interface ISongSearch
{
    /// <summary>
    /// Wyniki dla zapytania wpisanego „na szybko”: numer (<c>47</c>) albo fragment tytułu lub tekstu
    /// bez polskich znaków i interpunkcji (<c>jezus moj</c>). Pusty tekst = wszystkie pieśni.
    /// </summary>
    IReadOnlyList<Song> Search(string query, int limit = 50);
}

public sealed class SongSearch : ISongSearch
{
    private readonly IReadOnlyList<(Song Song, string Title, string FirstLine, string Text)> _index;

    public SongSearch(ISongLibrary library)
    {
        _index = library.Songs
            .Select(s => (s, Normalize(s.Title), Normalize(s.FirstLine),
                Normalize(string.Join(' ', s.Sections.SelectMany(x => x.Lines).Where(l => l.IsSung).Select(l => l.Text)))))
            .ToList();
    }

    public IReadOnlyList<Song> Search(string query, int limit = 50)
    {
        var q = Normalize(query);
        if (q.Length == 0)
            return _index.Select(x => x.Song).Take(limit).ToList();

        if (int.TryParse(q, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
        {
            return _index
                .Where(x => x.Song.Number.ToString(CultureInfo.InvariantCulture).StartsWith(q, StringComparison.Ordinal))
                .OrderBy(x => x.Song.Number != number)
                .ThenBy(x => x.Song.Number)
                .Select(x => x.Song)
                .Take(limit)
                .ToList();
        }

        var words = q.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return _index
            .Select(x => (x.Song, Score: Score(x.Title, x.FirstLine, x.Text, q, words)))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Song.Number)
            .Select(x => x.Song)
            .Take(limit)
            .ToList();
    }

    private static int Score(string title, string firstLine, string text, string query, string[] words)
    {
        if (title.StartsWith(query, StringComparison.Ordinal)) return 100;
        if (title.Contains(query, StringComparison.Ordinal)) return 80;
        if (firstLine.Contains(query, StringComparison.Ordinal)) return 70;
        if (words.All(w => title.Contains(w, StringComparison.Ordinal))) return 60;
        if (text.Contains(query, StringComparison.Ordinal)) return 40;
        if (words.All(w => text.Contains(w, StringComparison.Ordinal))) return 20;
        return 0;
    }

    /// <summary>Małe litery, bez polskich znaków i interpunkcji, pojedyncze spacje.</summary>
    public static string Normalize(string value)
    {
        var sb = new StringBuilder(value.Length);
        var lastSpace = true;
        foreach (var c in value.Replace('ł', 'l').Replace('Ł', 'l').Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(char.ToLowerInvariant(c));
                lastSpace = false;
            }
            else if (!lastSpace)
            {
                sb.Append(' ');
                lastSpace = true;
            }
        }
        return sb.ToString().TrimEnd();
    }
}
