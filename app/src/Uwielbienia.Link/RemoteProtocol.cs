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
    string? Next,
    bool ShowTitle = false,
    IReadOnlyList<LineMessage>? NextLines = null)
{
    public string Type => "state";

    public static StateMessage From(LiveState state) => new(
        state.Version,
        state.Item?.ContentId,
        state.Item?.PlanItemId,
        state.Item?.Number,
        state.Item?.Title,
        state.SlideIndex,
        state.Item?.Slides.Count ?? 0,
        state.Slide?.Label,
        LinesOf(state.Slide),
        state.Item?.Slides.Select(s => s.Label).ToList() ?? [],
        state.IsBlank,
        state.Next,
        state.Item is { ShowTitle: true } && state.SlideIndex == 0 && state.Slide is { Lines.Count: > 0 },
        // następny slajd tej samej pieśni — muzyk widzi, co nadchodzi (inna pieśń: tylko tytuł w Next)
        state.Item is { } item && state.SlideIndex + 1 < item.Slides.Count ? LinesOf(item.Slides[state.SlideIndex + 1]) : null);

    private static List<LineMessage> LinesOf(Slide? slide) =>
        slide?.Lines.Select(l => new LineMessage(l.Text, l.Repeat, l.IsChorus, l.StartsPart, l.IsTitle, l.Chords)).ToList() ?? [];
}

/// <param name="Chorus">Refren dołączony do zwrotki (kursywa).</param>
/// <param name="PartStart">Początek dołączonej części (odstęp nad wersem).</param>
/// <param name="Title">Wers powtarza tytuł pieśni (kolor tytułu).</param>
/// <param name="Chords">Akordy wersu (telefon muzyka), jak w kolumnie „Na ekranie”.</param>
public sealed record LineMessage(
    string Text,
    int Repeat,
    bool Chorus = false,
    bool PartStart = false,
    bool Title = false,
    string? Chords = null);

public sealed record PlanMessage(string? Name, IReadOnlyList<PlanItemMessage> Items)
{
    public string Type => "plan";

    public static PlanMessage From(Plan? plan, IReadOnlyList<LiveItem> playlist) =>
        new(plan?.Name, playlist.Select(i => new PlanItemMessage(i.PlanItemId!.Value, i.ContentId, i.Number, i.Title, Kind(plan, i))).ToList());

    /// <summary>Znacznik na liście jak w aplikacji: <c>song</c> (numer), <c>text</c> (kropka), <c>presentation</c> (znak slajdu).</summary>
    private static string Kind(Plan? plan, LiveItem item) =>
        plan?.Items.FirstOrDefault(p => p.Id == item.PlanItemId) is PresentationPlanItem ? "presentation"
        : item.Number is null ? "text"
        : "song";
}

public sealed record PlanItemMessage(Guid Id, string SongId, int? Number, string Title, string Kind = "song");

public sealed record SongHit(string SongId, int? Number, string Title, string FirstLine, bool IsText = false);

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
