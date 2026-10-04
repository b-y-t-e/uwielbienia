using Uwielbienia.Core.Songs;

namespace Uwielbienia.Core.Slideshows;

/// <summary>Zamienia plik prezentacji na obrazy slajdów (na Windows — przez PowerPointa).</summary>
public interface ISlideExporter
{
    /// <summary>Zapisuje widoczne slajdy w <paramref name="outDir"/> jako PNG (<c>slide001.png</c>…) o szerokości <paramref name="width"/>.</summary>
    Task ExportAsync(string presentation, string outDir, int width, IProgress<string> progress);
}

/// <summary>Stan slajdów prezentacji.</summary>
/// <param name="Slides">Pliki obrazów w kolejności pokazu; puste, dopóki slajdy nie są gotowe.</param>
/// <param name="Note">Dlaczego nie ma slajdów: przygotowywanie albo błąd; <c>null</c> = gotowe.</param>
public sealed record SlideshowState(IReadOnlyList<string> Slides, string? Note = null, bool IsFailed = false);

/// <param name="Folder">Folder prezentacji w katalogu aplikacji (nazwa, nie pełna ścieżka).</param>
public sealed record ImportedSlideshow(string Folder, string Title);

/// <summary>Postęp przygotowania slajdów, np. „Slajd 3 z 20…”.</summary>
public sealed record SlideshowProgress(string Folder, string Text);

/// <summary>
/// Prezentacje w planie: plik (PowerPoint) albo obrazy kopiowane do folderu aplikacji, żeby plan działał
/// także po wyjęciu pendrive'a. Slajdy prezentacji eksportowane raz (w tle) do <c>slajdy/</c> obok pliku.
/// </summary>
public interface ISlideshowLibrary
{
    Task<ImportedSlideshow> ImportAsync(IReadOnlyList<string> files);

    /// <summary>Niezależna kopia prezentacji (plik i gotowe slajdy) — zwraca nowy folder.</summary>
    Task<string> CopyAsync(string folder);

    /// <summary>Slajdy prezentacji; gdy jeszcze ich nie ma — zaczyna je przygotowywać w tle.</summary>
    SlideshowState Load(string folder);

    /// <summary>Stan bez rozpoczynania przygotowania (np. lista planów).</summary>
    SlideshowState Peek(string folder);

    /// <summary>Ponowna próba po błędzie (np. zamknięty PowerPoint).</summary>
    void Retry(string folder);

    /// <summary>Slajdy prezentacji są gotowe albo nie udało się ich przygotować (argument: folder).</summary>
    event EventHandler<string>? Changed;

    event EventHandler<SlideshowProgress>? Progress;
}

public sealed class SlideshowLibrary(string root, ISlideExporter exporter, IUiDispatcher dispatcher) : ISlideshowLibrary
{
    public const int ExportWidth = 1920;

    public static readonly IReadOnlyList<string> ImageExtensions = [".png", ".jpg", ".jpeg", ".bmp", ".webp"];

    public static readonly IReadOnlyList<string> PresentationExtensions = [".pptx", ".ppt", ".pps", ".ppsx", ".pptm", ".ppsm", ".odp"];

    private const string SlidesFolder = "slajdy";
    private const string CompleteMarker = ".complete";
    private const string PreparingNote = "Przygotowywanie slajdów…";

    // Stan przygotowania zmieniany tylko na wątku interfejsu (przez dispatcher).
    private readonly Dictionary<string, string> _preparing = [];
    private readonly Dictionary<string, string> _errors = [];
    private readonly SemaphoreSlim _exportLock = new(1, 1);

    public event EventHandler<string>? Changed;

    public event EventHandler<SlideshowProgress>? Progress;

    public static bool IsSupported(string file) => IsImage(file) || IsPresentation(file);

    public async Task<ImportedSlideshow> ImportAsync(IReadOnlyList<string> files)
    {
        // Prezentacja = jeden plik; obrazy — każdy to slajd (w kolejności nazw).
        var presentation = files.FirstOrDefault(IsPresentation);
        List<string> sources = presentation is not null ? [presentation] : SortNaturally(files.Where(IsImage));
        if (sources.Count == 0)
            throw new NotSupportedException("Wybierz prezentację (PowerPoint) albo obrazy.");

        var title = Path.GetFileNameWithoutExtension(sources[0]);
        var folder = $"{Guid.NewGuid().ToString("N")[..8]}-{LocalSongEditor.Slug(title)}";
        var directory = Path.Combine(root, folder);
        Directory.CreateDirectory(directory);
        await Task.Run(() =>
        {
            for (var i = 0; i < sources.Count; i++)
            {
                var name = presentation is not null
                    ? Path.GetFileName(sources[i])
                    : $"{i + 1:D3}{Path.GetExtension(sources[i]).ToLowerInvariant()}";
                File.Copy(sources[i], Path.Combine(directory, name));
            }
        }).ConfigureAwait(false);
        return new ImportedSlideshow(folder, title);
    }

    public async Task<string> CopyAsync(string folder)
    {
        // nowy początek nazwy, reszta (slug tytułu) bez zmian
        var dash = folder.IndexOf('-');
        var copy = Guid.NewGuid().ToString("N")[..8] + (dash >= 0 ? folder[dash..] : "-" + folder);
        var source = Path.Combine(root, folder);
        var target = Path.Combine(root, copy);
        await Task.Run(() => CopyDirectory(source, target)).ConfigureAwait(false);
        return copy;
    }

