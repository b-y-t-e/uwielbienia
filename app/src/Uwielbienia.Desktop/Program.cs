using Avalonia;
using Avalonia.Media;

namespace Uwielbienia.Desktop;

internal static class Program
{
    // Nie używać Avalonii ani kodu zależnego od SynchronizationContext przed wywołaniem AppMain.
    [STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    // Używane także przez podgląd XAML w IDE.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App.App>()
            .UsePlatformDetect()
            .With(new FontManagerOptions
            {
                DefaultFamilyName = "avares://Uwielbienia.App/Assets/Fonts#Atkinson Hyperlegible Next",
            })
#if DEBUG
            .WithDeveloperTools()
#endif
            .LogToTrace();
}
