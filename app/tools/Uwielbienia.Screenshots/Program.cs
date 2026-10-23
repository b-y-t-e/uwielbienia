using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Uwielbienia.App;
using Uwielbienia.App.Services;
using Uwielbienia.App.ViewModels;
using Uwielbienia.App.Views;
using Uwielbienia.Core.Plans;
using Uwielbienia.Core.Presentation;
using Uwielbienia.Core.Slideshows;
using Uwielbienia.Core.Songs;

// Renderuje okna aplikacji do PNG bez wyświetlania ich na ekranie — do przeglądu wyglądu.
// Użycie: dotnet run --project tools/Uwielbienia.Screenshots -- <katalog_wyjściowy> [light] [--video <plik filmu>]
//        [--app-window <proces>[|<tytuł okna>]]
// Z --video film gra naprawdę (VLC) — rzutnik i „Na ekranie” pokazują jego klatki. Z --app-window pozycja „Aplikacja”
// pokazuje prawdziwe okno tego programu (np. ApplicationFrameHost|Ekran bezprzewodowy); bez — program nieuruchomiony.

var output = args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "uwielbienia-shots");
var light = args.Contains("light");
var videoArg = Array.IndexOf(args, "--video") is var at and >= 0 && at + 1 < args.Length ? args[at + 1] : null;
var appArg = Array.IndexOf(args, "--app-window") is var appAt and >= 0 && appAt + 1 < args.Length ? args[appAt + 1].Split('|', 2) : null;
Directory.CreateDirectory(output);

AppBuilder.Configure<App>()
    .UseSkia()
    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
    .With(new FontManagerOptions { DefaultFamilyName = "avares://Uwielbienia.App/Assets/Fonts#Atkinson Hyperlegible Next" })
    .SetupWithoutStarting();

var root = Path.Combine(Path.GetTempPath(), "uwielbienia-shots-data-" + Guid.NewGuid().ToString("N"));
var services = AppComposition.Build(new AppPaths(root));
if (light)
    services.GetRequiredService<ISettingsStore>().Update(s => s with { Theme = AppTheme.Light });

var library = services.GetRequiredService<ISongLibrary>();
var active = services.GetRequiredService<ActivePlan>();
var plan = Plan.Create("Uwielbienie", new DateOnly(2026, 9, 25));
foreach (var number in new[] { 47, 94, 30, 36, 117, 128, 24, 143, 59 })
    plan = plan.Insert(plan.Items.Count, SongPlanItem.For(library.Songs.First(s => s.Number == number).Id));
// Prezentacja z obrazów (PowerPoint w narzędziu niedostępny): trzy slajdy narysowane tutaj.
var slideFiles = Enumerable.Range(1, 3).Select(i => DrawSlide(i, Path.Combine(root, $"ogloszenia-{i}.png"))).ToList();
var imported = services.GetRequiredService<ISlideshowLibrary>().ImportAsync(slideFiles).GetAwaiter().GetResult();
plan = plan.Insert(3, new PresentationPlanItem(Guid.NewGuid(), imported.Folder, "Ogłoszenia parafialne"));
plan = plan.Insert(5, new VideoPlanItem(Guid.NewGuid(), videoArg ?? Path.Combine(root, "swiadectwo.mp4"), "Świadectwo Ani"));
plan = plan.Insert(6, appArg is null
    ? new AppWindowPlanItem(Guid.NewGuid(), "Ekran bezprzewodowy", "nieuruchomiony-program", null)
    : new AppWindowPlanItem(Guid.NewGuid(), appArg.Length > 1 ? appArg[1] : appArg[0], appArg[0], appArg.Length > 1 ? appArg[1] : null));
// Kiosk: obraz poziomy z napisem (pod spodem), pionowy z napisem (z prawej), sam napis i sam obraz.
var kioskImages = services.GetRequiredService<ISlideshowLibrary>().ImportEachAsync(
    [DrawSlide(2, Path.Combine(root, "kiosk-poziomy.png")), DrawPortrait(Path.Combine(root, "kiosk-pionowy.png")), slideFiles[2]])
    .GetAwaiter().GetResult();
