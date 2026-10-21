using System.Text.Json;
using Uwielbienia.Core.Kiosks;
using Uwielbienia.Core.Plans;
using Uwielbienia.Core.Presentation;
using Uwielbienia.Core.Slideshows;

namespace Uwielbienia.Core.Tests;

public class KioskTests
{
    /// <summary>Prezentacje i obrazy kiosku bez dysku: folder → gotowe slajdy albo uwaga.</summary>
    private sealed class FakeSlideshows : ISlideshowLibrary
    {
        public Dictionary<string, SlideshowState> States { get; } = [];

        public event EventHandler<string>? Changed;

        public event EventHandler<SlideshowProgress>? Progress { add { } remove { } }

        public void Ready(string folder, params string[] slides)
        {
            States[folder] = new SlideshowState(slides);
            Changed?.Invoke(this, folder);
        }

        public SlideshowState Load(string folder) => Peek(folder);

        public SlideshowState Peek(string folder) => States.GetValueOrDefault(folder) ?? new SlideshowState([], "Przygotowywanie slajdów…");

        public Task<ImportedSlideshow> ImportAsync(IReadOnlyList<string> files) => throw new NotSupportedException();

        public Task<IReadOnlyList<ImportedSlideshow>> ImportEachAsync(IReadOnlyList<string> files) => throw new NotSupportedException();

        public Task<string> CopyAsync(string folder) => throw new NotSupportedException();

        public void Retry(string folder) { }
    }

    private readonly FakeSlideshows _slideshows = new();
    private readonly KioskPlanItemType _type;

    public KioskTests() => _type = new KioskPlanItemType(_slideshows);

    private static KioskPlanItem Kiosk(params KioskEntry[] entries) => KioskPlanItem.Create("Ogłoszenia") with { Entries = entries };

    [Fact]
    public void Images_presentation_slides_and_texts_become_frames_of_one_slide()
    {
        _slideshows.Ready("obraz", "obraz/001.png");
        _slideshows.Ready("prezentacja", "p/slide001.png", "p/slide002.png");
        var kiosk = Kiosk(
            new KioskEntry("obraz", Text: "  Rekolekcje\r\nod piątku  "),
            new KioskEntry(Title: "Witamy", Text: ""),
            new KioskEntry("prezentacja"),
            new KioskEntry(Text: " "));

        var live = _type.Present(kiosk)!;

        var slide = Assert.IsType<KioskSlide>(Assert.Single(live.Slides));
        Assert.Equal(TimeSpan.FromSeconds(KioskPlanItem.DefaultSeconds), slide.Interval);
        Assert.Equal(
            [
                new KioskFrame("obraz/001.png", null, "Rekolekcje\nod piątku"),
                new KioskFrame(null, "Witamy", null),
                new KioskFrame("p/slide001.png", null, null),
                new KioskFrame("p/slide002.png", null, null),
            ],
            slide.Frames);
        Assert.False(live.ShowTitle);
        Assert.Equal(PlanItemKind.Kiosk, _type.Describe(kiosk)!.Kind);
    }

    [Fact]
    public void Empty_kiosk_has_no_slides_and_a_note_in_the_plan()
    {
        var kiosk = Kiosk(new KioskEntry(Text: ""));

        Assert.Empty(_type.Present(kiosk)!.Slides);
        Assert.Equal(KioskPlanItemType.EmptyNote, _type.Describe(kiosk)!.Note);
    }

    [Fact]
    public void Presentation_being_prepared_shows_its_note_and_rebuilds_the_plan_when_ready()
    {
        var kiosk = Kiosk(new KioskEntry("prezentacja"));
        var changed = 0;
        _type.Changed += (_, _) => changed++;

        Assert.Equal("Przygotowywanie slajdów…", _type.Describe(kiosk)!.Note);
        _slideshows.Ready("prezentacja", "s1.png");

        Assert.Equal(1, changed);
        Assert.Null(_type.Describe(kiosk)!.Note);
        Assert.Single(_type.Present(kiosk)!.Slides);
    }

