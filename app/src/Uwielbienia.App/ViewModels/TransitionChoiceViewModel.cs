using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Uwielbienia.Core.Presentation;

namespace Uwielbienia.App.ViewModels;

/// <summary>
/// Lista efektów przejścia do zaznaczania (żaden — przenikanie, kilka — losowo jeden) z osobnymi „Przenikanie”
/// (<see cref="IsCrossFade"/>) i „Brak efektu” (<see cref="IsCut"/>). <see cref="Chosen"/> zgłasza tylko zmiany operatora, nie <see cref="Show"/>.
/// </summary>
public sealed class TransitionChoiceViewModel : ObservableObject
{
    private bool _updatingOptions;
    private bool _cut;

    public TransitionChoiceViewModel()
    {
        foreach (var transition in SlideTransitions.All)
        {
            var option = new TransitionOptionViewModel(transition, false);
            option.PropertyChanged += (_, _) =>
            {
                if (_updatingOptions)
                    return;
                if (option.IsSelected)
                    _cut = false; // efekt wyklucza „Brak efektu”
                NotifyExclusiveChoices();
                Chosen?.Invoke(this, Selected);
            };
            Options.Add(option);
        }
    }

    public event EventHandler<IReadOnlyList<SlideTransition>>? Chosen;

    public ObservableCollection<TransitionOptionViewModel> Options { get; } = [];

    /// <summary>
    /// „Przenikanie” — żaden efekt. Zaznaczenie odznacza wszystkie efekty; odznaczyć się go nie da (wybiera się wtedy efekt).
    /// </summary>
    public bool IsCrossFade
    {
        get => !_cut && Options.All(o => !o.IsSelected);
        set
        {
            if (value && !IsCrossFade)
                ChooseWithoutEffects(SlideTransitions.CrossFade);
            NotifyExclusiveChoices();
        }
    }

    /// <summary>
    /// „Brak efektu” — nowy slajd od razu. Zaznaczenie odznacza efekty i przenikanie; odznaczyć się go nie da (jak „Przenikanie”).
    /// </summary>
    public bool IsCut
    {
        get => _cut;
        set
        {
            if (value && !_cut)
                ChooseWithoutEffects(SlideTransitions.Cut);
            NotifyExclusiveChoices(); // odznaczony przycisk wraca do stanu z wyboru
        }
    }

    public IReadOnlyList<SlideTransition> Selected =>
        _cut ? SlideTransitions.Cut : Options.Where(o => o.IsSelected).Select(o => o.Transition).ToList();

    /// <summary>Np. „Akwarela”, „losowe (3)”, „przenikanie”.</summary>
    public string Description => SlideTransitions.Describe(Selected);

    /// <summary>Opis jako samodzielny napis (bez „Przejście: ”), np. „Losowe (3)”.</summary>
    public string Title => Description is [var first, .. var rest] ? char.ToUpper(first) + rest : Description;

    public void Show(IReadOnlyList<SlideTransition> chosen)
    {
        _updatingOptions = true;
        _cut = SlideTransitions.IsCut(chosen);
        foreach (var option in Options)
            option.IsSelected = !_cut && chosen.Contains(option.Transition);
        _updatingOptions = false;
        NotifyExclusiveChoices();
    }

    /// <summary>Wybór bez efektów do zaznaczania: „Przenikanie” albo „Brak efektu”.</summary>
    private void ChooseWithoutEffects(IReadOnlyList<SlideTransition> choice)
    {
        _updatingOptions = true;
        _cut = SlideTransitions.IsCut(choice);
        foreach (var option in Options)
            option.IsSelected = false;
        _updatingOptions = false;
        Chosen?.Invoke(this, Selected);
    }

    private void NotifyExclusiveChoices()
    {
        OnPropertyChanged(nameof(IsCut));
        OnPropertyChanged(nameof(IsCrossFade));
    }
}
