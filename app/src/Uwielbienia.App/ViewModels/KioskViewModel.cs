using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Animation;
using Uwielbienia.App.Services;
using Uwielbienia.App.Transitions;
using Uwielbienia.Core.Kiosks;
using Uwielbienia.Core.Plans;
using Uwielbienia.Core.Presentation;
using Uwielbienia.Core.Slideshows;

namespace Uwielbienia.App.ViewModels;

/// <summary>
/// Kolumna „Pieśń” dla kiosku: nazwa, co ile sekund zmiana i elementy (obraz, prezentacja, napis, obraz z napisem).
/// Każda zmiana zapisuje się od razu w planie, a kiosk na ekranie zmienia się na żywo.
/// </summary>
public sealed partial class KioskViewModel : ObservableObject, IDisposable
{
    private readonly ActivePlan _plan;
    private readonly ISlideshowLibrary _library;
    private readonly bool _loaded;

    public KioskViewModel(KioskPlanItem item, ActivePlan plan, ISlideshowLibrary library)
    {
        Item = item;
        _plan = plan;
        _library = library;
        Title = item.Title;
        Seconds = KioskPlanItemType.Seconds(item);
        SlowZoom = item.SlowZoom;
        foreach (var entry in item.Entries)
            Entries.Add(CreateEntry(entry));
        foreach (var transition in SlideTransitions.All)
        {
            var option = new TransitionOptionViewModel(transition, item.ChosenTransitions.Contains(transition));
            option.PropertyChanged += (_, _) => Save();
            Transitions.Add(option);
        }
        library.Changed += OnLibraryChanged;
        _loaded = true;
    }

    public KioskPlanItem Item { get; private set; }

    public ObservableCollection<KioskEntryViewModel> Entries { get; } = [];

    public bool IsEmpty => Entries.Count == 0;

    /// <summary>Efekty przejścia do wyboru: żaden — przenikanie, jeden — zawsze ten, kilka — losowo jeden z nich.</summary>
    public ObservableCollection<TransitionOptionViewModel> Transitions { get; } = [];

    /// <summary>Powolny zoom zdjęć.</summary>
    [ObservableProperty]
    public partial bool SlowZoom { get; set; }

    partial void OnSlowZoomChanged(bool value) => Save();

    [ObservableProperty]
    public partial string Title { get; set; }

    [ObservableProperty]
    public partial decimal? Seconds { get; set; }

    /// <summary>Nie udało się dodać obrazów albo prezentacji.</summary>
    [ObservableProperty]
    public partial string? Error { get; private set; }

    partial void OnTitleChanged(string value) => Save();

    partial void OnSecondsChanged(decimal? value) => Save();

    [RelayCommand]
    private void AddText()
    {
        Entries.Add(CreateEntry(new KioskEntry(Text: "")));
        Save();
    }

    /// <summary>„Obraz”: każdy obraz osobnym elementem (kopie w folderze aplikacji, jak prezentacje).</summary>
    public async Task AddImagesAsync(IReadOnlyList<string> files) =>
        await AddAsync(async () => (await _library.ImportEachAsync(files)).Select(i => i.Folder).ToList());

    /// <summary>„Prezentacja”: jej slajdy (przygotowywane w tle) to kolejne elementy kiosku.</summary>
    public async Task AddPresentationAsync(string file) =>
        await AddAsync(async () => [(await _library.ImportAsync([file])).Folder]);

    private async Task AddAsync(Func<Task<List<string>>> import)
    {
        try
        {
            var folders = await import();
            if (_disposed)
            {
                // Kopiowanie trwało, a operator w tym czasie zaznaczył coś innego — nowe elementy trafiają do aktualnej
                // wersji kiosku w planie (kolumna „Pieśń” wczyta ją od nowa).
                _plan.Update(p => p.Items.FirstOrDefault(i => i.Id == Item.Id) is KioskPlanItem current
                    ? p.Replace(current with { Entries = [.. current.Entries, .. folders.Select(f => new KioskEntry(f))] })
                    : p);
                return;
            }
            foreach (var folder in folders)
                Entries.Add(CreateEntry(new KioskEntry(folder)));
            Error = null;
            Save();
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or UnauthorizedAccessException or ArgumentException)
        {
            Error = ex.Message;
        }
    }

    [RelayCommand]
    private void Remove(KioskEntryViewModel entry)
    {
        if (Entries.Remove(entry))
            Save();
    }

    public void Move(KioskEntryViewModel entry, int newIndex)
    {
        var index = Entries.IndexOf(entry);
        if (index < 0 || newIndex == index)
            return;
        Entries.Move(index, Math.Clamp(newIndex, 0, Entries.Count - 1));
        Save();
    }

    private bool _disposed;

    public void Dispose()
    {
        _disposed = true;
        _library.Changed -= OnLibraryChanged;
    }

    private KioskEntryViewModel CreateEntry(KioskEntry entry)
    {
        var vm = new KioskEntryViewModel(entry, _library);
        vm.Edited += (_, _) => Save();
        return vm;
    }

