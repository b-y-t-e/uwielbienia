using System.Collections.ObjectModel;
using Uwielbienia.Core.Presentation;

namespace Uwielbienia.App.ViewModels;

/// <summary>
/// Lista efektów przejścia do zaznaczania (żaden — przenikanie, kilka — losowo jeden). <see cref="Chosen"/> zgłasza tylko
/// zmiany operatora, nie <see cref="Show"/>.
/// </summary>
public sealed class TransitionChoiceViewModel
{
    private bool _showing;

    public TransitionChoiceViewModel()
    {
        foreach (var transition in SlideTransitions.All)
        {
            var option = new TransitionOptionViewModel(transition, false);
            option.PropertyChanged += (_, _) =>
            {
                if (!_showing)
                    Chosen?.Invoke(this, Selected);
            };
            Options.Add(option);
        }
    }

    public event EventHandler<IReadOnlyList<SlideTransition>>? Chosen;

    public ObservableCollection<TransitionOptionViewModel> Options { get; } = [];

    public IReadOnlyList<SlideTransition> Selected => Options.Where(o => o.IsSelected).Select(o => o.Transition).ToList();

    /// <summary>Np. „Akwarela”, „losowe (3)”, „przenikanie”.</summary>
    public string Description => SlideTransitions.Describe(Selected);

    public void Show(IReadOnlyList<SlideTransition> chosen)
    {
        _showing = true;
        foreach (var option in Options)
            option.IsSelected = chosen.Contains(option.Transition);
        _showing = false;
    }
}
