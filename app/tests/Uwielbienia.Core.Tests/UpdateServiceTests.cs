using Uwielbienia.Core;
using Uwielbienia.Core.Updates;

namespace Uwielbienia.Core.Tests;

public class UpdateServiceTests
{
    private sealed class FakeFeed : IReleaseFeed
    {
        public bool CanUpdate { get; init; } = true;
        public string? NewerVersion { get; init; }
        public Exception? Failure { get; init; }
        public int Downloads { get; private set; }
        public bool Installed { get; private set; }

        public string? DownloadNewerRelease()
        {
            Downloads++;
            if (Failure is not null)
                throw Failure;
            return NewerVersion;
        }

        public void InstallAndRestart()
        {
            if (Failure is not null)
                throw Failure;
            Installed = true;
        }
    }

    private sealed class MemoryLog : IUpdateLog
    {
        public List<string> Messages { get; } = [];
        public void Write(string message) => Messages.Add(message);
    }

    private static UpdateService Create(FakeFeed feed, MemoryLog? log = null) =>
        new(feed, log ?? new MemoryLog(), new ImmediateDispatcher());

    [Fact]
    public async Task Check_WithNewerRelease_ReportsItsVersion()
    {
        var service = Create(new FakeFeed { NewerVersion = "0.2.0" });
        var notified = false;
        service.UpdateAvailable += () => notified = true;

        await service.CheckAsync();

        Assert.True(notified);
        Assert.True(service.HasUpdate);
        Assert.Equal("0.2.0", service.NewVersion);
    }

    [Fact]
    public async Task Check_AfterUpdateWasDownloaded_DoesNotDownloadAgain()
    {
        var feed = new FakeFeed { NewerVersion = "0.2.0" };
        var service = Create(feed);

        await service.CheckAsync();
        await service.CheckAsync();

        Assert.Equal(1, feed.Downloads);
    }

    [Fact]
    public async Task Check_WhenFeedFails_LogsAndReportsNoUpdate()
    {
        var log = new MemoryLog();
        var service = Create(new FakeFeed { Failure = new IOException("offline") }, log);

        await service.CheckAsync();

        Assert.False(service.HasUpdate);
        Assert.Single(log.Messages);
    }

    [Fact]
    public void StartPeriodicCheck_WhenInstallationCannotUpdate_NeverQueriesFeed()
    {
        var feed = new FakeFeed { CanUpdate = false };
        using var service = Create(feed);

        service.StartPeriodicCheck();

        Assert.Equal(0, feed.Downloads);
    }

    [Fact]
    public void Apply_WithoutDownloadedUpdate_ReturnsError()
    {
        var feed = new FakeFeed();

        Assert.NotNull(Create(feed).ApplyUpdate());
        Assert.False(feed.Installed);
    }

    [Fact]
    public async Task Apply_WithDownloadedUpdate_Installs()
    {
        var feed = new FakeFeed { NewerVersion = "0.2.0" };
        var service = Create(feed);
        await service.CheckAsync();

        Assert.Null(service.ApplyUpdate());
        Assert.True(feed.Installed);
    }
}
