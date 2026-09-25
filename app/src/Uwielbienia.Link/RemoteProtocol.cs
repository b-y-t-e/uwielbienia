using System.Text.Json;
using System.Text.Json.Serialization;
using Uwielbienia.Core.Plans;
using Uwielbienia.Core.Presentation;

namespace Uwielbienia.Link;

// Protokół między aplikacją (host) a stroną www (pilot / ekran). Wszystko w JSON, camelCase.
//
// host → strona (notify):   {"type":"state", ...StateMessage} | {"type":"plan", ...PlanMessage}
// strona → host (request):  {"cmd":"hello"|"next"|"prev"|"blank"|"goto"|"showItem"|"showSong"|"search", ...}
//                           odpowiedź: CommandReply

public sealed record RemoteCommand(
    string Cmd,
    int? Slide = null,
    Guid? ItemId = null,
    string? SongId = null,
    string? Query = null);

public sealed record CommandReply(
    bool Ok,
    string? Error = null,
    StateMessage? State = null,
    PlanMessage? Plan = null,
    IReadOnlyList<SongHit>? Results = null);

public sealed record StateMessage(
    long Version,
    string? SongId,
    Guid? ItemId,
    int? Number,
    string? Title,
    int SlideIndex,
    int SlideCount,
    string? Label,
    IReadOnlyList<LineMessage> Lines,
    IReadOnlyList<string> SlideLabels,
    bool IsBlank,
    string? Next)
{
    public string Type => "state";

    public static StateMessage From(LiveState state) => new(
        state.Version,
        state.Item?.SongId,
        state.Item?.PlanItemId,
        state.Item?.Number,
        state.Item?.Title,
        state.SlideIndex,
        state.Item?.Slides.Count ?? 0,
        state.Slide?.Label,
        state.Slide?.Lines.Select(l => new LineMessage(l.Text, l.Repeat)).ToList() ?? [],
        state.Item?.Slides.Select(s => s.Label).ToList() ?? [],
        state.IsBlank,
        state.Next);
}

public sealed record LineMessage(string Text, int Repeat);

public sealed record PlanMessage(string? Name, IReadOnlyList<PlanItemMessage> Items)
{
    public string Type => "plan";

    public static PlanMessage From(Plan? plan, IReadOnlyList<LiveItem> playlist) =>
        new(plan?.Name, playlist.Select(i => new PlanItemMessage(i.PlanItemId!.Value, i.SongId, i.Number, i.Title)).ToList());
}

public sealed record PlanItemMessage(Guid Id, string SongId, int Number, string Title);

public sealed record SongHit(string SongId, int Number, string Title, string FirstLine);

public static class RemoteJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
    };

    public static byte[] Serialize<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value, Options);
}
