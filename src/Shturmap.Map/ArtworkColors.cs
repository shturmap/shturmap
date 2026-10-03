using SkiaSharp;

namespace Shturmap.Map;

/// <summary>
/// How the map artwork recedes behind Shturmap's own symbols (docs/DESIGN.md, "Visual language" and "Map drawing").
/// Two steps: all colours lose most of their saturation and a little brightness, then colours near the quest amber
/// lose the rest of their chroma, so no part of the artwork reads as a quest marker.
/// </summary>
public static class ArtworkColors
{
    /// <summary>The colour filter the artwork is drawn through.</summary>
    public static SKColorFilter Filter { get; } = SKColorFilter.CreateCompose(AmberLimit(MapRenderer.QuestAmber), Recede(saturation: 0.38f, brightness: 0.85f));

    // A saturation matrix around Rec. 709 luma, scaled by the brightness (owner, 2026-10-02: unhighlighted quest
    // markers were hard to find at a glance).
    internal static SKColorFilter Recede(float saturation, float brightness)
    {
        float s = saturation, b = brightness, lr = 0.2126f, lg = 0.7152f, lb = 0.0722f;
        return SKColorFilter.CreateColorMatrix(
        [
            b * (lr + (1 - lr) * s), b * lg * (1 - s), b * lb * (1 - s), 0, 0,
            b * lr * (1 - s), b * (lg + (1 - lg) * s), b * lb * (1 - s), 0, 0,
            b * lr * (1 - s), b * lg * (1 - s), b * (lb + (1 - lb) * s), 0, 0,
            0, 0, 0, 1, 0,
        ]);
    }

    /// <summary>The CIELAB chroma colours near the amber keep at most (C*ab; the amber has about 46).</summary>
    public const float AmberChroma = 6;

    // In CIELAB: within 28° of the amber's hue and 14 of its lightness the chroma is cut to AmberChroma, easing out
    // to no change at 55° and 26 (cartography review, 2026-10-02: after the plain recede, Customs' dashed lines were
    // ΔE 3.9 from the amber and 3.3 % of Factory's artwork within ΔE 10; the aim is nothing within ΔE 15). Browns
    // darker than the amber and creams lighter than it keep their colour. The input is premultiplied.
    private const string AmberLimitSksl = """
        uniform float amberL;
        uniform float amberHue;
        uniform float maxChroma;

        float3 toLinear(float3 c) { return mix(c / 12.92, pow((c + 0.055) / 1.055, float3(2.4)), step(0.04045, c)); }
        float3 toGamma(float3 c) { return mix(c * 12.92, 1.055 * pow(c, float3(1.0 / 2.4)) - 0.055, step(0.0031308, c)); }

        float3 toLab(float3 rgb) {
            float3 l = toLinear(rgb);
            float3 xyz = float3(dot(l, float3(0.4124, 0.3576, 0.1805)) / 0.95047, dot(l, float3(0.2126, 0.7152, 0.0722)),
                                dot(l, float3(0.0193, 0.1192, 0.9505)) / 1.08883);
            float3 f = mix(7.787 * xyz + 16.0 / 116.0, pow(max(xyz, float3(0)), float3(1.0 / 3.0)), step(0.008856, xyz));
            return float3(116.0 * f.y - 16.0, 500.0 * (f.x - f.y), 200.0 * (f.y - f.z));
        }

        float3 fromLab(float3 lab) {
            float fy = (lab.x + 16.0) / 116.0;
            float3 f = float3(fy + lab.y / 500.0, fy, fy - lab.z / 200.0);
            float3 xyz = mix((f - 16.0 / 116.0) / 7.787, f * f * f, step(0.2069, f)) * float3(0.95047, 1.0, 1.08883);
            float3 l = float3(dot(xyz, float3(3.2406, -1.5372, -0.4986)), dot(xyz, float3(-0.9689, 1.8758, 0.0415)),
                              dot(xyz, float3(0.0557, -0.2040, 1.0570)));
            return toGamma(clamp(l, 0.0, 1.0));
        }

        half4 main(half4 color) {
            if (color.a <= 0.0) { return color; }
            float3 lab = toLab(clamp(float3(color.rgb) / color.a, 0.0, 1.0));
            float chroma = length(lab.yz);
            if (chroma <= maxChroma) { return color; }
            float dh = abs(atan(lab.z, lab.y) - amberHue);
            dh = min(dh, 6.2831853 - dh);
            float near = (1.0 - smoothstep(0.4887, 0.9599, dh)) * (1.0 - smoothstep(14.0, 26.0, abs(lab.x - amberL)));
            if (near <= 0.0) { return color; }
            lab.yz *= mix(1.0, maxChroma / chroma, near);
            return half4(half3(fromLab(lab)) * color.a, color.a);
        }
        """;

    private static SKColorFilter AmberLimit(SKColor amber)
    {
        using var effect = SKRuntimeEffect.CreateColorFilter(AmberLimitSksl, out var errors)
                           ?? throw new InvalidOperationException("Artwork colour filter: " + errors);
        var (l, a, b) = Lab(amber);
        var uniforms = new SKRuntimeEffectUniforms(effect)
        {
            ["amberL"] = (float)l,
            ["amberHue"] = (float)Math.Atan2(b, a),
            ["maxChroma"] = AmberChroma,
        };
        return effect.ToColorFilter(uniforms);
    }

    /// <summary>CIELAB (D65) of an sRGB colour.</summary>
    public static (double L, double A, double B) Lab(SKColor c)
    {
        static double Lin(double v)
        {
            v /= 255;
            return v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        }
        static double F(double t) => t > 0.008856 ? Math.Cbrt(t) : 7.787 * t + 16.0 / 116;
        double r = Lin(c.Red), g = Lin(c.Green), bl = Lin(c.Blue);
        double x = (0.4124 * r + 0.3576 * g + 0.1805 * bl) / 0.95047, y = 0.2126 * r + 0.7152 * g + 0.0722 * bl, z = (0.0193 * r + 0.1192 * g + 0.9505 * bl) / 1.08883;
        return (116 * F(y) - 16, 500 * (F(x) - F(y)), 200 * (F(y) - F(z)));
    }
}