    private void Save()
    {
        OnPropertyChanged(nameof(IsEmpty));
        // pola ustawiane w konstruktorze nie są zmianą; zamknięty model nie nadpisuje planu
        if (!_loaded || _disposed)
            return;
        var title = string.IsNullOrWhiteSpace(Title) ? "Kiosk" : Title.Trim();
        var seconds = Math.Clamp((int)(Seconds ?? KioskPlanItem.DefaultSeconds), KioskPlanItem.MinSeconds, KioskPlanItem.MaxSeconds);
        var transitions = Transitions.Where(t => t.IsSelected).Select(t => t.Transition).ToList();
        var updated = Item with
        {
            Title = title,
            Seconds = seconds,
            Entries = Entries.Select(e => e.Entry).ToList(),
            Transitions = transitions,
            SlowZoom = SlowZoom,
        };
        if (updated.Title == Item.Title && updated.Seconds == Item.Seconds && updated.SlowZoom == Item.SlowZoom
            && updated.Entries.SequenceEqual(Item.Entries) && transitions.SequenceEqual(Item.ChosenTransitions))
            return;
        Item = updated;
        _plan.Update(p => p.Replace(updated));
    }

    private void OnLibraryChanged(object? sender, string folder)
    {
        foreach (var entry in Entries.Where(e => e.Entry.Folder == folder))
            entry.Refresh();
    }
}

/// <summary>Efekt przejścia do zaznaczenia w kiosku.</summary>
public sealed partial class TransitionOptionViewModel(SlideTransition transition, bool selected) : ObservableObject
{
    public SlideTransition Transition { get; } = transition;

    public string Name { get; } = SlideTransitions.Name(transition);

    [ObservableProperty]
    public partial bool IsSelected { get; set; } = selected;
}

/// <summary>Element kiosku w kolumnie „Pieśń”: miniatura obrazu (albo liczba slajdów prezentacji), tytuł i tekst napisu.</summary>
public sealed partial class KioskEntryViewModel : ObservableObject, IReorderableItem
{
    private readonly ISlideshowLibrary _library;
    private readonly bool _loaded;

    public KioskEntryViewModel(KioskEntry entry, ISlideshowLibrary library)
    {
        _library = library;
        Entry = entry;
        Title = entry.Title ?? "";
        Text = entry.Text ?? "";
        Refresh();
        _loaded = true;
    }

    public KioskEntry Entry { get; private set; }

    /// <summary>Zmieniony tytuł albo tekst — kiosk zapisuje się w planie.</summary>
    public event EventHandler? Edited;

    public bool HasImage => Entry.Folder is not null;

    public string TextHint => HasImage ? "Napis przy obrazie (opcjonalnie)" : "Napis";

    [ObservableProperty]
    public partial string Title { get; set; }

    [ObservableProperty]
    public partial string Text { get; set; }

    [ObservableProperty]
    public partial Task<Bitmap?>? Thumbnail { get; private set; }

    /// <summary>Prezentacja: liczba slajdów; przygotowywanie albo błąd.</summary>
    [ObservableProperty]
    public partial string? Note { get; private set; }

    [ObservableProperty]
    public partial bool IsFailed { get; private set; }

    [ObservableProperty]
    public partial bool IsDragged { get; set; }

    [ObservableProperty]
    public partial bool IsDropBefore { get; set; }

    [ObservableProperty]
    public partial bool IsDropAfter { get; set; }

    partial void OnTitleChanged(string value) => Edit();

    partial void OnTextChanged(string value) => Edit();

    private void Edit()
    {
        // pola ustawiane w konstruktorze nie są zmianą
        if (!_loaded)
            return;
        var updated = Entry with { Title = Title.Length == 0 ? null : Title, Text = Text.Length == 0 ? null : Text };
        if (updated == Entry)
            return;
        Entry = updated;
        Edited?.Invoke(this, EventArgs.Empty);
    }

    public void Refresh()
    {
        if (Entry.Folder is not { } folder)
            return;
        var state = _library.Peek(folder);
        Thumbnail = state.Slides.Count > 0 ? SlideImages.LoadThumbnailAsync(state.Slides[0]) : null;
        IsFailed = state.IsFailed;
        Note = state.Note ?? (state.Slides.Count > 1 ? Slides(state.Slides.Count) : null);
    }

    private static string Slides(int n) =>
        n % 10 is >= 2 and <= 4 && n % 100 is < 12 or > 14 ? $"{n} slajdy — każdy osobno" : $"{n} slajdów — każdy osobno";
}

/// <summary>
/// Kiosk na rzutniku i w kolumnie „Na ekranie”. Strona równa poprzedniej, dopóki na ekranie jest ten sam kiosk — elementy
/// przenikają się w niej same (<see cref="KioskPlayback.Frame"/>), bez ponownego przejścia całej strony.
/// </summary>
public sealed record ProjectedKiosk(KioskSlide Kiosk, KioskPlayback Playback) : ProjectedPage(Kiosk, null)
{
    // Równa każdej innej stronie tego kiosku — także po edycji treści na żywo (inny KioskSlide): strona nie przenika się
    // i nie buduje od nowa (zoom zdjęcia nie skacze); zmiany elementów przychodzą przez Playback.
    public bool Equals(ProjectedKiosk? other) => other is not null && ReferenceEquals(Playback, other.Playback);

