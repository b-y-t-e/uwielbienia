namespace Uwielbienia.Core.Presentation;

/// <summary>Punkt na ekranie projekcji: x, y jako część szerokości i wysokości (0..1).</summary>
public readonly record struct CornerPoint(double X, double Y);

/// <summary>
/// Dopasowanie obrazu do rzutnika (korekcja trapezu, zmniejszenie, przesunięcie): cały obraz projekcji
/// rozpięty perspektywicznie na czworokącie z 4 narożników — lewy górny, prawy górny, prawy dolny, lewy dolny.
/// </summary>
public static class Keystone
{
    public static readonly IReadOnlyList<CornerPoint> FullScreen =
        [new(0, 0), new(1, 0), new(1, 1), new(0, 1)];

    public static bool IsFullScreen(IReadOnlyList<CornerPoint> corners) => corners.SequenceEqual(FullScreen);

    /// <summary>Narożniki z ustawień (8 liczb: x, y kolejnych narożników); błędne = pełny ekran.</summary>
    public static IReadOnlyList<CornerPoint> FromValues(IReadOnlyList<double>? values)
    {
        if (values is not { Count: 8 } || values.Any(v => !double.IsFinite(v)))
            return FullScreen;
        var corners = Enumerable.Range(0, 4).Select(i => Clamp(new CornerPoint(values[2 * i], values[2 * i + 1]))).ToList();
        return IsConvex(corners) ? corners : FullScreen;
    }

    public static double[] ToValues(IReadOnlyList<CornerPoint> corners) => [.. corners.SelectMany(c => new[] { c.X, c.Y })];

    public static CornerPoint Clamp(CornerPoint p) => new(Math.Clamp(p.X, 0, 1), Math.Clamp(p.Y, 0, 1));

    /// <summary>Czworokąt wypukły, nieprzecięty i niezdegenerowany — tylko taki daje poprawną perspektywę.</summary>
    public static bool IsConvex(IReadOnlyList<CornerPoint> q)
    {
        if (q.Count != 4)
            return false;
        var sign = 0;
        for (var i = 0; i < 4; i++)
        {
            var c = Cross(q[i], q[(i + 1) % 4], q[(i + 2) % 4]);
            if (Math.Abs(c) < 1e-6)
                return false;
            var s = Math.Sign(c);
            if (sign != 0 && s != sign)
                return false;
            sign = s;
        }
        return true;
    }

    public static bool Contains(IReadOnlyList<CornerPoint> q, CornerPoint p)
    {
        var sign = 0;
        for (var i = 0; i < 4; i++)
        {
            var s = Math.Sign(Cross(q[i], q[(i + 1) % 4], p));
            if (s == 0)
                continue;
            if (sign != 0 && s != sign)
                return false;
            sign = s;
        }
        return true;
    }

    /// <summary>Przesuwa cały obraz, nie dalej niż do krawędzi ekranu.</summary>
    public static IReadOnlyList<CornerPoint> Move(IReadOnlyList<CornerPoint> q, double dx, double dy)
    {
        dx = Math.Clamp(dx, -q.Min(c => c.X), 1 - q.Max(c => c.X));
        dy = Math.Clamp(dy, -q.Min(c => c.Y), 1 - q.Max(c => c.Y));
        return q.Select(c => new CornerPoint(c.X + dx, c.Y + dy)).ToList();
    }

    /// <summary>
    /// Zmienia szerokość (<paramref name="factorX"/>) i wysokość (<paramref name="factorY"/>) obrazu względem jego
    /// środka; <c>null</c>, gdy wyszedłby poza ekran.
    /// </summary>
    public static IReadOnlyList<CornerPoint>? Scale(IReadOnlyList<CornerPoint> q, double factorX, double factorY)
    {
        var cx = q.Average(c => c.X);
        var cy = q.Average(c => c.Y);
        var scaled = q.Select(c => new CornerPoint(cx + (c.X - cx) * factorX, cy + (c.Y - cy) * factorY)).ToList();
        return scaled.All(c => c.X is >= 0 and <= 1 && c.Y is >= 0 and <= 1) && IsConvex(scaled) ? scaled : null;
    }

    public static IReadOnlyList<CornerPoint>? Scale(IReadOnlyList<CornerPoint> q, double factor) => Scale(q, factor, factor);

    /// <summary>
    /// Macierz perspektywy 3×3 (wierszami, dla wektora kolumnowego) przenosząca prostokąt
    /// 0..<paramref name="width"/> × 0..<paramref name="height"/> na czworokąt <paramref name="quad"/> (w tych samych jednostkach).
    /// Metoda Heckberta: kwadrat jednostkowy → czworokąt, poprzedzony skalowaniem prostokąta do kwadratu.
    /// </summary>
    public static double[] RectToQuad(double width, double height, IReadOnlyList<CornerPoint> quad)
    {
        double x0 = quad[0].X, y0 = quad[0].Y, x1 = quad[1].X, y1 = quad[1].Y;
        double x2 = quad[2].X, y2 = quad[2].Y, x3 = quad[3].X, y3 = quad[3].Y;

        double dx1 = x1 - x2, dx2 = x3 - x2, dx3 = x0 - x1 + x2 - x3;
        double dy1 = y1 - y2, dy2 = y3 - y2, dy3 = y0 - y1 + y2 - y3;

        double g = 0, h = 0;
        if (Math.Abs(dx3) > 1e-12 || Math.Abs(dy3) > 1e-12)
        {
            var den = dx1 * dy2 - dx2 * dy1;
            if (Math.Abs(den) < 1e-15)
                return [1, 0, 0, 0, 1, 0, 0, 0, 1];
            g = (dx3 * dy2 - dx2 * dy3) / den;
            h = (dx1 * dy3 - dx3 * dy1) / den;
        }

        // kwadrat jednostkowy → czworokąt, kolumny przeskalowane przez 1/szerokość i 1/wysokość
        return
        [
            (x1 - x0 + g * x1) / width, (x3 - x0 + h * x3) / height, x0,
            (y1 - y0 + g * y1) / width, (y3 - y0 + h * y3) / height, y0,
            g / width, h / height, 1,
        ];
    }

    /// <summary>Punkt po przekształceniu macierzą z <see cref="RectToQuad"/>.</summary>
    public static CornerPoint Apply(double[] m, double x, double y)
    {
        var w = m[6] * x + m[7] * y + m[8];
        return new CornerPoint((m[0] * x + m[1] * y + m[2]) / w, (m[3] * x + m[4] * y + m[5]) / w);
    }

    private static double Cross(CornerPoint o, CornerPoint a, CornerPoint b) =>
        (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);
}
