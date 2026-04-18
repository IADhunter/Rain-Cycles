using System;
using UnityEngine;

namespace RainCycles.Snapshot;

// INTERPOLACIÓN

public partial class SettingsSnapshot
{
    public static SettingsSnapshot Lerp(SettingsSnapshot a, SettingsSnapshot b, float t)
    {
        t = Mathf.Clamp01(t);
        bool useB = t >= 0.5f;
        var snap = new SettingsSnapshot();

        snap.Palette               = a.Palette;
        snap.Grime                 = Mathf.Lerp(a.Grime,                 b.Grime,                 t);
        // Clouds: el juego trata 0f exacto especialmente (ShadPropLight1 = 1f sin lerp).
        // Respetar 0f en los extremos para evitar parpadeo al entrar al blend.
        float cloudsLerped = Mathf.Lerp(a.Clouds, b.Clouds, t);
        if (a.Clouds <= 0f && b.Clouds <= 0f)
            snap.Clouds = 0f;
        else if (t <= 0f)
            snap.Clouds = a.Clouds;
        else if (t >= 1f)
            snap.Clouds = b.Clouds;
        else
            snap.Clouds = Mathf.Max(cloudsLerped, 0.001f);
        snap.CeilingDrips          = Mathf.Lerp(a.CeilingDrips,          b.CeilingDrips,          t);
        snap.BkgDroneVolume        = Mathf.Lerp(a.BkgDroneVolume,        b.BkgDroneVolume,        t);
        snap.RandomItemDensity     = Mathf.Lerp(a.RandomItemDensity,     b.RandomItemDensity,     t);
        snap.RandomItemSpearChance = Mathf.Lerp(a.RandomItemSpearChance, b.RandomItemSpearChance, t);

        snap.EffectColorA   = useB ? b.EffectColorA   : a.EffectColorA;
        snap.EffectColorB   = useB ? b.EffectColorB   : a.EffectColorB;
        snap.DangerType     = useB ? b.DangerType     : a.DangerType;
        snap.Template       = useB ? b.Template       : a.Template;
        snap.Effects        = useB ? b.Effects        : a.Effects;
        snap.Triggers       = useB ? b.Triggers       : a.Triggers;
        snap.AmbientSounds  = useB ? b.AmbientSounds  : a.AmbientSounds;

        snap.FadePaletteID = useB ? b.FadePaletteID : a.FadePaletteID;
        snap._hasFadePalette = useB ? b._hasFadePalette : a._hasFadePalette;
        int opCount = Math.Max(a.FadePaletteOpacities.Length, b.FadePaletteOpacities.Length);
        snap.FadePaletteOpacities = new float[opCount];
        for (int i = 0; i < opCount; i++)
        {
            float opA = i < a.FadePaletteOpacities.Length ? a.FadePaletteOpacities[i] : 0f;
            float opB = i < b.FadePaletteOpacities.Length ? b.FadePaletteOpacities[i] : 0f;
            snap.FadePaletteOpacities[i] = Mathf.Lerp(opA, opB, t);
        }

        snap.PlacedObjectLines = new System.Collections.Generic.List<string>(a.PlacedObjectLines);

        foreach (var kv in a.DecalOpacities)
        {
            float[] opsA = kv.Value;
            float[] opsB;
            if (!b.DecalOpacities.TryGetValue(kv.Key, out opsB)) opsB = new float[4];
            float[] lerped = new float[4];
            for (int i = 0; i < 4; i++) lerped[i] = Mathf.Lerp(opsA[i], opsB[i], t);
            snap.DecalOpacities[kv.Key] = lerped;
        }

        foreach (var kv in a.LightIntensities)
        {
            float ib;
            if (!b.LightIntensities.TryGetValue(kv.Key, out ib)) ib = kv.Value;
            snap.LightIntensities[kv.Key] = Mathf.Lerp(kv.Value, ib, t);
        }

        foreach (var kv in a.LightBeams)
        {
            var la = kv.Value;
            LightBeamData lb;
            if (!b.LightBeams.TryGetValue(kv.Key, out lb)) lb = la;
            snap.LightBeams[kv.Key] = new LightBeamData
            {
                Opacity = LerpBeamOpacity(la.Opacity, lb.Opacity, t),
                ColorA  = Mathf.Lerp(la.ColorA, lb.ColorA, t),
                ColorB  = Mathf.Lerp(la.ColorB, lb.ColorB, t),
            };
        }

        snap.RawText = a.RawText;

        // Efectos escalares — si un snapshot no tiene el efecto (-1), se usa 0
        snap.EffectDarkness        = LerpEffect(a.EffectDarkness,        b.EffectDarkness,        t);
        snap.EffectBrightness      = LerpEffect(a.EffectBrightness,      b.EffectBrightness,      t);
        snap.EffectContrast        = LerpEffect(a.EffectContrast,        b.EffectContrast,        t);
        snap.EffectDesaturation    = LerpEffect(a.EffectDesaturation,    b.EffectDesaturation,    t);
        snap.EffectHue             = LerpEffect(a.EffectHue,             b.EffectHue,             t);
        snap.EffectDarkenLights    = LerpEffect(a.EffectDarkenLights,    b.EffectDarkenLights,    t);
        snap.EffectFog             = LerpEffect(a.EffectFog,             b.EffectFog,             t);
        snap.EffectSkyBloom        = LerpEffect(a.EffectSkyBloom,        b.EffectSkyBloom,        t);
        snap.EffectSkyAndLightBloom = LerpEffect(a.EffectSkyAndLightBloom, b.EffectSkyAndLightBloom, t);
        snap.EffectLightBurn       = LerpEffect(a.EffectLightBurn,       b.EffectLightBurn,       t);
        snap.EffectBloom           = LerpEffect(a.EffectBloom,           b.EffectBloom,           t);
        snap.EffectSurfaceSandstorm = LerpEffect(a.EffectSurfaceSandstorm, b.EffectSurfaceSandstorm, t);
        snap.WaterReflectionAlpha = Mathf.Lerp(a.WaterReflectionAlpha, b.WaterReflectionAlpha, t);

        // Terrain Palette — nombre swap en t≥0.5, opacidades interpoladas por screen
        snap.TerrainPaletteName     = useB ? b.TerrainPaletteName     : a.TerrainPaletteName;
        snap.TerrainFadePaletteName = useB ? b.TerrainFadePaletteName : a.TerrainFadePaletteName;
        snap._hasTerrainPalette     = useB ? b._hasTerrainPalette     : a._hasTerrainPalette;
        snap._hasTerrainFadePalette = useB ? b._hasTerrainFadePalette : a._hasTerrainFadePalette;

        // Interpolar opacidades del fade — igual que FadePaletteOpacities
        int tpCount = System.Math.Max(a.TerrainFadeOpacities.Length, b.TerrainFadeOpacities.Length);
        snap.TerrainFadeOpacities = new float[tpCount];
        for (int i = 0; i < tpCount; i++)
        {
            float opA = i < a.TerrainFadeOpacities.Length ? a.TerrainFadeOpacities[i] : 0f;
            float opB = i < b.TerrainFadeOpacities.Length ? b.TerrainFadeOpacities[i] : 0f;
            snap.TerrainFadeOpacities[i] = Mathf.Lerp(opA, opB, t);
        }

        // Colores de tinte RC_TINT — si uno no está declarado, se trata como transparente/ninguno.
        if (a.TintMultiply.HasValue || b.TintMultiply.HasValue)
        {
            var ca = a.TintMultiply ?? b.TintMultiply.Value;
            var cb = b.TintMultiply ?? a.TintMultiply.Value;
            snap.TintMultiply = Color.Lerp(ca, cb, t);
        }
        if (a.TintAtmosphere.HasValue || b.TintAtmosphere.HasValue)
        {
            var ca = a.TintAtmosphere ?? b.TintAtmosphere.Value;
            var cb = b.TintAtmosphere ?? a.TintAtmosphere.Value;
            snap.TintAtmosphere = Color.Lerp(ca, cb, t);
        }
        if (a.TintCloudAtmosphere.HasValue || b.TintCloudAtmosphere.HasValue)
        {
            var ca = a.TintCloudAtmosphere ?? b.TintCloudAtmosphere.Value;
            var cb = b.TintCloudAtmosphere ?? a.TintCloudAtmosphere.Value;
            snap.TintCloudAtmosphere = Color.Lerp(ca, cb, t);
        }

        // Terrain scalars — float? null=no declarado, se lerpa solo si al menos uno lo declara
        snap.TerrainWaves           = LerpNullable(a.TerrainWaves,           b.TerrainWaves,           t);
        snap.TerrainLight           = LerpNullable(a.TerrainLight,           b.TerrainLight,           t);
        snap.TerrainGrain           = LerpNullable(a.TerrainGrain,           b.TerrainGrain,           t);
        snap.TerrainDepth           = LerpNullable(a.TerrainDepth,           b.TerrainDepth,           t);
        snap.TerrainSkyFade         = LerpNullable(a.TerrainSkyFade,         b.TerrainSkyFade,         t);
        snap.TerrainEdgeRadius      = LerpNullable(a.TerrainEdgeRadius,      b.TerrainEdgeRadius,      t);
        snap.TerrainGooHeight       = LerpNullable(a.TerrainGooHeight,       b.TerrainGooHeight,       t);
        snap.TerrainStainAmount     = LerpNullable(a.TerrainStainAmount,     b.TerrainStainAmount,     t);
        snap.TerrainStainBrightness = LerpNullable(a.TerrainStainBrightness, b.TerrainStainBrightness, t);
        snap.TerrainStainHeight     = LerpNullable(a.TerrainStainHeight,     b.TerrainStainHeight,     t);

        return snap;
    }

