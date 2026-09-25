using System.Text.Json;
using Uwielbienia.Core;
using Uwielbienia.Core.Plans;
using Uwielbienia.Core.Presentation;
using Uwielbienia.Core.Songs;

namespace Uwielbienia.Link;

/// <summary>Zamienia polecenia ze strony www na wywołania <see cref="ILiveControl"/>. Nie wie nic o sieci.</summary>
public sealed class RemoteCommandHandler(
    ILiveControl control,
    ILiveStateSource live,
    ActivePlan plan,
    ISongLibrary library,
    ISongSearch search,
    ILiveItemFactory itemFactory,
    IUiDispatcher dispatcher)
{
    public async Task<byte[]> HandleAsync(ReadOnlyMemory<byte> request)
    {
        RemoteCommand? command;
        try
        {
            command = JsonSerializer.Deserialize<RemoteCommand>(request.Span, RemoteJson.Options);
        }
        catch (JsonException)
        {
            command = null;
        }

        var reply = command is null
            ? new CommandReply(false, "Nieczytelne polecenie.")
            : await dispatcher.InvokeAsync(() => Execute(command));
        return RemoteJson.Serialize(reply);
    }

    private CommandReply Execute(RemoteCommand command)
    {
        switch (command.Cmd)
        {
            case "hello":
                return new CommandReply(true, State: StateMessage.From(live.State), Plan: CurrentPlan());
            case "next":
                control.Next();
                break;
            case "prev":
                control.Previous();
                break;
            case "blank":
                control.ToggleBlank();
                break;
            case "goto" when command.Slide is { } slide:
                control.GoToSlide(slide);
                break;
            case "showItem" when command.ItemId is { } id && plan.FindLiveItem(id) is { } item:
                control.Show(item);
                break;
            case "showSong" when command.SongId is { } songId && library.Find(songId) is { } song:
                control.Show(itemFactory.Create(song));
                break;
            case "search":
                return new CommandReply(true, Results: search.Search(command.Query ?? "", 30)
                    .Select(s => new SongHit(s.Id, s.Number, s.Title, s.FirstLine)).ToList());
            default:
                return new CommandReply(false, $"Nieznane polecenie: {command.Cmd}");
        }
        return new CommandReply(true, State: StateMessage.From(live.State));
    }

    public PlanMessage CurrentPlan() => PlanMessage.From(plan.Plan, plan.Playlist);
}
