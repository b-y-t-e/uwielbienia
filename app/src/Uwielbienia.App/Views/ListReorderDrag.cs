using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Uwielbienia.App.ViewModels;

namespace Uwielbienia.App.Views;

/// <summary>
/// Zmiana kolejności wierszy listy zwykłym przeciąganiem myszą (bez systemowego drag &amp; drop):
/// znacznik miejsca upuszczenia między wierszami i przewijanie przy górnej / dolnej krawędzi.
/// Używane w planie i w układzie części pieśni.
/// </summary>
internal sealed class ListReorderDrag
{
    private const double Threshold = 6;
    private const double EdgeSize = 48;
    private const double ScrollStep = 14;
    private static readonly Cursor DragCursor = new(StandardCursorType.SizeNorthSouth);

    private readonly ItemsControl _list;
    private readonly Func<object?, IReorderableItem?> _itemAt;
    private readonly Action<IReorderableItem, int> _move;
    private readonly DispatcherTimer _autoScroll = new() { Interval = TimeSpan.FromMilliseconds(30) };
    private IReorderableItem? _candidate;
    private Point _start;
    private Point _position;
    private bool _started;
    private int _dropIndex = -1;

    /// <param name="itemAt">Wiersz, który można chwycić w miejscu wskazanym przez źródło zdarzenia (albo <c>null</c>).</param>
    /// <param name="move">Przeniesienie wiersza na nowy indeks (liczony po wyjęciu go z listy).</param>
    public ListReorderDrag(ItemsControl list, Func<object?, IReorderableItem?> itemAt, Action<IReorderableItem, int> move)
    {
        _list = list;
        _itemAt = itemAt;
        _move = move;
        // Wiersze bywają przyciskami, które same oznaczają wciśnięcie jako obsłużone — słuchamy mimo to.
        list.AddHandler(InputElement.PointerPressedEvent, OnPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        list.AddHandler(InputElement.PointerMovedEvent, OnMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
        list.AddHandler(InputElement.PointerReleasedEvent, OnReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
        list.AddHandler(InputElement.PointerCaptureLostEvent, (_, _) => End(), RoutingStrategies.Bubble, handledEventsToo: true);
        _autoScroll.Tick += (_, _) => AutoScroll();
    }

    private ScrollViewer? Scroll =>
        _list.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault() ?? _list.FindAncestorOfType<ScrollViewer>();

    /// <summary>Układ współrzędnych: widoczny obszar listy (także gdy lista leży w zewnętrznym ScrollViewerze).</summary>
    private Visual Viewport => (Visual?)Scroll ?? _list;

    private void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_itemAt(e.Source) is not { } item || !e.GetCurrentPoint(_list).Properties.IsLeftButtonPressed)
            return;
        _candidate = item;
        _start = e.GetPosition(Viewport);
        _started = false;
    }

    private void OnMoved(object? sender, PointerEventArgs e)
    {
        if (_candidate is not { } item || !e.GetCurrentPoint(_list).Properties.IsLeftButtonPressed)
            return;

        var position = e.GetPosition(Viewport);
        if (!_started)
        {
            if (Math.Abs(position.Y - _start.Y) < Threshold && Math.Abs(position.X - _start.X) < Threshold)
                return;
            _started = true;
            item.IsDragged = true;
            _list.Cursor = DragCursor;
            _autoScroll.Start();
        }

        _position = position;
        ShowDropMarker(InsertionIndex(position.Y));
        AutoScroll();
        e.Handled = true;
    }

    private void OnReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_started && _candidate is { } item)
        {
            var source = IndexOf(item);
            var target = _dropIndex > source ? _dropIndex - 1 : _dropIndex;
            End();
            if (source >= 0 && target >= 0 && target != source)
                _move(item, target);
            // Upuszczenie nie jest kliknięciem — nie wybieraj wiersza.
            e.Handled = true;
            return;
        }
        End();
    }

    private void AutoScroll()
    {
        if (!_started || Scroll is not { } scroll)
            return;
        var y = _position.Y;
        var step = y < EdgeSize ? -ScrollStep : y > scroll.Bounds.Height - EdgeSize ? ScrollStep : 0;
        var maxOffset = Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height);
        var offset = Math.Clamp(scroll.Offset.Y + step, 0, maxOffset);
        if (step == 0 || offset == scroll.Offset.Y)
            return;
        scroll.Offset = scroll.Offset.WithY(offset);
        ShowDropMarker(InsertionIndex(y));
    }

    /// <summary>
    /// Indeks, przed którym wstawić przeciągany wiersz. Lista tworzy tylko widoczne wiersze — poza nimi
    /// wynik nie może „przeskoczyć” niewidocznych.
    /// </summary>
    private int InsertionIndex(double y)
    {
        var afterLastVisible = 0;
        for (var i = 0; i < _list.ItemCount; i++)
        {
            if (_list.ContainerFromIndex(i) is not Control row || row.TranslatePoint(default, Viewport) is not { } top)
                continue;
            if (y < top.Y + row.Bounds.Height / 2)
                return i;
            afterLastVisible = i + 1;
        }
        return afterLastVisible;
    }

    private void ShowDropMarker(int insertionIndex)
    {
        var source = _candidate is { } dragged ? IndexOf(dragged) : -1;
        // Upuszczenie tuż nad albo tuż pod sobą niczego nie zmienia — bez znacznika.
        var noOp = insertionIndex == source || insertionIndex == source + 1;
        _dropIndex = insertionIndex;
        var count = _list.ItemCount;
        for (var i = 0; i < count; i++)
        {
            if (_list.Items[i] is not IReorderableItem row)
                continue;
            row.IsDropBefore = !noOp && i == insertionIndex;
            row.IsDropAfter = !noOp && i == count - 1 && insertionIndex == count;
        }
    }

    private void End()
    {
        _autoScroll.Stop();
        foreach (var row in _list.Items.OfType<IReorderableItem>())
        {
            row.IsDragged = false;
            row.IsDropBefore = false;
            row.IsDropAfter = false;
        }
        _candidate = null;
        _started = false;
        _dropIndex = -1;
        _list.Cursor = null;
    }

    private int IndexOf(IReorderableItem item) => _list.Items.IndexOf(item);
}
