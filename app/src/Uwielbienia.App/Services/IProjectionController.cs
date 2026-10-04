namespace Uwielbienia.App.Services;

public enum ProjectionMode
{
    /// <summary>Okno projekcji zamknięte.</summary>
    Off,
    /// <summary>Pełny ekran na innym monitorze niż okno operatora.</summary>
    SecondScreen,
    /// <summary>Jedno urządzenie: projekcja zakrywa okno operatora (Esc wraca).</summary>
    SameScreen,
}

public interface IProjectionController
{
    ProjectionMode Mode { get; }

    /// <summary>Krótki napis na przycisku, np. „Projekcja: HDMI-1” albo „Włącz projekcję”.</summary>
    string Status { get; }

    /// <summary>Podpowiedź do przycisku: co się stanie i jakie klawisze działają.</summary>
    string Hint { get; }

    bool HasSecondScreen { get; }

    event EventHandler? Changed;

    /// <summary>F5: włącza projekcję (na drugim ekranie, jeśli jest; inaczej na tym samym) albo ją wyłącza.</summary>
    void Toggle();

    void ShowOnSameScreen();

    void Close();
}