// zrzuty układów bez przejść i zoomu (przejścia — osobno, klatka po klatce)
var kioskItem = KioskPlanItem.Create("Ogłoszenia przed Mszą") with
{
    Transitions = [],
    SlowZoom = false,
    Entries =
    [
        new KioskEntry(kioskImages[0].Folder, "Rekolekcje adwentowe", "1–3 grudnia, początek o 18:00\nprowadzi ks. Marek"),
        new KioskEntry(kioskImages[1].Folder, "Schola dziecięca", "Zapraszamy dzieci w każdą sobotę o 10:00 do salki pod kościołem."),
        new KioskEntry(null, "Witamy na wieczorze uwielbienia", "Telefony wyciszamy — dziękujemy!"),
        new KioskEntry(kioskImages[2].Folder),
    ],
};
plan = plan.Insert(6, kioskItem);
services.GetRequiredService<IPlanStore>().Save(plan);
active.Open(plan);

var control = services.GetRequiredService<ILiveControl>();
control.Show(active.Playlist[1], 1);

var main = services.GetRequiredService<MainViewModel>();
main.Plan.Selected = main.Plan.Items[2];

var window = services.GetRequiredService<MainWindow>();
window.Width = 1600;
window.Height = 960;
window.Show();
Save(window, "operator");

// prawy klik na pozycji planu (menu w osobnym oknie podręcznym — zrzut jego okna)
if (window.GetVisualDescendants().OfType<Grid>().FirstOrDefault(g => g.ContextMenu is not null && g.DataContext is PlanItemViewModel) is { } row)
{
    row.ContextMenu!.Open(row);
    Dispatcher.UIThread.RunJobs();
    // kursor nad „Usuń z planu” — kolor usuwania zostaje pod kursorem
    if (row.ContextMenu.Items.OfType<MenuItem>().LastOrDefault() is { } remove)
        ((Avalonia.Controls.IPseudoClasses)remove.Classes).Set(":pointerover", true);
    AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    if (TopLevel.GetTopLevel(row.ContextMenu) is Window popupHost && !ReferenceEquals(popupHost, window))
        Save(popupHost, "plan-menu");
    else
        Save(window, "plan-menu");
    row.ContextMenu.Close();
}

// Pasek górny: przejście między pozycjami planu — panel z efektami (kilka zaznaczonych = losowe).
if (window.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.Flyout is not null && b.DataContext is MainViewModel) is { } transitionsButton)
{
    main.Transitions.Choice.Options[0].IsSelected = true;
    transitionsButton.Flyout!.ShowAt(transitionsButton);
    Dispatcher.UIThread.RunJobs();
    AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    Save(window, "przejscie-planu");
    if (active.Plan!.ChosenTransitions is not [SlideTransition.Slide, SlideTransition.Watercolor])
        throw new InvalidOperationException("Wybór przejścia nie trafił do planu");
    transitionsButton.Flyout.Hide();
    main.Transitions.Choice.Options[0].IsSelected = false;
    // „Przenikanie” odznacza wszystkie efekty, a wybrany efekt odznacza „Przenikanie”
    var before = active.Plan!.ChosenTransitions;
    main.Transitions.Choice.IsCrossFade = true;
    if (active.Plan!.ChosenTransitions.Count != 0 || main.Transitions.Choice.Options.Any(o => o.IsSelected))
        throw new InvalidOperationException("„Przenikanie” nie odznaczyło efektów");
    main.Transitions.Choice.Options[1].IsSelected = true;
    if (main.Transitions.Choice.IsCrossFade)
        throw new InvalidOperationException("Wybrany efekt nie odznaczył „Przenikanie”");
    // „Brak efektu” — sam, bez efektów i przenikania; wybrany efekt go odznacza
    main.Transitions.Choice.IsCut = true;
    if (active.Plan!.ChosenTransitions is not [SlideTransition.None] || main.Transitions.Choice.IsCrossFade
        || main.Transitions.Choice.Options.Any(o => o.IsSelected) || main.Transitions.ShortText != "Brak efektu")
        throw new InvalidOperationException("„Brak efektu” nie trafił do planu");
    main.Transitions.Choice.Options[2].IsSelected = true;
    if (main.Transitions.Choice.IsCut || active.Plan!.ChosenTransitions is not [SlideTransition.Flame])
        throw new InvalidOperationException("Wybrany efekt nie odznaczył „Brak efektu”");
    active.Update(p => p.WithTransitions(before));
}

