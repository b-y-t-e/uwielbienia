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

    /// <summary>Tytuł pieśni — ciepły odcień świecy, odróżnia go od śpiewanego tekstu.</summary>
    [ObservableProperty]
    public partial IBrush Title { get; private set; } = Brushes.Goldenrod;

    /// <summary>Kolor świecy — linia pod tytułem pieśni.</summary>
    [ObservableProperty]
    public partial IBrush Accent { get; private set; } = Brushes.Goldenrod;

    private void Apply(AppTheme theme)
    {
        var dark = theme == AppTheme.Dark;
        // Ciemny: czysta czerń (projektor nie świeci) i ciepła kość słoniowa zamiast ostrej bieli.
        Background = dark ? Brushes.Black : Brushes.White;
        Foreground = new SolidColorBrush(Color.Parse(dark ? "#F4ECDD" : "#15182A"));
        Muted = new SolidColorBrush(Color.Parse(dark ? "#9C968C" : "#6B7080"));
        Accent = new SolidColorBrush(Color.Parse(dark ? "#C9A15A" : "#D2B07A"));
        Title = new SolidColorBrush(Color.Parse(dark ? "#D8C4A0" : "#A68B62"));
    }
}