    [Theory]
    [InlineData(0.75, KioskLayout.TextRight)]   // zdjęcie pionowe
    [InlineData(1.0, KioskLayout.TextRight)]    // kwadrat
    [InlineData(4 / 3.0, KioskLayout.TextRight)]
    [InlineData(16 / 9.0, KioskLayout.TextBelow)]
    [InlineData(3.0, KioskLayout.TextBelow)]    // panorama
    public void Text_goes_where_the_image_stays_largest(double aspect, KioskLayout expected) =>
        Assert.Equal(expected, new KioskFrame("a.png", null, "Napis").LayoutFor(aspect));

    [Fact]
    public void Frame_without_text_or_image_fills_the_screen_with_what_it_has()
    {
        Assert.Equal(KioskLayout.Image, new KioskFrame("a.png", null, null).LayoutFor(0.75));
        Assert.Equal(KioskLayout.Text, new KioskFrame(null, "Tytuł", null).LayoutFor(double.NaN));
    }

    [Fact]
    public void Kiosk_survives_saving_and_loading_the_plan()
    {
        var temp = Path.Combine(Path.GetTempPath(), "uwielbienia-kiosk-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new JsonPlanStore(temp);
            var kiosk = Kiosk(new KioskEntry("obraz", "Tytuł", "Tekst"), new KioskEntry(Text: "Napis")) with { Seconds = 12 };
            store.Save(Plan.Create("Wieczór", null).Insert(0, kiosk));

            var loaded = Assert.IsType<KioskPlanItem>(Assert.Single(Assert.Single(store.LoadAll()).Items));

            Assert.Equal((kiosk.Id, kiosk.Title, 12), (loaded.Id, loaded.Title, loaded.Seconds));
            Assert.Equal(kiosk.Entries, loaded.Entries);
        }
        finally
        {
            if (Directory.Exists(temp))
                Directory.Delete(temp, recursive: true);
        }
    }

    [Fact]
    public void Kiosk_from_older_plan_file_without_seconds_changes_every_8_seconds()
    {
        var item = JsonSerializer.Deserialize<PlanItem>(
            """{"type":"kiosk","id":"00000000-0000-0000-0000-000000000001","title":"Kiosk","entries":[{"text":"Napis"}]}""",
            JsonPlanStore.JsonOptions);

        Assert.Equal(KioskPlanItem.DefaultSeconds, Assert.IsType<KioskPlanItem>(item).Seconds);
    }

    [Fact]
    public void New_kiosk_uses_watercolor_and_slow_zoom_and_passes_them_to_the_screen()
    {
        _slideshows.Ready("obraz", "a.png");
        var kiosk = Kiosk(new KioskEntry("obraz"));

        var slide = Assert.IsType<KioskSlide>(Assert.Single(_type.Present(kiosk)!.Slides));

        Assert.Equal([SlideTransition.Watercolor], slide.ChosenTransitions);
        Assert.True(slide.SlowZoom);
    }

    [Fact]
    public void Chosen_transitions_and_zoom_survive_saving_and_empty_choice_stays_empty()
    {
        var chosen = Kiosk() with { Transitions = [SlideTransition.Slide, SlideTransition.StainedGlass], SlowZoom = false };
        var none = Kiosk() with { Transitions = [] };

        var loadedChosen = Assert.IsType<KioskPlanItem>(RoundTrip(chosen));
        var loadedNone = Assert.IsType<KioskPlanItem>(RoundTrip(none));

        Assert.Equal([SlideTransition.Slide, SlideTransition.StainedGlass], loadedChosen.ChosenTransitions);
        Assert.False(loadedChosen.SlowZoom);
        Assert.Empty(loadedNone.ChosenTransitions);
    }

