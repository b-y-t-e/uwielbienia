using System.Text.Json;
using Uwielbienia.Core;
using Uwielbienia.Core.Kiosks;
using Uwielbienia.Core.Plans;
using Uwielbienia.Core.Presentation;
using Uwielbienia.Core.Songs;
using Uwielbienia.Core.Videos;

namespace Uwielbienia.Link;

/// <summary>Źródło pieśni dodanych z telefonu — plan w oknie operatora na chwilę je podświetla.</summary>
public interface IRemoteAdditions
{
    event EventHandler<PlanItem>? ItemAdded;
}

/// <summary>
/// Zamienia polecenia ze strony www na wywołania <see cref="ILiveControl"/>. Nie wie nic o sieci — kto pyta i co mu
/// wolno, podaje <see cref="RemoteCaller"/>.
/// </summary>
public sealed class RemoteCommandHandler(
    ILiveControl control,
    ILiveStateSource live,
    ActivePlan plan,
    ISongLibrary library,
    ISongSearch search,
    ISongPresenter songs,
    IUiDispatcher dispatcher,
    IPlanItemTypes itemTypes,
    IVideoPlayer? video = null,
    UndoableAdditions? undoable = null,
    KioskRotation? kiosk = null,
    IImagePreviews? previews = null) : IRemoteAdditions
{
    private readonly UndoableAdditions _undoable = undoable ?? new(live);

    /// <summary>Telefon dodał pieśń do planu — plan w oknie operatora na chwilę ją podświetla.</summary>
    public event EventHandler<PlanItem>? ItemAdded;

    public async Task<byte[]> HandleAsync(ReadOnlyMemory<byte> request, RemoteCaller? caller = null)
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
            : await dispatcher.InvokeAsync(() => Execute(command, caller ?? RemoteCaller.Pilot));
        // pełny stan po (ponownym) połączeniu — także bieżący element kiosku
        if (command?.Cmd == "hello" && reply.Ok)
            reply = reply with { Kiosk = await CurrentKioskAsync() };
        return RemoteJson.Serialize(reply);
    }

    private CommandReply Execute(RemoteCommand command, RemoteCaller caller)
    {
        switch (command.Cmd)
        {
            case "hello":
                return new CommandReply(true, State: StateMessage.From(live.State), Plan: CurrentPlan(), Video: CurrentVideo(),
                    Device: DeviceMessage.From(caller));
            case "addNext" or "undoAdd" when !caller.CanAddSongs:
                return new CommandReply(false, "To urządzenie nie może dodawać pieśni.");
            case "addNext":
                return AddNext(command.SongId);
            case "undoAdd" when command.ItemId is { } added:
                return UndoAdd(added);
            case "videoToggle" when video is not null:
                video.TogglePause();
                return new CommandReply(true, Video: CurrentVideo());
            case "videoSkip" when video is not null && command.Seconds is { } seconds:
                video.Skip(TimeSpan.FromSeconds(Math.Clamp(seconds, -600, 600)));
                return new CommandReply(true, Video: CurrentVideo());
            case "videoOffset" when video is not null && command.Milliseconds is { } milliseconds && video.IsOnScreen(live.State):
                // telefon wybiera tylko kierunek — krok zawsze 100 ms
                video.ShiftPicture(Math.Sign(milliseconds) * VideoPlayerExtensions.PictureStep);
                return new CommandReply(true, Video: CurrentVideo());
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
                control.Show(songs.Present(song));
                break;
            case "search":
                return new CommandReply(true, Results: search.Search(command.Query ?? "", 30)
                    .Select(s => new SongHit(s.Id, s.Number, s.DisplayTitle, s.FirstLine, s.IsText)).ToList());
            default:
                return new CommandReply(false, $"Nieznane polecenie: {command.Cmd}");
        }
        return new CommandReply(true, State: StateMessage.From(live.State));
    }

    /// <summary>Pieśń lub tekst zaraz za pozycją na ekranie (na koniec, gdy ekran pokazuje coś spoza planu).</summary>
    private CommandReply AddNext(string? songId)
    {
        if (plan.Plan is null)
            return new CommandReply(false, "W aplikacji nie ma otwartego planu.");
        if (songId is null || library.Find(songId) is not { } song)
            return new CommandReply(false, "Nie ma takiej pieśni.");
        var item = SongPlanItem.For(song.Id);
        plan.Update(p => p.InsertAfter(live.State.Item?.PlanItemId, item));
        _undoable.Add(item.Id);
        ItemAdded?.Invoke(this, item);
        return new CommandReply(true, Added: new PlanItemMessage(item.Id, song.Id, song.Number, song.DisplayTitle,
            song.IsText ? "text" : "song"));
    }

    /// <summary>„Cofnij” po dodaniu: tylko własne dodanie z telefonu i tylko póki pieśni nie pokazano.</summary>
    private CommandReply UndoAdd(Guid itemId)
    {
        if (!_undoable.TryTake(itemId))
            return new CommandReply(false, "Tej pieśni nie da się już cofnąć.");
        if (plan.Plan?.IndexOf(itemId) is not >= 0)
            return new CommandReply(false, "Tej pieśni nie ma już w planie.");
        plan.Update(p => p.Remove(itemId));
        return new CommandReply(true);
    }

    public PlanMessage CurrentPlan() => PlanMessage.From(plan.Plan, plan.Playlist, itemTypes);

    public VideoMessage? CurrentVideo() => video is null ? null : VideoMessage.From(video);

    /// <summary>Bieżący element kiosku (stan czytany na wątku interfejsu, podgląd obrazu — w tle).</summary>
    public async Task<KioskMessage?> CurrentKioskAsync() =>
        kiosk is null ? null : await await dispatcher.InvokeAsync(() => KioskMessage.FromAsync(kiosk, previews));
}
