namespace Uwielbienia.Core.Presentation;

/// <summary>
/// Efekt przejścia między dwoma slajdami (dziś w kiosku; ten sam mechanizm może służyć każdej zmianie slajdu).
/// Brak efektu = zwykłe przenikanie.
/// </summary>
public enum SlideTransition
{
    /// <summary>Stary slajd wysuwa się, nowy wjeżdża za nim — w losowym kierunku (wstecz: z powrotem).</summary>
    Slide,
    /// <summary>Obraz rozpada się na szybki witrażu, które po kolei zapalają się nowym obrazem; ołowiane linie świecą.</summary>
    StainedGlass,
    /// <summary>Stary slajd wypala się od brzegów, z żarzącą się krawędzią.</summary>
    Flame,
    /// <summary>Nowy obraz rozlewa się jak farba na mokrym papierze.</summary>
    Watercolor,
    /// <summary>Ukośny pas światła przesuwa się przez ekran i odsłania nowy obraz.</summary>
    LightRay,
    /// <summary>Krąg fali rozchodzi się od środka; za nim nowy obraz.</summary>
    Ripple,
}

public static class SlideTransitions
{
    public static readonly IReadOnlyList<SlideTransition> All = Enum.GetValues<SlideTransition>();

    /// <summary>Domyślny wybór (plan i kiosk): efektowne, a spokojne — nowy obraz rozlewa się jak farba.</summary>
    public static readonly IReadOnlyList<SlideTransition> Default = [SlideTransition.Watercolor];

    /// <summary>Wybór do zapisu: równy domyślnemu = <c>null</c> (podąża za domyślnym).</summary>
    public static IReadOnlyList<SlideTransition>? ToStored(IReadOnlyList<SlideTransition> chosen) =>
        chosen.SequenceEqual(Default) ? null : chosen;

    /// <summary>Opis wyboru: „przenikanie”, nazwa efektu albo „losowe (n)”.</summary>
    public static string Describe(IReadOnlyList<SlideTransition> chosen) => chosen.Count switch
    {
        0 => "przenikanie",
        1 => Name(chosen[0]),
        var n => $"losowe ({n})",
    };

    public static string Name(SlideTransition transition) => transition switch
    {
        SlideTransition.Slide => "Przesunięcie",
        SlideTransition.StainedGlass => "Witraż",
        SlideTransition.Flame => "Płomień",
        SlideTransition.Watercolor => "Akwarela",
        SlideTransition.LightRay => "Promień światła",
        SlideTransition.Ripple => "Fala",
        _ => transition.ToString(),
    };

    /// <summary>Jeden z wybranych efektów (losowo, gdy jest ich kilka); <c>null</c> = nic nie wybrano — zwykłe przenikanie.</summary>
    public static SlideTransition? Pick(IReadOnlyList<SlideTransition> chosen, Random random) =>
        chosen.Count == 0 ? null : chosen[random.Next(chosen.Count)];
}
