using Uwielbienia.Core.Updates;

namespace Uwielbienia.App.Services;

public sealed class FileUpdateLog(AppPaths paths) : IUpdateLog
{
    /// <summary>Checks repeat every few minutes (also offline), so the log starts over instead of growing forever.</summary>
    private const long MaxLogBytes = 256 * 1024;

    public void Write(string message)
    {
        try
        {
            StartOverWhenTooLarge();
            File.AppendAllText(paths.UpdateLogFile, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A missing log must not break the update check.
        }
    }

    private void StartOverWhenTooLarge()
    {
        var log = new FileInfo(paths.UpdateLogFile);
        if (log.Exists && log.Length > MaxLogBytes)
            log.Delete();
    }
}
