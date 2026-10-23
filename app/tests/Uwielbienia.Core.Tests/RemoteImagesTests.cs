using System.Text;
using System.Text.Json;
using Uwielbienia.Core.Kiosks;
using Uwielbienia.Core.Plans;
using Uwielbienia.Core.Presentation;
using Uwielbienia.Core.Songs;
using Uwielbienia.Link;

namespace Uwielbienia.Core.Tests;

/// <summary>Obrazy slajdów i kiosku na telefonie i panelu: klucz w stanie, obraz na prośbę.</summary>
public sealed class RemoteImagesTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "uwielbienia-images-" + Guid.NewGuid().ToString("N"));
    private readonly LiveSession _session = new();
    private readonly FakePreviews _previews = new();
    private readonly KioskRotation _kiosk;
    private readonly RemoteCommandHandler _handler;

    public RemoteImagesTests()
    {
        Directory.CreateDirectory(_folder);
        var library = new SongLibrary(new DirectorySongSource(_folder), new MarkdownSongParser());
        var songs = new SongPlanItemType(library, new SectionSlideBuilder());
        var types = new PlanItemTypes([songs]);
        var plan = new ActivePlan(new JsonPlanStore(_folder), types, _session);
        _kiosk = new KioskRotation(_session, new ImmediateDispatcher());
        _handler = new RemoteCommandHandler(_session, _session, plan, TestPlanActions.Create(plan, _session, library, _folder),
            library, new SongSearch(library), songs, new ImmediateDispatcher(), types, kiosk: _kiosk, previews: _previews);
    }

    public void Dispose()
    {
        _kiosk.Dispose();
        Directory.Delete(_folder, recursive: true);
    }

    private string Image(string name)
    {
        var file = Path.Combine(_folder, name);
        File.WriteAllBytes(file, [1, 2, 3]);
        return file;
    }

    private async Task<JsonElement> Send(string json) =>
        JsonDocument.Parse(await _handler.HandleAsync(Encoding.UTF8.GetBytes(json))).RootElement;

    private static string? Key(JsonElement state, string name) =>
        state.TryGetProperty(name, out var key) ? key.GetString() : null;

    [Fact]
    public async Task Presentation_slide_state_carries_image_keys_and_the_image_comes_on_request()
    {
        var first = Image("slide001.png");
        var second = Image("slide002.png");
        _session.Show(new LiveItem(Guid.NewGuid(), "p", null, "Ogłoszenia", [new ImageSlide(first, 1), new ImageSlide(second, 2)]));

        var state = (await Send("""{"cmd":"hello"}""")).GetProperty("state");
        var key = Key(state, "image")!;
        var next = Key(state, "nextImage")!;
        Assert.NotEqual(key, next);
        Assert.DoesNotContain(_folder, key); // strona nie widzi ścieżek

        var reply = await Send($$"""{"cmd":"image","key":"{{key}}","width":300}""");
        Assert.True(reply.GetProperty("ok").GetBoolean());
        Assert.Equal($"data:{first}@{RemoteImages.Thumbnail}", reply.GetProperty("image").GetString());

        _session.Next();
        Assert.Equal(next, Key((await Send("""{"cmd":"hello"}""")).GetProperty("state"), "image"));
    }

    [Fact]
    public void Changed_file_gets_a_new_key()
    {
        var file = Image("slide001.png");
        var keys = new RemoteImageKeys();
        var before = keys.KeyOf(file);
        File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddMinutes(1));
        var after = keys.KeyOf(file);
        Assert.NotEqual(before, after);
        Assert.Null(keys.FileOf(before)); // stary klucz już nie wydaje pliku
        Assert.Equal(file, keys.FileOf(after));
    }

    [Fact]
    public async Task Only_files_with_a_key_issued_by_the_host_are_sent()
    {
        var reply = await Send($$"""{"cmd":"image","key":"{{Path.Combine(_folder, "x.png").Replace("\\", "\\\\")}}"}""");
        Assert.False(reply.GetProperty("ok").GetBoolean());
        Assert.Empty(_previews.Requested);
    }

    [Fact]
    public async Task Text_slide_has_no_image()
    {
        _session.Show(new LiveItem(Guid.NewGuid(), "t", null, "Tekst", [new Slide("O1", "Część 1", [new SlideLine("Alleluja", null, 1, false, false)])]));
        var state = (await Send("""{"cmd":"hello"}""")).GetProperty("state");
        Assert.Null(Key(state, "image"));
        Assert.Null(Key(state, "nextImage"));
    }

    [Fact]
    public async Task Kiosk_element_is_sent_with_its_image_preview()
    {
        var picture = Image("plakat.png");
        var itemId = Guid.NewGuid();
        _session.Show(new LiveItem(itemId, "k", null, "Kiosk",
            [new KioskSlide([new KioskFrame(picture, "Rekolekcje", "od piątku"), new KioskFrame(null, null, "Witamy")], TimeSpan.FromMinutes(5))]));

        var kiosk = (await Send("""{"cmd":"hello"}""")).GetProperty("kiosk");
        Assert.Equal(itemId, kiosk.GetProperty("itemId").GetGuid());
        Assert.Equal($"data:{picture}@{RemoteImages.Kiosk}", kiosk.GetProperty("image").GetString());
        Assert.Equal("Rekolekcje", kiosk.GetProperty("title").GetString());

        _kiosk.Advance();
        kiosk = (await Send("""{"cmd":"hello"}""")).GetProperty("kiosk");
        Assert.Equal(1, kiosk.GetProperty("index").GetInt32());
        Assert.False(kiosk.TryGetProperty("image", out _)); // element bez obrazu — sam napis
    }

    [Fact]
    public void Widths_are_a_few_fixed_sizes()
    {
        Assert.Equal(RemoteImages.Phone, RemoteImages.WidthFor(null));
        Assert.Equal(RemoteImages.Thumbnail, RemoteImages.WidthFor(10));
        Assert.Equal(RemoteImages.Phone, RemoteImages.WidthFor(641 - 1));
        Assert.Equal(RemoteImages.Screen, RemoteImages.WidthFor(5000));
    }

    private sealed class FakePreviews : IImagePreviews
    {
        public List<string> Requested { get; } = [];

        public Task<string?> DataUrlAsync(string file, int width = RemoteImages.Phone)
        {
            Requested.Add(file);
            return Task.FromResult<string?>($"data:{file}@{width}");
        }
    }
}
