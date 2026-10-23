using Uwielbienia.Core.Presentation;

namespace Uwielbienia.Core.AppWindows;

/// <summary>
/// Okno programu jest pokazywane wtedy, gdy jego slajd jest na ekranie (także pod czarnym ekranem — po jego zdjęciu obraz
/// jest od razu); zejście ze slajdu kończy pokazywanie.
/// </summary>
public sealed class AppWindowDirector
{
    private readonly IAppWindowCapture _capture;

    public AppWindowDirector(ILiveStateSource live, IAppWindowCapture capture)
    {
        _capture = capture;
        live.StateChanged += (_, state) => Apply(state);
        Apply(live.State);
    }

    private void Apply(LiveState state)
    {
        if (state.Slide is AppWindowSlide slide)
            _capture.Show(slide.Target);
        else
            _capture.Stop();
    }
}
