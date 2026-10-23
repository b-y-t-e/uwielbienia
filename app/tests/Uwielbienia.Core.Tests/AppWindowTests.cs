using Uwielbienia.Core.AppWindows;
using Uwielbienia.Core.Plans;
using Uwielbienia.Core.Presentation;

namespace Uwielbienia.Core.Tests;

/// <summary>Aplikacja w planie: które okno pokazać, zapis pozycji i pokazywanie wtedy, gdy jej slajd jest na ekranie.</summary>
public sealed class AppWindowTests : IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "uwielbienia-tests-" + Guid.NewGuid().ToString("N"));
    private readonly LiveSession _session = new();
    private readonly FakeCapture _capture = new();

    public AppWindowTests() => _ = new AppWindowDirector(_session, _capture);

    public void Dispose()
    {
        if (Directory.Exists(_temp))
            Directory.Delete(_temp, recursive: true);
    }

    private sealed class FakeCapture : IAppWindowCapture
    {
        public List<string> Calls { get; } = [];

        public string? Unavailable { get; set; }

        public IReadOnlyList<AppWindowInfo> Windows() => [];

        public AppWindowInfo? Shown => null;

        public event EventHandler? ShownChanged { add { } remove { } }

        public void Show(AppWindowTarget target) => Calls.Add("show " + target.Process);

        public void Stop() => Calls.Add("stop");
    }

    private static readonly AppWindowInfo Wireless = new(1, "ApplicationFrameHost", "Ekran bezprzewodowy");
    private static readonly AppWindowInfo Settings = new(2, "ApplicationFrameHost", "Ustawienia");
    private static readonly AppWindowInfo Browser = new(3, "brave", "Ogłoszenia — Brave");

    private static LiveItem Text() =>
        new(Guid.NewGuid(), "tekst", null, "Tekst", [new Slide("O1", "Część 1", [new SlideLine("Amen", null, 1, false, false)])]);

    private LiveItem Present(AppWindowInfo window) => new AppWindowPlanItemType(_capture).Present(AppWindowPlanItem.For(window))!;

    [Fact]
    public void Store_app_is_recognized_by_window_title_other_programs_by_process()
    {
        var wireless = AppWindowTarget.For(Wireless);
        var browser = AppWindowTarget.For(Browser);
        var otherTab = Browser with { Title = "YouTube — Brave" };

        Assert.Equal(new AppWindowTarget("ApplicationFrameHost", "Ekran bezprzewodowy"), wireless);
        Assert.Same(Wireless, wireless.FirstMatch([Settings, Browser, Wireless]));
        // Inna karta przeglądarki zmienia tytuł okna — to wciąż ten sam program.
        Assert.Equal(new AppWindowTarget("brave", null), browser);
        Assert.Same(otherTab, browser.FirstMatch([Wireless, otherTab]));
    }

    [Fact]
    public void First_found_window_wins_and_none_means_not_running()
    {
        var target = AppWindowTarget.For(Browser);
        var second = Browser with { Handle = 9 };

        Assert.Same(Browser, target.FirstMatch([Wireless, Browser, second]));
        Assert.Null(target.FirstMatch([Wireless, Settings]));
    }

    [Fact]
    public void App_item_survives_plan_save_and_has_one_slide()
    {
        var item = AppWindowPlanItem.For(Wireless);
        var type = new AppWindowPlanItemType(_capture);
        var store = new JsonPlanStore(Path.Combine(_temp, "plany"));

        store.Save(Plan.Create("Test", null).Insert(0, item));
        var live = type.Present(item)!;

        Assert.Equal(new PlanItemInfo("Ekran bezprzewodowy", null, PlanItemKind.App), type.Describe(item));
        Assert.False(live.ShowTitle);
        Assert.Equal(AppWindowTarget.For(Wireless), Assert.IsType<AppWindowSlide>(Assert.Single(live.Slides)).Target);
        Assert.Equal(item, Assert.Single(store.LoadAll()).Items.Single());
    }

    [Fact]
    public void Unavailable_capture_is_a_note_in_the_plan()
    {
        _capture.Unavailable = "Tylko Windows";

        Assert.Equal("Tylko Windows", new AppWindowPlanItemType(_capture).Describe(AppWindowPlanItem.For(Wireless))!.Note);
    }

    [Fact]
    public void Window_is_shown_while_on_screen_also_under_blank_and_stops_when_leaving()
    {
        _session.Show(Text());
        _session.Show(Present(Wireless));
        _session.ToggleBlank();
        _session.ToggleBlank();
        _session.Show(Present(Browser));
        _session.Show(Text());
        _session.Clear();

        Assert.Equal(
            ["stop", "stop", "show ApplicationFrameHost", "show ApplicationFrameHost", "show ApplicationFrameHost", "show brave", "stop", "stop"],
            _capture.Calls);
    }
}
