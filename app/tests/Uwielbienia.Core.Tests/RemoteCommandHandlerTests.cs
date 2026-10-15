using System.Text;
using System.Text.Json;
using Uwielbienia.Core;
using Uwielbienia.Core.Plans;
using Uwielbienia.Core.Presentation;
using Uwielbienia.Core.Songs;
using Uwielbienia.Link;

namespace Uwielbienia.Core.Tests;

public sealed class RemoteCommandHandlerTests : IDisposable
{
    private static readonly SongLibrary Library =
        new(new DirectorySongSource(RepositoryPaths.Songs), new MarkdownSongParser());

    private readonly string _plans = Path.Combine(Path.GetTempPath(), "uwielbienia-tests-" + Guid.NewGuid().ToString("N"));
    private readonly LiveSession _session = new();
    private readonly ActivePlan _plan;
    private readonly RemoteCommandHandler _handler;

    public RemoteCommandHandlerTests()
    {
        var songs = new SongPlanItemType(Library, new SectionSlideBuilder());
        var types = new PlanItemTypes([songs, new Videos.VideoPlanItemType()]);
        _plan = new ActivePlan(new JsonPlanStore(_plans), types, _session);
        _plan.Open(Plan.Create("Test", null)
            .Insert(0, SongPlanItem.For(Library.Songs.First(s => s.Number == 47).Id))
            .Insert(1, SongPlanItem.For(Library.Songs.First(s => s.Number == 94).Id))
            .Insert(2, VideoPlanItem.For(Path.Combine(Path.GetTempPath(), "film.mp4"))));
        _handler = new RemoteCommandHandler(_session, _session, _plan, Library, new SongSearch(Library), songs, new ImmediateDispatcher(), types);
    }

    private async Task<JsonElement> Send(string json) =>
        JsonDocument.Parse(await _handler.HandleAsync(Encoding.UTF8.GetBytes(json))).RootElement;

    [Fact]
    public async Task Hello_returns_state_and_plan()
    {
        var reply = await Send("""{"cmd":"hello"}""");
        Assert.True(reply.GetProperty("ok").GetBoolean());
        var items = reply.GetProperty("plan").GetProperty("items").EnumerateArray().ToList();
        // rodzaj pozycji z opisu planu (PlanItemKind) — film to „video”, nie „text”
        Assert.Equal(["song", "song", "video"], items.Select(i => i.GetProperty("kind").GetString()));
        Assert.Equal("state", reply.GetProperty("state").GetProperty("type").GetString());
    }

    [Fact]
    public async Task Next_and_blank_drive_the_live_session()
    {
        await Send("""{"cmd":"next"}""");
        var reply = await Send("""{"cmd":"blank"}""");
        Assert.Equal(47, _session.State.Item!.Number);
        Assert.True(reply.GetProperty("state").GetProperty("isBlank").GetBoolean());
    }

    [Fact]
    public async Task Show_item_and_search()
    {
        var second = _plan.Playlist[1].PlanItemId!.Value;
        await Send($$"""{"cmd":"showItem","itemId":"{{second}}"}""");
        Assert.Equal(94, _session.State.Item!.Number);

        var reply = await Send("""{"cmd":"search","query":"duchu swiety"}""");
        Assert.True(reply.GetProperty("results").GetArrayLength() > 0);
    }

    [Fact]
    public async Task Garbage_is_refused()
    {
        var reply = await Send("not json");
        Assert.False(reply.GetProperty("ok").GetBoolean());
    }

    public void Dispose() => Directory.Delete(_plans, recursive: true);
}
