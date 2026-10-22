using Uwielbienia.Core;
using Avalonia.Threading;

namespace Uwielbienia.App.Services;

/// <summary>
/// Ostatnia linia obrony: nieobsłużony wyjątek (na wątku interfejsu, w porzuconym zadaniu, w timerze) trafia do
/// <c>bledy.log</c>, a aplikacja działa dalej — w trakcie nabożeństwa zamknięcie rzutnika jest gorsze niż błąd jednej czynności.
/// </summary>
public static class CrashGuard
{
    private static string? _file;
    private static readonly Lock WriteLock = new();

    public static void Start(string file)
    {
        _file = file;
        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            Log(e.Exception);
            e.Handled = true;
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log(e.Exception);
            e.SetObserved();
        };
        // wyjątek poza interfejsem i zadaniami (timer, własny wątek) zamyka proces — zostaje przynajmniej wpis w dzienniku
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception exception)
                Log(exception);
        };
    }

    public static void Log(Exception exception)
    {
        if (_file is null)
            return;
        try
        {
            // błąd interfejsu i porzuconego zadania mogą przyjść naraz z dwóch wątków
            lock (WriteLock)
                File.AppendAllText(_file, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (Exception ex) when (FileErrors.IsFileAccess(ex))
        {
            // nie ma gdzie zapisać — trudno, byle aplikacja działała dalej
        }
    }
}

/// <summary><see cref="IErrorLog"/> zapisujący do tego samego <c>bledy.log</c> co <see cref="CrashGuard"/>.</summary>
public sealed class CrashGuardErrorLog : Uwielbienia.Core.IErrorLog
{
    public void Report(Exception exception) => CrashGuard.Log(exception);
}
