namespace Uwielbienia.Core.Songs;

public interface ISongParser
{
    /// <exception cref="SongFormatException">Gdy tekst nie jest poprawnym plikiem <c>piesn.md</c>.</exception>
    Song Parse(string id, string markdown);
}

public sealed class SongFormatException(string songId, string message)
    : Exception($"{songId}: {message}")
{
    public string SongId { get; } = songId;
}
