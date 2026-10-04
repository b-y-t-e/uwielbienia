using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Uwielbienia.App.Services;
using Uwielbienia.Core.Presentation;

namespace Uwielbienia.App.ViewModels;

/// <summary>
/// Dopasowanie obrazu do rzutnika (jak w pps_viewer): 4 narożniki obrazu projekcji — korekcja trapezu,
/// wielkość, proporcje, przesunięcie. Obsługiwane wprost na ekranie projekcji (mysz, klawisze G/K/R).
/// Dotyczy wszystkiego, co pokazuje projekcja (pieśni, teksty, prezentacje); zapisywane w ustawieniach.
/// </summary>
public sealed partial class ProjectionCalibration : ObservableObject
{
    private readonly ISettingsStore _settings;

    public ProjectionCalibration(ISettingsStore settings)
    {
        _settings = settings;
        Corners = Keystone.FromValues(settings.Current.ProjectionCorners);
    }

    /// <summary>Narożniki: lewy górny, prawy górny, prawy dolny, lewy dolny (0..1 rozmiaru ekranu).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAdjusted))]
    public partial IReadOnlyList<CornerPoint> Corners { get; private set; }

    /// <summary>Tryb kalibracji (K): uchwyty widoczne stale, działają klawisze G, R, Tab, Ctrl+strzałki.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGridVisible))]
    public partial bool IsActive { get; set; }

    /// <summary>Siatka kalibracyjna (G w trybie kalibracji).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGridVisible))]
    public partial bool ShowGrid { get; set; } = true;

    public bool IsGridVisible => IsActive && ShowGrid;

    /// <summary>Narożnik przesuwany klawiaturą (Ctrl+strzałki) — ostatnio chwycony albo wybrany Tabem.</summary>
    [ObservableProperty]
    public partial int SelectedCorner { get; set; }

    public bool IsAdjusted => !Keystone.IsFullScreen(Corners);

    /// <summary>Zmiana w trakcie przeciągania (bez zapisu); odrzucona, gdy czworokąt nie byłby wypukły.</summary>
    public bool TrySet(IReadOnlyList<CornerPoint> corners)
    {
        if (!Keystone.IsConvex(corners))
            return false;
        Corners = corners;
        return true;
    }

    public void Save() =>
        _settings.Update(s => s with { ProjectionCorners = IsAdjusted ? Keystone.ToValues(Corners) : null });

    public void SelectNextCorner() => SelectedCorner = (SelectedCorner + 1) % 4;

    /// <summary>Przesuwa zaznaczony narożnik o ułamek ekranu.</summary>
    public void Nudge(double dx, double dy)
    {
        var next = Corners.ToList();
        next[SelectedCorner] = Keystone.Clamp(new CornerPoint(next[SelectedCorner].X + dx, next[SelectedCorner].Y + dy));
        if (TrySet(next))
            Save();
    }

    /// <summary>Powiększa (&gt; 1) albo zmniejsza obraz względem jego środka: szerokość i wysokość osobno.</summary>
    public void Scale(double factorX, double factorY)
    {
        if (Keystone.Scale(Corners, factorX, factorY) is { } scaled)
        {
            Corners = scaled;
            Save();
        }
    }

    [RelayCommand]
    private void Reset()
    {
        Corners = Keystone.FullScreen;
        Save();
    }
}
