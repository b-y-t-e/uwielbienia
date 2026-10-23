namespace Uwielbienia.Core.Presentation;

/// <summary>
/// To, co naraz jest na ekranie. Podstawowy slajd to tekst (sekcja pieśni albo jej fragment).
/// Inny rodzaj treści (np. obraz) = rekord dziedziczący po <see cref="Slide"/> z własnymi polami
/// i własnym szablonem w <c>SlideView</c>; <see cref="Lines"/> zostaje wtedy pusty.
/// </summary>
/// <param name="SectionCode">Kod części, np. <c>C</c> — do dopasowania slajdu po zmianie układu.</param>
/// <param name="Label">Etykieta dla operatora, np. „Refren”, „Zwrotka 1 (2/2)” albo „Zwrotka 1 + Refren”.</param>
public record Slide(string SectionCode, string Label, IReadOnlyList<SlideLine> Lines)
{
    /// <summary>Między etykietami części połączonych na jednym slajdzie (zwrotka + refren).</summary>
    public const string JoinSeparator = " + ";

    /// <summary>Etykieta pierwszej części slajdu — ta sama dla zwrotki osobno i zwrotki z refrenem.</summary>
    public string FirstPartLabel => Label.Split(JoinSeparator)[0];
}

/// <summary>Slajd prezentacji: obraz na cały ekran.</summary>
/// <param name="ImageFile">Pełna ścieżka pliku obrazu.</param>
/// <param name="Number">Numer slajdu od 1.</param>
public sealed record ImageSlide(string ImageFile, int Number) : Slide($"S{Number}", $"Slajd {Number}", []);

/// <summary>Film na cały ekran; odtwarza go <see cref="Videos.IVideoPlayer"/>, gdy slajd jest na ekranie.</summary>
/// <param name="File">Pełna ścieżka pliku filmu.</param>
public sealed record VideoSlide(string File) : Slide("F", "Film", []);

/// <summary>Okno innego programu na cały ekran; pokazuje je <see cref="AppWindows.IAppWindowCapture"/>, gdy slajd jest na ekranie.</summary>
public sealed record AppWindowSlide(string Process, string? WindowTitle) : Slide("A", "Aplikacja", [])
{
    public AppWindows.AppWindowTarget Target => new(Process, WindowTitle);
}

/// <summary>Wers slajdu.</summary>
/// <param name="Text">Tekst do wyświetlenia (bez znaczników).</param>
/// <param name="Chords">Akordy (tylko dla operatora; nigdy na ekranie projekcji).</param>
/// <param name="Repeat">Liczba powtórzeń wersu/fragmentu, 1 = bez powtórzeń.</param>
/// <param name="TitleLength">Ile pierwszych znaków wersu powtarza tytuł pieśni (początek pierwszego slajdu) — rzutnik
/// pokazuje je w kolorze tytułu, resztę wersu zwykłym; 0 = wers bez tytułu.</param>
/// <param name="IsChorus">Wers refrenu dołączonego do zwrotki na tym samym slajdzie — kursywa.</param>
/// <param name="StartsPart">Pierwszy wers dołączonej części — odstęp nad nim.</param>
public sealed record SlideLine(
    string Text,
    string? Chords,
    int Repeat,
    bool RepeatStart,
    bool RepeatEnd,
    int TitleLength = 0,
    bool IsChorus = false,
    bool StartsPart = false)
{
    /// <summary>Początek wersu w kolorze tytułu.</summary>
    public string TitlePart => Text[..Math.Min(TitleLength, Text.Length)];

    /// <summary>Reszta wersu po tytule.</summary>
    public string Rest => Text[Math.Min(TitleLength, Text.Length)..];
}
