namespace Uwielbienia.Core.Presentation;

/// <summary>
/// Czy efekty przejść rysują się płynnie. Wyłącza je (do końca działania programu) dopiero po kilku wolnych klatkach
/// <b>pod rząd w jednym przejściu</b> (<see cref="Begin"/>) — tak jest na słabym komputerze bez karty graficznej, gdzie
/// każda klatka jest wolna. Pojedyncze zacięcia (np. odśmiecanie pamięci) na szybkim komputerze nie wyłączają efektów:
/// każda szybka klatka zeruje licznik. Przejścia trwające naraz (rzutnik i kolumna „Na ekranie”) liczą osobno — inaczej
/// ta sama wolna chwila liczyłaby się kilka razy. Bezpieczne wątkowo.
/// </summary>
public sealed class SmoothnessGuard(TimeSpan slowFrame, int slowFramesInRow)
{
    private readonly TimeSpan _slowFrame = slowFrame;
    private readonly int _slowFramesInRow = slowFramesInRow;
    private int _disabled;

    public bool IsSmooth => Volatile.Read(ref _disabled) == 0;

    /// <summary>Wiadomo od razu, że efekty nie będą płynne (np. brak karty graficznej). <c>true</c> = to wywołanie je wyłączyło.</summary>
    public bool Disable() => Interlocked.Exchange(ref _disabled, 1) == 0;

    /// <summary>Nowe przejście z własnym licznikiem wolnych klatek.</summary>
    public Run Begin() => new(this);

    public sealed class Run(SmoothnessGuard guard)
    {
        private int _slowInRow;

        /// <summary>Odstęp od poprzedniej klatki tego przejścia. <c>true</c> = ta klatka wyłączyła efekty.</summary>
        public bool ReportFrame(TimeSpan elapsed)
        {
            if (elapsed <= guard._slowFrame)
            {
                _slowInRow = 0;
                return false;
            }
            return ++_slowInRow >= guard._slowFramesInRow && guard.Disable();
        }
    }
}
