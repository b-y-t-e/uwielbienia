namespace Uwielbienia.Core.Presentation;

/// <summary>
/// To, co naraz jest na ekranie. Podstawowy slajd to tekst (sekcja pieśni albo jej fragment).
/// Inny rodzaj treści (np. obraz) = rekord dziedziczący po <see cref="Slide"/> z własnymi polami
/// i własnym szablonem w <c>SlideView</c>; <see cref="Lines"/> zostaje wtedy pusty.
/// </summary>
/// <param name="SectionCode">Kod części, np. <c>C</c> — do dopasowania slajdu po zmianie układu.</param>
/// <param name="Label">Etykieta dla operatora, np. „Refren” albo „Zwrotka 1 (2/2)”.</param>
public record Slide(string SectionCode, string Label, IReadOnlyList<SlideLine> Lines);

/// <summary>Wers slajdu.</summary>
/// <param name="Text">Tekst do wyświetlenia (bez znaczników).</param>
/// <param name="Chords">Akordy (tylko dla operatora; nigdy na ekranie projekcji).</param>
/// <param name="Repeat">Liczba powtórzeń wersu/fragmentu, 1 = bez powtórzeń.</param>
public sealed record SlideLine(string Text, string? Chords, int Repeat, bool RepeatStart, bool RepeatEnd);
