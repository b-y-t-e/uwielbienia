using Uwielbienia.Core.Plans;
using Uwielbienia.Core.Presentation;

namespace Uwielbienia.App.ViewModels;

/// <summary>
/// Pasek górny: przejście między slajdami jednej pozycji otwartego planu (<see cref="Plan.InnerTransitions"/>) — domyślnie
/// jak między pozycjami („Slajdy: jak w planie”). Każda zmiana od razu w planie.
/// </summary>
public sealed class PlanSlideTransitionsViewModel
{
    private readonly ActivePlan _plan;

    public PlanSlideTransitionsViewModel(ActivePlan plan)
    {
        _plan = plan;
        Choice = new TransitionOverrideViewModel(Save, "Slajdy");
        plan.Changed += (_, _) => Show();
        Show();
    }

    public TransitionOverrideViewModel Choice { get; }

    private void Save(IReadOnlyList<SlideTransition>? chosen)
    {
        if (_plan.Plan is { } plan && !SlideTransitions.SameChoice(plan.InnerTransitions, chosen))
            _plan.Update(p => p.WithInnerTransitions(chosen));
    }

    private void Show() =>
        Choice.Show(_plan.Plan?.InnerTransitions, _plan.Plan?.ChosenTransitions ?? Plan.DefaultTransitions);
}