// Okno „Telefon” ze sparowanymi urządzeniami (serwer udawany — bez sieci).
{
    var server = new ShotRemoteServer();
    var remoteModel = new RemoteViewModel(server);
    remoteModel.ShowCommand.Execute(null);
    var remoteWindow = new Window { Width = 900, Height = 1080, Content = new RemoteOverlay { DataContext = remoteModel } };
    remoteWindow.Show();
    server.Raise();
    Save(remoteWindow, "telefon");
    remoteWindow.Close();
}

window.Width = 1180;
window.Height = 720;
Save(window, "operator-waskie");
window.Width = 1600;
window.Height = 960;

main.Preview.EditCommand.Execute(null);
Save(window, "piesn-edycja");
main.Preview.Editor!.CancelCommand.Execute(null);
main.PlanAdd.Open(2);
Save(window, "plan-dodaj");
main.PlanAdd.NewTextCommand.Execute(null);
main.Preview.Editor!.Title = "";
main.Preview.Editor!.Parts[0].Text = "Przyjdź Duchu Święty i płoń we mnie świętym pragnieniem,\nabym z pokorą serca i wyczekującą wiarą";
Save(window, "tekst-nowy");
main.Preview.Editor!.SaveCommand.Execute(null);
Save(window, "tekst-zapisany");
main.Plan.Selected = main.Plan.Items[2];

main.PlanAdd.Open(main.Plan.Items.Count, "duch");
Save(window, "plan-dodaj-szukaj");
main.PlanAdd.CloseCommand.Execute(null);

services.GetRequiredService<IPlanStore>().Save(plan.CloneAs("Próba z dziećmi", null, PlanKind.Template));
services.GetRequiredService<IPlanStore>().Save(plan.CloneAs("Uwielbienie młodzieżowe", new DateOnly(2026, 9, 18), PlanKind.Event));
main.Plans.ShowCommand.Execute(null);
Save(window, "plany");
main.Plans.Selected = main.Plans.Items[1];
main.Plans.EditName = "Wieczór uwielbienia";
Save(window, "plany-edycja");
main.Plans.StartNewCommand.Execute(null);
Save(window, "plany-nowe");
main.Plans.ShowTemplatesTabCommand.Execute(null);
main.Plans.RequestDeleteCommand.Execute(null);
Save(window, "plany-szablony");
main.Plans.IsOpen = false;

var projection = services.GetRequiredService<Func<ProjectionWindow>>()();
projection.Width = 1920;
projection.Height = 1080;
projection.Show();
control.Show(active.Playlist[1], 0);
Save(projection, "projekcja-poczatek");
control.Show(active.Playlist[1], 1);
Save(projection, "projekcja");
// Krótka zwrotka z refrenem na jednym slajdzie (24), także z zawijanymi wersami (143) i najdłuższa (59: 9 wersów).
control.Show(active.Playlist.First(i => i.Number == 24), 1);
Save(projection, "projekcja-zwrotka-refren", settle: true);
Save(window, "operator-zwrotka-refren", settle: true);
control.Show(active.Playlist.First(i => i.Number == 143), 1);
Save(projection, "projekcja-zwrotka-refren-dlugie", settle: true);
var longest = active.Playlist.First(i => i.Number == 59);
control.Show(longest, longest.Slides.ToList().FindIndex(sl => sl.Label.Contains(Slide.JoinSeparator, StringComparison.Ordinal)));
Save(projection, "projekcja-zwrotka-refren-7", settle: true);
// Prezentacja: kolumna „Pieśń”, „Na ekranie” i rzutnik.
main.Plan.Selected = main.Plan.Items.First(i => i.IsPresentation);
control.Show(active.Playlist.First(i => i.Slides.FirstOrDefault() is ImageSlide), 1);
Save(window, "prezentacja", settle: true);
Save(projection, "projekcja-prezentacja", settle: true);