    // Si el efecto no está declarado en un snapshot (-1), se trata como 0
    private static float LerpEffect(float va, float vb, float t)
    {
        float a = va < 0f ? 0f : va;
        float b = vb < 0f ? 0f : vb;
        return Mathf.Lerp(a, b, t);
    }

    // Lerp nullable: null=no declarado. Si solo uno declara, usa ese valor fijo.
    // Si ninguno declara, retorna null (vanilla hereda del template).
    private static float? LerpNullable(float? va, float? vb, float t)
    {
        if (va == null && vb == null) return null;
        float a = va ?? vb.Value;
        float b = vb ?? va.Value;
        return Mathf.Lerp(a, b, t);
    }

    // ── LightBeam: interpolación sin cruzar bandas de render
    // El juego divide el alpha en 3 bandas via floor(alpha*3).

    private static float LerpBeamOpacity(float opA, float opB, float t)
    {
        GetBeamRange(opA, out float minA, out float maxA);
        GetBeamRange(opB, out float minB, out float maxB);

        float rangeB      = maxB - minB;
        float normB       = rangeB > 0f ? Mathf.Clamp01((opB - minB) / rangeB) : 0f;
        float opBInRangeA = minA + normB * (maxA - minA);

        return Mathf.Lerp(opA, opBInRangeA, t);
    }

    private static void GetBeamRange(float op, out float rMin, out float rMax)
    {
        // Umbrales reales del juego: floor(alpha * 3) → bandas 0, 1, 2
        if      (op < 0.3333f) { rMin = 0f;      rMax = 0.3333f; }
        else if (op < 0.6667f) { rMin = 0.3333f; rMax = 0.6667f; }
        else                   { rMin = 0.6667f; rMax = 1f;      }
    }
}