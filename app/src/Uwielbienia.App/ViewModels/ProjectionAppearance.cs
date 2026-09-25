using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Uwielbienia.App.Services;

namespace Uwielbienia.App.ViewModels;

/// <summary>
/// Wygląd ekranu projekcji — niezależny od motywu okna operatora. Domyślnie czysta czerń:
/// projektor świeci wtedy najmniej, a sala widzi tylko litery.
/// </summary>
public sealed partial class ProjectionAppearance : ObservableObject
{
    public ProjectionAppearance(ISettingsStore settings)
    {
        Apply(settings.Current.ProjectionTheme);
        settings.Changed += (_, s) => Apply(s.ProjectionTheme);
    }

    [ObservableProperty]
    public partial IBrush Background { get; private set; } = Brushes.Black;

    [ObservableProperty]
    public partial IBrush Foreground { get; private set; } = Brushes.White;

    [ObservableProperty]
    public partial IBrush Muted { get; private set; } = Brushes.Gray;

    private void Apply(AppTheme theme)
    {
        var dark = theme == AppTheme.Dark;
        Background = dark ? Brushes.Black : Brushes.White;
        Foreground = dark ? Brushes.White : Brushes.Black;
        Muted = new SolidColorBrush(dark ? Color.Parse("#8A8F9E") : Color.Parse("#6B7080"));
    }
}
