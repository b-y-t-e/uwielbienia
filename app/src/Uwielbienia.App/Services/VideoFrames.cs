using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace Uwielbienia.App.Services;

/// <summary>
/// Bieżąca klatka filmu — wspólna dla rzutnika i podglądu operatora. Odtwarzacz dekoduje wprost do
/// <see cref="Bitmap"/>, a <see cref="Views.VideoSurface"/> rysuje ją jak zwykły obraz, więc film
/// podlega „Dopasuj obraz” i przenikaniu slajdów (natywne okno odtwarzacza by ich nie znało).
/// </summary>
public sealed class VideoFrames
{
    /// <summary>Klatka; <c>null</c> = nic nie gra (czarny ekran).</summary>
    public WriteableBitmap? Bitmap { get; private set; }

    /// <summary>Nowa klatka albo nowa bitmapa — z wątku dekodera.</summary>
    public event EventHandler? FrameReady;

    internal void Reset(WriteableBitmap? bitmap)
    {
        var previous = Bitmap;
        Bitmap = bitmap;
        NotifyFrame();
        // Każde odtworzenie ma własną bitmapę (~8 MB poza GC) — poprzednią zwalniamy na wątku UI, po którym nikt
        // już jej nie pobierze; rysowanie w toku trzyma własną referencję (bitmapy Avalonii są zliczane).
        if (previous is not null && !ReferenceEquals(previous, bitmap))
            Dispatcher.UIThread.Post(previous.Dispose);
    }

    internal void NotifyFrame() => FrameReady?.Invoke(this, EventArgs.Empty);
}
