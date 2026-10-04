using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Threading;
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
// Użycie: dotnet run --project tools/Uwielbienia.Screenshots -- <katalog_wyjściowy> [light]

var output = args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "uwielbienia-shots");
var light = args.Contains("light");
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
foreach (var number in new[] { 47, 94, 30, 36, 117, 128 })
    plan = plan.Insert(plan.Items.Count, SongPlanItem.For(library.Songs.First(s => s.Number == number).Id));
// Prezentacja z obrazów (PowerPoint w narzędziu niedostępny): trzy slajdy narysowane tutaj.
var slideFiles = Enumerable.Range(1, 3).Select(i => DrawSlide(i, Path.Combine(root, $"ogloszenia-{i}.png"))).ToList();
var imported = services.GetRequiredService<ISlideshowLibrary>().ImportAsync(slideFiles).GetAwaiter().GetResult();
plan = plan.Insert(3, new PresentationPlanItem(Guid.NewGuid(), imported.Folder, "Ogłoszenia parafialne"));
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
// Prezentacja: kolumna „Pieśń”, „Na ekranie” i rzutnik.
main.Plan.Selected = main.Plan.Items.First(i => i.IsPresentation);
control.Show(active.Playlist.First(i => i.Slides.FirstOrDefault() is ImageSlide), 1);
Save(window, "prezentacja", settle: true);
Save(projection, "projekcja-prezentacja", settle: true);

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
