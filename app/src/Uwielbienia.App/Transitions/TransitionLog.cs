using System.Diagnostics;
using System.Threading.Channels;

namespace Uwielbienia.App.Transitions;

/// <summary>
/// Dziennik przejść (<c>przejscia.log</c> w danych aplikacji): które efekty wybrano, który wylosowano, gdzie (rzutnik /
/// „Na ekranie”), jak szybko szły klatki i dlaczego przejście skończyło się zwykłym przenikaniem. Do szukania przyczyn,
/// gdy efekt „ginie”. Plik większy niż 1 MB zaczyna się od nowa przy starcie.
/// </summary>
public static class TransitionLog
{
    private const long MaxSize = 1024 * 1024;
    private static readonly Channel<string> Pending = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });
    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private static string? _file;
    private static int _next;

    public static void Start(string file)
    {
        try
        {
            if (new FileInfo(file) is { Exists: true, Length: > MaxSize })
                File.Delete(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // zostaje stary — dopisujemy dalej
        }
        _file = file;
        _ = Task.Run(() => WriteInBackground(file));
        Write($"--- start programu {DateTime.Now:yyyy-MM-dd}");
    }

    /// <summary>Numer przejścia — łączy wpisy jednego przejścia.</summary>
    public static int NextId() => Interlocked.Increment(ref _next);

    /// <summary>Dopisuje wpis do kolejki — plik zapisuje się w tle, wątek UI i renderowania nie czekają na dysk.</summary>
    public static void Write(string message)
    {
        if (_file is null)
            return;
        Pending.Writer.TryWrite($"{DateTime.Now:HH:mm:ss.fff} [{Clock.Elapsed.TotalSeconds,9:0.000}] {message}{Environment.NewLine}");
    }

    private static async Task WriteInBackground(string file)
    {
        await foreach (var line in Pending.Reader.ReadAllAsync())
        {
            try
            {
                await File.AppendAllTextAsync(file, line);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // dziennik nie może przeszkadzać w pokazie
            }
        }
    }
}
