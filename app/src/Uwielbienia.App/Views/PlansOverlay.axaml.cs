using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Uwielbienia.App.ViewModels;

namespace Uwielbienia.App.Views;

public partial class PlansOverlay : UserControl
{
    public PlansOverlay() => InitializeComponent();

    /// <summary>Nowy plan: kursor od razu w polu nazwy (nazwa jest wymagana).</summary>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is PlansViewModel plans)
            plans.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(PlansViewModel.IsCreating) && plans.IsCreating)
                    Dispatcher.UIThread.Post(() => NameBox.Focus());
            };
    }

    /// <summary>Dwuklik na planie z listy otwiera go — tak samo jak przycisk „Otwórz”.</summary>
    private void OnPlanDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Control { DataContext: PlanSummaryViewModel plan } || DataContext is not PlansViewModel plans)
            return;
        plans.Selected = plan;
        plans.OpenSelectedCommand.Execute(null);
        e.Handled = true;
    }
}
