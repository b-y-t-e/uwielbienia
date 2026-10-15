using Uwielbienia.Core.Plans;
using Uwielbienia.Core.Videos;

namespace Uwielbienia.App.ViewModels;

/// <summary>
/// Kolumna „Pieśń” dla filmu: tytuł i wskazany plik (film nie jest kopiowany) oraz to, co go zatrzyma — brak pliku
/// albo odtwarzacza (np. VLC na Linuksie) — zanim operator wyśle go na ekran.
/// </summary>
public sealed class VideoDetailsViewModel(VideoPlanItem item, string? note, IVideoPlayer player)
{
    public VideoPlanItem Item { get; } = item;

    public string Title => Item.Title;

    public string File => Item.File;

    /// <summary>Uwaga z planu (<c>VideoPlanItemType</c>: brak pliku) — jedna reguła dla planu i tej kolumny.</summary>
    public string? Note { get; } = note;

    public bool IsMissing => Note is not null;

    /// <summary>Brak odtwarzacza filmów (sprawdzane przy zaznaczeniu filmu w planie).</summary>
    public string? Unavailable { get; } = player.Unavailable;
}
