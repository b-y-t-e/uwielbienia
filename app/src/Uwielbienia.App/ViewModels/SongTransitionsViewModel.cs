using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Uwielbienia.Core.Plans;
using Uwielbienia.Core.Presentation;

namespace Uwielbienia.App.ViewModels;

/// <summary>
/// Kolumna „Pieśń”: przejście na tę pieśń z innej pozycji planu (<see cref="SongPlanItem.Transitions"/>). „Jak w planie” —
/// efekty z paska górnego (widać je zaznaczone); wybranie efektu daje pieśni własne przejście.
/// </summary>
public sealed partial class SongTransitionsViewModel : ObservableObject
{
    private readonly Action<IReadOnlyList<SlideTransition>?> _save;
    private readonly TransitionChoiceViewModel _choice = new();
    private bool _showing;

    /// <param name="save">Zapis w pozycji planu: własne efekty albo <c>null</c> = jak w planie.</param>
    public SongTransitionsViewModel(Action<IReadOnlyList<SlideTransition>?> save)
    {
        _save = save;
        _choice.Chosen += (_, chosen) => OnChosen(chosen);
    }

    public ObservableCollection<TransitionOptionViewModel> Options => _choice.Options;

    /// <summary>Pieśń nie ma własnego przejścia — używa przejścia planu.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ButtonText))]
    public partial bool FollowsPlan { get; set; } = true;

    /// <summary>Np. „Przejście: jak w planie”, „Przejście: Płomień”, „Przejście: losowe (2)”.</summary>
    public string ButtonText => "Przejście: " + (FollowsPlan ? "jak w planie" : _choice.Description);

    /// <summary>Pokazuje wybór pozycji: własny albo (<paramref name="own"/> = <c>null</c>) przejście planu.</summary>
    public void Show(IReadOnlyList<SlideTransition>? own, IReadOnlyList<SlideTransition> plan)
    {
        _showing = true;
        FollowsPlan = own is null;
        _choice.Show(own ?? plan);
        _showing = false;
        OnPropertyChanged(nameof(ButtonText));
    }

    partial void OnFollowsPlanChanged(bool value)
    {
        if (!_showing)
            _save(value ? null : _choice.Selected);
    }

    private void OnChosen(IReadOnlyList<SlideTransition> chosen)
    {
        _showing = true;
        FollowsPlan = false; // zaznaczenie efektu = własne przejście pieśni
        _showing = false;
        OnPropertyChanged(nameof(ButtonText));
        _save(chosen);
    }
}
