using Uwielbienia.Core;
using System.Text.Json;
using Uwielbienia.Core.Plans;

namespace Uwielbienia.App.Services;

public enum AppTheme
{
    Dark,
    Light,
}

/// <param name="SongsFolder">Folder <c>Teksty/</c> z repozytorium; <c>null</c> = wykryj automatycznie / wbudowane.</param>
/// <param name="JoinVerseAndChorus">Krótka zwrotka i refren po niej na jednym slajdzie, gdy się mieszczą.</param>
/// <param name="PictureOffsetMs">Obraz filmu względem dźwięku w ms (+ = obraz później) — stałe opóźnienie rzutnika.</param>
/// <param name="ProjectionCorners">Dopasowanie obrazu do rzutnika: x, y 4 narożników (0..1); <c>null</c> = pełny ekran.</param>
public sealed record AppSettings(
    AppTheme Theme = AppTheme.Dark,
    AppTheme ProjectionTheme = AppTheme.Dark,
    string? SongsFolder = null,
    int MaxLinesPerSlide = 6,
    Guid? LastPlanId = null,
    bool ShowChords = true,
    double PlanColumnRatio = 0.16,
    double PreviewColumnRatio = 0.42,
    double LiveColumnRatio = 0.42,
    double[]? ProjectionCorners = null,
    bool JoinVerseAndChorus = true,
    int PictureOffsetMs = 0);

public sealed class AppPaths
{
    public AppPaths(string? root = null)
    {
        Root = root ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Uwielbienia");
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }

    public string Plans => Path.Combine(Root, "plany");

    /// <summary>Własne pieśni i teksty oraz lokalne poprawki śpiewnika (<c>{id}/piesn.md</c>).</summary>
    public string Texts => Path.Combine(Root, "teksty");

    /// <summary>Prezentacje w planach: kopie plików i slajdy jako obrazy (<c>{folder}/</c>).</summary>
    public string Presentations => Path.Combine(Root, "prezentacje");

    public string LinkState => Path.Combine(Root, "polaczenia");

    public string SettingsFile => Path.Combine(Root, "ustawienia.json");

    public string UpdateLogFile => Path.Combine(Root, "aktualizacje.log");

    /// <summary>Dziennik przejść slajdów (<see cref="Transitions.TransitionLog"/>).</summary>
    public string TransitionLogFile => Path.Combine(Root, "przejscia.log");

    public string ErrorLogFile => Path.Combine(Root, "bledy.log");
}

public interface ISettingsStore
{
    AppSettings Current { get; }

    void Update(Func<AppSettings, AppSettings> change);

    event EventHandler<AppSettings>? Changed;
}

public sealed class JsonSettingsStore : ISettingsStore
{
    private readonly string _file;

    public JsonSettingsStore(AppPaths paths)
    {
        _file = paths.SettingsFile;
        (Current, _unread) = Load(_file);
    }

    /// <summary>Plik istnieje, ale nie dało się go przeczytać — nie nadpisujemy go domyślnymi ustawieniami.</summary>
    private bool _unread;

    public AppSettings Current { get; private set; }

    public event EventHandler<AppSettings>? Changed;

    public void Update(Func<AppSettings, AppSettings> change)
    {
        Current = change(Current);
        if (_unread)
            ReplayOnSaved(change);
        if (!_unread)
            Save();
        Changed?.Invoke(this, Current);
    }

    /// <summary>
    /// Plik był zajęty przy starcie — zapamiętujemy zmiany operatora (także wybór wartości domyślnej, np. pełny obraz);
    /// gdy plik da się przeczytać, nakładamy je na zapisane ustawienia, a pozostałe (np. narożniki) zostają z pliku.
    /// </summary>
    private void ReplayOnSaved(Func<AppSettings, AppSettings> change)
    {
        _unsavedChanges.Add(change);
        var (saved, stillUnread) = Load(_file);
        if (stillUnread)
            return;
        Current = _unsavedChanges.Aggregate(saved, (settings, unsaved) => unsaved(settings));
        _unsavedChanges.Clear();
        _unread = false;
    }

    private readonly List<Func<AppSettings, AppSettings>> _unsavedChanges = [];

    private void Save()
    {
        try
        {
            // przez plik tymczasowy — przerwany zapis nie gubi narożników „Dopasuj obraz”
            var temp = _file + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(Current, JsonPlanStore.JsonOptions));
            File.Move(temp, _file, overwrite: true);
        }
        catch (Exception ex) when (FileErrors.IsFileAccess(ex))
        {
            // plik chwilowo zajęty — ustawienia zostają w pamięci, zapisze je następna zmiana
        }
    }

    /// <summary>Ustawienia z pliku; <c>Unread</c> = plik chwilowo niedostępny (zepsuty JSON to po prostu domyślne).</summary>
    private static (AppSettings Settings, bool Unread) Load(string file)
    {
        try
        {
            // bez File.Exists: przy zabronionym dostępie do folderu mówi „brak pliku” i nadpisalibyśmy zapisane ustawienia
            return (JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(file), JsonPlanStore.JsonOptions) ?? new(), false);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return (new(), false);
        }
        catch (JsonException)
        {
            return (new(), false);
        }
        catch (Exception ex) when (FileErrors.IsFileAccess(ex))
        {
            return (new(), true);
        }
    }
}
