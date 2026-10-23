namespace Uwielbienia.Core.AppWindows;

/// <summary>
/// Obraz okna innego programu na ekranie projekcji (np. „Ekran bezprzewodowy” — obraz z telefonu). Jedno na aplikację;
/// kiedy pokazywać, decyduje <see cref="AppWindowDirector"/>. Okno może być zasłonięte, ale nie zminimalizowane.
/// </summary>
public interface IAppWindowCapture
{
    /// <summary>Dlaczego okien nie da się pokazywać (np. Linux, stary Windows); <c>null</c> = można.</summary>
    string? Unavailable { get; }

    /// <summary>Otwarte okna programów (bez okien tej aplikacji) — do wyboru przy dodawaniu do planu.</summary>
    IReadOnlyList<AppWindowInfo> Windows();

    /// <summary>Pokazywane okno; <c>null</c> = takiego programu nie ma (czarny ekran) albo nic nie jest pokazywane.</summary>
    AppWindowInfo? Shown { get; }

    /// <summary>Zmieniło się <see cref="Shown"/> — zawsze na wątku interfejsu.</summary>
    event EventHandler? ShownChanged;

    /// <summary>
    /// Pokazuje pierwsze okno pasujące do <paramref name="target"/>; gdy go nie ma (albo zostanie zamknięte) — czarny ekran
    /// i szukanie dalej, aż się pojawi. Ten sam cel drugi raz nic nie zmienia.
    /// </summary>
    void Show(AppWindowTarget target);

    /// <summary>Koniec pokazywania; ostatni obraz znika po chwili (przejście do następnego slajdu zaczyna się od niego).</summary>
    void Stop();
}

/// <summary>Okno programu na pulpicie.</summary>
/// <param name="Handle">Uchwyt okna (HWND).</param>
/// <param name="Process">Nazwa procesu bez <c>.exe</c>.</param>
public sealed record AppWindowInfo(nint Handle, string Process, string Title);

/// <summary>
/// Które okno pokazać: program (nazwa procesu) i — dla aplikacji ze Sklepu, które wszystkie działają w
/// <see cref="StoreAppHost"/> — tytuł okna. Zwykłego programu nie rozpoznajemy po tytule, bo ten się zmienia
/// (np. tytuł karty przeglądarki).
/// </summary>
/// <param name="Title">Tytuł okna; <c>null</c> = dowolne okno programu.</param>
public sealed record AppWindowTarget(string Process, string? Title)
{
    /// <summary>Proces, w którym Windows wyświetla okna aplikacji ze Sklepu (np. „Ekran bezprzewodowy”).</summary>
    public const string StoreAppHost = "ApplicationFrameHost";

    public static AppWindowTarget For(AppWindowInfo window) =>
        new(window.Process, string.Equals(window.Process, StoreAppHost, StringComparison.OrdinalIgnoreCase) ? window.Title : null);

    public bool Matches(AppWindowInfo window) =>
        string.Equals(window.Process, Process, StringComparison.OrdinalIgnoreCase)
        && (Title is null || string.Equals(window.Title, Title, StringComparison.OrdinalIgnoreCase));

    /// <summary>Pierwsze pasujące okno (kilka uruchomionych — pierwsze znalezione); <c>null</c> = programu nie ma.</summary>
    public AppWindowInfo? FirstMatch(IEnumerable<AppWindowInfo> windows) => windows.FirstOrDefault(Matches);
}
