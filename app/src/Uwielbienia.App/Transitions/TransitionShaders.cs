using SkiaSharp;
using Uwielbienia.Core.Presentation;

namespace Uwielbienia.App.Transitions;

/// <summary>
/// Efekty przejść jako programy SkSL (Skia — ten sam silnik, którym rysuje Avalonia). Każdy dostaje dwa obrazy (<c>from</c>,
/// <c>to</c>) i postęp 0..1 — model gl-transitions (github.com/gl-transitions), więc ich efekty przenoszą się prawie 1:1.
/// Kompilowane raz; błąd kompilacji (np. stary sterownik) = <c>null</c> i zwykłe przenikanie.
/// </summary>
public static class TransitionShaders
{
    private static readonly Dictionary<SlideTransition, SKRuntimeEffect?> Compiled = [];

    /// <summary>
    /// Odstęp między klatkami jednego przejścia dłuższy niż 50 ms pięć razy pod rząd (słaba karta graficzna) albo brak karty
    /// graficznej — przejście by się zacinało, więc od razu nowa strona, a do końca działania programu zwykłe przenikanie.
    /// </summary>
    private static readonly SmoothnessGuard Guard = new(TimeSpan.FromMilliseconds(50), slowFramesInRow: 5);

    public static bool IsSmooth => Guard.IsSmooth;

    /// <summary>Nowe przejście: zgłasza odstępy między swoimi klatkami (czas rysowania na karcie graficznej razem z resztą).</summary>
    public static SmoothnessGuard.Run BeginTransition() => Guard.Begin();

    /// <summary>Brak karty graficznej (wątek renderowania).</summary>
    public static void DisableEffects() => Guard.Disable();

    public static SKRuntimeEffect? For(SlideTransition transition)
    {
        lock (Compiled)
        {
            if (!Compiled.TryGetValue(transition, out var effect))
            {
                effect = SKRuntimeEffect.CreateShader(Common + Body(transition), out var errors);
                if (effect is null)
                    System.Diagnostics.Debug.WriteLine($"Przejście {transition}: {errors}");
                Compiled[transition] = effect;
            }
            return effect;
        }
    }

    /// <summary>Wspólne wejścia i szum (wartościowy + fbm) — wszystkie efekty deklarują te same uniformy.</summary>
    private const string Common = """
        uniform shader from;
        uniform shader to;
        uniform float progress;
        uniform float2 resolution;
        uniform float seed;
        uniform float2 direction;

        float hash21(float2 p) {
            p = fract(p * float2(123.34, 456.21));
            p += dot(p, p + 45.32);
            return fract(p.x * p.y);
        }
        float2 hash22(float2 p) {
            float n = hash21(p);
            return float2(n, hash21(p + n * 17.17));
        }
        float noise(float2 p) {
            float2 i = floor(p);
            float2 f = fract(p);
            float2 u = f * f * (3.0 - 2.0 * f);
            return mix(mix(hash21(i), hash21(i + float2(1.0, 0.0)), u.x),
                       mix(hash21(i + float2(0.0, 1.0)), hash21(i + float2(1.0, 1.0)), u.x), u.y);
        }
        float fbm(float2 p) {
            float v = 0.0;
            float a = 0.5;
            for (int i = 0; i < 5; i++) {
                v += a * noise(p);
                p = p * 2.03 + float2(1.7, 9.2);
                a *= 0.5;
            }
            return v;
        }
        half4 fromAt(float2 uv) { return from.eval(clamp(uv, float2(0.0), float2(1.0)) * resolution); }
        half4 toAt(float2 uv) { return to.eval(clamp(uv, float2(0.0), float2(1.0)) * resolution); }

        """;

    private static string Body(SlideTransition transition) => transition switch
    {
        SlideTransition.Slide => Slide,
        SlideTransition.StainedGlass => StainedGlass,
        SlideTransition.Flame => Flame,
        SlideTransition.Watercolor => Watercolor,
        SlideTransition.LightRay => LightRay,
        SlideTransition.Ripple => Ripple,
        _ => throw new ArgumentOutOfRangeException(nameof(transition)),
    };

