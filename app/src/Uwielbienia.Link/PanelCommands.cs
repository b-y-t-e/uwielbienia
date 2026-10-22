using Uwielbienia.Core.Plans;
using Uwielbienia.Core.Presentation;
using Uwielbienia.Core.Songs;

namespace Uwielbienia.Link;

/// <summary>
/// Polecenia panelu (drugi komputer, tablet): plany, plan i układ części — te same operacje Core, których używa okno
/// operatora (<see cref="PlanLibrary"/>, <see cref="PlanActions"/>). Protokół: <c>PanelProtocol.cs</c>.
/// </summary>
public sealed class PanelCommands(
    PlanLibrary plans,
    PlanActions actions,
    ActivePlan plan,
    ISongLibrary library,
    ISlideBuilder slides,
    IPlanItemTypes itemTypes)
{
    /// <summary>Panel dodał pozycję — plan w oknie operatora na chwilę ją wyróżnia (zaznaczenie się nie zmienia).</summary>
    public event EventHandler<PlanItem>? ItemAdded;

    public static bool Handles(string cmd) =>
        cmd.StartsWith("plan", StringComparison.Ordinal) || cmd.StartsWith("item", StringComparison.Ordinal);

    public PlansMessage CurrentPlans() => PlansMessage.From(plans, plan.Plan?.Id);

    /// <summary>Wykonuje polecenie panelu (na wątku interfejsu).</summary>
    public async Task<CommandReply> ExecuteAsync(RemoteCommand command)
    {
        var (planErrorBefore, libraryErrorBefore) = (plan.SaveError, plans.SaveError);
        var reply = await ExecuteCommandAsync(command);
        return (NewError(plan.SaveError, planErrorBefore) ?? NewError(plans.SaveError, libraryErrorBefore)) is { } error
            ? new CommandReply(false, $"Nie udało się: {error.Message}", Plan: CurrentPlan())
            : reply;
    }

    private static Exception? NewError(Exception? now, Exception? before) => now is not null && now != before ? now : null;

    private async Task<CommandReply> ExecuteCommandAsync(RemoteCommand command)
    {
        switch (command.Cmd)
        {
            case "plans":
                return new CommandReply(true, Plans: CurrentPlans());
            case "planOpen" when command.PlanId is { } id:
                return Plans(plans.Open(id), "Nie ma takiego planu.");
            case "planCreate" when KindOf(command.Kind) is { } kind:
                return PlanLibrary.IsValid(command.Name, command.Date, kind)
                    ? Plans(plans.Create(command.Name!, command.Date, kind), "")
                    : new CommandReply(false, kind == PlanKind.Event ? "Podaj nazwę i datę." : "Podaj nazwę.");
            case "planRename" when command.PlanId is { } id:
                return Plans(plans.Rename(id, command.Name ?? "", command.Date), "Podaj nazwę (i datę wydarzenia).");
            case "planCopyToToday" when command.PlanId is { } id:
                return Plans(plans.CopyToToday(id), "Nie ma takiego planu.");
            case "planSaveAsTemplate" when command.PlanId is { } id:
                return Plans(plans.SaveAsTemplate(id), "Nie ma takiego planu.");
            case "planDelete" when command.PlanId is { } id:
                return plans.Delete(id)
                    ? new CommandReply(true, Plans: CurrentPlans())
                    : new CommandReply(false, "Otwartego planu nie można usunąć.");
            case "planAdd":
                return Add(command.SongId, command.Index);
            case "planMove" when command.ItemId is { } id && command.Index is { } index:
                actions.Move(id, index);
                return Ok();
            case "planRemove" when command.ItemId is { } id:
                actions.Remove(id);
                return Ok();
            case "planDuplicate" when command.ItemId is { } id:
                if (await actions.DuplicateAsync(id, select: false) is not { } copy)
                    return new CommandReply(false, "Nie udało się zduplikować pozycji.");
                ItemAdded?.Invoke(this, copy);
                return Ok();
            case "item" when command.ItemId is { } id:
                return Item(id);
            case "itemLayout" when command.ItemId is { } id && command.Layout is { } layout:
                return actions.SetLayout(id, layout.Select(e => new LayoutEntry(e.Code, e.Sung)).ToList()) is null
                    ? new CommandReply(false, "To nie jest pieśń z tego planu.")
                    : Item(id);
            default:
                return new CommandReply(false, $"Nieznane polecenie: {command.Cmd}");
        }
    }

    private CommandReply Add(string? songId, int? index)
    {
        if (plan.Plan is not { } current)
            return new CommandReply(false, "W aplikacji nie ma otwartego planu.");
        if (songId is null || library.Find(songId) is not { } song)
            return new CommandReply(false, "Nie ma takiej pieśni.");
        var item = actions.InsertAt(song, index ?? current.Items.Count, select: false);
        ItemAdded?.Invoke(this, item);
        return new CommandReply(true, Plan: CurrentPlan(), Added: new PlanItemMessage(item.Id, song.Id, song.Number,
            song.DisplayTitle, song.IsText ? "text" : "song"));
    }

    private CommandReply Item(Guid itemId) =>
        plan.Plan?.Items.FirstOrDefault(i => i.Id == itemId) is { } item && itemTypes.Describe(item) is { } info
            ? new CommandReply(true, Item: ItemMessage.From(item, info, plan.FindLiveItem(itemId), library, slides))
            : new CommandReply(false, "Tej pozycji nie ma już w planie.");

    private CommandReply Ok() => new(true, Plan: CurrentPlan());

    private PlanMessage CurrentPlan() => PlanMessage.From(plan.Plan, plan.Playlist, itemTypes);

    private CommandReply Plans(Plan? changed, string error) =>
        changed is null ? new CommandReply(false, error) : new CommandReply(true, Plans: CurrentPlans(), Plan: CurrentPlan());

    private static PlanKind? KindOf(string? kind) => kind switch
    {
        "event" or null => PlanKind.Event,
        "template" => PlanKind.Template,
        _ => null,
    };
}