// Kiosk: kolumna „Pieśń” (edycja), „Na ekranie” i każdy układ elementu na rzutniku.
{
    var rotation = services.GetRequiredService<Uwielbienia.Core.Kiosks.KioskRotation>();
    main.Plan.Selected = main.Plan.Items.First(i => i.IsKiosk);
    control.Show(active.Playlist.First(i => i.PlanItemId == kioskItem.Id));
    Save(window, "kiosk", settle: true);
    foreach (var (index, name) in new[] { (0, "pod"), (1, "obok"), (2, "napis"), (3, "obraz") })
    {
        rotation.GoTo(index);
        Save(projection, "projekcja-kiosk-" + name, settle: true);
    }

    // Przejście na żywo (zrzut stron, nakładka i animacja w oknie projekcji): po końcu ekran pokazuje nowy element, bez nakładki.
    // Bez karty graficznej (to narzędzie) klatki są za wolne — przejścia same przechodzą na przenikanie.
    active.Update(p => p.Replace(kioskItem with { Transitions = [SlideTransition.StainedGlass] }));
    Save(projection, "tmp", settle: true);
    rotation.GoTo(2);
    for (var i = 0; i < 25; i++)
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Thread.Sleep(80);
    }
    Save(projection, "projekcja-kiosk-po-przejsciu");
    // Powolny zoom: zdjęcie po kilku sekundach przybliżone / oddalone (rusza po przejściu).
    active.Update(p => p.Replace(kioskItem with { Transitions = [], SlowZoom = true }));
    rotation.GoTo(1);
    for (var i = 0; i < 50; i++)
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Thread.Sleep(80);
    }
    Save(projection, "projekcja-kiosk-zoom");
    active.Update(p => p.Replace(kioskItem));
    Save(projection, "tmp", settle: true);

    // Efekty przejść: zrzuty dwóch elementów i kilka klatek każdego efektu (shader rysowany jak na rzutniku).
    var slideView = projection.FindControl<SlideView>("Slide")!;
    rotation.GoTo(0);
    Save(projection, "tmp", settle: true);
    var first = Uwielbienia.App.Transitions.ShaderPageTransition.Capture(slideView)!;
    rotation.GoTo(1);
    Save(projection, "tmp", settle: true);
    var second = Uwielbienia.App.Transitions.ShaderPageTransition.Capture(slideView)!;
    File.Delete(Path.Combine(output, (light ? "jasny-" : "") + "tmp.png"));
    foreach (var effect in SlideTransitions.All)
    {
        var shader = Uwielbienia.App.Transitions.TransitionShaders.For(effect);
        if (shader is null)
        {
            Console.WriteLine("SHADER BLAD " + effect);
            continue;
        }
        var direction = effect == SlideTransition.LightRay ? Vector.Normalize(new Vector(1, 0.45)) : new Vector(1, 0);
        var transitionView = new Uwielbienia.App.Transitions.ShaderTransitionView(first, second, shader, 0.37f, direction) { AllowSoftware = true };
        var host = new Window { Width = 960, Height = 540, Content = transitionView };
        host.Show();
        foreach (var progress in new[] { 0.3, 0.55, 0.8 })
        {
            transitionView.Progress = progress;
            Save(host, $"przejscie-{effect.ToString().ToLowerInvariant()}-{progress * 100:0}");
        }
        host.Close();
    }
}

