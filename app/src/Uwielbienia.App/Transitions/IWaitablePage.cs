namespace Uwielbienia.App.Transitions;

/// <summary>
/// Strona, której treść wczytuje się w tle (np. obraz slajdu). Przejście czeka na nią przed zrzutem nowej strony —
/// inaczej efekt prowadziłby do pustego tła, a obraz pojawiałby się dopiero po nim.
/// </summary>
public interface IWaitablePage
{
    Task Ready { get; }
}
