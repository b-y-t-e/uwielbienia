namespace Uwielbienia.Core.Presentation;

/// <summary>
/// Efekt przejścia między dwoma slajdami (dziś w kiosku; ten sam mechanizm może służyć każdej zmianie slajdu).
/// Pusty wybór = zwykłe przenikanie, <see cref="SlideTransition.None"/> = bez przejścia.
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
    /// <summary>„Brak efektu” — nowy slajd od razu, bez przenikania. Wybierany sam (wyklucza efekty).</summary>
    None,
}

public static class SlideTransitions
{
    /// <summary>Efekty do zaznaczania (bez <see cref="SlideTransition.None"/> — ten ma osobny przycisk, jak przenikanie).</summary>
    public static readonly IReadOnlyList<SlideTransition> All =
        Enum.GetValues<SlideTransition>().Where(t => t != SlideTransition.None).ToList();

    /// <summary>„Przenikanie”: żaden efekt.</summary>
    public static readonly IReadOnlyList<SlideTransition> CrossFade = [];

    /// <summary>„Brak efektu”: nowy slajd od razu.</summary>
    public static readonly IReadOnlyList<SlideTransition> Cut = [SlideTransition.None];

    public static bool IsCut(IReadOnlyList<SlideTransition> chosen) => chosen is [SlideTransition.None];

    /// <summary>Domyślny wybór (plan i kiosk): efektowne, a spokojne — nowy obraz rozlewa się jak farba.</summary>
    public static readonly IReadOnlyList<SlideTransition> Default = [SlideTransition.Watercolor];

    /// <summary>Ten sam wybór efektów: te same w tej samej kolejności, a <c>null</c> („jak w planie”) tylko równy <c>null</c>.</summary>
    public static bool SameChoice(IReadOnlyList<SlideTransition>? a, IReadOnlyList<SlideTransition>? b) =>
        a is null ? b is null : b is not null && a.SequenceEqual(b);

    /// <summary>Wybór do zapisu: równy domyślnemu = <c>null</c> (podąża za domyślnym).</summary>
    public static IReadOnlyList<SlideTransition>? ToStored(IReadOnlyList<SlideTransition> chosen) =>
        chosen.SequenceEqual(Default) ? null : chosen;

    /// <summary>Opis wyboru: „przenikanie”, „brak efektu”, nazwa efektu albo „losowe (n)”.</summary>
    public static string Describe(IReadOnlyList<SlideTransition> chosen) => chosen.Count switch
    {
        0 => "przenikanie",
        1 when chosen[0] == SlideTransition.None => "brak efektu",
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
        SlideTransition.None => "Brak efektu",
        _ => transition.ToString(),
    };

    /// <summary>Jeden z wybranych efektów (losowo, gdy jest ich kilka); <c>null</c> = nic nie wybrano — zwykłe przenikanie.</summary>
    public static SlideTransition? Pick(IReadOnlyList<SlideTransition> chosen, Random random)
    {
        if (IsCut(chosen))
            return SlideTransition.None;
        var effects = chosen.Where(t => t != SlideTransition.None).ToList(); // „Brak efektu” działa tylko sam
        return effects.Count == 0 ? null : effects[random.Next(effects.Count)];
    }
}
