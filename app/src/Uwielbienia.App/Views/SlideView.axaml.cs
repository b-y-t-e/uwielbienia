using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Uwielbienia.App.ViewModels;

namespace Uwielbienia.App.Views;

/// <summary>Slajd tak, jak widzi go sala. Używany w oknie projekcji i w kolumnie „Na ekranie”.</summary>
public partial class SlideView : UserControl
{
    public static readonly StyledProperty<ProjectedPage?> PageProperty =
        AvaloniaProperty.Register<SlideView, ProjectedPage?>(nameof(Page));

    public static readonly StyledProperty<IBrush?> SlideBackgroundProperty =
        AvaloniaProperty.Register<SlideView, IBrush?>(nameof(SlideBackground), Brushes.Black);

    public static readonly StyledProperty<IBrush?> SlideForegroundProperty =
        AvaloniaProperty.Register<SlideView, IBrush?>(nameof(SlideForeground), Brushes.White);

    public static readonly StyledProperty<IBrush?> SlideMutedProperty =
        AvaloniaProperty.Register<SlideView, IBrush?>(nameof(SlideMuted), Brushes.Gray);

    public static readonly StyledProperty<IBrush?> SlideTitleProperty =
        AvaloniaProperty.Register<SlideView, IBrush?>(nameof(SlideTitle), Brushes.Goldenrod);

    public static readonly StyledProperty<IBrush?> SlideAccentProperty =
        AvaloniaProperty.Register<SlideView, IBrush?>(nameof(SlideAccent), Brushes.Goldenrod);

    public SlideView() => InitializeComponent();

    public ProjectedPage? Page
    {
        get => GetValue(PageProperty);
        set => SetValue(PageProperty, value);
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

    public IBrush? SlideTitle
    {
        get => GetValue(SlideTitleProperty);
        set => SetValue(SlideTitleProperty, value);
    }

    public IBrush? SlideAccent
    {
        get => GetValue(SlideAccentProperty);
        set => SetValue(SlideAccentProperty, value);
    }
}

public static class SlideConverters
{
    /// <summary>„ ×2” po wersie śpiewanym kilka razy — dyskretna wskazówka dla śpiewających.</summary>
    public static readonly IValueConverter RepeatSuffix =
        new FuncValueConverter<int, string>(n => n > 1 ? $"  ×{n}" : "");

    /// <summary>Kolor wersu: [czy powtarza tytuł, kolor tytułu, kolor tekstu].</summary>
    public static readonly IMultiValueConverter LineBrush =
        new FuncMultiValueConverter<object?, IBrush?>(values =>
        {
            var list = values.ToList();
            return list is [true, IBrush title, _] ? title : list.ElementAtOrDefault(2) as IBrush;
        });
}
