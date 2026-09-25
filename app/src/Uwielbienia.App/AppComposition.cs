using Microsoft.Extensions.DependencyInjection;
using Uwielbienia.App.Services;
using Uwielbienia.App.ViewModels;
using Uwielbienia.App.Views;
using Uwielbienia.Core;
using Uwielbienia.Core.Plans;
using Uwielbienia.Core.Presentation;
using Uwielbienia.Core.Songs;
using Uwielbienia.Link;

namespace Uwielbienia.App;

/// <summary>Korzeń kompozycji: jedyne miejsce, które zna konkretne klasy wszystkich usług.</summary>
public static class AppComposition
{
    public static ServiceProvider Build(AppPaths paths)
    {
        var services = new ServiceCollection();

        // ustawienia i ścieżki
        services.AddSingleton(paths);
        services.AddSingleton<ISettingsStore, JsonSettingsStore>();

        // pieśni
        services.AddSingleton<ISongParser, MarkdownSongParser>();
        services.AddSingleton<ISongSource>(sp => SongSourceFactory.Create(sp.GetRequiredService<ISettingsStore>().Current));
        services.AddSingleton<ISongLibrary, SongLibrary>();
        services.AddSingleton<ISongSearch, SongSearch>();

        // plany i ekran
        services.AddSingleton<IPlanStore>(sp => new JsonPlanStore(sp.GetRequiredService<AppPaths>().Plans));
        services.AddSingleton<ISlideBuilder>(sp => new SectionSlideBuilder(sp.GetRequiredService<ISettingsStore>().Current.MaxLinesPerSlide));
        services.AddSingleton<ILiveItemFactory, LiveItemFactory>();
        services.AddSingleton<LiveSession>();
        services.AddSingleton<ILiveControl>(sp => sp.GetRequiredService<LiveSession>());
        services.AddSingleton<ILiveStateSource>(sp => sp.GetRequiredService<LiveSession>());
        services.AddSingleton<ActivePlan>();
        services.AddSingleton<PlanActions>();
        services.AddSingleton<IUiDispatcher, AvaloniaUiDispatcher>();

        // połączenia (telefon / przeglądarka)
        services.AddSingleton(sp => new RemoteServerOptions(
            AppName: "uwielbienia",
            StateDirectory: sp.GetRequiredService<AppPaths>().LinkState,
            PageUrl: App.RemotePageUrl));
        services.AddSingleton<RemoteCommandHandler>();
        services.AddSingleton<IRemoteServer, LinkRemoteServer>();

        // okna i modele widoków
        services.AddSingleton<ProjectionAppearance>();
        services.AddSingleton<LiveViewModel>();
        services.AddSingleton<PlanViewModel>();
        services.AddSingleton<SearchViewModel>();
        services.AddSingleton<PreviewViewModel>();
        services.AddSingleton<PlansViewModel>();
        services.AddSingleton<RemoteViewModel>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
        services.AddSingleton<ProjectionController>();
        services.AddSingleton<IProjectionController>(sp => sp.GetRequiredService<ProjectionController>());
        services.AddSingleton<Func<ProjectionWindow>>(sp => () => new ProjectionWindow(new ProjectionViewModel(
            sp.GetRequiredService<LiveViewModel>(),
            ActivatorUtilities.CreateInstance<SearchViewModel>(sp),
            sp.GetRequiredService<ILiveControl>(),
            sp.GetRequiredService<IProjectionController>())));

        return services.BuildServiceProvider();
    }
}
