using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Uwielbienia.Core.Presentation;

namespace Uwielbienia.App.Views;

/// <summary>Slajd tak, jak widzi go sala. Używany w oknie projekcji i w podglądzie „Na ekranie”.</summary>
public partial class SlideView : UserControl
{
    public static readonly StyledProperty<Slide?> SlideProperty =
        AvaloniaProperty.Register<SlideView, Slide?>(nameof(Slide));

    public static readonly StyledProperty<IBrush?> SlideBackgroundProperty =
        AvaloniaProperty.Register<SlideView, IBrush?>(nameof(SlideBackground), Brushes.Black);

    public static readonly StyledProperty<IBrush?> SlideForegroundProperty =
        AvaloniaProperty.Register<SlideView, IBrush?>(nameof(SlideForeground), Brushes.White);

    public static readonly StyledProperty<IBrush?> SlideMutedProperty =
        AvaloniaProperty.Register<SlideView, IBrush?>(nameof(SlideMuted), Brushes.Gray);

    public SlideView() => InitializeComponent();

    public Slide? Slide
    {
        get => GetValue(SlideProperty);
        set => SetValue(SlideProperty, value);
    }

    public IBrush? SlideBackground
    {
        get => GetValue(SlideBackgroundProperty);
        set => SetValue(SlideBackgroundProperty, value);
    }

    public IBrush? SlideForeground
    {
        get => GetValue(SlideForegroundProperty);
        set => SetValue(SlideForegroundProperty, value);
    }

    public IBrush? SlideMuted
    {
        get => GetValue(SlideMutedProperty);
        set => SetValue(SlideMutedProperty, value);
    }
}

public static class SlideConverters
{
    /// <summary>„ ×2” po wersie śpiewanym kilka razy — dyskretna wskazówka dla śpiewających.</summary>
    public static readonly IValueConverter RepeatSuffix =
        new FuncValueConverter<int, string>(n => n > 1 ? $"  ×{n}" : "");
}