    public override int GetHashCode() => Playback.GetHashCode();
}

/// <summary>
/// Bieżący element kiosku dla widoków — z <see cref="KioskRotation"/> (jednej dla wszystkich ekranów) — i przejście między
/// elementami (<see cref="ShaderPageTransition"/> z efektów wybranych w kiosku).
/// </summary>
public sealed partial class KioskPlayback : ObservableObject
{
    private readonly KioskRotation _rotation;
    private int _version;

    public KioskPlayback(KioskRotation rotation)
    {
        _rotation = rotation;
        rotation.Changed += (_, _) => _ = ApplyAsync();
        _ = ApplyAsync();
    }

    [ObservableProperty]
    public partial KioskFramePage? Frame { get; private set; }

    [ObservableProperty]
    public partial IPageTransition Transition { get; private set; } = ShaderPageTransition.For([]);

    /// <summary>Dla operatora, np. „2 z 5 · co 8 s”.</summary>
    public string? PositionText => _rotation.Slide is { } slide
        ? $"{_rotation.Index + 1} z {_rotation.Count} · co {slide.Interval.TotalSeconds:0} s" + (_rotation.IsRunning || _rotation.Count < 2 ? "" : " · zatrzymany")
        : null;

    /// <summary>Nowy element pokazuje się dopiero z wczytanym obrazem — przejście ma co sfotografować.</summary>
    private async Task ApplyAsync()
    {
        var version = ++_version;
        OnPropertyChanged(nameof(PositionText));
        if (_rotation.Slide is { } slide && (Transition is not ShaderPageTransition current
                ? slide.ChosenTransitions.Count > 0
                : !current.Effects.SequenceEqual(slide.ChosenTransitions)))
            Transition = ShaderPageTransition.For(slide.ChosenTransitions);
        if (_rotation.Frame is not { } frame)
        {
            Frame = null;
            return;
        }
        var zoom = _rotation.Slide is { SlowZoom: true } shown ? shown.Interval + ZoomOverlap : TimeSpan.Zero;
        // Nowa strona tylko dla innego elementu — zmiana czasu albo zoomu działa od następnego (bez przejścia do tego samego).
        if (Frame?.Frame != frame)
        {
            var page = new KioskFramePage(frame, zoom);
            await page.Ready;
            if (version == _version)
                Frame = page;
        }
        // następny obraz wczytany zawczasu — zmiana bez czekania
        if (_rotation is { Slide: { } next, Count: > 1 } && next.Frames[(_rotation.Index + 1) % _rotation.Count].Image is { } file)
            SlideImages.Preload(file);
    }

    /// <summary>Zoom trwa dłużej niż element — nie staje przed przejściem do następnego.</summary>
    private static readonly TimeSpan ZoomOverlap = TimeSpan.FromSeconds(2);
}

/// <summary>
/// Element kiosku na płótnie 1920×1080: obraz na cały ekran, sam napis, albo obraz z napisem z prawej lub pod spodem —
/// wg proporcji obrazu (<see cref="KioskFrame.LayoutFor"/>), więc układ jest znany dopiero po wczytaniu obrazu (<see cref="Ready"/>).
/// </summary>
public sealed partial class KioskFramePage : ObservableObject
{
    /// <param name="zoomDuration">Powolny zoom zdjęcia (<see cref="Views.SlowZoom"/>); zero = bez.</param>
    public KioskFramePage(KioskFrame frame, TimeSpan zoomDuration = default)
    {
        Frame = frame;
        ZoomDuration = zoomDuration;
        if (frame.Image is { } image)
            Ready = LoadAsync(image);
        else
        {
            Layout = KioskLayout.Text;
            Ready = Task.CompletedTask;
        }
    }

    public KioskFrame Frame { get; }

    public TimeSpan ZoomDuration { get; }

    /// <summary>Obraz wczytany i układ znany.</summary>
    public Task Ready { get; }

    public string? Title => Frame.Title;

    public string? Text => Frame.Text;

    public bool HasTitle => Frame.Title is not null;

    public bool HasBody => Frame.Text is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsImageOnly), nameof(IsTextOnly), nameof(IsTextRight), nameof(IsTextBelow))]
    public partial KioskLayout? Layout { get; private set; }

    [ObservableProperty]
    public partial Bitmap? Picture { get; private set; }

    public bool IsImageOnly => Layout == KioskLayout.Image;

    public bool IsTextOnly => Layout == KioskLayout.Text;

    public bool IsTextRight => Layout == KioskLayout.TextRight;

    public bool IsTextBelow => Layout == KioskLayout.TextBelow;

    private async Task LoadAsync(string image)
    {
        var picture = await SlideImages.LoadAsync(image);
        Picture = picture;
        // brak obrazu (uszkodzony plik) — sam napis, a bez napisu czarny ekran
        Layout = picture is null
            ? Frame.HasText ? KioskLayout.Text : null
            : Frame.LayoutFor(picture.Size.Width / picture.Size.Height);
    }
}