// Przejście na inną pozycję planu: efekty planu; następny slajd tej samej pieśni — „Slajdy: …” (domyślnie jak w planie).
{
    // przejścia planu nie zmieniają kiosku — ten ma własne
    var kioskTransitions = active.FindLiveItem(kioskItem.Id)!.Slides.OfType<Uwielbienia.Core.Kiosks.KioskSlide>().Single().ChosenTransitions;
    active.Update(p => p with { Transitions = [SlideTransition.Flame] });
    if (!active.FindLiveItem(kioskItem.Id)!.Slides.OfType<Uwielbienia.Core.Kiosks.KioskSlide>().Single().ChosenTransitions.SequenceEqual(kioskTransitions))
        throw new InvalidOperationException("Przejścia planu zmieniły kiosk");
    active.Update(p => p with { Transitions = [SlideTransition.Watercolor] });
    var song = active.Playlist.First(i => i.Slides.Count > 1 && i.Slides[0] is not ImageSlide);
    control.Show(song);
    control.Next();
    if (main.Live.PageTransition is not Uwielbienia.App.Transitions.ShaderPageTransition { Effects: [SlideTransition.Watercolor] })
        throw new InvalidOperationException("Następny slajd pieśni bez efektu planu („Slajdy: jak w planie”)");
    active.Update(p => p.WithInnerTransitions([]));
    control.Next();
    if (main.Live.PageTransition != LiveViewModel.SlideChange)
        throw new InvalidOperationException("„Slajdy: przenikanie” — następny slajd pieśni nie powinien mieć efektu");
    active.Update(p => p.WithInnerTransitions(null));
    control.NextItem();
    if (main.Live.PageTransition is not Uwielbienia.App.Transitions.ShaderPageTransition { Effects: [SlideTransition.Watercolor] })
        throw new InvalidOperationException("Następna pozycja planu bez efektu planu");
    for (var i = 0; i < 25; i++)
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Thread.Sleep(80);
    }
    Save(projection, "projekcja-nastepna-pozycja");
    // Zrzut strony z tekstem (sama przezroczysta) ma tło ekranu — inaczej efekt w jasnym wyglądzie szedłby przez czerń.
    control.Show(song);
    Save(projection, "tmp", settle: true);
    var textPage = projection.GetVisualDescendants().OfType<Avalonia.Controls.Presenters.ContentPresenter>()
        .First(c => c.Content is ProjectedPage { Slide: not ImageSlide } && c.IsVisible);
    using (var pageImage = Uwielbienia.App.Transitions.ShaderPageTransition.Capture(textPage)!)
    using (var pixels = SkiaSharp.SKBitmap.FromImage(pageImage))
    {
        var corner = pixels.GetPixel(2, 2);
        var expected = ((Avalonia.Media.ISolidColorBrush)main.Live.Appearance.Background).Color;
        if (Math.Abs(corner.Red - expected.R) > 8 || Math.Abs(corner.Green - expected.G) > 8 || corner.Alpha != 255)
            throw new InvalidOperationException($"Zrzut strony bez tła ekranu: {corner}, oczekiwane {expected}");
    }
    File.Delete(Path.Combine(output, (light ? "jasny-" : "") + "tmp.png"));
    active.Update(p => p with { Transitions = null });
}

// Film: kolumna „Pieśń”, „Na ekranie” (podgląd, pauza, suwak) i rzutnik — także z „Dopasuj obraz”.
services.GetRequiredService<Uwielbienia.Core.Videos.VideoDirector>();
main.Plan.Selected = main.Plan.Items.First(i => i.IsVideo);
if (videoArg is not null)
{
    control.Show(active.Playlist.First(i => i.Slides.FirstOrDefault() is VideoSlide));
    for (var i = 0; i < 3000 / 20; i++)
    {
        Dispatcher.UIThread.RunJobs();
        Thread.Sleep(20);
    }
    Save(window, "film", settle: true);
    Save(projection, "projekcja-film", settle: true);
    services.GetRequiredService<ProjectionCalibration>().TrySet([new(0.06, 0.04), new(0.95, 0.09), new(0.9, 0.93), new(0.1, 0.97)]);
    Save(projection, "projekcja-film-dopasowanie", settle: true);
    services.GetRequiredService<ProjectionCalibration>().ResetCommand.Execute(null);
    Console.WriteLine("FILM " + services.GetRequiredService<Uwielbienia.Core.Videos.IVideoPlayer>().Status);
}
else
{
    Save(window, "film-brak-pliku", settle: true);
}

