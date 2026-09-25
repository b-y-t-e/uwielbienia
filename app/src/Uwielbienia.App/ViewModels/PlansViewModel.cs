using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Uwielbienia.App.Services;
using Uwielbienia.Core.Plans;

namespace Uwielbienia.App.ViewModels;

/// <summary>Przeglądarka planów: wydarzenia (historia wg daty) i szablony. Otwieranie, klonowanie, usuwanie.</summary>
public sealed partial class PlansViewModel : ObservableObject
{
    private static readonly CultureInfo Polish = CultureInfo.GetCultureInfo("pl-PL");

    private readonly IPlanStore _store;
    private readonly ActivePlan _active;
    private readonly ISettingsStore _settings;

    public PlansViewModel(IPlanStore store, ActivePlan active, ISettingsStore settings)
    {
        _store = store;
        _active = active;
        _settings = settings;
        NewDate = DateTime.Today;
        NewName = DefaultName(DateOnly.FromDateTime(DateTime.Today));
    }

    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    [ObservableProperty]
    public partial string NewName { get; set; }

    [ObservableProperty]
    public partial DateTime? NewDate { get; set; }

    [ObservableProperty]
    public partial bool NewIsTemplate { get; set; }

    public ObservableCollection<PlanSummaryViewModel> Events { get; } = [];

    public ObservableCollection<PlanSummaryViewModel> Templates { get; } = [];

    /// <summary>Przy starcie: ostatnio otwarty plan albo nowy „Uwielbienie {dzisiaj}”.</summary>
    public void OpenInitial()
    {
        var plans = _store.LoadAll();
        var last = plans.FirstOrDefault(p => p.Id == _settings.Current.LastPlanId);
        Open(last ?? CreateAndSave(DefaultName(DateOnly.FromDateTime(DateTime.Today)), DateOnly.FromDateTime(DateTime.Today), PlanKind.Event));
    }

    [RelayCommand]
    private void Show()
    {
        Reload();
        IsOpen = true;
    }

    [RelayCommand]
    private void Close() => IsOpen = false;

    [RelayCommand]
    private void Create()
    {
        var kind = NewIsTemplate ? PlanKind.Template : PlanKind.Event;
        var date = kind == PlanKind.Event && NewDate is { } d ? DateOnly.FromDateTime(d) : (DateOnly?)null;
        var name = string.IsNullOrWhiteSpace(NewName) ? DefaultName(date ?? DateOnly.FromDateTime(DateTime.Today)) : NewName.Trim();
        Open(CreateAndSave(name, date, kind));
    }

    [RelayCommand]
    private void OpenPlan(PlanSummaryViewModel plan) => Open(plan.Plan);

    /// <summary>„Użyj ponownie” — kopia na dziś, np. szablon „Próba z dziećmi” albo zeszłotygodniowe uwielbienie.</summary>
    [RelayCommand]
    private void Reuse(PlanSummaryViewModel source)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var name = source.Plan.Kind == PlanKind.Template ? $"{source.Plan.Name} — {today.ToString("d MMMM yyyy", Polish)}" : DefaultName(today);
        var copy = source.Plan.CloneAs(name, today, PlanKind.Event);
        _store.Save(copy);
        Open(copy);
    }

    [RelayCommand]
    private void SaveAsTemplate(PlanSummaryViewModel source)
    {
        _store.Save(source.Plan.CloneAs(source.Plan.Name, null, PlanKind.Template));
        Reload();
    }

    [RelayCommand]
    private void Delete(PlanSummaryViewModel plan)
    {
        if (plan.Plan.Id == _active.Plan?.Id)
            return;
        _store.Delete(plan.Plan.Id);
        Reload();
    }

    private Plan CreateAndSave(string name, DateOnly? date, PlanKind kind)
    {
        var plan = Plan.Create(name, date, kind);
        _store.Save(plan);
        return plan;
    }

    private void Open(Plan plan)
    {
        _active.Open(plan);
        _settings.Update(s => s with { LastPlanId = plan.Id });
        IsOpen = false;
    }

    private void Reload()
    {
        var plans = _store.LoadAll();
        Events.Clear();
        foreach (var plan in plans.Where(p => p.Kind == PlanKind.Event)
                     .OrderByDescending(p => p.Date ?? DateOnly.MinValue).ThenByDescending(p => p.UpdatedAt))
            Events.Add(new PlanSummaryViewModel(plan, plan.Id == _active.Plan?.Id));
        Templates.Clear();
        foreach (var plan in plans.Where(p => p.Kind == PlanKind.Template).OrderBy(p => p.Name, StringComparer.CurrentCulture))
            Templates.Add(new PlanSummaryViewModel(plan, plan.Id == _active.Plan?.Id));
    }

    private static string DefaultName(DateOnly date) => $"Uwielbienie {date.ToString("d MMMM yyyy", Polish)}";
}

public sealed class PlanSummaryViewModel(Plan plan, bool isOpen)
{
    private static readonly CultureInfo Polish = CultureInfo.GetCultureInfo("pl-PL");

    public Plan Plan { get; } = plan;

    public bool IsOpen { get; } = isOpen;

    public string Name => Plan.Name;

    public string Details
    {
        get
        {
            var count = Plan.Items.Count;
            var songs = count == 1 ? "1 pieśń" : $"{count} pieśni";
            return Plan.Date is { } date ? $"{date.ToString("ddd, d MMM yyyy", Polish)}, {songs}" : songs;
        }
    }
}
