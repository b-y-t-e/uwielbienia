namespace Uwielbienia.Core;

/// <summary>Jedna definicja błędu dostępu do pliku (zajęty przez antywirusa lub synchronizację, wyjęty nośnik, brak uprawnień).</summary>
public static class FileErrors
{
    public static bool IsFileAccess(Exception ex) => ex is IOException or UnauthorizedAccessException;
}
