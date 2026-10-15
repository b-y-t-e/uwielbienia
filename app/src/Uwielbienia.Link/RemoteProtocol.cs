using System.Text.Json;
using System.Text.Json.Serialization;
using Uwielbienia.Core.Plans;
using Uwielbienia.Core.Presentation;
using Uwielbienia.Core.Videos;

namespace Uwielbienia.Link;

// Protokół między aplikacją (host) a stroną www (pilot / ekran). Wszystko w JSON, camelCase.
//
// host → strona (notify):   {"type":"state", ...StateMessage} | {"type":"plan", ...PlanMessage} | {"type":"video", ...VideoMessage}
// strona → host (request):  {"cmd":"hello"|"next"|"prev"|"blank"|"goto"|"showItem"|"showSong"|"search"
//                                  |"videoToggle"|"videoSkip" (seconds: ±15)
//                                  |"videoOffset" (milliseconds: ±100 — obraz później / wcześniej względem dźwięku), ...}
//                           odpowiedź: CommandReply

public sealed record RemoteCommand(
    string Cmd,
    int? Slide = null,
    Guid? ItemId = null,
    string? SongId = null,
    string? Query = null,
    int? Seconds = null,
    int? Milliseconds = null);

public sealed record CommandReply(
    bool Ok,
    string? Error = null,
    StateMessage? State = null,
    PlanMessage? Plan = null,
    IReadOnlyList<SongHit>? Results = null,
    VideoMessage? Video = null);

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
    IReadOnlyList<LineMessage>? NextLines = null,
    bool IsVideo = false)
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
        // tylko slajd z tekstem (także sam tytuł bez wersów) — nie obraz ani film
        state.Item is { ShowTitle: true } && state.SlideIndex == 0 && state.Slide?.GetType() == typeof(Slide),
        // następny slajd tej samej pieśni — muzyk widzi, co nadchodzi (inna pieśń: tylko tytuł w Next)
        state.Item is { } item && state.SlideIndex + 1 < item.Slides.Count ? LinesOf(item.Slides[state.SlideIndex + 1]) : null,
        state.Slide is VideoSlide);

    private static List<LineMessage> LinesOf(Slide? slide) =>
        slide?.Lines.Select(l => new LineMessage(l.Text, l.Repeat, l.IsChorus, l.StartsPart, l.TitleLength, l.Chords)).ToList() ?? [];
}

/// <param name="Chorus">Refren dołączony do zwrotki (kursywa).</param>
/// <param name="PartStart">Początek dołączonej części (odstęp nad wersem).</param>
/// <param name="TitleLength">Ile pierwszych znaków wersu powtarza tytuł pieśni (kolor tytułu).</param>
/// <param name="Chords">Akordy wersu (telefon muzyka), jak w kolumnie „Na ekranie”.</param>
public sealed record LineMessage(
    string Text,
    int Repeat,
    bool Chorus = false,
    bool PartStart = false,
    int TitleLength = 0,
    string? Chords = null);

/// <summary>Film na ekranie: telefon pokazuje czas, pasek postępu, pauzę, ±15 s i przesunięcie obrazu względem dźwięku
/// (<see cref="PictureOffsetMs"/>, + = obraz później). Wysyłane przy każdej zmianie stanu odtwarzacza, czas — najwyżej
/// raz na sekundę.</summary>
public sealed record VideoMessage(
    bool IsLoaded, bool IsPlaying, double Position, double Duration, string? Error, int PictureOffsetMs = 0)
{
    public string Type => "video";

    public static VideoMessage From(IVideoPlayer player) => new(
        player.Status.File is not null, player.Status.IsPlaying, Math.Round(player.Status.Position.TotalSeconds, 1),
        Math.Round(player.Status.Duration.TotalSeconds, 1), player.Status.Error,
        player.PictureOffsetMs());
}

public sealed record PlanMessage(string? Name, IReadOnlyList<PlanItemMessage> Items)
{
    public string Type => "plan";

    public static PlanMessage From(Plan? plan, IReadOnlyList<LiveItem> playlist, IPlanItemTypes itemTypes) =>
        new(plan?.Name, playlist.Select(i => new PlanItemMessage(i.PlanItemId!.Value, i.ContentId, i.Number, i.Title, Kind(plan, i, itemTypes))).ToList());

    /// <summary>
    /// Znacznik na liście jak w aplikacji — z opisu pozycji (<see cref="PlanItemKind"/>): <c>song</c>, <c>text</c>,
    /// <c>presentation</c>, <c>video</c>; nowy rodzaj pozycji działa bez zmian tutaj.
    /// </summary>
    private static string Kind(Plan? plan, LiveItem item, IPlanItemTypes itemTypes) =>
        plan?.Items.FirstOrDefault(p => p.Id == item.PlanItemId) is { } planItem && itemTypes.Describe(planItem) is { } info
            ? JsonNamingPolicy.CamelCase.ConvertName(info.Kind.ToString())
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
