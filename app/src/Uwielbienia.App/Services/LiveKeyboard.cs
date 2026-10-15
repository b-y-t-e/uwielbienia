using Avalonia.Input;
using Uwielbienia.Core.Presentation;
using Uwielbienia.Core.Videos;

namespace Uwielbienia.App.Services;

/// <summary>
/// Jedna mapa klawiszy dla okna operatora i okna projekcji. Pilot do prezentacji wysyła PageDown / PageUp (krótko),
/// a przytrzymany — inny klawisz: <c>b</c> (do przodu) i na zmianę <c>Shift+F5</c> / <c>Escape</c> (do tyłu);
/// strzałki działają jak pilot.
/// Krótko = slajd, przy filmie ±15 s; długo do przodu = czarny ekran, przy filmie pauza; długo do tyłu = włącz
/// projekcję, przy filmie podpowiedź i wyjście z filmu (<see cref="FilmRemote"/>). Ctrl+PageDown / Ctrl+PageUp =
/// następna / poprzednia pozycja planu. Spacja przy filmie = pauza.
/// </summary>
public static class LiveKeyboard
{
    public static bool Handle(KeyEventArgs e, ILiveControl control, IProjectionController projection, FilmRemote film)
    {
        if (e.KeyModifiers == KeyModifiers.Control && e.Key is Key.PageDown or Key.PageUp)
        {
            film.CancelPrompt();
            if (e.Key == Key.PageDown)
                control.NextItem();
            else
                control.PreviousItem();
            return true;
        }
        if (e.KeyModifiers is not (KeyModifiers.None or KeyModifiers.Shift))
            return false;

        switch (e.Key)
        {
            // pilot: przytrzymane „do tyłu” wysyła na zmianę Shift+F5 i Escape (start / koniec pokazu) — przy filmie
            // oba to samo; bez filmu Escape zostaje „zamknij” (okna, tryb jednego ekranu)
            case Key.Escape when e.KeyModifiers == KeyModifiers.None:
                return film.HoldBack();
            case Key.F5 when e.KeyModifiers == KeyModifiers.Shift:
                if (!film.HoldBack() && projection.Mode == ProjectionMode.Off)
                    projection.Toggle(); // tylko włącza — przytrzymany pilot nie może zgasić projekcji
                return true;
            // pilot: przytrzymane „do przodu”
            case Key.B:
                if (!film.HoldForward())
                    control.ToggleBlank();
                return true;
            case Key.Right or Key.Down or Key.PageDown:
                if (!film.Forward())
                    control.Next();
                return true;
            case Key.Back or Key.Left or Key.Up or Key.PageUp:
                if (!film.Back())
                    control.Previous();
                return true;
        }

        film.CancelPrompt();
        switch (e.Key)
        {
            case Key.Space:
                if (!film.TogglePause())
                    control.Next();
                return true;
            case Key.OemPeriod:
                control.ToggleBlank();
                return true;
            case Key.F5:
                projection.Toggle();
                return true;
            case >= Key.D1 and <= Key.D9 when e.KeyModifiers == KeyModifiers.Shift:
                control.GoToSlide(e.Key - Key.D1);
                return true;
            default:
                return false;
        }
    }

    /// <summary>Znak, od którego zaczyna się szybkie wyszukiwanie (litera lub cyfra), albo <c>null</c>.</summary>
    public static string? SearchStart(TextInputEventArgs e) =>
        e.Text is { Length: > 0 } text && char.IsLetterOrDigit(text[0]) ? text : null;
}
