using Uwielbienia.Core.Presentation;
using Uwielbienia.Core.Slideshows;
using Uwielbienia.Core.Songs;

namespace Uwielbienia.Core.Plans;

/// <summary>
/// Wszystko, co można zrobić z otwartym planem: dodawanie, kolejność, usuwanie, duplikowanie, układ części pieśni,
/// pokazanie pozycji na ekranie. Jedno miejsce dla okna operatora, panelu w przeglądarce i telefonu.
/// </summary>
public sealed class PlanActions(
    ActivePlan plan,
    ILiveStateSource live,
    ILiveControl control,
    ISongLibrary library,
    ISongEditor editor,
    ISlideshowLibrary slideshows)
{
    /// <summary>
    /// Operator dodał pozycję do planu — plan ją zaznacza, żeby kolumna „Pieśń” pokazywała pozycję planu. Dodanie
    /// z innego urządzenia (<c>select: false</c>) nie zmienia zaznaczenia operatora.
    /// </summary>
    public event EventHandler<PlanItem>? ItemAdded;

    public LiveItem? LiveItemFor(PlanItem item) => plan.FindLiveItem(item.Id);

    /// <summary>Wstawia pieśń zaraz za tą, która jest na ekranie (albo na koniec, gdy ekran pokazuje coś spoza planu).</summary>
    public SongPlanItem AddAfterLive(Song song, IReadOnlyList<string>? arrangement = null, bool select = true)
    {
        var item = SongPlanItem.For(song.Id) with { Arrangement = arrangement };
        plan.Update(p => p.InsertAfter(live.State.Item?.PlanItemId, item));
        if (select)
            ItemAdded?.Invoke(this, item);
        return item;
    }

    /// <summary>Wstawia dowolną pozycję (pieśń, tekst, inny rodzaj) w podane miejsce planu.</summary>
    public T Insert<T>(T item, int index, bool select = true) where T : PlanItem
    {
        plan.Update(p => p.Insert(index, item));
        if (select)
            ItemAdded?.Invoke(this, item);
        return item;
    }

    /// <summary>Wstawia pieśń lub tekst w podane miejsce planu („Dodaj powyżej / poniżej” w planie).</summary>
    public SongPlanItem InsertAt(Song song, int index, bool select = true) => Insert(SongPlanItem.For(song.Id), index, select);

    public SongPlanItem AddToEnd(Song song, IReadOnlyList<string>? arrangement = null) =>
        Insert(SongPlanItem.For(song.Id) with { Arrangement = arrangement }, plan.Plan?.Items.Count ?? 0);

    public void Move(Guid itemId, int newIndex)
    {
        if (plan.Plan?.IndexOf(itemId) is >= 0)
            plan.Update(p => p.Move(itemId, newIndex));
    }

    public void Remove(Guid itemId) => plan.Update(p => p.Remove(itemId));

    /// <summary>
    /// Kopia pozycji zaraz za nią — niezależna: pieśń lub tekst jako nowa własna pieśń (z tym samym układem
    /// części w planie), prezentacja jako nowy folder. Zmiana kopii nie zmienia oryginału.
    /// </summary>
    /// <returns><c>null</c>, gdy pozycji nie ma w planie albo kopia się nie udała (brak miejsca, dostępu).</returns>
    public async Task<PlanItem?> DuplicateAsync(Guid itemId, bool select = true)
    {
        if (plan.Plan?.Items.FirstOrDefault(i => i.Id == itemId) is not { } item)
            return null;
        try
        {
            PlanItem copy = item switch
            {
                SongPlanItem songItem when library.Find(songItem.SongId) is { } song =>
                    songItem with { Id = Guid.NewGuid(), SongId = editor.Copy(song).Id },
                PresentationPlanItem presentation =>
                    presentation with { Id = Guid.NewGuid(), Folder = await slideshows.CopyAsync(presentation.Folder) },
                _ => item.WithNewId(),
            };
            // plan mógł się zmienić w trakcie kopiowania prezentacji — miejsce za oryginałem liczone na nowo
            var index = plan.Plan?.IndexOf(itemId) is >= 0 and var at ? at + 1 : plan.Plan?.Items.Count ?? 0;
            return Insert(copy, index, select);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Pokazuje pozycję planu na ekranie od podanego slajdu (spoza zakresu — najbliższy istniejący);
    /// <c>false</c>, gdy nie ma jej w planie.
    /// </summary>
    public bool Show(Guid itemId, int slideIndex = 0)
    {
        if (plan.FindLiveItem(itemId) is not { } item)
            return false;
        control.Show(item, Math.Clamp(slideIndex, 0, Math.Max(0, item.Slides.Count - 1)));
        return true;
    }

    /// <summary>
    /// Nowy układ części pieśni w tym planie (kolejność, powtórzenia, pominięcia; brakujące części są usunięte).
    /// Dla granej pieśni działa od razu na ekranie.
    /// </summary>
    /// <returns>Zapisana pozycja; <c>null</c>, gdy pozycja nie jest pieśnią z tego planu.</returns>
    public SongPlanItem? SetLayout(Guid itemId, IReadOnlyList<LayoutEntry> layout)
    {
        if (plan.Plan?.Items.FirstOrDefault(i => i.Id == itemId) is not SongPlanItem item
            || library.Find(item.SongId) is not { } song)
            return null;
        var known = song.Sections.Select(s => s.Code).ToHashSet(StringComparer.Ordinal);
        var changed = item.WithLayout(song, layout.Where(e => known.Contains(e.Code)).ToList());
        plan.Update(p => p.Replace(changed));
        return changed;
    }

    /// <summary>Zmienia pozycję planu (np. własne przejścia slajdów); <c>null</c>, gdy jej nie ma w planie.</summary>
    public T? Change<T>(Guid itemId, Func<T, T> change) where T : PlanItem
    {
        if (plan.Plan?.Items.FirstOrDefault(i => i.Id == itemId) is not T item)
            return null;
        var changed = change(item);
        plan.Update(p => p.Replace(changed));
        return changed;
    }
}