    // Przesunięcie: oba slajdy jadą razem w kierunku `direction` (łagodny start i koniec), nowy wjeżdża za starym.
    private const string Slide = """
        half4 main(float2 p) {
            float2 uv = p / resolution;
            float t = progress < 0.5 ? 4.0 * progress * progress * progress : 1.0 - pow(2.0 - 2.0 * progress, 3.0) / 2.0;
            float2 a = uv - direction * t;
            if (a.x >= 0.0 && a.x <= 1.0 && a.y >= 0.0 && a.y <= 1.0)
                return fromAt(a);
            return toAt(uv + direction * (1.0 - t));
        }
        """;

    // Witraż: komórki Woronoja („szybki”) przełączają się od środka na zewnątrz, każda z błyskiem barwnego szkła;
    // ołowiane linie między nimi świecą kolorem świecy w połowie przejścia.
    private const string StainedGlass = """
        half4 main(float2 p) {
            float2 uv = p / resolution;
            float aspect = resolution.x / resolution.y;
            float scale = 6.0;
            float2 q = float2(uv.x * aspect, uv.y) * scale;
            float2 ip = floor(q);
            float2 fp = fract(q);
            float2 mg = float2(0.0);
            float2 mr = float2(0.0);
            float md = 8.0;
            for (int j = -1; j <= 1; j++) {
                for (int i = -1; i <= 1; i++) {
                    float2 g = float2(float(i), float(j));
                    float2 r = g + hash22(ip + g + seed * 31.0) - fp;
                    float d = dot(r, r);
                    if (d < md) { md = d; mr = r; mg = g; }
                }
            }
            md = 8.0;
            for (int j = -2; j <= 2; j++) {
                for (int i = -2; i <= 2; i++) {
                    float2 g = mg + float2(float(i), float(j));
                    float2 r = g + hash22(ip + g + seed * 31.0) - fp;
                    if (dot(mr - r, mr - r) > 0.00001)
                        md = min(md, dot(0.5 * (mr + r), normalize(r - mr)));
                }
            }
            float2 cell = ip + mg;
            float2 center = (cell + hash22(cell + seed * 31.0)) / scale;
            float dist = length(center - float2(0.5 * aspect, 0.5)) / length(float2(0.5 * aspect, 0.5));
            float start = clamp(dist, 0.0, 1.0) * 0.55 + hash21(cell + seed * 7.0) * 0.2;
            float t = smoothstep(start, start + 0.25, progress);
            float flash = sin(t * 3.14159);
            half4 glass = toAt(center / float2(aspect, 1.0));
            half3 col = mix(fromAt(uv).rgb, toAt(uv).rgb, t);
            col = mix(col, min(glass.rgb * 1.35 + 0.06, half3(1.0)), 0.45 * flash);
            float lead = 1.0 - smoothstep(0.012, 0.03, md);
            float glow = sin(progress * 3.14159);
            half3 leadColor = mix(half3(0.05, 0.04, 0.03), half3(1.0, 0.8, 0.4), flash);
            col = mix(col, leadColor, lead * max(flash, 0.35 * glow));
            return half4(col, 1.0);
        }
        """;

    // Płomień: stary obraz przypala się (ciemnieje) i wypala od brzegów nieregularną, żarzącą się krawędzią.
    private const string Flame = """
        half4 main(float2 p) {
            float2 uv = p / resolution;
            float aspect = resolution.x / resolution.y;
            float n = fbm(float2(uv.x * aspect, uv.y) * 3.5 + seed * 13.0);
            float border = min(min(uv.x, 1.0 - uv.x), min(uv.y, 1.0 - uv.y)) * 2.0;
            float v = 0.5 * n + 0.5 * border;
            float e = progress * 1.3 - 0.15 - v;
            half4 b = toAt(uv);
            if (e > 0.05)
                return b;
            if (e > 0.0) {
                float k = e / 0.05;
                half3 ember = mix(half3(1.0, 0.86, 0.5), half3(0.95, 0.38, 0.06), k);
                return half4(mix(ember, b.rgb, smoothstep(0.55, 1.0, k)), 1.0);
            }
            half4 a = fromAt(uv);
            float scorch = smoothstep(-0.05, 0.0, e);
            return half4(a.rgb * (1.0 - 0.7 * scorch) + half3(0.2, 0.06, 0.0) * scorch, 1.0);
        }
        """;

