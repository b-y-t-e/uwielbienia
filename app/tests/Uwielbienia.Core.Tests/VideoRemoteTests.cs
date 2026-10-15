using System.Text;
using System.Text.Json;
using Uwielbienia.Core.Plans;
using Uwielbienia.Core.Presentation;
using Uwielbienia.Core.Songs;
using Uwielbienia.Core.Videos;
using Uwielbienia.Link;

namespace Uwielbienia.Core.Tests;

/// <summary>Film z telefonu: pauza, ±15 s w granicach filmu i obraz względem dźwięku.</summary>
public sealed class VideoRemoteTests : IDisposable
{
    private static readonly SongLibrary Library =
        new(new DirectorySongSource(RepositoryPaths.Songs), new MarkdownSongParser());

    private readonly string _plans = Path.Combine(Path.GetTempPath(), "uwielbienia-tests-" + Guid.NewGuid().ToString("N"));
    private readonly PlayingVideo _video = new();
    private readonly LiveSession _session = new();
    private readonly RemoteCommandHandler _handler;

    public VideoRemoteTests()
    {
        var session = _session;
        var songs = new SongPlanItemType(Library, new SectionSlideBuilder());
        var types = new PlanItemTypes([songs]);
        var plan = new ActivePlan(new JsonPlanStore(_plans), types, session);
        _handler = new RemoteCommandHandler(session, session, plan, Library, new SongSearch(Library), songs,
            new ImmediateDispatcher(), types, _video);
    }

    public void Dispose()
    {
        if (Directory.Exists(_plans))
            Directory.Delete(_plans, recursive: true);
    }

    /// <summary>Odtwarzacz w trakcie filmu: 1:00 z 2:00.</summary>
    private sealed class PlayingVideo : IVideoPlayer
    {
        public VideoStatus Status { get; private set; } =
            new("film.mp4", true, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(120));

        public string? Unavailable => null;

        public bool IsHeld { get; set; }

        public TimeSpan PictureOffset { get; set; }

        public event EventHandler? StatusChanged { add { } remove { } }

        public void Open(string file, TimeSpan start = default) { }

        public void Stop() { }

        public void Pause() => Status = Status with { IsPlaying = false };

        public void Resume() => Status = Status with { IsPlaying = true };

        public void Seek(TimeSpan position) => Status = Status with { Position = position };
    }

    private async Task<JsonElement> Send(string json) =>
        JsonDocument.Parse(await _handler.HandleAsync(Encoding.UTF8.GetBytes(json))).RootElement.GetProperty("video");

    private Task<JsonElement> Skip(int seconds) => Send($$"""{"cmd":"videoSkip","seconds":{{seconds}}}""");

    [Fact]
    public async Task Phone_pauses_the_video()
    {
        Assert.False((await Send("""{"cmd":"videoToggle"}""")).GetProperty("isPlaying").GetBoolean());
        Assert.True((await Send("""{"cmd":"videoToggle"}""")).GetProperty("isPlaying").GetBoolean());
    }

    [Fact]
    public async Task Phone_skips_15_seconds_within_the_video_length()
    {
        Assert.Equal(45, (await Skip(-15)).GetProperty("position").GetDouble());
        await Skip(-15);
        await Skip(-15);
        Assert.Equal(0, (await Skip(-15)).GetProperty("position").GetDouble());

        _video.Seek(TimeSpan.FromSeconds(110));
        Assert.Equal(120, (await Skip(15)).GetProperty("position").GetDouble());
    }

    [Fact]
    public async Task Phone_shifts_the_picture_against_the_sound_by_one_step_only_on_film()
    {
        await _handler.HandleAsync("""{"cmd":"videoOffset","milliseconds":100}"""u8.ToArray());
        Assert.Equal(TimeSpan.Zero, _video.PictureOffset);

        _session.Show(new LiveItem(Guid.NewGuid(), "film", null, "Film", [new VideoSlide(_video.Status.File!)]));
        await Send("""{"cmd":"videoOffset","milliseconds":100}""");
        var reply = await Send("""{"cmd":"videoOffset","milliseconds":5000}""");
        Assert.Equal(200, reply.GetProperty("pictureOffsetMs").GetInt32());
        Assert.Equal(TimeSpan.FromMilliseconds(200), _video.PictureOffset);
    }

    [Fact]
    public async Task Hello_includes_the_video_state()
    {
        var reply = JsonDocument.Parse(await _handler.HandleAsync("""{"cmd":"hello"}"""u8.ToArray())).RootElement;
        Assert.Equal(120, reply.GetProperty("video").GetProperty("duration").GetDouble());
    }
}
