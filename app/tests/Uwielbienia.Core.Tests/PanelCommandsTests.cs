using System.Text;
using System.Text.Json;
using Uwielbienia.Core.Plans;
using Uwielbienia.Core.Presentation;
using Uwielbienia.Core.Slideshows;
using Uwielbienia.Core.Songs;
using Uwielbienia.Link;

namespace Uwielbienia.Core.Tests;

/// <summary>Panel (drugi komputer, tablet): plany, plan i układ części przez te same operacje Core co okno operatora.</summary>
public sealed class PanelCommandsTests : IDisposable
{
    private readonly string _temp = Directory.CreateTempSubdirectory("uwielbienia-panel-").FullName;
    private readonly SongLibrary _library;
    private readonly LiveSession _session = new();
    private readonly ActivePlan _plan;
    private readonly PlanLibrary _plans;
    private readonly RemoteCommandHandler _handler;

    public PanelCommandsTests()
    {
        var local = Path.Combine(_temp, "teksty");
        Directory.CreateDirectory(local);
        _library = new SongLibrary(
            new LayeredSongSource(new DirectorySongSource(RepositoryPaths.Songs), new DirectorySongSource(local)),
            new MarkdownSongParser());
        var slides = new SectionSlideBuilder();
        var songs = new SongPlanItemType(_library, slides);
        var types = new PlanItemTypes([songs]);
        _plan = new ActivePlan(new JsonPlanStore(Path.Combine(_temp, "plany")), types, _session);
        _plans = new PlanLibrary(new JsonPlanStore(Path.Combine(_temp, "plany")), _plan);
        _plans.OpenInitial(null);
        var actions = TestPlanActions.Create(_plan, _session, _library, _temp);
        var imageKeys = new RemoteImageKeys();
        var panel = new PanelCommands(_plans, actions, _plan, _library, slides, types, imageKeys);
        _handler = new RemoteCommandHandler(_session, _session, _plan, actions, _library, new SongSearch(_library), songs,
            new ImmediateDispatcher(), types, panel: panel, imageKeys: imageKeys);
    }

    public void Dispose() => Directory.Delete(_temp, recursive: true);

    private async Task<JsonElement> Send(string json, RemoteCaller? caller = null) =>
        JsonDocument.Parse(await _handler.HandleAsync(Encoding.UTF8.GetBytes(json), caller ?? RemoteCaller.Panel)).RootElement;

    private static bool Ok(JsonElement reply) => reply.GetProperty("ok").GetBoolean();

    private string Id(int number) => _library.Songs.First(s => s.Number == number).Id;

    private Task<JsonElement> Add(int number, int? index = null) =>
        Send(index is { } i
            ? $$"""{"cmd":"planAdd","songId":"{{Id(number)}}","index":{{i}}}"""
            : $$"""{"cmd":"planAdd","songId":"{{Id(number)}}"}""");

    private int?[] Numbers() => _plan.Plan!.Items.Select(i => _library.Find(((SongPlanItem)i).SongId)?.Number).ToArray();

    private Guid ItemAt(int index) => _plan.Plan!.Items[index].Id;

