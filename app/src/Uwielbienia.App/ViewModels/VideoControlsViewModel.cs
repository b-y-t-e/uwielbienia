using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Uwielbienia.App.Services;
using Uwielbienia.Core.Videos;

namespace Uwielbienia.App.ViewModels;

/// <summary>Film w kolumnie „Na ekranie” i na rzutniku: podgląd, pauza, ±15 s, przewijanie i czas.</summary>
public sealed partial class VideoControlsViewModel : ObservableObject
{
    private readonly IVideoPlayer _player;

    public VideoControlsViewModel(IVideoPlayer player, VideoFrames frames)
    {
        _player = player;
        Frames = frames;
        player.StatusChanged += (_, _) => Refresh();
    }

    public VideoFrames Frames { get; }

    public bool IsPlaying => _player.Status.IsPlaying;

    public string? Error => _player.Status.Error;

    public double DurationSeconds => Math.Max(1, _player.Status.Duration.TotalSeconds);

    /// <summary>Suwak: zmiana przez operatora przewija film (stan odtwarzacza tylko go przesuwa).</summary>
    public double PositionSeconds
    {
        get => _player.Status.Position.TotalSeconds;
        set
        {
            // Dopóki VLC nie poda długości, suwak ma maksimum 1 i przycina wartość — to nie jest przewinięcie
            // (inaczej powrót do filmu przerwanego w 2:00 zaczynałby od 0:01).
            if (_player.Status.Duration > TimeSpan.Zero && Math.Abs(value - PositionSeconds) > 0.5)
                _player.Seek(TimeSpan.FromSeconds(value));
        }
    }

    public string TimeText => $"{Format(_player.Status.Position)} / {Format(_player.Status.Duration)}";

    [RelayCommand]
    private void TogglePause() => _player.TogglePause();

    public static readonly TimeSpan SkipStep = TimeSpan.FromSeconds(15);

    [RelayCommand]
    private void SkipBack() => _player.Skip(-SkipStep);

    [RelayCommand]
    private void SkipForward() => _player.Skip(SkipStep);

    private static string Format(TimeSpan time) =>
        time.TotalHours >= 1 ? time.ToString(@"h\:mm\:ss") : time.ToString(@"m\:ss");

    private void Refresh()
    {
        OnPropertyChanged(nameof(IsPlaying));
        OnPropertyChanged(nameof(Error));
        OnPropertyChanged(nameof(DurationSeconds));
        OnPropertyChanged(nameof(PositionSeconds));
        OnPropertyChanged(nameof(TimeText));
    }
}
