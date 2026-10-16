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

    private async Task<JsonElement> Send(string json, RemoteCaller? caller = null) =>
        JsonDocument.Parse(await _handler.HandleAsync(Encoding.UTF8.GetBytes(json), caller)).RootElement;

    private static readonly RemoteCaller Musician = new(CanAddSongs: true);

    private static string Id(int number) => Library.Songs.First(s => s.Number == number).Id;

    private Task<JsonElement> AddNext(int number, RemoteCaller? caller = null) =>
        Send($$"""{"cmd":"addNext","songId":"{{Id(number)}}"}""", caller ?? Musician);

    private int[] Numbers() => _plan.Playlist.Where(i => i.Number is not null).Select(i => i.Number!.Value).ToArray();

    [Fact]
    public async Task Phone_without_permission_cannot_add_songs()
    {
        var reply = await AddNext(36, RemoteCaller.Pilot);
        Assert.False(reply.GetProperty("ok").GetBoolean());
        Assert.Equal([47, 94], Numbers());
        Assert.False((await Send("""{"cmd":"hello"}""")).GetProperty("device").GetProperty("canAddSongs").GetBoolean());
        Assert.True((await Send("""{"cmd":"hello"}""", Musician)).GetProperty("device").GetProperty("canAddSongs").GetBoolean());
    }

    [Fact]
    public async Task Added_song_goes_right_after_the_one_on_screen_without_changing_the_screen()
    {
        PlanItem? announced = null;
        _handler.ItemAdded += (_, item) => announced = item;
        _session.Show(_plan.Playlist[0]);

        var reply = await AddNext(36);

        Assert.True(reply.GetProperty("ok").GetBoolean());
        Assert.Equal([47, 36, 94], Numbers());
        Assert.Equal(47, _session.State.Item!.Number);
        Assert.Equal(36, reply.GetProperty("added").GetProperty("number").GetInt32());
        Assert.Equal(_plan.Playlist[1].PlanItemId, announced!.Id);
    }

    [Fact]
    public async Task Nothing_on_screen_adds_at_the_end()
    {
        await AddNext(36);
        Assert.Equal([47, 94, 36], Numbers());
    }

    [Fact]
    public async Task Undo_removes_only_own_addition_until_it_is_shown()
    {
        var added = (await AddNext(36)).GetProperty("added").GetProperty("id").GetGuid();
        var foreign = _plan.Playlist[0].PlanItemId!.Value;

        Assert.False((await Send($$"""{"cmd":"undoAdd","itemId":"{{foreign}}"}""", Musician)).GetProperty("ok").GetBoolean());
        Assert.True((await Send($$"""{"cmd":"undoAdd","itemId":"{{added}}"}""", Musician)).GetProperty("ok").GetBoolean());
        Assert.Equal([47, 94], Numbers());
    }

    [Fact]
    public async Task Undo_refuses_a_song_that_was_already_shown()
    {
        var added = (await AddNext(36)).GetProperty("added").GetProperty("id").GetGuid();
        _session.Show(_plan.FindLiveItem(added)!);
        _session.Show(_plan.Playlist[0]);

        Assert.False((await Send($$"""{"cmd":"undoAdd","itemId":"{{added}}"}""", Musician)).GetProperty("ok").GetBoolean());
        Assert.Equal([47, 94, 36], Numbers());
    }

    [Fact]
    public void Permissions_survive_restart_and_default_to_none()
    {
        var folder = Path.Combine(_plans, "polaczenia");
        var permissions = new DevicePermissions(folder);
        Assert.False(permissions.CanAddSongs("klucz"));
        permissions.SetCanAddSongs("klucz", true);
        Assert.True(new DevicePermissions(folder).CanAddSongs("klucz"));
        permissions.SetCanAddSongs("klucz", false);
        Assert.False(new DevicePermissions(folder).CanAddSongs("klucz"));
    }

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
