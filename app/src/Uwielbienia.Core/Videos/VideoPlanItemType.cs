using System.Collections.Concurrent;
using Uwielbienia.Core.Plans;
using Uwielbienia.Core.Presentation;

namespace Uwielbienia.Core.Videos;

/// <summary>
/// Rodzaj pozycji „film”: jeden slajd <see cref="VideoSlide"/> z plikiem wskazanym na dysku. Brakujący plik = uwaga
/// w planie i pozycja bez slajdów (ekran ją pomija). Pliki sprawdzane są co kilka sekund — włożony pendrive albo
/// przywrócony plik przebudowuje plan (<see cref="Changed"/>), a usunięty daje uwagę.
/// </summary>
public sealed class VideoPlanItemType : IPlanItemType
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(5);

    private readonly IUiDispatcher? _dispatcher;
    private readonly ConcurrentDictionary<string, bool> _exists = new(StringComparer.OrdinalIgnoreCase);
    private readonly Timer? _timer;
    private int _checking;

    /// <param name="dispatcher">Bez niego (testy) pliki nie są sprawdzane w tle — tylko przez <see cref="CheckFiles"/>.</param>
    public VideoPlanItemType(IUiDispatcher? dispatcher = null)
    {
        _dispatcher = dispatcher;
        if (dispatcher is not null)
            _timer = new Timer(_ => CheckFiles(), null, CheckInterval, CheckInterval);
    }

    /// <summary>Pliki, które można dodać jako film (odtwarza je VLC — także inne formaty, gdyby ktoś je wskazał).</summary>
    public static readonly IReadOnlyList<string> Extensions =
    [
        ".mp4", ".m4v", ".mkv", ".avi", ".mov", ".wmv", ".webm", ".mpg", ".mpeg", ".flv", ".ts", ".m2ts", ".mts", ".3gp", ".ogv", ".vob",
    ];

    public static bool IsSupported(string file) => Extensions.Contains(Path.GetExtension(file).ToLowerInvariant());

    public event EventHandler? Changed;

    /// <summary>Czy któryś znany plik pojawił się albo zniknął — wtedy <see cref="Changed"/> (na wątku UI).</summary>
    public void CheckFiles()
    {
        // Plik na niedostępnym udziale sieciowym potrafi blokować sprawdzenie na długo — kolejne nie startuje,
        // dopóki trwa poprzednie (wywołania timera nie nakładają się).
        if (Interlocked.Exchange(ref _checking, 1) == 1)
            return;
        try
        {
            CheckChangedFiles();
        }
        finally
        {
            Volatile.Write(ref _checking, 0);
        }
    }

    private void CheckChangedFiles()
    {
        var changed = false;
        foreach (var (file, existed) in _exists)
            if (File.Exists(file) != existed)
            {
                _exists[file] = !existed;
                changed = true;
            }
        if (!changed)
            return;
        if (_dispatcher is null)
            Changed?.Invoke(this, EventArgs.Empty);
        else
            _ = _dispatcher.InvokeAsync(() =>
            {
                Changed?.Invoke(this, EventArgs.Empty);
                return true;
            });
    }

    public bool Handles(PlanItem item) => item is VideoPlanItem;

    public PlanItemInfo? Describe(PlanItem item) =>
        item is VideoPlanItem video ? new PlanItemInfo(video.Title, null, PlanItemKind.Video, MissingNote(video)) : null;

    public LiveItem? Present(PlanItem item)
    {
        if (item is not VideoPlanItem video)
            return null;
        var note = MissingNote(video);
        IReadOnlyList<Slide> slides = note is null ? [new VideoSlide(video.File)] : [];
        return new LiveItem(item.Id, video.File, null, video.Title, slides, ShowTitle: false, note);
    }

    /// <summary>
    /// Plan przebudowuje się na wątku UI przy każdej zmianie — znany plik bierze wynik zapamiętany przez sprawdzanie
    /// w tle (<see cref="CheckFiles"/>), żeby film na niedostępnym udziale sieciowym nie blokował okna za każdym razem.
    /// Bez sprawdzania w tle (testy) — zawsze aktualnie.
    /// </summary>
    private string? MissingNote(VideoPlanItem video)
    {
        if (_timer is null || !_exists.TryGetValue(video.File, out var exists))
            _exists[video.File] = exists = File.Exists(video.File);
        return exists ? null : $"Nie ma pliku: {video.File}";
    }
}
