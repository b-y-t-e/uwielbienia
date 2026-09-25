using CommunityToolkit.Mvvm.ComponentModel;
using Uwielbienia.App.Services;
using Uwielbienia.Core.Presentation;

namespace Uwielbienia.App.ViewModels;

/// <summary>Okno projekcji: slajd + (w trybie jednego urządzenia) nakładka szybkiego wyboru pieśni.</summary>
public sealed partial class ProjectionViewModel(
    LiveViewModel live,
    SearchViewModel quickPick,
    ILiveControl control,
    IProjectionController projection) : ObservableObject
{
    public LiveViewModel Live { get; } = live;

    public SearchViewModel QuickPick { get; } = quickPick;

    public ILiveControl Control { get; } = control;

    public IProjectionController Projection { get; } = projection;

    [ObservableProperty]
    public partial bool IsQuickPickOpen { get; set; }

    public void OpenQuickPick(string? firstChar)
    {
        QuickPick.Query = firstChar ?? "";
        IsQuickPickOpen = true;
    }

    public void CloseQuickPick()
    {
        QuickPick.Clear();
        IsQuickPickOpen = false;
    }
}