// Aplikacja: kolumna „Pieśń”, „Na ekranie” i rzutnik — obraz okna programu albo czarny ekran, gdy go nie ma.
services.GetRequiredService<Uwielbienia.Core.AppWindows.AppWindowDirector>();
main.Plan.Selected = main.Plan.Items.First(i => i.IsApp);
control.Show(active.Playlist.First(i => i.Slides.FirstOrDefault() is AppWindowSlide));
for (var i = 0; i < 2000 / 20; i++)
{
    Dispatcher.UIThread.RunJobs();
    Thread.Sleep(20);
}
Save(window, "aplikacja", settle: true);
Save(projection, "projekcja-aplikacja", settle: true);
Console.WriteLine("APLIKACJA " + (services.GetRequiredService<AppWindowCapture>().Shown?.ToString() ?? "brak okna"));
control.Clear();
if (appArg is not null)
{
    main.PlanAdd.Open(main.Plan.Items.Count);
    main.PlanAdd.ChooseWindowCommand.Execute(null);
    Save(window, "plan-dodaj-aplikacja");
    main.PlanAdd.CloseCommand.Execute(null);
}

// Dopasowanie obrazu: przekrzywiony czworokąt z siatką i uchwytami.
var calibration = services.GetRequiredService<ProjectionCalibration>();
calibration.TrySet([new(0.06, 0.04), new(0.95, 0.09), new(0.9, 0.93), new(0.1, 0.97)]);
calibration.IsActive = true;
control.Show(active.Playlist[1], 1);
Save(projection, "projekcja-dopasowanie");
Save(window, "operator-dopasowanie");
{
    calibration.ResetCommand.Execute(null);
    var none = Avalonia.Input.RawInputModifiers.None;
    // chwyt daleko od narożników, w prawej dolnej części obrazu → prawy dolny narożnik
    projection.MouseMove(new Point(1500, 540), none);
    projection.MouseDown(new Point(1500, 540), Avalonia.Input.MouseButton.Left, none);
    projection.MouseMove(new Point(1400, 560), Avalonia.Input.RawInputModifiers.LeftMouseButton);
    projection.MouseMove(new Point(1300, 580), Avalonia.Input.RawInputModifiers.LeftMouseButton);
    projection.MouseUp(new Point(1300, 580), Avalonia.Input.MouseButton.Left, none);
    // chwyt po lewej u góry → lewy górny narożnik
    projection.MouseDown(new Point(500, 250), Avalonia.Input.MouseButton.Left, none);
    projection.MouseMove(new Point(620, 330), Avalonia.Input.RawInputModifiers.LeftMouseButton);
    projection.MouseUp(new Point(620, 330), Avalonia.Input.MouseButton.Left, none);
    // Shift+kółko: węziej
    projection.MouseWheel(new Point(900, 500), new Vector(0, -1), Avalonia.Input.RawInputModifiers.Shift);
    projection.MouseMove(new Point(700, 300), none);
    Console.WriteLine("NAROZNIKI " + string.Join(" ", calibration.Corners.Select(c => $"({c.X:0.000},{c.Y:0.000})")));
    Save(projection, "projekcja-krawedzie");
}
calibration.IsActive = false;
Save(projection, "projekcja-dopasowana");
calibration.ResetCommand.Execute(null);

projection.IsSameScreen = true;
((ProjectionViewModel)projection.DataContext!).OpenQuickPick("jez");
Save(projection, "projekcja-szybki-wybor");

Console.WriteLine(output);
control.Clear();
Directory.Delete(root, recursive: true);

