using System.Diagnostics;

namespace Uwielbienia.App.Services;

/// <summary>Uruchamianie programów do eksportu slajdów (DeckCraft, LibreOffice): szukanie pliku i wywołanie z limitem czasu.</summary>
internal static class ExternalTool
{
    /// <summary>Pierwszy istniejący plik z podanych miejsc, potem z <c>PATH</c>.</summary>
    public static string? Find(IEnumerable<string> candidates, params string[] names)
    {
        foreach (var path in candidates)
            if (File.Exists(path))
                return path;
        var dirs = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        foreach (var dir in dirs)
            foreach (var name in names)
            {
                var path = Path.Combine(dir, OperatingSystem.IsWindows() ? name + ".exe" : name);
                if (File.Exists(path))
                    return path;
            }
        return null;
    }

    public static string ProgramFiles(params string[] parts) =>
        Path.Combine([Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), .. parts]);

    /// <summary>Uruchamia program bez okna; wyjście standardowe przy kodzie 0, inaczej wyjątek z komunikatem programu.</summary>
    public static async Task<string> RunAsync(string exe, IEnumerable<string> args, TimeSpan timeout)
    {
        var info = new ProcessStartInfo(exe)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var arg in args)
            info.ArgumentList.Add(arg);
        using var process = Process.Start(info) ?? throw new InvalidOperationException($"Nie udało się uruchomić {Path.GetFileName(exe)}");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var cancel = new CancellationTokenSource(timeout);
        try
        {
            await process.WaitForExitAsync(cancel.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"{Path.GetFileNameWithoutExtension(exe)} nie odpowiada");
        }
        if (process.ExitCode != 0)
        {
            var message = (await error.ConfigureAwait(false)).Trim();
            throw new InvalidOperationException(message.Length > 0 ? message : $"{Path.GetFileNameWithoutExtension(exe)}: kod {process.ExitCode}");
        }
        return await output.ConfigureAwait(false);
    }
}
