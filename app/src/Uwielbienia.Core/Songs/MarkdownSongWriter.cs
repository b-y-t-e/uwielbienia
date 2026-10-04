using System.Globalization;
using System.Text;

namespace Uwielbienia.Core.Songs;

/// <summary>Zapisuje pieśń albo tekst w formacie <c>piesn.md</c> — odwrotność <see cref="MarkdownSongParser"/>.</summary>
public static class MarkdownSongWriter
{
    public static string Write(Song song)
    {
        var sb = new StringBuilder();
        sb.Append("---\n");
        sb.Append("numer: ").Append(song.Number?.ToString(CultureInfo.InvariantCulture) ?? "null").Append('\n');
        sb.Append("tytul: ").Append(Quote(song.Title)).Append('\n');
        sb.Append("kategoria: ").Append(Quote(song.Category)).Append('\n');
        sb.Append("numer_zrodlowy: ").Append(song.SourceNumber?.ToString(CultureInfo.InvariantCulture) ?? "null").Append('\n');
        sb.Append("tonacja: ").Append(song.Key is null ? "null" : Quote(song.Key)).Append('\n');
        sb.Append("kolejnosc: [").Append(string.Join(", ", song.Arrangement)).Append("]\n");
        sb.Append("zrodlo: ").Append(Quote(song.Source ?? "")).Append('\n');
        if (song.IsText)
            sb.Append("rodzaj: \"tekst\"\n");
        sb.Append("---\n\n");
        sb.Append("# ").Append(song.Title).Append('\n');

        foreach (var section in song.Sections)
        {
            sb.Append("\n## [").Append(section.Code).Append("] ").Append(section.Name);
            if (section.Repeat > 1)
                sb.Append(" {x").Append(section.Repeat.ToString(CultureInfo.InvariantCulture)).Append('}');
            sb.Append('\n');
            foreach (var line in section.Lines)
                sb.Append(WriteLine(line)).Append('\n');
        }
        return sb.ToString();
    }

    /// <summary>Jeden wiersz w gramatyce z CLAUDE.md: <c>[|: ]tekst[ :|][ {xN}][ `akordy`]</c>.</summary>
    public static string WriteLine(SongLine line)
    {
        if (line.Kind == LineKind.Note)
            return "> " + line.Text;

        var parts = new List<string>();
        var text = line.Text;
        if (line.RepeatStart)
            text = "|: " + text;
        if (line.RepeatEnd)
            text += " :|";
        if (text.Length > 0)
            parts.Add(text);
        if (line.Repeat > 1)
            parts.Add("{x" + line.Repeat.ToString(CultureInfo.InvariantCulture) + "}");
        if (line.Chords is { Length: > 0 } chords)
            parts.Add("`" + chords + "`");
        return string.Join(' ', parts);
    }

    /// <summary>Wartość w cudzysłowie; prosty cudzysłów w środku zamieniamy na typograficzny, bo parser go nie rozróżnia.</summary>
    private static string Quote(string value) => "\"" + value.Replace('"', '”') + "\"";
}
