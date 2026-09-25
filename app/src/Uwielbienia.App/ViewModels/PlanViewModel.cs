using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Uwielbienia.Core.Plans;
using Uwielbienia.Core.Presentation;
using Uwielbienia.Core.Songs;

namespace Uwielbienia.App.ViewModels;

/// <summary>Lewa kolumna: pozycje otwartego planu. Zaznaczenie = podgląd, nie zmienia ekranu.</summary>
public sealed partial class PlanViewModel : ObservableObject
{
    private static readonly CultureInfo Polish = CultureInfo.GetCultureInfo("pl-PL");

    private readonly ActivePlan _plan;
    private readonly ISongLibrary _library;
    private readonly ILiveControl _control;
    private readonly ILiveStateSource _live;

    public PlanViewModel(ActivePlan plan, ISongLibrary library, ILiveControl control, ILiveStateSource live)
    {
        _plan = plan;
        _library = library;
        _control = control;
        _live = live;
        plan.Changed += (_, _) => Rebuild();
        live.StateChanged += (_, _) => MarkLive();
        Rebuild();
    }

    public ObservableCollection<PlanItemViewModel> Items { get; } = [];

    [ObservableProperty]
    public partial PlanItemViewModel? Selected { get; set; }

    public string Name => _plan.Plan?.Name ?? "Brak planu";

    /// <summary>Data planu — pomijana, gdy nazwa już ją zawiera („Uwielbienie 25 września 2026”).</summary>
    public string? DateText =>
        _plan.Plan?.Date?.ToString("d MMMM yyyy", Polish) is { } date && !Name.Contains(date, StringComparison.CurrentCultureIgnoreCase)
            ? date
            : null;

    public bool IsEmpty => Items.Count == 0;

    /// <summary>Wybrana pozycja do podglądu (zmienia się tylko przez zaznaczenie w planie).</summary>
    public event EventHandler<PlanItemViewModel>? ItemSelected;

    partial void OnSelectedChanged(PlanItemViewModel? value)
    {
        if (value is not null)
            ItemSelected?.Invoke(this, value);
    }

    [RelayCommand]
    private void ShowLive(PlanItemViewModel item)
    {
        if (_plan.FindLiveItem(item.Id) is { } live)
            _control.Show(live);
    }

    [RelayCommand]
    private void MoveUp(PlanItemViewModel item) => _plan.Update(p => p.Move(item.Id, p.IndexOf(item.Id) - 1));

    [RelayCommand]
    private void MoveDown(PlanItemViewModel item) => _plan.Update(p => p.Move(item.Id, p.IndexOf(item.Id) + 1));

    [RelayCommand]
    private void Remove(PlanItemViewModel item) => _plan.Update(p => p.Remove(item.Id));

    private void Rebuild()
    {
        var selectedId = Selected?.Id;
        Items.Clear();
        var position = 1;
        foreach (var item in _plan.Plan?.Items.OfType<SongPlanItem>() ?? [])
        {
            if (_library.Find(item.SongId) is { } song)
                Items.Add(new PlanItemViewModel(item, song, position++));
        }
        Selected = Items.FirstOrDefault(i => i.Id == selectedId);
        MarkLive();
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(DateText));
        OnPropertyChanged(nameof(IsEmpty));
    }

    private void MarkLive()
    {
        var liveId = _live.State.Item?.PlanItemId;
        foreach (var item in Items)
            item.IsLive = item.Id == liveId;
    }
}

public sealed partial class PlanItemViewModel(SongPlanItem item, Song song, int position) : ObservableObject
{
    public Guid Id => Item.Id;

    public SongPlanItem Item { get; } = item;

    public Song Song { get; } = song;

    public int Position { get; } = position;

    public int Number => Song.Number;

    public string Title => Song.Title;

    public string Arrangement => string.Join(" ", Item.Arrangement ?? Song.Arrangement);

    [ObservableProperty]
    public partial bool IsLive { get; set; }
}
