namespace Uwielbienia.Core.Updates;

/// <summary>Finds and downloads newer application releases and installs them on request.</summary>
public interface IUpdateService
{
    event Action? UpdateAvailable;

    bool HasUpdate { get; }

    string? NewVersion { get; }

    void StartPeriodicCheck();

    /// <summary>Installs the downloaded update and restarts; returns an error message when installation fails.</summary>
    string? ApplyUpdate();
}
