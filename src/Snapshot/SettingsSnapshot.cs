using System.Collections.Generic;

namespace RainCycles.Snapshot;

// Estructura de datos de un settings_N.txt
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

    // Efectos escalares (-1 = no declarado)
    public float EffectDarkness         = -1f;
    public float EffectBrightness       = -1f;
    public float EffectContrast         = -1f;
    public float EffectDesaturation     = -1f;
    public float EffectHue              = -1f;
    public float EffectDarkenLights     = -1f;
    public float EffectFog              = -1f;
    public float EffectSkyBloom         = -1f;
    public float EffectSkyAndLightBloom = -1f;
    public float EffectLightBurn        = -1f;
    public float EffectBloom            = -1f;
    public float EffectSurfaceSandstorm = -1f;

    // ModifyEffectColorA/B parameters (Hue, Saturation, Value) - null = no declarado
    public float? ModifyEffectColorA_Hue = null;
    public float? ModifyEffectColorA_Saturation = null;
    public float? ModifyEffectColorA_Value = null;
    public float? ModifyEffectColorB_Hue = null;
    public float? ModifyEffectColorB_Saturation = null;
    public float? ModifyEffectColorB_Value = null;

    // Terrain Palette
    public string  TerrainPaletteName     = null;
    public string  TerrainFadePaletteName = null;
    public float[] TerrainFadeOpacities   = new float[0];
    public bool    _hasTerrainPalette;
    public bool    _hasTerrainFadePalette;

    // Terrain scalars (null = no declarado)
    public float? TerrainWaves           = null;
    public float? TerrainLight           = null;
    public float? TerrainGrain           = null;
    public float? TerrainDepth           = null;
    public float? TerrainSkyFade         = null;
    public float? TerrainEdgeRadius      = null;
    public float? TerrainGooHeight       = null;
    public float? TerrainStainAmount     = null;
    public float? TerrainStainBrightness = null;
    public float? TerrainStainHeight     = null;

    public string RawText = "";

    // RC_TINT - SOLO DOS COLORES (Multiply y Atmosphere)
    public UnityEngine.Color? TintMultiply   = null;
    public UnityEngine.Color? TintAtmosphere = null;

    // RC_VIEW
    public ViewType ViewType = ViewType.None;

    // RC_TYPE - Nueva fuente de verdad
    public RcType RcType = RcType.None;
    public bool HasRcType = false;
}

public class LightBeamData
{
    public float Opacity;
    public float ColorA;
    public float ColorB;
}

public enum ViewType
{
    None,
    ACV,
    RTV,
    PSV
}

// Nuevo enum para RC_TYPE
public enum RcType
{
    None,   // No declarado → sala vanilla
    Static, // Sala estática (tintes fijos, sin blend)
    Blend   // Sala participa en blend system
}