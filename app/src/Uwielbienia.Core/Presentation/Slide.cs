namespace Uwielbienia.Core.Presentation;

/// <summary>To, co naraz jest na ekranie: cała sekcja albo jej fragment.</summary>
/// <param name="SectionCode">Kod sekcji, np. <c>C</c>.</param>
/// <param name="Label">Etykieta dla operatora, np. „Refren” albo „Zwrotka 1 (2/2)”.</param>
public sealed record Slide(string SectionCode, string Label, IReadOnlyList<SlideLine> Lines);

/// <summary>Wers slajdu.</summary>
/// <param name="Text">Tekst do wyświetlenia (bez znaczników).</param>
/// <param name="Chords">Akordy (tylko dla operatora; nigdy na ekranie projekcji).</param>
/// <param name="Repeat">Liczba powtórzeń wersu/fragmentu, 1 = bez powtórzeń.</param>
public sealed record SlideLine(string Text, string? Chords, int Repeat, bool RepeatStart, bool RepeatEnd);
