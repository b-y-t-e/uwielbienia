using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace Uwielbienia.App.Services;

/// <summary>Ends the application process.</summary>
public interface IApplicationShutdown
{
    void ShutDown();
}

/// <summary>Closes through the desktop lifetime, so windows close and services dispose cleanly.</summary>
public sealed class DesktopApplicationShutdown : IApplicationShutdown
{
    public void ShutDown()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
        else
            Environment.Exit(0);
    }
}
