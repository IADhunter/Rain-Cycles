using System;
using UnityEngine;

namespace RainCycles.Snapshot;

// Interpolación entre dos snapshots
public partial class SettingsSnapshot
{
    public static SettingsSnapshot Lerp(SettingsSnapshot a, SettingsSnapshot b, float t)
    {
        t = Mathf.Clamp01(t);
        bool useB = t >= 0.5f;
        var snap = new SettingsSnapshot();

        snap.Palette = a.Palette;
        snap.Grime   = Mathf.Lerp(a.Grime, b.Grime, t);

        // Clouds: respetar 0f exacto para evitar parpadeo
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
        snap.WaterReflectionAlpha  = Mathf.Lerp(a.WaterReflectionAlpha,  b.WaterReflectionAlpha,  t);

        snap.EffectColorA  = useB ? b.EffectColorA  : a.EffectColorA;
        snap.EffectColorB  = useB ? b.EffectColorB  : a.EffectColorB;
        snap.DangerType    = useB ? b.DangerType    : a.DangerType;
        snap.Template      = useB ? b.Template      : a.Template;
        snap.Effects       = useB ? b.Effects       : a.Effects;
        snap.Triggers      = useB ? b.Triggers      : a.Triggers;
        snap.AmbientSounds = useB ? b.AmbientSounds : a.AmbientSounds;

        snap.FadePaletteID    = useB ? b.FadePaletteID    : a.FadePaletteID;
        snap._hasFadePalette  = useB ? b._hasFadePalette  : a._hasFadePalette;
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
            if (!b.DecalOpacities.TryGetValue(kv.Key, out float[] opsB)) opsB = new float[4];
            float[] lerped = new float[4];
            for (int i = 0; i < 4; i++) lerped[i] = Mathf.Lerp(kv.Value[i], opsB[i], t);
            snap.DecalOpacities[kv.Key] = lerped;
        }

        foreach (var kv in a.LightIntensities)
        {
            if (!b.LightIntensities.TryGetValue(kv.Key, out float ib)) ib = kv.Value;
            snap.LightIntensities[kv.Key] = Mathf.Lerp(kv.Value, ib, t);
        }

        foreach (var kv in a.LightBeams)
        {
            if (!b.LightBeams.TryGetValue(kv.Key, out LightBeamData lb)) lb = kv.Value;
            snap.LightBeams[kv.Key] = new LightBeamData
            {
                Opacity = LerpBeamOpacity(kv.Value.Opacity, lb.Opacity, t),
                ColorA  = Mathf.Lerp(kv.Value.ColorA, lb.ColorA, t),
                ColorB  = Mathf.Lerp(kv.Value.ColorB, lb.ColorB, t),
            };
        }

        snap.RawText = a.RawText;

        snap.EffectDarkness         = LerpEffect(a.EffectDarkness,         b.EffectDarkness,         t);
        snap.EffectBrightness       = LerpEffect(a.EffectBrightness,       b.EffectBrightness,       t);
        snap.EffectContrast         = LerpEffect(a.EffectContrast,         b.EffectContrast,         t);
        snap.EffectDesaturation     = LerpEffect(a.EffectDesaturation,     b.EffectDesaturation,     t);
        snap.EffectHue              = LerpEffect(a.EffectHue,              b.EffectHue,              t);
        snap.EffectDarkenLights     = LerpEffect(a.EffectDarkenLights,     b.EffectDarkenLights,     t);
        snap.EffectFog              = LerpEffect(a.EffectFog,              b.EffectFog,              t);
        snap.EffectSkyBloom         = LerpEffect(a.EffectSkyBloom,         b.EffectSkyBloom,         t);
        snap.EffectSkyAndLightBloom = LerpEffect(a.EffectSkyAndLightBloom, b.EffectSkyAndLightBloom, t);
        snap.EffectLightBurn        = LerpEffect(a.EffectLightBurn,        b.EffectLightBurn,        t);
        snap.EffectBloom            = LerpEffect(a.EffectBloom,            b.EffectBloom,            t);
        snap.EffectSurfaceSandstorm = LerpEffect(a.EffectSurfaceSandstorm, b.EffectSurfaceSandstorm, t);