void Save(Window w, string name, bool settle = false)
{
    // obrazy slajdów wczytują się w tle
    for (var i = 0; i < (settle ? 30 : 5); i++)
    {
        Dispatcher.UIThread.RunJobs();
        if (settle)
            Thread.Sleep(20);
    }
    AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    var frame = w.CaptureRenderedFrame();
    frame?.Save(Path.Combine(output, (light ? "jasny-" : "") + name + ".png"), new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
}

static string DrawPortrait(string file)
{
    var picture = new Border
    {
        Width = 900,
        Height = 1200,
        Background = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Color.Parse("#E8A33D"), 0), new GradientStop(Color.Parse("#3A1E5C"), 1) },
        },
        Child = new Avalonia.Controls.Shapes.Ellipse { Width = 420, Height = 420, Fill = new SolidColorBrush(Color.Parse("#FFF2C6")), Margin = new Thickness(0, 0, 0, 260) },
    };
    picture.Measure(new Size(900, 1200));
    picture.Arrange(new Rect(0, 0, 900, 1200));
    using var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(900, 1200));
    bitmap.Render(picture);
    bitmap.Save(file, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
    return file;
}

static string DrawSlide(int number, string file)
{
    var colors = new[] { "#1D3557", "#5B2A86", "#2A6041" };
    var slide = new Border
    {
        Width = 1920,
        Height = 1080,
        Background = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Color.Parse(colors[number - 1]), 0), new GradientStop(Colors.Black, 1) },
        },
        Child = new StackPanel
        {
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Margin = new Thickness(160, 0),
            Children =
            {
                new TextBlock { Text = $"Ogłoszenia — {number}", FontSize = 110, FontWeight = FontWeight.Bold, Foreground = Brushes.White },
                new TextBlock { Text = "Spotkanie wspólnoty w czwartek o 19:00", FontSize = 64, Foreground = Brushes.Gold, Margin = new Thickness(0, 30, 0, 0) },
            },
        },
    };
    slide.Measure(new Size(1920, 1080));
    slide.Arrange(new Rect(0, 0, 1920, 1080));
    using var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(1920, 1080));
    bitmap.Render(slide);
    bitmap.Save(file, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
    return file;
}

sealed class ShotRemoteServer : Uwielbienia.Link.IRemoteServer
{
    private readonly Dictionary<string, bool> _canAdd = new() { ["a"] = true, ["b"] = false };

    public Uwielbienia.Link.RemoteServerStatus Status => Uwielbienia.Link.RemoteServerStatus.Ready;

    public int ConnectedDevices => 1;

    public string? LastError => null;
    public string? DevicesError => null;

    public IReadOnlyList<Uwielbienia.Link.RemoteDevice> Devices =>
    [
        new("a", "telefon", new DateTimeOffset(2026, 9, 12, 18, 0, 0, TimeSpan.Zero), true, _canAdd["a"]),
        new("b", "telefon", new DateTimeOffset(2026, 10, 2, 18, 0, 0, TimeSpan.Zero), false, _canAdd["b"]),
        new("c", "panel", new DateTimeOffset(2026, 10, 20, 18, 0, 0, TimeSpan.Zero), true, false, IsPanel: true),
    ];

    public event EventHandler? Changed;

    public void Raise() => Changed?.Invoke(this, EventArgs.Empty);

    public void SetPermission(string deviceKey, Uwielbienia.Link.DevicePermission permission, bool allowed)
    {
        if (permission == Uwielbienia.Link.DevicePermission.AddSongs)
            _canAdd[deviceKey] = allowed;
        Raise();
    }

    public Task ForgetAsync(string deviceKey, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<Uwielbienia.Link.RemoteInvitation> InviteAsync(
        Uwielbienia.Link.RemoteClient client = Uwielbienia.Link.RemoteClient.Pilot, CancellationToken cancellationToken = default) =>
        Task.FromResult(new Uwielbienia.Link.RemoteInvitation(
            "tco2FwWCDSeyRbjMTA2mWu3c", new Uri("https://greysource.eu/uwielbienie/telefon/#code=tco2FwWCDSeyRbjMTA2mWu3c"),
            DateTimeOffset.Now.AddMinutes(10), client, "ajk59", new Uri("https://greysource.eu/uwielbienie/telefon/#code=ajk59"),
            DateTimeOffset.Now.AddMinutes(5)));

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
