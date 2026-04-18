using System.Collections.Generic;

namespace RainCycles.Snapshot;

// ESTRUCTURA DE DATOS

public partial class SettingsSnapshot
{
    public int    Palette;
    public float  Grime;
    public float  Clouds;
    public float  CeilingDrips;
    public float  BkgDroneVolume;
    public float  RandomItemDensity;
    public float  RandomItemSpearChance;
    public float  WaterReflectionAlpha;

    public int     FadePaletteID;
    public float[] FadePaletteOpacities = new float[0];

    public int    EffectColorA;
    public int    EffectColorB;
    public string DangerType    = "";
    public string Template      = "";
    public string Effects       = "";
    public string Triggers      = "";
    public string AmbientSounds = "";

    // ── Flags de campos declarados explícitamente
    // Cuando un campo no está declarado en el settings, se usa el valor del template
    public bool _hasPalette;
    public bool _hasGrime;
    public bool _hasClouds;
    public bool _hasCeilingDrips;
    public bool _hasBkgDroneVolume;
    public bool _hasRandomItemDensity;
    public bool _hasRandomItemSpearChance;
    public bool _hasEffectColorA;
    public bool _hasEffectColorB;
    public bool _hasFadePalette;

    public List<string>                   PlacedObjectLines = new List<string>();
    public Dictionary<int, float[]>       DecalOpacities    = new Dictionary<int, float[]>();
    public Dictionary<int, float>         LightIntensities  = new Dictionary<int, float>();
    public Dictionary<int, LightBeamData> LightBeams        = new Dictionary<int, LightBeamData>();

    // ── Efectos escalares (interpolables en blend)
    // Valor -1 indica que el efecto no está declarado en este setting
    public float EffectDarkness        = -1f;
    public float EffectBrightness      = -1f;
    public float EffectContrast        = -1f;
    public float EffectDesaturation    = -1f;
    public float EffectHue             = -1f;
    public float EffectDarkenLights    = -1f;
    public float EffectFog             = -1f;
    public float EffectSkyBloom        = -1f;
    public float EffectSkyAndLightBloom = -1f;
    public float EffectLightBurn       = -1f;
    public float EffectBloom           = -1f;
    public float EffectSurfaceSandstorm = -1f;

    // ── Terrain Palette (sistema Watcher)
    // Null = no declarado en este settings
    public string  TerrainPaletteName     = null;
    public string  TerrainFadePaletteName = null;
    public float[] TerrainFadeOpacities   = new float[0];
    public bool    _hasTerrainPalette;
    public bool    _hasTerrainFadePalette;

    // ── Terrain scalars (campos directos de RoomSettings, no RoomEffect)
    // Null = no declarado en este settings → vanilla hereda del template
    public float? TerrainWaves          = null;
    public float? TerrainLight          = null;
    public float? TerrainGrain          = null;
    public float? TerrainDepth          = null;
    public float? TerrainSkyFade        = null;
    public float? TerrainEdgeRadius     = null;
    public float? TerrainGooHeight      = null;
    public float? TerrainStainAmount    = null;
    public float? TerrainStainBrightness = null;
    public float? TerrainStainHeight    = null;

    public string RawText = "";

    // ── Colores de tinte personalizados (campo RC_TINT del mod)
    // Null = no declarado → fallback al valor hardcodeado o de paleta.
    public UnityEngine.Color? TintMultiply         = null;
    public UnityEngine.Color? TintAtmosphere       = null;
    public UnityEngine.Color? TintCloudAtmosphere  = null;
}

// ── Datos de un LightBeam capturado en el snapshot
public class LightBeamData
{
    public float Opacity;  // t[8]  — alpha / capa de render (LightBeamData.alpha)
    public float ColorA;   // t[9]  — mezcla hacia blanco puro (LightBeamData.colorA)
    public float ColorB;   // t[10] — mezcla hacia color ambiente (LightBeamData.colorB)
}