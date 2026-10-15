using Uwielbienia.Core.Presentation;

namespace Uwielbienia.Core.Videos;

/// <summary>
/// Film gra wtedy, gdy jego slajd jest na ekranie: wejście na film = odtwarzanie, zejście = koniec. Przerwany film
/// (zejście przed końcem) po powrocie wznawia się od tego miejsca; od początku gra tylko nowy albo obejrzany do końca.
/// Czarny ekran (B) pauzuje film, a powrót obrazu go wznawia — chyba że operator sam go zatrzymał.
/// </summary>
public sealed class VideoDirector
{
    private readonly IVideoPlayer _player;
    private string? _file;
    private string? _key;
    private bool _pausedByBlank;

    /// <summary>Gdzie przerwano film — osobno dla każdej pozycji planu (ten sam plik może być w planie dwa razy).</summary>
    private readonly Dictionary<string, TimeSpan> _resumeAt = new(StringComparer.Ordinal);

    public VideoDirector(ILiveStateSource live, IVideoPlayer player)
    {
        _player = player;
        live.StateChanged += (_, state) => Apply(state);
        Apply(live.State);
    }

    private void Apply(LiveState state)
    {
        if (state.Slide is not VideoSlide video || (state.IsBlank && _file != video.File))
        {
            // Inny slajd albo inny film pod czarnym ekranem — nowy zacznie się dopiero, gdy będzie widoczny.
            if (_file is not null)
                Leave();
            return;
        }

        if (state.IsBlank)
        {
            _player.IsHeld = true;
            if (_player.Status.IsPlaying)
            {
                _pausedByBlank = true;
                _player.Pause();
            }
            return;
        }

        _player.IsHeld = false;
        var key = $"{state.Item?.PlanItemId}|{video.File}";
        if (_file != video.File || _key != key)
        {
            if (_file is not null)
                Leave(stop: false); // nowy film i tak zastępuje poprzedni
            _file = video.File;
            _key = key;
            _pausedByBlank = false;
            _player.Open(video.File, _resumeAt.GetValueOrDefault(key));
        }
        else if (_pausedByBlank)
        {
            _pausedByBlank = false;
            _player.Resume();
        }
    }

    /// <summary>Zejście z filmu: zapamiętuje miejsce (chyba że film się skończył) i zatrzymuje odtwarzanie.</summary>
    private void Leave(bool stop = true)
    {
        if (_key is not null)
        {
            if (_player.Status is { HasEnded: false, Error: null, Position: var position } && position > TimeSpan.Zero)
                _resumeAt[_key] = position;
            else
                _resumeAt.Remove(_key);
        }
        _file = null;
        _key = null;
        _pausedByBlank = false;
        _player.IsHeld = false;
        if (stop)
            _player.Stop();
    }
}
