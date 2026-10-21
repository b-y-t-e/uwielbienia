using Avalonia;
using Avalonia.Controls;

namespace Uwielbienia.App.Views;

/// <summary>
/// Poziomy rząd przycisków z ikoną i napisem: gdy napisy się nie mieszczą, dostaje klasę <c>compact</c>
/// (style chowają napisy, zostają ikony z podpowiedzią). Napisy wracają, gdy znów jest na nie miejsce.
/// </summary>
public sealed class FitLabelsPanel : StackPanel
{
    public const string CompactClass = "compact";

    // Szerokość rzędu z napisami, zmierzona ostatnio bez klasy compact.
    private double _fullWidth;

    public FitLabelsPanel() => Orientation = Avalonia.Layout.Orientation.Horizontal;

    protected override Size MeasureOverride(Size availableSize)
    {
        var compact = Classes.Contains(CompactClass);
        if (compact && availableSize.Width >= _fullWidth)
            SetCompact(compact = false);
        var size = base.MeasureOverride(availableSize);
        if (!compact)
        {
            _fullWidth = size.Width;
            if (size.Width > availableSize.Width)
            {
                SetCompact(true);
                size = base.MeasureOverride(availableSize);
            }
        }
        return size;
    }

    private void SetCompact(bool compact) => Classes.Set(CompactClass, compact);
}
