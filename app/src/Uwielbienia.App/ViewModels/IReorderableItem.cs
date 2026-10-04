namespace Uwielbienia.App.ViewModels;

/// <summary>Wiersz listy, którego kolejność zmienia się przeciąganiem (plan, części pieśni).</summary>
public interface IReorderableItem
{
    /// <summary>Wiersz jest właśnie przeciągany.</summary>
    bool IsDragged { get; set; }

    /// <summary>Znacznik miejsca upuszczenia nad wierszem.</summary>
    bool IsDropBefore { get; set; }

    /// <summary>Znacznik miejsca upuszczenia pod wierszem (tylko ostatni wiersz).</summary>
    bool IsDropAfter { get; set; }
}
