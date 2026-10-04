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
    bool JoinVerseAndChorus = true);

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
        Current = Load(_file);
    }

    public AppSettings Current { get; private set; }

    public event EventHandler<AppSettings>? Changed;

    public void Update(Func<AppSettings, AppSettings> change)
    {
        Current = change(Current);
        File.WriteAllText(_file, JsonSerializer.Serialize(Current, JsonPlanStore.JsonOptions));
        Changed?.Invoke(this, Current);
    }

    private static AppSettings Load(string file)
    {
        try
        {
            return File.Exists(file)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(file), JsonPlanStore.JsonOptions) ?? new()
                : new();
        }
        catch (JsonException)
        {
            return new();
        }
    }
}
