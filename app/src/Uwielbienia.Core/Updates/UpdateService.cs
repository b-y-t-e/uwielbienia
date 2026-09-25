namespace Uwielbienia.Core.Updates;

/// <summary>Periodically checks the release feed, downloads an update in the background, and installs it on request.</summary>
public sealed class UpdateService(IReleaseFeed feed, IUpdateLog log, IUiDispatcher dispatcher) : IUpdateService, IDisposable
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(10);

    private Timer? _timer;
    private volatile string? _newVersion;
    private int _checking;

    public event Action? UpdateAvailable;

    public bool HasUpdate => _newVersion is not null;

    public string? NewVersion => _newVersion;

    public void StartPeriodicCheck()
    {
        if (!feed.CanUpdate || _timer is not null)
            return;
        _timer = new Timer(_ => _ = CheckAsync(), null, TimeSpan.Zero, CheckInterval);
    }

    public async Task CheckAsync()
    {
        if (HasUpdate || Interlocked.CompareExchange(ref _checking, 1, 0) != 0)
            return;

        try
        {
            if (await Task.Run(feed.DownloadNewerRelease) is not { } version)
                return;
            _newVersion = version;
            await dispatcher.InvokeAsync(NotifyUpdateAvailable);
        }
        catch (Exception ex)
        {
            log.Write($"Update check failed: {ex}");
        }
        finally
        {
            Interlocked.Exchange(ref _checking, 0);
        }
    }

    public string? ApplyUpdate()
    {
        if (!HasUpdate)
            return "Brak pobranej aktualizacji.";

        try
        {
            feed.InstallAndRestart();
            return null;
        }
        catch (Exception ex)
        {
            log.Write($"Update installation failed: {ex}");
            return $"Nie udało się zainstalować aktualizacji: {ex.Message}";
        }
    }

    private bool NotifyUpdateAvailable()
    {
        UpdateAvailable?.Invoke();
        return true;
    }

    public void Dispose() => _timer?.Dispose();
}
