using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace Uwielbienia.App.Views;

/// <summary>Edytor pieśni i tekstów w kolumnie „Pieśń”.</summary>
public partial class SongEditorView : UserControl
{
    public SongEditorView() => InitializeComponent();

    /// <summary>Kursor od razu w tytule.</summary>
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        Dispatcher.UIThread.Post(() => TitleBox.Focus());
    }
}
