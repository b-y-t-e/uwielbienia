using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Uwielbienia.App.Services;
using Uwielbienia.App.ViewModels;
using Uwielbienia.App.Views;
using Uwielbienia.Core.Updates;
using Uwielbienia.Link;

namespace Uwielbienia.App;

public partial class App : Application
{
    /// <summary>Adres strony pilota; kod parowania trafia w fragment (#code=…), więc nie idzie do serwera WWW.</summary>
    public static readonly Uri RemotePageUrl = new("https://greysource.eu/uwielbienie/");

    private ServiceProvider? _services;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _services = AppComposition.Build(new AppPaths());
            _services.GetRequiredService<PlansViewModel>().OpenInitial();

            var window = _services.GetRequiredService<MainWindow>();
            var projection = _services.GetRequiredService<ProjectionController>();
            window.Opened += (_, _) => projection.Attach(window);
            desktop.MainWindow = window;
            desktop.Exit += (_, _) => DisposeServices();

            _ = _services.GetRequiredService<RemoteViewModel>().StartAsync();
            _services.GetRequiredService<IUpdateService>().StartPeriodicCheck();
        }
        base.OnFrameworkInitializationCompleted();
    }

    private void DisposeServices()
    {
        var services = _services;
        _services = null;
        if (services is null)
            return;

        services.DisposeAsync().AsTask().Wait(2000);
    }
}
