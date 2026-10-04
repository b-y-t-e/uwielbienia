using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Uwielbienia.Core.Plans;
using Uwielbienia.Core.Presentation;
using Uwielbienia.Core.Songs;

namespace Uwielbienia.App.ViewModels;

/// <summary>Lewa kolumna: kolejka wykonawcza. Klik pokazuje pozycję, przeciąganie zmienia kolejność.</summary>
public sealed partial class PlanViewModel : ObservableObject
{
    private static readonly CultureInfo Polish = CultureInfo.GetCultureInfo("pl-PL");

    private readonly ActivePlan _plan;
    private readonly IPlanItemTypes _itemTypes;
    private readonly ILiveControl _control;
    private readonly ILiveStateSource _live;

    public PlanViewModel(ActivePlan plan, IPlanItemTypes itemTypes, ILiveControl control, ILiveStateSource live, PlanActions actions)
    {
        _plan = plan;
        _itemTypes = itemTypes;
        _control = control;
        _live = live;
        plan.Changed += (_, _) => Rebuild();
        plan.Opened += (_, _) => StartFromBeginning();
        actions.ItemAdded += (_, added) => Selected = Items.FirstOrDefault(i => i.Id == added.Id) ?? Selected;
        live.StateChanged += (_, _) => MarkLive();
        Rebuild();
    }

    public ObservableCollection<PlanItemViewModel> Items { get; } = [];

    [ObservableProperty]
    public partial PlanItemViewModel? Selected { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRemoveConfirmationOpen))]
    public partial PlanItemViewModel? PendingRemoval { get; set; }

    public string Name => _plan.Plan?.Name ?? "Brak planu";

    /// <summary>Data planu — pomijana, gdy nazwa już ją zawiera („Uwielbienie 25 września 2026”).</summary>
    public string? DateText =>
        _plan.Plan?.Date?.ToString("d MMMM yyyy", Polish) is { } date && !Name.Contains(date, StringComparison.CurrentCultureIgnoreCase)
            ? date
            : null;

    public bool IsEmpty => Items.Count == 0;

    public bool IsRemoveConfirmationOpen => PendingRemoval is not null;

    /// <summary>Pozycja wybrana do kolumny „Pieśń” (zmienia się tylko przez zaznaczenie w planie).</summary>
    public event EventHandler<PlanItemViewModel>? ItemSelected;

    /// <summary>Nie ma już wybranej pozycji (pusty plan, usunięta pieśń) — kolumna „Pieśń” ma być pusta.</summary>
    public event EventHandler? SelectionCleared;

    partial void OnSelectedChanged(PlanItemViewModel? value)
    {
        if (value is not null)
            ItemSelected?.Invoke(this, value);
    }

    [RelayCommand]
    private void ShowLive(PlanItemViewModel item)
    {
        Selected = item;
        if (_plan.FindLiveItem(item.Id) is { } live)
            _control.Show(live);
    }

    public void Reorder(PlanItemViewModel item, int newIndex) =>
        _plan.Update(p => p.Move(item.Id, newIndex));

    [RelayCommand]
    private void RequestRemove(PlanItemViewModel item) => PendingRemoval = item;

    [RelayCommand]
    private void ConfirmRemove()
    {
        if (PendingRemoval is not { } item)
            return;
        PendingRemoval = null;
        _plan.Update(p => p.Remove(item.Id));
    }

    [RelayCommand]
    private void CancelRemove() => PendingRemoval = null;

    private void Rebuild()
    {
        var selectedId = Selected?.Id;
        Items.Clear();
        var position = 1;
        foreach (var item in _plan.Plan?.Items ?? [])
        {
            if (_itemTypes.Describe(item) is { } info)
                Items.Add(new PlanItemViewModel(item, info, position++));
        }
        Selected = Items.FirstOrDefault(i => i.Id == selectedId);
        // Wybrana pieśń zniknęła z planu (usunięta) — kolumna „Pieśń” nie może jej dalej pokazywać.
        if (selectedId is not null && Selected is null)
            SelectionCleared?.Invoke(this, EventArgs.Empty);
        MarkLive();
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(DateText));
        OnPropertyChanged(nameof(IsEmpty));
    }

    /// <summary>
    /// Po otwarciu innego planu: zaznaczona pierwsza pieśń, a ekran z pieśnią spoza tego planu
    /// jest czyszczony — „Dalej” zacznie wtedy od pierwszej pieśni nowego planu.
    /// </summary>
    private void StartFromBeginning()
    {
        if (_live.State.Item is { } shown && Items.All(i => i.Id != shown.PlanItemId))
            _control.Clear();
        Selected = Items.FirstOrDefault();
        if (Selected is null)
            SelectionCleared?.Invoke(this, EventArgs.Empty);
    }

    private void MarkLive()
    {
        var liveId = _live.State.Item?.PlanItemId;
        foreach (var item in Items)
            item.IsLive = item.Id == liveId;
    }
}

/// <summary>Wiersz planu — dowolny rodzaj pozycji; tytuł, numer i rodzaj z <see cref="IPlanItemTypes"/>.</summary>
public sealed partial class PlanItemViewModel(PlanItem item, PlanItemInfo info, int position) : ObservableObject, IReorderableItem
{
    public Guid Id => Item.Id;

    public PlanItem Item { get; } = item;

    public PlanItemInfo Info { get; } = info;

    public int Position { get; } = position;

    public int? Number => Info.Number;

    public bool IsText => Info.Kind == PlanItemKind.Text;

    public string Title => Info.Title;

    [ObservableProperty]
    public partial bool IsLive { get; set; }

    /// <summary>Wiersz jest właśnie przeciągany.</summary>
    [ObservableProperty]
    public partial bool IsDragged { get; set; }

    /// <summary>Znacznik miejsca upuszczenia nad wierszem.</summary>
    [ObservableProperty]
    public partial bool IsDropBefore { get; set; }

    /// <summary>Znacznik miejsca upuszczenia pod wierszem (tylko ostatni wiersz).</summary>
    [ObservableProperty]
    public partial bool IsDropAfter { get; set; }
}
