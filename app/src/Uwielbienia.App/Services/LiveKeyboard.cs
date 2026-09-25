using Avalonia.Input;
using Uwielbienia.Core.Presentation;

namespace Uwielbienia.App.Services;

/// <summary>
/// Jedna mapa klawiszy dla okna operatora i okna projekcji. Piloty do prezentacji wysyłają PageUp/PageDown,
/// więc działają bez konfiguracji.
/// </summary>
public static class LiveKeyboard
{
    public static bool Handle(KeyEventArgs e, ILiveControl control, IProjectionController projection)
    {
        if (e.KeyModifiers is not (KeyModifiers.None or KeyModifiers.Shift))
            return false;

        switch (e.Key)
        {
            case Key.Space or Key.Right or Key.Down or Key.PageDown:
                control.Next();
                return true;
            case Key.Back or Key.Left or Key.Up or Key.PageUp:
                control.Previous();
                return true;
            case Key.B or Key.OemPeriod:
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
