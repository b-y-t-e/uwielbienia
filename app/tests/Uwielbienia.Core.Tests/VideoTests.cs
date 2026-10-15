using Uwielbienia.Core.Plans;
using Uwielbienia.Core.Presentation;
using Uwielbienia.Core.Videos;

namespace Uwielbienia.Core.Tests;

/// <summary>Filmy w planie: pozycja wskazująca plik, jej zapis i odtwarzanie wtedy, gdy film jest na ekranie.</summary>
public sealed class VideoTests : IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "uwielbienia-tests-" + Guid.NewGuid().ToString("N"));
    private readonly LiveSession _session = new();
    private readonly FakePlayer _player = new();

    public VideoTests()
    {
        Directory.CreateDirectory(_temp);
        _ = new VideoDirector(_session, _player);
    }

    public void Dispose() => Directory.Delete(_temp, recursive: true);

    private sealed class FakePlayer : IVideoPlayer
    {
        public List<string> Calls { get; } = [];

        public VideoStatus Status { get; private set; } = VideoStatus.Stopped;

        public string? Unavailable => null;

        public bool IsHeld { get; set; }

        public TimeSpan PictureOffset { get; set; }

        public event EventHandler? StatusChanged;

        public void Open(string file, TimeSpan start = default)
        {
            Calls.Add("open " + Path.GetFileName(file) + (start > TimeSpan.Zero ? $" od {start.TotalSeconds:0}s" : ""));
            Status = new VideoStatus(file, true, start, TimeSpan.Zero);
        }

        /// <summary>Film doszedł do <paramref name="position"/> (jak zdarzenie czasu z VLC).</summary>
        public void PlayedTo(TimeSpan position, bool ended = false) => Status = Status with { Position = position, HasEnded = ended };

        public void Stop()
        {
            Calls.Add("stop");
            Status = VideoStatus.Stopped;
        }

        public void Pause()
        {
            Calls.Add("pause");
            Status = Status with { IsPlaying = false };
            StatusChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Resume()
        {
            Calls.Add("resume");
            Status = Status with { IsPlaying = true };
            StatusChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Seek(TimeSpan position) => Calls.Add("seek");
    }

    private string Video(string name)
    {
        var file = Path.Combine(_temp, name);
        File.WriteAllText(file, "x");
        return file;
    }

    private static LiveItem Text() =>
        new(Guid.NewGuid(), "tekst", null, "Tekst", [new Slide("O1", "Część 1", [new SlideLine("Amen", null, 1, false, false)])]);

    private static LiveItem Present(string file) => new VideoPlanItemType().Present(VideoPlanItem.For(file))!;

    [Fact]
    public void Video_item_points_to_the_file_and_survives_plan_save()
    {
        var item = VideoPlanItem.For(Video("Świadectwo.mkv"));
        var type = new VideoPlanItemType();
        var store = new JsonPlanStore(Path.Combine(_temp, "plany"));

        store.Save(Plan.Create("Test", null).Insert(0, item));
        var live = type.Present(item)!;

        Assert.Equal(new PlanItemInfo("Świadectwo", null, PlanItemKind.Video), type.Describe(item));
        Assert.False(live.ShowTitle);
        Assert.Equal(item.File, Assert.IsType<VideoSlide>(Assert.Single(live.Slides)).File);
        Assert.Equal(item, Assert.Single(store.LoadAll()).Items.Single());
    }

    [Fact]
    public void Missing_file_has_a_note_and_no_slides()
    {
        var item = VideoPlanItem.For(Path.Combine(_temp, "brak.mp4"));
        var type = new VideoPlanItemType();

        Assert.StartsWith("Nie ma pliku", type.Describe(item)!.Note);
        Assert.Empty(type.Present(item)!.Slides);
    }

    [Theory]
    [InlineData("film.MP4", true)]
    [InlineData("film.mkv", true)]
    [InlineData("film.avi", true)]
    [InlineData("slajdy.pptx", false)]
    public void Video_files_are_recognized_by_extension(string file, bool expected) =>
        Assert.Equal(expected, VideoPlanItemType.IsSupported(file));

    [Fact]
    public void Video_plays_while_on_screen_and_stops_when_leaving()
    {
        _session.Show(Text());
        _session.Show(Present(Video("a.mp4")));
        _session.Show(Present(Video("b.mp4")));
        _session.Show(Text());

        Assert.Equal(["open a.mp4", "open b.mp4", "stop"], _player.Calls);
    }

    [Fact]
    public void Blank_screen_pauses_and_resumes_the_video()
    {
        _session.Show(Present(Video("a.mp4")));
        _session.ToggleBlank();
        _session.ToggleBlank();

        Assert.Equal(["open a.mp4", "pause", "resume"], _player.Calls);
    }

    [Fact]
    public void Video_paused_by_operator_stays_paused_after_blank()
    {
        _session.Show(Present(Video("a.mp4")));
        _player.Pause();
        _session.ToggleBlank();
        _session.ToggleBlank();

        Assert.Equal(["open a.mp4", "pause"], _player.Calls);
    }

    [Fact]
    public void Video_shown_under_blank_screen_starts_when_visible()
    {
        var text = Text();
        _session.SetPlaylist([text, Present(Video("a.mp4"))]);
        _session.Show(text);
        _session.ToggleBlank();
        _session.Next();
        Assert.IsType<VideoSlide>(_session.State.Slide);
        Assert.Empty(_player.Calls);

        _session.ToggleBlank();

        Assert.Equal(["open a.mp4"], _player.Calls);
    }

    [Fact]
    public void Interrupted_video_resumes_where_it_stopped_and_finished_one_starts_over()
    {
        var film = Present(Video("film.mp4"));
        var text = Text();
        _session.SetPlaylist([film, text]);

        _session.Show(film);
        _player.PlayedTo(TimeSpan.FromSeconds(42));
        _session.Next();
        _session.Previous();
        _player.PlayedTo(TimeSpan.FromSeconds(50), ended: true);
        _session.Next();
        _session.Previous();

        Assert.Equal(["open film.mp4", "stop", "open film.mp4 od 42s", "stop", "open film.mp4"], _player.Calls);
    }

    [Fact]
    public void Same_file_twice_in_the_plan_remembers_each_position_separately()
    {
        var file = Video("film.mp4");
        var first = Present(file);
        var second = Present(file);
        _session.SetPlaylist([first, second]);

        _session.Show(first);
        _player.PlayedTo(TimeSpan.FromSeconds(10));
        _session.Next();

        Assert.Equal(["open film.mp4", "open film.mp4"], _player.Calls);
    }

    [Fact]
    public void Video_never_plays_under_black_screen_and_returns_with_the_picture()
    {
        var film = Present(Video("film.mp4"));
        _session.SetPlaylist([film]);
        _session.Show(film);

        _session.ToggleBlank();                  // czarny ekran: pauza
        Assert.True(_player.TogglePause());      // spacja / kliknięcie / telefon — obsłużone, ale film stoi
        _session.ToggleBlank();                  // obraz wraca — film też

        Assert.Equal(["open film.mp4", "pause", "resume"], _player.Calls);
    }

    [Fact]
    public void Operator_pause_before_black_screen_stays_after_it()
    {
        var film = Present(Video("film.mp4"));
        _session.SetPlaylist([film]);
        _session.Show(film);

        _player.TogglePause();                   // operator zatrzymuje
        _session.ToggleBlank();
        _session.ToggleBlank();

        Assert.Equal(["open film.mp4", "pause"], _player.Calls);
    }

    [Fact]
    public void Restored_file_rebuilds_the_plan_so_the_video_can_be_shown()
    {
        var file = Path.Combine(_temp, "pendrive.mp4");
        var type = new VideoPlanItemType();
        var item = VideoPlanItem.For(file);
        var changes = 0;
        type.Changed += (_, _) => changes++;

        Assert.Empty(type.Present(item)!.Slides);
        type.CheckFiles();
        File.WriteAllText(file, "x");   // włożony pendrive
        type.CheckFiles();

        Assert.Equal(1, changes);
        Assert.Single(type.Present(item)!.Slides);
    }

    private sealed class ManualTime : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }

    /// <summary>Plan: tekst, film, tekst; na ekranie film. Pilot przy filmie: <see cref="FilmRemote"/>.</summary>
    private (FilmRemote Remote, ManualTime Time, LiveItem Before, LiveItem After) OnFilm()
    {
        var before = Text();
        var film = Present(Video("film.mp4"));
        var after = Text();
        _session.SetPlaylist([before, film, after]);
        _session.Show(film);
        var time = new ManualTime();
        return (new FilmRemote(_session, _session, _player, time), time, before, after);
    }

    [Fact]
    public void Remote_short_presses_only_skip_and_long_forward_pauses()
    {
        var (remote, _, _, _) = OnFilm();
        var shown = 0; // rzutnik pokazuje wtedy na chwilę pasek postępu
        remote.FilmChanged += (_, _) => shown++;

        Assert.True(remote.Forward());
        Assert.True(remote.Forward());
        Assert.True(remote.Back());
        Assert.True(remote.HoldForward());
        Assert.True(remote.TogglePause());

        Assert.Equal(["open film.mp4", "seek", "seek", "seek", "pause", "resume"], _player.Calls);
        Assert.Equal(5, shown);
        Assert.IsType<VideoSlide>(_session.State.Slide);
    }

    [Fact]
    public void Long_back_then_long_back_goes_to_the_previous_item_and_long_forward_to_the_next()
    {
        var (remote, _, before, after) = OnFilm();

        remote.HoldBack();
        Assert.True(remote.IsPrompting);
        remote.HoldBack();
        Assert.Same(before, _session.State.Item);
        Assert.False(remote.IsPrompting);

        _session.Show(_session.State.Item!); // znowu …
        _session.NextItem();                  // … film
        remote.HoldBack();
        remote.HoldForward();
        Assert.Same(after, _session.State.Item);
    }

    [Fact]
    public void Prompt_expires_and_any_other_press_cancels_it()
    {
        var (remote, time, _, _) = OnFilm();

        remote.HoldBack();
        time.Now += FilmRemote.PromptTime + TimeSpan.FromSeconds(1);
        remote.HoldForward();                 // po czasie — zwykła pauza
        Assert.IsType<VideoSlide>(_session.State.Slide);

        remote.HoldBack();
        remote.Forward();                     // inny przycisk — przewija i zamyka podpowiedź
        Assert.False(remote.IsPrompting);
        remote.HoldForward();
        Assert.IsType<VideoSlide>(_session.State.Slide);
    }

    [Fact]
    public void Without_a_film_the_remote_keys_do_their_usual_thing()
    {
        var text = Text();
        _session.SetPlaylist([text]);
        _session.Show(text);
        var remote = new FilmRemote(_session, _session, _player);

        Assert.False(remote.Forward());
        Assert.False(remote.Back());
        Assert.False(remote.HoldForward());
        Assert.False(remote.HoldBack());
        Assert.False(remote.ShiftPicture(VideoPlayerExtensions.PictureStep));
        Assert.Equal(TimeSpan.Zero, _player.PictureOffset);
    }

    [Fact]
    public void Brackets_shift_the_picture_against_the_sound_within_limits()
    {
        var (remote, _, _, _) = OnFilm();
        var shown = 0;
        remote.FilmChanged += (_, _) => shown++;

        Assert.True(remote.ShiftPicture(VideoPlayerExtensions.PictureStep));
        Assert.True(remote.ShiftPicture(VideoPlayerExtensions.PictureStep));
        Assert.True(remote.ShiftPicture(-VideoPlayerExtensions.PictureStep));
        Assert.Equal(TimeSpan.FromMilliseconds(100), _player.PictureOffset);
        Assert.Equal(3, shown);

        for (var i = 0; i < 100; i++)
            _player.ShiftPicture(-VideoPlayerExtensions.PictureStep);
        Assert.Equal(-VideoPlayerExtensions.MaxPictureOffset, _player.PictureOffset);
    }

    [Fact]
    public void Pause_and_skip_under_black_screen_do_not_signal_a_change()
    {
        var (remote, _, _, _) = OnFilm();
        var shown = 0;
        remote.FilmChanged += (_, _) => shown++;

        _session.ToggleBlank();
        Assert.True(remote.TogglePause());
        Assert.True(remote.HoldForward());
        Assert.True(remote.Forward());
        Assert.True(remote.Back());

        Assert.Equal(0, shown);
    }
}
