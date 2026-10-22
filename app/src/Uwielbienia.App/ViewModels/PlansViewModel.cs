using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Uwielbienia.App.Services;
using Uwielbienia.Core.Plans;
using Uwielbienia.Core.Presentation;
using Uwielbienia.Core.Songs;

namespace Uwielbienia.App.ViewModels;

/// <summary>
/// Zarządzanie planami jako zwykły CRUD: zakładki „Wydarzenia” i „Szablony”, lista po lewej,
/// formularz zaznaczonego (albo nowego) planu po prawej.
/// </summary>
public sealed partial class PlansViewModel : ObservableObject
{
    private readonly PlanLibrary _plans;
    private readonly ActivePlan _active;
    private readonly ISettingsStore _settings;
    private readonly IPlanItemTypes _itemTypes;

    public PlansViewModel(PlanLibrary plans, ActivePlan active, ISettingsStore settings, IPlanItemTypes itemTypes)
    {
        _plans = plans;
        _active = active;
        _settings = settings;
        _itemTypes = itemTypes;
        EditName = "";
        // otwarty plan (stąd, z panelu w przeglądarce, przy starcie) — następne uruchomienie zaczyna od niego;
        // okno zamyka tylko otwarcie z niego samego — otwarcie z panelu odświeża w nim listę (znacznik otwartego planu)
        active.Opened += (_, _) =>
        {
            if (active.Plan is { } plan)
                settings.Update(s => s with { LastPlanId = plan.Id });
            ReloadUnlessEditing();
        };
        plans.Changed += (_, _) => ReloadUnlessEditing();
    }

    /// <summary>Zmiana z panelu w przeglądarce przy otwartym oknie — lista na nowo, chyba że operator coś tu wpisuje.</summary>
    private void ReloadUnlessEditing()
    {
        if (IsOpen && !IsCreating && !IsDirty)
            Reload(Selected?.Plan.Id);
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
        : Selected.Plan.Items
            .Select(_itemTypes.Describe)
            .OfType<PlanItemInfo>()
            .Select(info => info.Number is { } n ? $"{n}. {info.Title}" : info.Title)
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
    private bool IsFormValid => PlanLibrary.IsValid(EditName, EditDateOnly, ShowingTemplates ? PlanKind.Template : PlanKind.Event);

    private DateOnly? EditDateOnly => ShowingTemplates || EditDate is not { } d ? null : DateOnly.FromDateTime(d);

    [ObservableProperty]
    public partial bool IsDeleteConfirmationOpen { get; set; }

    /// <summary>Przy starcie: ostatnio otwarty plan albo nowe „Uwielbienie” z dzisiejszą datą.</summary>
    public void OpenInitial() => _plans.OpenInitial(_settings.Current.LastPlanId);

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
        _plans.Create(EditName, EditDateOnly, ShowingTemplates ? PlanKind.Template : PlanKind.Event);
        IsOpen = false;
    }

    [RelayCommand]
    private void Save()
    {
        if (!CanSave || Selected is not { } selected)
            return;
        _plans.Rename(selected.Plan.Id, EditName, EditDateOnly);
        Reload(selected.Plan.Id);
    }

    [RelayCommand]
    private void OpenSelected()
    {
        if (Selected is not { } selected)
            return;
        // Ten plan już jest otwarty — tylko zamknij okno, bez przewijania widoków do początku.
        if (!selected.IsOpen)
            _plans.Open(selected.Plan.Id);
        IsOpen = false;
    }

    /// <summary>Nowe wydarzenie na dziś z tymi samymi pieśniami — z szablonu albo z wcześniejszego wydarzenia.</summary>
    [RelayCommand]
    private void CopyToToday()
    {
        if (Selected is not { } selected)
            return;
        _plans.CopyToToday(selected.Plan.Id);
        IsOpen = false;
    }

    [RelayCommand]
    private void SaveAsTemplate()
    {
        if (Selected is not { } selected || _plans.SaveAsTemplate(selected.Plan.Id) is not { } template)
            return;
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
        _plans.Delete(selected.Plan.Id);
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

    /// <summary>Wczytuje listę bieżącej zakładki i zaznacza wskazany plan (albo pierwszy).</summary>
    private void Reload(Guid? selectId)
    {
        var plans = _plans.List(ShowingTemplates ? PlanKind.Template : PlanKind.Event);

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
