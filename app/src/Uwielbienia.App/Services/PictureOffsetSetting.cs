using Uwielbienia.Core.Videos;

namespace Uwielbienia.App.Services;

/// <summary>
/// Przesunięcie obrazu filmu względem dźwięku zależy od rzutnika i nagłośnienia — jedno dla wszystkich filmów:
/// wczytane z ustawień przy starcie, każda zmiana zapisana w <see cref="AppSettings.PictureOffsetMs"/>.
/// </summary>
public static class PictureOffsetSetting
{
    public static void Bind(IVideoPlayer player, ISettingsStore settings)
    {
        player.PictureOffset = TimeSpan.FromMilliseconds(settings.Current.PictureOffsetMs);
        player.StatusChanged += (_, _) =>
        {
            var ms = player.PictureOffsetMs();
            if (ms != settings.Current.PictureOffsetMs)
                settings.Update(s => s with { PictureOffsetMs = ms });
        };
    }
}