    [Fact]
    public void Plan_transitions_survive_saving_and_copying_and_older_plans_use_the_default()
    {
        var temp = Path.Combine(Path.GetTempPath(), "uwielbienia-kiosk-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new JsonPlanStore(temp);
            var plan = Plan.Create("Wieczór", null) with { Transitions = [SlideTransition.LightRay] };
            store.Save(plan);
            store.Save(Plan.Create("Starszy", null));

            var loaded = store.LoadAll().ToDictionary(p => p.Name);

            Assert.Equal([SlideTransition.LightRay], loaded["Wieczór"].Transitions);
            Assert.Equal([SlideTransition.LightRay], plan.CloneAs("Kopia", null, PlanKind.Template).Transitions);
            Assert.Null(loaded["Starszy"].Transitions);
            Assert.Equal(Plan.DefaultTransitions, loaded["Starszy"].ChosenTransitions);
        }
        finally
        {
            if (Directory.Exists(temp))
                Directory.Delete(temp, recursive: true);
        }
    }

    private static PlanItem? RoundTrip(PlanItem item) =>
        JsonSerializer.Deserialize<PlanItem>(JsonSerializer.Serialize(item, JsonPlanStore.JsonOptions), JsonPlanStore.JsonOptions);

    [Fact]
    public void Pick_returns_nothing_one_or_a_random_one_of_several()
    {
        var random = new Random(7);

        Assert.Null(SlideTransitions.Pick([], random));
        Assert.Equal(SlideTransition.Flame, SlideTransitions.Pick([SlideTransition.Flame], random));
        var picked = Enumerable.Range(0, 200).Select(_ => SlideTransitions.Pick([SlideTransition.Slide, SlideTransition.Ripple], random)).ToHashSet();
        Assert.Equal([SlideTransition.Slide, SlideTransition.Ripple], picked.Order());
    }

    [Fact]
    public void Changed_transitions_change_the_slide_on_screen()
    {
        var frames = new[] { new KioskFrame(null, null, "a") };
        var interval = TimeSpan.FromSeconds(8);

        Assert.Equal(new KioskSlide(frames, interval, [SlideTransition.Flame]), new KioskSlide(frames.ToList(), interval, [SlideTransition.Flame]));
        Assert.NotEqual(new KioskSlide(frames, interval, [SlideTransition.Flame]), new KioskSlide(frames, interval, []));
        Assert.NotEqual(new KioskSlide(frames, interval, [], SlowZoom: true), new KioskSlide(frames, interval, []));
    }

    [Fact]
    public void Single_slow_frames_do_not_disable_effects_but_slow_frames_in_a_row_do()
    {
        var guard = new SmoothnessGuard(TimeSpan.FromMilliseconds(50), slowFramesInRow: 3);
        var run = guard.Begin();
        var slow = TimeSpan.FromMilliseconds(120);
        var fast = TimeSpan.FromMilliseconds(4);

        // szybki komputer: zacięcia rozrzucone w czasie
        for (var i = 0; i < 10; i++)
        {
            run.ReportFrame(slow);
            run.ReportFrame(slow);
            run.ReportFrame(fast);
        }
        Assert.True(guard.IsSmooth);

        // słaby komputer: każda klatka wolna
        run.ReportFrame(slow);
        run.ReportFrame(slow);
        run.ReportFrame(slow);
        Assert.False(guard.IsSmooth);
        run.ReportFrame(fast);
        Assert.False(guard.IsSmooth);
    }

    [Fact]
    public void Transitions_running_at_once_count_their_slow_frames_separately()
    {
        // wejście na kiosk: przejście strony na rzutniku i pierwsze elementy kiosku na rzutniku i w kolumnie „Na ekranie”
        var guard = new SmoothnessGuard(TimeSpan.FromMilliseconds(50), slowFramesInRow: 3);
        var runs = new[] { guard.Begin(), guard.Begin(), guard.Begin() };
        var slow = TimeSpan.FromMilliseconds(70);
        var fast = TimeSpan.FromMilliseconds(30);

        foreach (var frame in new[] { slow, slow, fast, slow, slow, fast })
            foreach (var run in runs)
                run.ReportFrame(frame);

        Assert.True(guard.IsSmooth);
    }

