using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Uwielbienia.App.Services;
using Uwielbienia.Core.Plans;
using Uwielbienia.Core.Songs;

namespace Uwielbienia.App.ViewModels;

/// <summary>
/// Zarządzanie planami jako zwykły CRUD: zakładki „Wydarzenia” i „Szablony”, lista po lewej,
/// formularz zaznaczonego (albo nowego) planu po prawej.
/// </summary>
public sealed partial class PlansViewModel : ObservableObject
{
    private static readonly CultureInfo Polish = CultureInfo.GetCultureInfo("pl-PL");

    private const string DefaultEventName = "Uwielbienie";

    private readonly IPlanStore _store;
    private readonly ActivePlan _active;
    private readonly ISettingsStore _settings;
    private readonly ISongLibrary _library;

    public PlansViewModel(IPlanStore store, ActivePlan active, ISettingsStore settings, ISongLibrary library)
    {
        _store = store;
        _active = active;
        _settings = settings;
        _library = library;
        EditName = "";
    }

    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    // --- Zakładki i lista ---

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowingEvents), nameof(NewButtonText), nameof(EmptyListText), nameof(OpenButtonText))]
    public partial bool ShowingTemplates { get; set; }

    public bool ShowingEvents => !ShowingTemplates;

    public string NewButtonText => ShowingTemplates ? "+ Nowy szablon" : "+ Nowe wydarzenie";

    public string EmptyListText => ShowingTemplates
        ? "Nie ma jeszcze szablonów. Szablon to stały zestaw pieśni bez daty, np. „Próba z dziećmi” — tworzy się z niego kolejne wydarzenia."
        : "Nie ma jeszcze wydarzeń. Wydarzenie to plan pieśni na konkretny dzień.";

    public ObservableCollection<PlanSummaryViewModel> Items { get; } = [];

    public bool IsListEmpty => Items.Count == 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection), nameof(HasForm), nameof(ShowPlaceholder), nameof(SelectedIsOpen),
        nameof(SelectedIsClosed), nameof(SelectedSongs), nameof(SelectedSongsHeader), nameof(CanDelete))]
    public partial PlanSummaryViewModel? Selected { get; set; }

    public bool HasSelection => Selected is not null && !IsCreating;

    public bool SelectedIsOpen => Selected?.IsOpen == true;

    public bool SelectedIsClosed => Selected is { IsOpen: false };

    public bool CanDelete => SelectedIsClosed;

    public string OpenButtonText => ShowingTemplates ? "Edytuj pieśni" : "Otwórz";

    public IReadOnlyList<string> SelectedSongs => Selected is null
        ? []
        : Selected.Plan.Items.OfType<SongPlanItem>()
            .Select(i => _library.Find(i.SongId) is { } song ? song.Number is { } n ? $"{n}. {song.DisplayTitle}" : song.DisplayTitle : "Nieznana pieśń")
            .ToList();

    public string SelectedSongsHeader => Selected?.Plan.Items.Count switch
    {
        null or 0 => "Plan nie ma jeszcze pieśni",
        1 => "1 pieśń",
        var n => $"Pieśni: {n}",
    };

    // --- Formularz ---

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection), nameof(HasForm), nameof(ShowPlaceholder), nameof(FormTitle))]
    public partial bool IsCreating { get; set; }

    public bool HasForm => IsCreating || Selected is not null;

    public bool ShowPlaceholder => !HasForm;

    public string FormTitle => IsCreating
        ? ShowingTemplates ? "Nowy szablon" : "Nowe wydarzenie"
        : ShowingTemplates ? "Szablon" : "Wydarzenie";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave), nameof(CanCreate), nameof(IsDirty))]
    public partial string EditName { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave), nameof(CanCreate), nameof(IsDirty))]
    public partial DateTime? EditDate { get; set; }

    public string NamePlaceholder => "Wpisz nazwę";

    public bool IsDirty => Selected is { } s && (EditName.Trim() != s.Plan.Name || EditDateOnly != s.Plan.Date);

    public bool CanSave => HasSelection && IsDirty && IsFormValid;

    public bool CanCreate => IsCreating && IsFormValid;

    /// <summary>Nazwa zawsze wymagana; data tylko dla wydarzenia i tylko w osobnym polu, nie w nazwie.</summary>
    private bool IsFormValid => !string.IsNullOrWhiteSpace(EditName) && (ShowingTemplates || EditDate is not null);

    private DateOnly? EditDateOnly => ShowingTemplates || EditDate is not { } d ? null : DateOnly.FromDateTime(d);

    [ObservableProperty]
    public partial bool IsDeleteConfirmationOpen { get; set; }

    /// <summary>Przy starcie: ostatnio otwarty plan albo nowe „Uwielbienie” z dzisiejszą datą.</summary>
    public void OpenInitial()
    {
        var plans = _store.LoadAll();
        var last = plans.FirstOrDefault(p => p.Id == _settings.Current.LastPlanId);
        var today = DateOnly.FromDateTime(DateTime.Today);
        Open(last ?? CreateAndSave(DefaultEventName, today, PlanKind.Event));
    }

    partial void OnSelectedChanged(PlanSummaryViewModel? value)
    {
        IsDeleteConfirmationOpen = false;
        if (value is null)
            return;
        IsCreating = false;
        EditName = value.Plan.Name;
        EditDate = value.Plan.Date?.ToDateTime(TimeOnly.MinValue);
        RefreshFormState();
    }

    [RelayCommand]
    private void Show()
    {
        var current = _active.Plan;
        ShowingTemplates = current?.Kind == PlanKind.Template;
        IsCreating = false;
        Reload(current?.Id);
        IsOpen = true;
    }

    [RelayCommand]
    private void Close() => IsOpen = false;

    [RelayCommand]
    private void ShowEventsTab() => SwitchTab(templates: false);

    [RelayCommand]
    private void ShowTemplatesTab() => SwitchTab(templates: true);

    [RelayCommand]
    private void StartNew()
    {
        Selected = null;
        IsDeleteConfirmationOpen = false;
        IsCreating = true;
        EditName = "";
        EditDate = ShowingTemplates ? null : DateTime.Today;
        RefreshFormState();
    }

    [RelayCommand]
    private void CancelNew()
    {
        IsCreating = false;
        Selected = Items.FirstOrDefault(i => i.IsOpen) ?? Items.FirstOrDefault();
    }

    [RelayCommand]
    private void Create()
    {
        if (!CanCreate)
            return;
        var kind = ShowingTemplates ? PlanKind.Template : PlanKind.Event;
        Open(CreateAndSave(EditName.Trim(), EditDateOnly, kind));
    }

    [RelayCommand]
    private void Save()
    {
        if (!CanSave || Selected is not { } selected)
            return;
        var name = EditName.Trim();
        var date = EditDateOnly;
        if (selected.Plan.Id == _active.Plan?.Id)
            _active.Update(p => p with { Name = name, Date = date, UpdatedAt = DateTimeOffset.Now });
        else
            _store.Save(selected.Plan with { Name = name, Date = date, UpdatedAt = DateTimeOffset.Now });
        Reload(selected.Plan.Id);
    }

    [RelayCommand]
    private void OpenSelected()
    {
        if (Selected is not { } selected)
            return;
        // Ten plan już jest otwarty — tylko zamknij okno, bez przewijania widoków do początku.
        if (selected.IsOpen)
            IsOpen = false;
        else
            Open(selected.Plan);
    }

    /// <summary>Nowe wydarzenie na dziś z tymi samymi pieśniami — z szablonu albo z wcześniejszego wydarzenia.</summary>
    [RelayCommand]
    private void CopyToToday()
    {
        if (Selected is not { } selected)
            return;
        var today = DateOnly.FromDateTime(DateTime.Today);
        var copy = selected.Plan.CloneAs(NameWithoutDate(selected.Plan), today, PlanKind.Event);
        _store.Save(copy);
        Open(copy);
    }

    [RelayCommand]
    private void SaveAsTemplate()
    {
        if (Selected is not { } selected)
            return;
        var template = selected.Plan.CloneAs(selected.Plan.Name, null, PlanKind.Template);
        _store.Save(template);
        ShowingTemplates = true;
        Reload(template.Id);
    }

    [RelayCommand]
    private void RequestDelete()
    {
        if (CanDelete)
            IsDeleteConfirmationOpen = true;
    }

    [RelayCommand]
    private void CancelDelete() => IsDeleteConfirmationOpen = false;

    [RelayCommand]
    private void ConfirmDelete()
    {
        IsDeleteConfirmationOpen = false;
        if (Selected is not { IsOpen: false } selected)
            return;
        _store.Delete(selected.Plan.Id);
        Reload(null);
    }

    private void SwitchTab(bool templates)
    {
        if (ShowingTemplates == templates && !IsCreating)
            return;
        ShowingTemplates = templates;
        IsCreating = false;
        Reload(_active.Plan?.Id);
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

    /// <summary>Wczytuje listę bieżącej zakładki i zaznacza wskazany plan (albo pierwszy).</summary>
    private void Reload(Guid? selectId)
    {
        var kind = ShowingTemplates ? PlanKind.Template : PlanKind.Event;
        var plans = _store.LoadAll().Where(p => p.Kind == kind);
        plans = kind == PlanKind.Event
            ? plans.OrderByDescending(p => p.Date ?? DateOnly.MinValue).ThenByDescending(p => p.UpdatedAt)
            : plans.OrderBy(p => p.Name, StringComparer.CurrentCulture);

        Selected = null;
        Items.Clear();
        foreach (var plan in plans)
            Items.Add(new PlanSummaryViewModel(plan, plan.Id == _active.Plan?.Id));
        OnPropertyChanged(nameof(IsListEmpty));

        Selected = Items.FirstOrDefault(i => i.Plan.Id == selectId) ?? Items.FirstOrDefault();
        RefreshFormState();
    }

    private void RefreshFormState()
    {
        OnPropertyChanged(nameof(FormTitle));
        OnPropertyChanged(nameof(NamePlaceholder));
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(CanCreate));
    }

    /// <summary>Starsze plany miały datę w nazwie — kopia na inny dzień nie powinna jej powielać.</summary>
    private static string NameWithoutDate(Plan plan)
    {
        if (plan.Date is not { } date)
            return plan.Name;
        var name = plan.Name.Replace(date.ToString("d MMMM yyyy", Polish), "", StringComparison.CurrentCultureIgnoreCase).Trim(' ', '—', '-');
        return name.Length > 0 ? name : DefaultEventName;
    }
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
            var songs = count switch
            {
                0 => "bez pieśni",
                1 => "1 pieśń",
                _ => $"{count} pieśni",
            };
            return Plan.Date is { } date ? $"{date.ToString("ddd, d MMM yyyy", Polish)} · {songs}" : songs;
        }
    }
}
