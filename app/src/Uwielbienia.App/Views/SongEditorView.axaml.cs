using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Uwielbienia.App.ViewModels;

namespace Uwielbienia.App.Views;

/// <summary>Edytor pieśni i tekstów w kolumnie „Pieśń”.</summary>
public partial class SongEditorView : UserControl
{
    public SongEditorView()
    {
        InitializeComponent();
        // Kolejność części: przeciąganie za uchwyt ⠿ (jak w planie), nie za pole tekstu.
        _ = new ListReorderDrag(PartsList,
            source => (source as Visual)?.GetSelfAndVisualAncestors().OfType<Control>()
                .FirstOrDefault(c => c.Classes.Contains("editorPartHandle"))?.DataContext as PartEditorViewModel,
            (part, index) => (DataContext as SongEditorViewModel)?.MovePart((PartEditorViewModel)part, index));
    }

    /// <summary>Kursor od razu w tytule.</summary>
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        Dispatcher.UIThread.Post(() => TitleBox.Focus());
    }
}
