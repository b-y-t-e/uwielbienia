namespace Uwielbienia.Core.Presentation;

/// <summary>
/// Czy efekty przejść rysują się płynnie. Wyłącza je (do końca działania programu) dopiero po kilku wolnych klatkach
/// <b>pod rząd</b> — tak jest na słabym komputerze bez karty graficznej, gdzie każda klatka jest wolna. Pojedyncze
/// zacięcia (np. odśmiecanie pamięci) na szybkim komputerze nie wyłączają efektów: każda szybka klatka zeruje licznik.
/// Bezpieczne wątkowo (klatki zgłasza wątek renderowania).
/// </summary>
public sealed class SmoothnessGuard(TimeSpan slowFrame, int slowFramesInRow)
{
    private int _slowInRow;
    private int _disabled;

    public bool IsSmooth => Volatile.Read(ref _disabled) == 0;

    /// <summary>Wiadomo od razu, że efekty nie będą płynne (np. brak karty graficznej).</summary>
    public void Disable() => Interlocked.Exchange(ref _disabled, 1);

    public void ReportFrame(TimeSpan elapsed)
    {
        if (elapsed <= slowFrame)
            Interlocked.Exchange(ref _slowInRow, 0);
        else if (Interlocked.Increment(ref _slowInRow) >= slowFramesInRow)
            Interlocked.Exchange(ref _disabled, 1);
    }
}
