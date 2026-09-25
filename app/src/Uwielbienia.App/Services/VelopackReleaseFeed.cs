using Uwielbienia.Core.Updates;
using Velopack;
using Velopack.Sources;

namespace Uwielbienia.App.Services;

/// <summary>GitHub Releases feed of the installation created by <c>Uwielbienia-win-Setup.exe</c>.</summary>
public sealed class VelopackReleaseFeed(IApplicationShutdown shutdown) : IReleaseFeed
{
    private const string GitHubRepository = "https://github.com/b-y-t-e/uwielbienia";

    private readonly Lazy<UpdateManager?> _lazyManager = new(CreateManagerIfLocated);
    private UpdateInfo? _downloaded;

    private UpdateManager Manager => _lazyManager.Value
        ?? throw new InvalidOperationException("Velopack is not initialized in this process.");

    public bool CanUpdate => _lazyManager.Value?.IsInstalled == true;

    /// <summary>Without <c>VelopackApp.Run</c> (e.g. the screenshot tool) there is no locator, so no updates.</summary>
    private static UpdateManager? CreateManagerIfLocated()
    {
        try
        {
            return new UpdateManager(new GithubSource(GitHubRepository, null, false));
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    public string? DownloadNewerRelease()
    {
        if (Manager.CheckForUpdates() is not { } update)
            return null;
        Manager.DownloadUpdates(update);
        _downloaded = update;
        return update.TargetFullRelease.Version.ToString();
    }

    public void InstallAndRestart()
    {
        if (_downloaded is null)
            throw new InvalidOperationException("No release has been downloaded.");
        Manager.WaitExitThenApplyUpdates(_downloaded, silent: false, restart: true);
        // The updater waits for this process to exit.
        shutdown.ShutDown();
    }
}
