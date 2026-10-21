using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Uwielbienia.Core.Plans;
using Uwielbienia.Core.Presentation;

namespace Uwielbienia.App.ViewModels;

/// <summary>
/// Pasek górny: przejście z jednej pozycji otwartego planu na inną (<see cref="Plan.Transitions"/>). Przycisk pokazuje obecny
/// wybór, a efekty zaznacza się w rozwijanym panelu — każda zmiana od razu w planie. Kiosk ma własne przejścia.
/// </summary>
public sealed partial class PlanTransitionsViewModel : ObservableObject
{
    private readonly ActivePlan _plan;
    private bool _showingPlan;

    public PlanTransitionsViewModel(ActivePlan plan)
    {
        _plan = plan;
        foreach (var transition in SlideTransitions.All)
        {
            var option = new TransitionOptionViewModel(transition, false);
            option.PropertyChanged += (_, _) => OnChosen();
            Options.Add(option);
        }
        plan.Changed += (_, _) => ShowPlan();
        ShowPlan();
    }

    /// <summary>Żaden — zwykłe przenikanie, jeden — zawsze ten, kilka — losowo jeden z nich.</summary>
    public ObservableCollection<TransitionOptionViewModel> Options { get; } = [];

    /// <summary>Np. „Przejście: Akwarela”, „Przejście: losowe (3)”, „Przejście: przenikanie”.</summary>
    public string ButtonText =>
        "Przejście: " + SlideTransitions.Describe(Options.Where(o => o.IsSelected).Select(o => o.Transition).ToList());

    private void OnChosen()
    {
        if (_showingPlan)
            return;
        if (_plan.Plan is not { } plan)
        {
            ShowPlan();
            return;
        }
        OnPropertyChanged(nameof(ButtonText));
        var chosen = Options.Where(o => o.IsSelected).Select(o => o.Transition).ToList();
        if (!plan.ChosenTransitions.SequenceEqual(chosen))
            _plan.Update(p => p.WithTransitions(chosen));
    }

    private void ShowPlan()
    {
        var chosen = _plan.Plan?.ChosenTransitions ?? Plan.DefaultTransitions;
        _showingPlan = true;
        foreach (var option in Options)
            option.IsSelected = chosen.Contains(option.Transition);
        _showingPlan = false;
        OnPropertyChanged(nameof(ButtonText));
    }
}
