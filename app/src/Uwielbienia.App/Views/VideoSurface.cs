using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Uwielbienia.App.Services;

namespace Uwielbienia.App.Views;

/// <summary>
/// Klatki filmu wpisane w kontrolkę (proporcje zachowane, reszta czarna). Zwykła kontrolka Avalonii —
/// na rzutniku podlega przekształceniu „Dopasuj obraz”, jak tekst i slajdy.
/// </summary>
public sealed class VideoSurface : Control
{
    public static readonly StyledProperty<VideoFrames?> FramesProperty =
        AvaloniaProperty.Register<VideoSurface, VideoFrames?>(nameof(Frames));

    // Klatki przychodzą z wątku dekodera — jedno odświeżenie naraz, bez kolejki zaległych.
    private int _invalidatePending;

    static VideoSurface() => AffectsRender<VideoSurface>(FramesProperty);

    public VideoFrames? Frames
    {
        get => GetValue(FramesProperty);
        set => SetValue(FramesProperty, value);
    }

    /// <summary>Bez narzuconej wysokości (podgląd operatora) — proporcje ekranu 16:9.</summary>
    protected override Size MeasureOverride(Size availableSize) =>
        double.IsInfinity(availableSize.Width)
            ? default
            : new Size(availableSize.Width, double.IsInfinity(availableSize.Height) ? availableSize.Width * 9 / 16 : availableSize.Height);

    public override void Render(DrawingContext context)
    {
        context.FillRectangle(Brushes.Black, new Rect(Bounds.Size));
        if (Frames?.Bitmap is not { } bitmap)
            return;
        var source = new Rect(bitmap.Size);
        var scale = Math.Min(Bounds.Width / source.Width, Bounds.Height / source.Height);
        var size = source.Size * scale;
        var target = new Rect(new Point((Bounds.Width - size.Width) / 2, (Bounds.Height - size.Height) / 2), size);
        context.DrawImage(bitmap, source, target);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FramesProperty && VisualRoot is not null)
        {
            Detach(change.GetOldValue<VideoFrames?>());
            Attach(change.GetNewValue<VideoFrames?>());
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Attach(Frames);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Detach(Frames);
    }

    private void Attach(VideoFrames? frames)
    {
        if (frames is not null)
            frames.FrameReady += OnFrameReady;
    }

    private void Detach(VideoFrames? frames)
    {
        if (frames is not null)
            frames.FrameReady -= OnFrameReady;
    }

    private void OnFrameReady(object? sender, EventArgs e)
    {
        if (Interlocked.Exchange(ref _invalidatePending, 1) == 1)
            return;
        Dispatcher.UIThread.Post(() =>
        {
            _invalidatePending = 0;
            InvalidateVisual();
        }, DispatcherPriority.Render);
    }
}