        // Terrain Palette
        snap.TerrainPaletteName     = useB ? b.TerrainPaletteName     : a.TerrainPaletteName;
        snap.TerrainFadePaletteName = useB ? b.TerrainFadePaletteName : a.TerrainFadePaletteName;
        snap._hasTerrainPalette     = useB ? b._hasTerrainPalette     : a._hasTerrainPalette;
        snap._hasTerrainFadePalette = useB ? b._hasTerrainFadePalette : a._hasTerrainFadePalette;

        int tpCount = Math.Max(a.TerrainFadeOpacities.Length, b.TerrainFadeOpacities.Length);
        snap.TerrainFadeOpacities = new float[tpCount];
        for (int i = 0; i < tpCount; i++)
        {
            float opA = i < a.TerrainFadeOpacities.Length ? a.TerrainFadeOpacities[i] : 0f;
            float opB = i < b.TerrainFadeOpacities.Length ? b.TerrainFadeOpacities[i] : 0f;
            snap.TerrainFadeOpacities[i] = Mathf.Lerp(opA, opB, t);
        }

        // RC_TINT
        if (a.TintMultiply.HasValue || b.TintMultiply.HasValue)
            snap.TintMultiply = Color.Lerp(a.TintMultiply ?? b.TintMultiply.Value, b.TintMultiply ?? a.TintMultiply.Value, t);
        if (a.TintAtmosphere.HasValue || b.TintAtmosphere.HasValue)
            snap.TintAtmosphere = Color.Lerp(a.TintAtmosphere ?? b.TintAtmosphere.Value, b.TintAtmosphere ?? a.TintAtmosphere.Value, t);
        if (a.TintCloudAtmosphere.HasValue || b.TintCloudAtmosphere.HasValue)
            snap.TintCloudAtmosphere = Color.Lerp(a.TintCloudAtmosphere ?? b.TintCloudAtmosphere.Value, b.TintCloudAtmosphere ?? a.TintCloudAtmosphere.Value, t);

        // Terrain scalars
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

        // NOTA: NO interpolamos ModifyEffectColorA/B parameters
        // Se mantienen tal cual en cada snapshot y se usan para calcular colores finales
        // La interpolación se hace sobre los colores finales en RoomCameraBlend.Effects.cs

        return snap;
    }

    private static float LerpEffect(float va, float vb, float t)
    {
        float a = va < 0f ? 0f : va;
        float b = vb < 0f ? 0f : vb;
        return Mathf.Lerp(a, b, t);
    }

    private static float? LerpNullable(float? va, float? vb, float t)
    {
        if (va == null && vb == null) return null;
        return Mathf.Lerp(va ?? vb.Value, vb ?? va.Value, t);
    }

    // LightBeam: interpola sin cruzar bandas de render (floor(alpha*3))
    private static float LerpBeamOpacity(float opA, float opB, float t)
    {
        GetBeamRange(opA, out float minA, out float maxA);
        GetBeamRange(opB, out float minB, out float maxB);

        float rangeB = maxB - minB;
        float normB  = rangeB > 0f ? Mathf.Clamp01((opB - minB) / rangeB) : 0f;
        float opBInRangeA = minA + normB * (maxA - minA);

        return Mathf.Lerp(opA, opBInRangeA, t);
    }

    private static void GetBeamRange(float op, out float rMin, out float rMax)
    {
        if (op < 0.3333f)      { rMin = 0f;      rMax = 0.3333f; }
        else if (op < 0.6667f) { rMin = 0.3333f; rMax = 0.6667f; }
        else                    { rMin = 0.6667f; rMax = 1f;      }
    }
}