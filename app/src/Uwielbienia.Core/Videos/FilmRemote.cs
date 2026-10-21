using Uwielbienia.Core.Presentation;

namespace Uwielbienia.Core.Videos;

/// <summary>
/// Pilot (i klawiatura) przy filmie na ekranie. Pilot ma tylko cztery sygnały — krótko i długo „do przodu” / „do tyłu”
/// (długie przytrzymanie wysyła inny klawisz, np. <c>b</c> i <c>Shift+F5</c>):
/// krótko = ±15 s (szybkie klikanie tylko przewija, a z początku / końca filmu zmienia slajd — <see cref="Edge"/>), długo „do przodu” = pauza / wznowienie, długo „do tyłu” = podpowiedź
/// na ekranie, a po niej długo „do tyłu” = poprzednia pozycja planu, długo „do przodu” = następna. Każde inne naciśnięcie
/// albo upływ <see cref="PromptTime"/> zamyka podpowiedź. Bez filmu na ekranie metody zwracają <c>false</c> — wtedy
/// klawisz robi to co zwykle (slajd, projekcja…).
/// </summary>
public sealed class FilmRemote
{
    public static readonly TimeSpan Step = TimeSpan.FromSeconds(15);

    /// <summary>Jak długo czeka podpowiedź na drugie długie naciśnięcie.</summary>
    public static readonly TimeSpan PromptTime = TimeSpan.FromSeconds(4);

    private readonly ILiveControl _control;
    private readonly ILiveStateSource _live;
    private readonly IVideoPlayer _player;
    private readonly TimeProvider _time;
    private DateTimeOffset? _promptSince;

    public FilmRemote(ILiveControl control, ILiveStateSource live, IVideoPlayer player, TimeProvider? time = null)
    {
        _control = control;
        _live = live;
        _player = player;
        _time = time ?? TimeProvider.System;
        // zejście z filmu (np. przyciskiem w oknie) zamyka podpowiedź
        live.StateChanged += (_, state) =>
        {
            if (state.Slide is not VideoSlide)
                CancelPrompt();
        };
    }

    /// <summary>Podpowiedź „długo do tyłu: poprzednia, długo do przodu: następna” — na rzutniku i w oknie operatora.</summary>
    public bool IsPrompting => _promptSince is { } since && _time.GetUtcNow() - since < PromptTime;

    /// <summary>Podpowiedź pojawiła się albo zniknęła.</summary>
    public event EventHandler? PromptChanged;

    /// <summary>Przewinięto albo zapauzowano film — rzutnik pokazuje na chwilę pasek postępu (nie pod czarnym ekranem).</summary>
    public event EventHandler? FilmChanged;

    /// <summary>Na ekranie jest film, który da się odtworzyć.</summary>
    private bool OnFilm => _player.IsOnScreen(_live.State);

    /// <summary>
    /// Tyle od początku / końca filmu krótko „do tyłu” / „do przodu” zmienia slajd zamiast przewijać — z 0:00 nie ma
    /// dokąd cofnąć, a z końca dokąd przewinąć.
    /// </summary>
    public static readonly TimeSpan Edge = TimeSpan.FromSeconds(2);

    /// <summary>Krótko „do przodu” (PageDown, →, ↓): +15 s, a przy końcu filmu (<see cref="Edge"/>) <c>false</c> — następny slajd.</summary>
    public bool Forward() => AtEnd() ? LeaveToSlide() : Skip(Step);

    /// <summary>Krótko „do tyłu” (PageUp, ←, ↑, Backspace): −15 s, a na początku filmu <c>false</c> — poprzedni slajd.</summary>
    public bool Back() => AtStart() ? LeaveToSlide() : Skip(-Step);

    private bool AtStart() => OnFilm && _player.Status.Position <= Edge;

    private bool AtEnd() =>
        OnFilm && _player.Status is var status &&
        (status.HasEnded || status.Duration > TimeSpan.Zero && status.Duration - status.Position <= Edge);

    /// <summary>Klawisz zmienia slajd jak zwykle (<c>false</c> dla wołającego).</summary>
    private bool LeaveToSlide()
    {
        CancelPrompt();
        return false;
    }

    /// <summary>Długo „do przodu” (pilot: <c>b</c>): pauza / wznowienie, a po podpowiedzi — następna pozycja planu.</summary>
    public bool HoldForward()
    {
        if (!OnFilm)
            return false;
        if (TakePrompt())
            _control.NextItem();
        else
            TogglePauseAndNotify();
        return true;
    }

    /// <summary>Spacja albo kliknięcie w film: pauza / wznowienie; <c>false</c> = filmu nie ma.</summary>
    public bool TogglePause()
    {
        CancelPrompt();
        return TogglePauseAndNotify();
    }

    /// <summary>Pod czarnym ekranem film stoi bez zmian (<see cref="IVideoPlayer.IsHeld"/>) — wtedy bez sygnału.</summary>
    private bool TogglePauseAndNotify()
    {
        var held = _player.IsHeld;
        if (!_player.TogglePause())
            return false;
        if (!held)
            FilmChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>Długo „do tyłu” (pilot: <c>Shift+F5</c>): podpowiedź, a przy podpowiedzi — poprzednia pozycja planu.</summary>
    public bool HoldBack()
    {
        if (!OnFilm)
            return false;
        if (TakePrompt())
        {
            _control.PreviousItem();
            return true;
        }
        _promptSince = _time.GetUtcNow();
        PromptChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>Inny klawisz albo upływ czasu — podpowiedź znika bez działania.</summary>
    public void CancelPrompt()
    {
        if (_promptSince is null)
            return;
        _promptSince = null;
        PromptChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary><c>[</c> / <c>]</c>: obraz o 100 ms wcześniej / później względem dźwięku; <c>false</c> = filmu nie ma.</summary>
    public bool ShiftPicture(TimeSpan by)
    {
        CancelPrompt();
        if (!OnFilm)
            return false;
        _player.ShiftPicture(by);
        if (!_player.IsHeld)
            FilmChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private bool Skip(TimeSpan by)
    {
        CancelPrompt();
        if (!OnFilm)
            return false;
        _player.Skip(by);
        // pod czarnym ekranem przewija po cichu — sala tego nie widzi, więc i pasek się nie pokaże
        if (!_player.IsHeld)
            FilmChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private bool TakePrompt()
    {
        var prompting = IsPrompting;
        CancelPrompt();
        return prompting;
    }
}