    // Akwarela: nowy obraz rozlewa się z losowego miejsca miękkimi plamami (szum fbm), z ciemniejszym brzegiem farby
    // i lekkim falowaniem tam, gdzie farba jeszcze mokra.
    private const string Watercolor = """
        half4 main(float2 p) {
            float2 uv = p / resolution;
            float aspect = resolution.x / resolution.y;
            float2 q = float2(uv.x * aspect, uv.y);
            float2 c = float2((0.3 + 0.4 * fract(seed * 7.13)) * aspect, 0.3 + 0.4 * fract(seed * 3.71));
            float warp = fbm(q * 2.5 + seed * 10.0);
            float n = fbm(q * 3.0 + float2(warp * 1.5) + seed * 20.0);
            float far = max(max(length(c), length(c - float2(aspect, 0.0))), max(length(c - float2(0.0, 1.0)), length(c - float2(aspect, 1.0))));
            float d = length(q - c) / far;
            float v = 0.6 * d + 0.4 * clamp((n - 0.2) * 1.7, 0.0, 1.0);
            float edge = progress * 1.2 - 0.1;
            float m = smoothstep(edge + 0.035, edge - 0.035, v);
            float rimX = (v - edge) / 0.025;
            float rim = exp(-rimX * rimX) * smoothstep(0.0, 0.08, progress) * (1.0 - smoothstep(0.85, 1.0, progress));
            float2 off = float2(noise(q * 8.0 + seed), noise(q * 8.0 + seed + 3.1)) - 0.5;
            half3 a = fromAt(uv).rgb;
            half3 b = toAt(uv + off * 0.03 * (1.0 - m) * (1.0 - progress)).rgb;
            half3 col = mix(a, b, m);
            col *= 1.0 - 0.3 * rim;
            return half4(col, 1.0);
        }
        """;

    // Promień światła: ukośny pas ciepłego światła (z delikatnymi smugami) przesuwa się przez ekran; za nim nowy obraz,
    // w połowie lekkie prześwietlenie całości.
    private const string LightRay = """
        half4 main(float2 p) {
            float2 uv = p / resolution;
            float2 dir = normalize(direction);
            float2 perp = float2(-dir.y, dir.x);
            float s = dot(uv - 0.5, dir) / (abs(dir.x) + abs(dir.y)) + 0.5;
            float w = 0.16;
            float b = progress * (1.0 + 2.0 * w) - w;
            float m = smoothstep(b + 0.03, b - 0.03, s);
            half3 col = mix(fromAt(uv).rgb, toAt(uv).rgb, m);
            float flare = sin(progress * 3.14159);
            float streak = 0.7 + 0.3 * noise(float2(dot(uv, perp) * 40.0 + seed * 50.0, progress * 3.0));
            float gx = (s - b) / w;
            float g = exp(-gx * gx) * streak;
            half3 light = half3(1.0, 0.87, 0.62);
            col = min(col + light * (g * 0.9 + 0.12) * flare, half3(1.0));
            return half4(col, 1.0);
        }
        """;

    // Fala: krąg od środka zniekształca obraz jak fala na wodzie i odsłania za sobą nowy.
    private const string Ripple = """
        half4 main(float2 p) {
            float2 uv = p / resolution;
            float aspect = resolution.x / resolution.y;
            float2 q = float2(uv.x * aspect, uv.y);
            float2 c = float2(0.5 * aspect, 0.5);
            float d = length(q - c);
            float r = progress * (length(c) + 0.3);
            float x = d - r;
            float ax = x * 9.0;
            float amp = exp(-ax * ax) * (1.0 - progress * 0.6);
            float2 dirv = d > 0.0001 ? (q - c) / d : float2(0.0);
            float wave = sin(x * 45.0);
            float2 off = dirv * wave * 0.014 * amp;
            float2 suv = uv + float2(off.x / aspect, off.y);
            float m = smoothstep(0.02, -0.02, x);
            half3 col = mix(fromAt(suv).rgb, toAt(suv).rgb, m);
            col = min(col + half3(1.0, 0.95, 0.85) * amp * 0.2 * max(0.0, wave), half3(1.0));
            return half4(col, 1.0);
        }
        """;
}
