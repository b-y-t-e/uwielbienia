using Uwielbienia.Core.Presentation;

namespace Uwielbienia.Core.Videos;

/// <summary>
/// Odtwarzacz filmu na ekranie projekcji. Jeden na aplikację: obraz rysuje rzutnik (i podgląd operatora),
/// dźwięk idzie na domyślne urządzenie. Kiedy grać, decyduje <see cref="VideoDirector"/>.
/// </summary>
public interface IVideoPlayer
{
    VideoStatus Status { get; }

    /// <summary>Dlaczego filmów nie da się odtwarzać (np. brak VLC na Linuksie); <c>null</c> = można.</summary>
    string? Unavailable { get; }

    /// <summary>
    /// Film wstrzymany przez ekran (czarny ekran — <see cref="VideoDirector"/>): nic go nie wznowi (spacja, kliknięcie,
    /// telefon, przewinięcie po końcu), bo w sali nie może grać dźwięk filmu, którego nie widać.
    /// </summary>
    bool IsHeld { get; set; }

    /// <summary>
    /// Przesunięcie obrazu względem dźwięku: dodatnie = obraz później (np. dźwięk z nagłośnienia wyprzedza obraz
    /// rzutnika). Stałe dla wszystkich filmów; zmiana zgłasza <see cref="StatusChanged"/>.
    /// </summary>
    TimeSpan PictureOffset { get; set; }

    /// <summary>Zmienił się stan (start, pauza, koniec, czas, błąd) — zawsze na wątku interfejsu.</summary>
    event EventHandler? StatusChanged;

    /// <summary>Odtwarza plik od początku albo od <paramref name="start"/> (powrót do przerwanego filmu).</summary>
    void Open(string file, TimeSpan start = default);

    /// <summary>Kończy odtwarzanie i czyści obraz.</summary>
    void Stop();

    void Pause();

    /// <summary>Wznawia; po końcu filmu — od początku.</summary>
    void Resume();

    void Seek(TimeSpan position);
}

/// <summary>Stan odtwarzania.</summary>
/// <param name="File">Otwarty plik; <c>null</c> = nic nie gra.</param>
/// <param name="Error">Film się nie odtwarza (brak pliku, brak VLC, nieobsługiwany format).</param>
public sealed record VideoStatus(
    string? File,
    bool IsPlaying,
    TimeSpan Position,
    TimeSpan Duration,
    bool HasEnded = false,
    string? Error = null)
{
    public static readonly VideoStatus Stopped = new(null, false, TimeSpan.Zero, TimeSpan.Zero);
}

public static class VideoPlayerExtensions
{
    /// <summary>Na ekranie jest film, który da się odtworzyć — spacja i kliknięcie w obraz to wtedy pauza / wznowienie.</summary>
    public static bool HasVideo(this IVideoPlayer player) => player.Status is { File: not null, Error: null };

    /// <summary>Na ekranie (<paramref name="state"/>) jest film, który da się odtworzyć.</summary>
    public static bool IsOnScreen(this IVideoPlayer player, LiveState state) => state.Slide is VideoSlide && player.HasVideo();

    /// <summary>Przesunięcie obrazu względem dźwięku w pełnych ms (ustawienia, telefon, napisy).</summary>
    public static int PictureOffsetMs(this IVideoPlayer player) => (int)Math.Round(player.PictureOffset.TotalMilliseconds);

    /// <summary>Krok przesunięcia obrazu względem dźwięku (przyciski, <c>[</c> / <c>]</c>, telefon).</summary>
    public static readonly TimeSpan PictureStep = TimeSpan.FromMilliseconds(100);

    /// <summary>Największe przesunięcie obrazu w każdą stronę.</summary>
    public static readonly TimeSpan MaxPictureOffset = TimeSpan.FromSeconds(5);

    /// <summary>Obraz później (dodatnie) albo wcześniej (ujemne) względem dźwięku, w granicach ±5 s.</summary>
    public static void ShiftPicture(this IVideoPlayer player, TimeSpan by)
    {
        var offset = player.PictureOffset + by;
        player.PictureOffset = offset > MaxPictureOffset ? MaxPictureOffset : offset < -MaxPictureOffset ? -MaxPictureOffset : offset;
    }

    /// <summary>Przewinięcie o <paramref name="by"/> (np. ±15 s), w granicach filmu.</summary>
    public static void Skip(this IVideoPlayer player, TimeSpan by)
    {
        if (!player.HasVideo())
            return;
        var status = player.Status;
        var to = status.Position + by;
        if (status.Duration > TimeSpan.Zero && to > status.Duration)
            to = status.Duration;
        player.Seek(to < TimeSpan.Zero ? TimeSpan.Zero : to);
    }

    /// <summary>
    /// Jedyna reguła „przy filmie spacja / kliknięcie = pauza albo wznowienie” (klawiatura, kliknięcie w projekcję
    /// i w podgląd, przycisk, telefon): działa tylko, gdy na ekranie jest film, który da się odtworzyć.
    /// </summary>
    /// <returns><c>false</c> = filmu nie ma — spacja / kliknięcie robią wtedy to co zwykle (np. dalej).</returns>
    public static bool TogglePause(this IVideoPlayer player)
    {
        if (!player.HasVideo())
            return false;
        if (player.IsHeld)
            return true; // czarny ekran — film czeka na powrót obrazu
        if (player.Status.IsPlaying)
            player.Pause();
        else
            player.Resume();
        return true;
    }
}
