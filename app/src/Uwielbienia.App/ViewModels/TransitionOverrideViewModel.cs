using CommunityToolkit.Mvvm.ComponentModel;
using Uwielbienia.Core.Plans;
using Uwielbienia.Core.Presentation;

namespace Uwielbienia.App.ViewModels;

/// <summary>
/// Wybór efektów przejścia z „Jak w planie” — kolumna „Pieśń”: przejście między slajdami pieśni, tekstu lub prezentacji (<see cref="SongPlanItem.SlideEffects"/>). „Jak w planie” —
/// efekty z paska górnego (widać je zaznaczone); wybranie efektu daje pieśni własne przejście. Ten sam wybór służy
/// przejściu między slajdami w pasku górnym (<see cref="PlanSlideTransitionsViewModel"/>).
/// </summary>
public sealed partial class TransitionOverrideViewModel : ObservableObject
{
    private readonly Action<IReadOnlyList<SlideTransition>?> _save;
    private readonly TransitionChoiceViewModel _choice = new();
    private readonly string _label;
    private bool _showing;

    /// <param name="save">Zapis: własne efekty albo <c>null</c> = jak w planie.</param>
    /// <param name="label">Początek napisu na przycisku, np. „Przejście”, „Slajdy”.</param>
    public TransitionOverrideViewModel(Action<IReadOnlyList<SlideTransition>?> save, string label = "Przejście")
    {
        _save = save;
        _label = label;
        _choice.Chosen += (_, chosen) => OnChosen(chosen);
    }

    public TransitionChoiceViewModel Choice => _choice;

    /// <summary>Pieśń nie ma własnego przejścia — używa przejścia planu.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ButtonText), nameof(ShortText))]
    public partial bool FollowsPlan { get; set; } = true;

    /// <summary>Np. „Przejście: jak w planie”, „Przejście: Płomień”, „Przejście: losowe (2)”.</summary>
    public string ButtonText => _label + ": " + (FollowsPlan ? "jak w planie" : _choice.Description);

    /// <summary>Napis w pasku górnym obok ikony, np. „Jak w planie”, „Płomień”.</summary>
    public string ShortText => FollowsPlan ? "Jak w planie" : _choice.Title;

    /// <summary>Pokazuje wybór pozycji: własny albo (<paramref name="own"/> = <c>null</c>) przejście planu.</summary>
    public void Show(IReadOnlyList<SlideTransition>? own, IReadOnlyList<SlideTransition> plan)
    {
        _showing = true;
        FollowsPlan = own is null;
        _choice.Show(own ?? plan);
        _showing = false;
        OnPropertyChanged(nameof(ButtonText));
        OnPropertyChanged(nameof(ShortText));
    }

    partial void OnFollowsPlanChanged(bool value)
    {
        if (!_showing)
            _save(value ? null : _choice.Selected);
    }

    private void OnChosen(IReadOnlyList<SlideTransition> chosen)
    {
        _showing = true;
        FollowsPlan = false; // zaznaczenie efektu = własne przejście (np. slajdów pozycji)
        _showing = false;
        OnPropertyChanged(nameof(ButtonText));
        OnPropertyChanged(nameof(ShortText));
        _save(chosen);
    }
}
