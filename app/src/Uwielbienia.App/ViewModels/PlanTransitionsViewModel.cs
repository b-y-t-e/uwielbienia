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
    private readonly TransitionChoiceViewModel _choice = new();

    public PlanTransitionsViewModel(ActivePlan plan)
    {
        _plan = plan;
        _choice.Chosen += (_, chosen) => OnChosen(chosen);
        plan.Changed += (_, _) => ShowPlan();
        ShowPlan();
    }

    /// <summary>Żaden — zwykłe przenikanie, jeden — zawsze ten, kilka — losowo jeden z nich.</summary>
    public ObservableCollection<TransitionOptionViewModel> Options => _choice.Options;

    /// <summary>Np. „Przejście: Akwarela”, „Przejście: losowe (3)”, „Przejście: przenikanie”.</summary>
    public string ButtonText => "Przejście: " + _choice.Description;

    private void OnChosen(IReadOnlyList<SlideTransition> chosen)
    {
        if (_plan.Plan is not { } plan)
        {
            ShowPlan();
            return;
        }
        OnPropertyChanged(nameof(ButtonText));
        if (!plan.ChosenTransitions.SequenceEqual(chosen))
            _plan.Update(p => p.WithTransitions(chosen));
    }

    private void ShowPlan()
    {
        _choice.Show(_plan.Plan?.ChosenTransitions ?? Plan.DefaultTransitions);
        OnPropertyChanged(nameof(ButtonText));
    }
}