    [Fact]
    public async Task Locked_plan_file_gives_readable_reply_and_change_stays()
    {
        var file = Path.Combine(_temp, "plany", $"{_plan.Plan!.Id}.json");
        Assert.True(Ok(await Add(47)));
        using (File.Open(file, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var reply = await Add(48);
            Assert.False(Ok(reply));
            Assert.StartsWith("Nie udało się: ", reply.GetProperty("error").GetString());
        }
        Assert.Equal([47, 48], Numbers());
        Assert.True(Ok(await Add(49)));
    }

    [Fact]
    public async Task Phone_cannot_use_panel_commands()
    {
        var reply = await Send($$"""{"cmd":"planAdd","songId":"{{Id(47)}}"}""", RemoteCaller.Pilot);
        Assert.False(Ok(reply));
        Assert.Empty(_plan.Plan!.Items);
        Assert.False(Ok(await Send("""{"cmd":"nextItem"}""", RemoteCaller.Pilot)));
        Assert.True((await Send("""{"cmd":"hello"}""")).GetProperty("device").GetProperty("isPanel").GetBoolean());
    }

    [Fact]
    public async Task Hello_gives_panel_the_plans()
    {
        var reply = await Send("""{"cmd":"hello"}""");
        var events = reply.GetProperty("plans").GetProperty("events").EnumerateArray().ToList();
        Assert.Single(events);
        Assert.True(events[0].GetProperty("isOpen").GetBoolean());
        Assert.False((await Send("""{"cmd":"hello"}""", RemoteCaller.Pilot)).TryGetProperty("plans", out _));
    }

    [Fact]
    public async Task Add_move_remove_and_duplicate_items()
    {
        PlanItem? announced = null;
        _handler.ItemAdded += (_, item) => announced = item;
        await Add(47);
        await Add(94);
        var reply = await Add(36, index: 0);

        Assert.True(Ok(reply));
        Assert.Equal([36, 47, 94], Numbers());
        Assert.Equal(ItemAt(0), announced!.Id);
        Assert.Equal(3, reply.GetProperty("plan").GetProperty("items").GetArrayLength());

        await Send($$"""{"cmd":"planMove","itemId":"{{ItemAt(0)}}","index":2}""");
        Assert.Equal([47, 94, 36], Numbers());

        await Send($$"""{"cmd":"planRemove","itemId":"{{ItemAt(1)}}"}""");
        Assert.Equal([47, 36], Numbers());

        Assert.True(Ok(await Send($$"""{"cmd":"planDuplicate","itemId":"{{ItemAt(0)}}"}""")));
        Assert.Equal(3, _plan.Plan!.Items.Count);
        var copy = (SongPlanItem)_plan.Plan.Items[1];
        Assert.NotEqual(Id(47), copy.SongId);
        Assert.Equal(_library.Find(Id(47))!.Title, _library.Find(copy.SongId)!.Title);
    }

    [Fact]
    public async Task Show_item_from_given_slide()
    {
        await Add(47);
        await Send($$"""{"cmd":"showItem","itemId":"{{ItemAt(0)}}","slide":1}""");
        Assert.Equal(1, _session.State.SlideIndex);
        Assert.Equal(47, _session.State.Item!.Number);
    }

    [Fact]
    public async Task Item_details_and_layout_change()
    {
        await Add(47);
        var id = ItemAt(0);
        var item = (await Send($$"""{"cmd":"item","itemId":"{{id}}"}""")).GetProperty("item");
        var parts = item.GetProperty("parts").EnumerateArray().ToList();
        Assert.True(item.GetProperty("canReorder").GetBoolean());
        Assert.True(parts.Count > 1);
        Assert.True(parts[0].GetProperty("lines").GetArrayLength() > 0);

        // pierwsza część pominięta, ostatnia usunięta
        var layout = parts.Take(parts.Count - 1)
            .Select((p, i) => new LayoutEntryMessage(p.GetProperty("code").GetString()!, i != 0)).ToList();
        var json = JsonSerializer.Serialize(new { cmd = "itemLayout", itemId = id, layout }, RemoteJson.Options);
        var changed = (await Send(json)).GetProperty("item");

        Assert.False(changed.GetProperty("parts")[0].GetProperty("sung").GetBoolean());
        Assert.Equal(parts.Count - 1, changed.GetProperty("parts").GetArrayLength());
        var sung = ((SongPlanItem)_plan.Plan!.Items[0]).Arrangement!;
        Assert.Equal(layout.Skip(1).Select(e => e.Code), sung);
        if (layout.All(e => e.Code != parts[^1].GetProperty("code").GetString()))
            Assert.Contains(changed.GetProperty("removed").EnumerateArray(),
                r => r.GetProperty("code").GetString() == parts[^1].GetProperty("code").GetString());
    }

    [Fact]
    public async Task Plans_create_rename_copy_template_and_delete()
    {
        var first = _plan.Plan!.Id;
        Assert.False(Ok(await Send("""{"cmd":"planCreate","name":"Bez daty","kind":"event"}""")));

        var created = await Send("""{"cmd":"planCreate","name":"Próba","kind":"template"}""");
        Assert.True(Ok(created));
        var template = _plan.Plan!.Id;
        Assert.NotEqual(first, template);
        Assert.Equal(PlanKind.Template, _plan.Plan.Kind);
        await Add(47);

        await Send($$"""{"cmd":"planRename","planId":"{{template}}","name":"Próba z dziećmi"}""");
        Assert.Equal("Próba z dziećmi", _plan.Plan!.Name);

        await Send($$"""{"cmd":"planCopyToToday","planId":"{{template}}"}""");
        Assert.Equal(PlanKind.Event, _plan.Plan!.Kind);
        Assert.Equal(DateOnly.FromDateTime(DateTime.Today), _plan.Plan.Date);
        Assert.Equal([47], Numbers());

        Assert.False(Ok(await Send($$"""{"cmd":"planDelete","planId":"{{_plan.Plan.Id}}"}""")));
        Assert.True(Ok(await Send($$"""{"cmd":"planDelete","planId":"{{first}}"}""")));

        var plans = (await Send("""{"cmd":"plans"}""")).GetProperty("plans");
        Assert.Single(plans.GetProperty("events").EnumerateArray());
        Assert.Equal("Próba z dziećmi", plans.GetProperty("templates")[0].GetProperty("name").GetString());

        await Send($$"""{"cmd":"planOpen","planId":"{{template}}"}""");
        Assert.Equal(template, _plan.Plan!.Id);
    }

    [Fact]
    public void Panel_permission_survives_restart_and_includes_adding_songs()
    {
        var folder = Path.Combine(_temp, "polaczenia");
        var permissions = new DevicePermissions(folder);
        Assert.Equal(RemoteCaller.Pilot, permissions.CallerFor("klucz"));
        permissions.Set("klucz", DevicePermission.Panel, true);
        var caller = new DevicePermissions(folder).CallerFor("klucz");
        Assert.True(caller.IsPanel);
        Assert.True(caller.CanAddSongs);
        Assert.False(new DevicePermissions(folder).CanAddSongs("klucz"));
    }

    [Fact]
    public async Task Revoked_panel_permission_rejects_panel_commands()
    {
        var permissions = new DevicePermissions(Path.Combine(_temp, "polaczenia"));
        permissions.Set("klucz", DevicePermission.Panel, true);
        Assert.True(Ok(await Send($$"""{"cmd":"planAdd","songId":"{{Id(47)}}"}""", permissions.CallerFor("klucz"))));
        permissions.Set("klucz", DevicePermission.Panel, false);
        var caller = permissions.CallerFor("klucz");
        Assert.False(Ok(await Send("""{"cmd":"nextItem"}""", caller)));
        Assert.Single(_plan.Plan!.Items);
        Assert.False((await Send("""{"cmd":"hello"}""", caller)).GetProperty("device").GetProperty("isPanel").GetBoolean());
    }

    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public void Device_paired_with_panel_code_is_panel()
    {
        var invitations = new PairingInvitations();
        var panel = Guid.NewGuid();
        var pilot = Guid.NewGuid();
        invitations.Issued(panel, RemoteClient.Panel, Now.AddMinutes(10));
        invitations.Issued(pilot, RemoteClient.Pilot, Now.AddMinutes(10));
        Assert.True(invitations.NewDeviceIsPanel(new HashSet<Guid> { pilot }, Now));
        Assert.False(invitations.NewDeviceIsPanel(new HashSet<Guid>(), Now));
    }

    [Fact]
    public void Device_paired_with_pilot_code_or_known_earlier_is_not_panel()
    {
        var invitations = new PairingInvitations();
        Assert.False(invitations.NewDeviceIsPanel(new HashSet<Guid>(), Now));
        var expired = Guid.NewGuid();
        invitations.Issued(expired, RemoteClient.Panel, Now.AddMinutes(-1));
        Assert.False(invitations.NewDeviceIsPanel(new HashSet<Guid>(), Now));
    }

    [Fact]
    public void Ambiguous_pairing_gives_no_panel_permission()
    {
        var invitations = new PairingInvitations();
        invitations.Issued(Guid.NewGuid(), RemoteClient.Panel, Now.AddMinutes(10));
        invitations.Issued(Guid.NewGuid(), RemoteClient.Pilot, Now.AddMinutes(10));
        Assert.False(invitations.NewDeviceIsPanel(new HashSet<Guid>(), Now));
        Assert.False(invitations.NewDeviceIsPanel(new HashSet<Guid>(), Now));
    }
}