    // ---------- na ekranie ----------

    private static LiveItem KioskItem(Guid id, params string[] texts) =>
        new(id, "kiosk", null, "Kiosk", [new KioskSlide(texts.Select(t => new KioskFrame(null, null, t)).ToList(), TimeSpan.FromSeconds(8))], false);

    private static LiveItem Song(string title) =>
        new(Guid.NewGuid(), title, 1, title, [new Slide("V1", "Zwrotka 1", [new SlideLine("t", null, 1, false, false)])]);

    [Fact]
    public void Next_leaves_the_kiosk_for_the_next_plan_item()
    {
        var session = new LiveSession();
        var kiosk = KioskItem(Guid.NewGuid(), "a", "b", "c");
        var song = Song("Pieśń");
        session.SetPlaylist([kiosk, song]);
        session.Show(kiosk);

        session.Next();

        Assert.Same(song, session.State.Item);
    }

    [Fact]
    public void Rotation_goes_round_and_stops_under_black_screen()
    {
        var session = new LiveSession();
        var kiosk = KioskItem(Guid.NewGuid(), "a", "b", "c");
        session.SetPlaylist([kiosk]);
        using var rotation = new KioskRotation(session);
        var changes = 0;
        rotation.Changed += (_, _) => changes++;

        session.Show(kiosk);
        Assert.Equal(("a", true), (rotation.Frame?.Text, rotation.IsRunning));
        rotation.Advance();
        rotation.Advance();
        Assert.Equal("c", rotation.Frame?.Text);
        rotation.Advance();
        Assert.Equal("a", rotation.Frame?.Text);

        session.ToggleBlank();
        Assert.False(rotation.IsRunning);
        Assert.Equal("a", rotation.Frame?.Text);
        session.ToggleBlank();
        Assert.True(rotation.IsRunning);
        Assert.True(changes >= 6);
    }

    [Fact]
    public void Returning_to_a_kiosk_resumes_from_the_same_frame()
    {
        var session = new LiveSession();
        var kiosk = KioskItem(Guid.NewGuid(), "a", "b", "c");
        var song = Song("Pieśń");
        session.SetPlaylist([kiosk, song]);
        using var rotation = new KioskRotation(session);

        session.Show(kiosk);
        rotation.Advance();
        session.Next();
        Assert.Null(rotation.Frame);
        Assert.False(rotation.IsRunning);
        session.Previous();

        Assert.Equal("b", rotation.Frame?.Text);
    }

    [Fact]
    public void Edited_kiosk_on_screen_keeps_showing_the_same_frame()
    {
        var session = new LiveSession();
        var id = Guid.NewGuid();
        var shown = KioskItem(id, "a", "b", "c");
        session.SetPlaylist([shown]);
        session.Show(shown);
        using var rotation = new KioskRotation(session);
        rotation.Advance();

        // ten sam kiosk po przebudowie planu (nowe listy, ta sama treść) — ekran bez zmiany
        session.SetPlaylist([KioskItem(id, "a", "b", "c")]);
        Assert.Same(shown, session.State.Item);
        Assert.Equal("b", rotation.Frame?.Text);

        // nowy element przed bieżącym — dalej ten sam element
        session.SetPlaylist([KioskItem(id, "nowy", "a", "b", "c")]);
        Assert.Equal(("b", 2), (rotation.Frame?.Text, rotation.Index));

        // bieżący usunięty — najbliższy
        session.SetPlaylist([KioskItem(id, "nowy", "a")]);
        Assert.Equal("a", rotation.Frame?.Text);
    }

    [Fact]
    public void Single_frame_does_not_rotate()
    {
        var session = new LiveSession();
        var kiosk = KioskItem(Guid.NewGuid(), "a");
        session.SetPlaylist([kiosk]);
        using var rotation = new KioskRotation(session);

        session.Show(kiosk);

        Assert.False(rotation.IsRunning);
        Assert.Equal("a", rotation.Frame?.Text);
    }
}
