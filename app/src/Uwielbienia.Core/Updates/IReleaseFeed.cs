namespace Uwielbienia.Core.Updates;

/// <summary>Source of application releases for an installation that can update itself.</summary>
public interface IReleaseFeed
{
    /// <summary>False for developer runs, portable builds and other platforms — they cannot install updates.</summary>
    bool CanUpdate { get; }

    /// <summary>Downloads a newer release, if there is one, and returns its version.</summary>
    string? DownloadNewerRelease();

    /// <summary>Installs the downloaded release and restarts the application.</summary>
    void InstallAndRestart();
}
