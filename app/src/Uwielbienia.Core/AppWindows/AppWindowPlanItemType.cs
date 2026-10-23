using Uwielbienia.Core.Plans;
using Uwielbienia.Core.Presentation;

namespace Uwielbienia.Core.AppWindows;

/// <summary>
/// Rodzaj pozycji „aplikacja”: jeden slajd <see cref="AppWindowSlide"/> — obraz okna wybranego programu. Program nie musi
/// być uruchomiony: do tego czasu ekran jest czarny (<see cref="IAppWindowCapture.Show"/>).
/// </summary>
public sealed class AppWindowPlanItemType(IAppWindowCapture capture) : IPlanItemType
{
    // Treść pozycji się nie zmienia — uruchomienie programu widać tylko na ekranie.
    public event EventHandler? Changed { add { } remove { } }

    public bool Handles(PlanItem item) => item is AppWindowPlanItem;

    public PlanItemInfo? Describe(PlanItem item) =>
        item is AppWindowPlanItem app ? new PlanItemInfo(app.Title, null, PlanItemKind.App, capture.Unavailable) : null;

    public LiveItem? Present(PlanItem item) =>
        item is AppWindowPlanItem app
            ? new LiveItem(item.Id, $"app:{app.Process}|{app.WindowTitle}", null, app.Title,
                [new AppWindowSlide(app.Process, app.WindowTitle)], ShowTitle: false)
            : null;
}