    /// <summary>Bez niedokończonego eksportu (<c>slajdy.tmp</c>) — kopia przygotuje slajdy sama.</summary>
    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.EnumerateFiles(source))
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
        foreach (var directory in Directory.EnumerateDirectories(source).Where(d => !d.EndsWith(".tmp", StringComparison.Ordinal)))
            CopyDirectory(directory, Path.Combine(target, Path.GetFileName(directory)));
    }

    public SlideshowState Load(string folder) => State(folder, prepare: true);

    public SlideshowState Peek(string folder) => State(folder, prepare: false);

    public void Retry(string folder)
    {
        if (_errors.Remove(folder))
            Changed?.Invoke(this, folder);
    }

    private SlideshowState State(string folder, bool prepare)
    {
        var directory = Path.Combine(root, folder);
        if (!Directory.Exists(directory))
            return new SlideshowState([], "Brak plików prezentacji", IsFailed: true);
        if (PresentationIn(directory) is not { } presentation)
            return Ready(ImagesIn(directory));

        var slides = Path.Combine(directory, SlidesFolder);
        if (File.Exists(Path.Combine(slides, CompleteMarker)))
            return Ready(ImagesIn(slides));
        if (_errors.TryGetValue(folder, out var error))
            return new SlideshowState([], error, IsFailed: true);
        if (_preparing.TryGetValue(folder, out var progress))
            return new SlideshowState([], progress);
        if (!prepare)
            return new SlideshowState([], PreparingNote);
        _preparing[folder] = PreparingNote;
        _ = ExportAsync(folder, presentation, slides);
        // eksport mógł już coś zgłosić (albo skończyć) — zwracamy aktualny stan
        return _preparing.TryGetValue(folder, out var started) ? new SlideshowState([], started) : State(folder, prepare: false);
    }

    private static SlideshowState Ready(List<string> images) =>
        images.Count > 0 ? new SlideshowState(images) : new SlideshowState([], "Brak slajdów", IsFailed: true);

    /// <summary>Eksport do folderu tymczasowego, potem znacznik i przeniesienie — przerwany eksport nie udaje gotowego.</summary>
    private async Task ExportAsync(string folder, string presentation, string slides)
    {
        string? error = null;
        await _exportLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var temp = slides + ".tmp";
            if (Directory.Exists(temp))
                Directory.Delete(temp, recursive: true);
            Directory.CreateDirectory(temp);
            await exporter.ExportAsync(presentation, temp, ExportWidth, new ReportProgress(text => OnUi(() =>
            {
                _preparing[folder] = text;
                Progress?.Invoke(this, new SlideshowProgress(folder, text));
            }))).ConfigureAwait(false);
            File.WriteAllText(Path.Combine(temp, CompleteMarker), "");
            if (Directory.Exists(slides))
                Directory.Delete(slides, recursive: true);
            Directory.Move(temp, slides);
        }
        catch (Exception ex)
        {
            error = ex.Message;
        }
        finally
        {
            _exportLock.Release();
        }

        OnUi(() =>
        {
            _preparing.Remove(folder);
            if (error is not null)
                _errors[folder] = error;
            Changed?.Invoke(this, folder);
        });
    }

    private void OnUi(Action action) => _ = dispatcher.InvokeAsync(() =>
    {
        action();
        return true;
    });

    private static bool IsImage(string file) => ImageExtensions.Contains(Path.GetExtension(file).ToLowerInvariant());

    private static bool IsPresentation(string file) => PresentationExtensions.Contains(Path.GetExtension(file).ToLowerInvariant());

    private static string? PresentationIn(string directory) => Directory.EnumerateFiles(directory).FirstOrDefault(IsPresentation);

    private static List<string> ImagesIn(string directory) => SortNaturally(Directory.EnumerateFiles(directory).Where(IsImage));

    private static List<string> SortNaturally(IEnumerable<string> files)
    {
        var list = files.ToList();
        list.Sort((a, b) => NaturalCompare(Path.GetFileName(a), Path.GetFileName(b)));
        return list;
    }

    /// <summary>Porównanie „naturalne”: slide2 &lt; slide10.</summary>
    internal static int NaturalCompare(string a, string b)
    {
        int i = 0, j = 0;
        while (i < a.Length && j < b.Length)
        {
            if (char.IsDigit(a[i]) && char.IsDigit(b[j]))
            {
                int si = i, sj = j;
                while (i < a.Length && char.IsDigit(a[i]))
                    i++;
                while (j < b.Length && char.IsDigit(b[j]))
                    j++;
                var na = a[si..i].TrimStart('0');
                var nb = b[sj..j].TrimStart('0');
                var c = na.Length.CompareTo(nb.Length);
                if (c == 0)
                    c = string.CompareOrdinal(na, nb);
                if (c != 0)
                    return c;
            }
            else
            {
                var c = char.ToUpperInvariant(a[i]).CompareTo(char.ToUpperInvariant(b[j]));
                if (c != 0)
                    return c;
                i++;
                j++;
            }
        }
        return (a.Length - i).CompareTo(b.Length - j);
    }

    private sealed class ReportProgress(Action<string> report) : IProgress<string>
    {
        public void Report(string value) => report(value);
    }
}
