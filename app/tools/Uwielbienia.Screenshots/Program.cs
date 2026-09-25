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
var plan = Plan.Create("Uwielbienie 25 września 2026", new DateOnly(2026, 9, 25));
foreach (var number in new[] { 47, 94, 30, 36, 117, 128 })
    plan = plan.Insert(plan.Items.Count, SongPlanItem.For(library.Songs.First(s => s.Number == number).Id));
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

main.Search.Query = "duch";
Save(window, "operator-szukaj");
main.Search.Clear();

main.Plans.ShowCommand.Execute(null);
Save(window, "plany");
main.Plans.IsOpen = false;

var projection = services.GetRequiredService<Func<ProjectionWindow>>()();
projection.Width = 1920;
projection.Height = 1080;
projection.Show();
Save(projection, "projekcja");
projection.IsSameScreen = true;
((ProjectionViewModel)projection.DataContext!).OpenQuickPick("jez");
Save(projection, "projekcja-szybki-wybor");

Console.WriteLine(output);
Directory.Delete(root, recursive: true);

void Save(Window w, string name)
{
    for (var i = 0; i < 5; i++)
        Dispatcher.UIThread.RunJobs();
    AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    var frame = w.CaptureRenderedFrame();
    frame?.Save(Path.Combine(output, (light ? "jasny-" : "") + name + ".png"), new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
}
