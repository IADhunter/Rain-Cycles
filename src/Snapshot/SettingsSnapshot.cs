using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace RainCycles.Snapshot;

// ================================================================
// SETTINGS SNAPSHOT - ESTRUCTURA DE DATOS
// ================================================================

public partial class SettingsSnapshot
{
    // ── Cache estática ────────────────────────────────────────────────
    private static readonly Dictionary<string, SettingsSnapshot> _snapshotCache = 
        new Dictionary<string, SettingsSnapshot>(StringComparer.OrdinalIgnoreCase);

    // ── Propiedades ────────────────────────────────────────────────────
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
    public float EffectSnowLight        = -1f;
    public float EffectSnowSparkle      = -1f;

    public float? ModifyEffectColorA_Hue = null;
    public float? ModifyEffectColorA_Saturation = null;
    public float? ModifyEffectColorA_Value = null;
    public float? ModifyEffectColorB_Hue = null;
    public float? ModifyEffectColorB_Saturation = null;
    public float? ModifyEffectColorB_Value = null;

    public string  TerrainPaletteName     = null;
    public string  TerrainFadePaletteName = null;
    public float[] TerrainFadeOpacities   = new float[0];
    public bool    _hasTerrainPalette;
    public bool    _hasTerrainFadePalette;

    // ============================================================
    // TERRAIN SCALARS - SOLO LOS QUE MANEJAMOS
    // ============================================================
    public float? TerrainWaves           = null;
    public float? TerrainLight           = null;
    public float? TerrainGrain           = null;
    public float? TerrainSkyFade         = null;
    public float? TerrainStainAmount     = null;
    public float? TerrainStainBrightness = null;
    public float? TerrainStainHeight     = null;

    public string RawText = "";

    // ============================================================
    // RAINCYCLES DATA
    // ============================================================
    public RcType RcType = RcType.None;
    public ViewType ViewType = ViewType.None;
    public Color? TintMultiply = null;
    public Color? TintAtmosphere = null;

    public bool HasRcType => RcType != RcType.None;
    public bool HasView => HasRcType && ViewType != ViewType.None;
    public bool HasTint => HasView && (TintMultiply.HasValue || TintAtmosphere.HasValue);

    // ============================================================
    // CACHE API
    // ============================================================

    public static SettingsSnapshot GetCached(string path, string roomName = null)
    {
        if (string.IsNullOrEmpty(path)) return null;
        
        if (!_snapshotCache.TryGetValue(path, out var snap))
        {
            snap = string.IsNullOrEmpty(roomName) 
                ? FromFile(path) 
                : FromFileWithTemplate(path, roomName);
            _snapshotCache[path] = snap;
        }
        return snap;
    }

    public static bool TryGetCached(string path, out SettingsSnapshot snap)
    {
        return _snapshotCache.TryGetValue(path, out snap);
    }

    public static void InvalidateCache(string path)
    {
        if (!string.IsNullOrEmpty(path))
            _snapshotCache.Remove(path);
    }

    public static void InvalidateAllCache()
    {
        _snapshotCache.Clear();
    }

    public static void PreloadRegionTemplates(string regionCode)
    {
        if (string.IsNullOrEmpty(regionCode)) return;
        string lower = regionCode.ToLowerInvariant();
        string searchPattern = $"{lower}_settingstemplate_*.txt";
        string regionFolder = Path.Combine("world", lower);

        for (int i = ModManager.ActiveMods.Count - 1; i >= 0; i--)
        {
            string dir = Path.Combine(ModManager.ActiveMods[i].path, regionFolder);
            if (Directory.Exists(dir))
            {
                foreach (string file in Directory.GetFiles(dir, searchPattern))
                {
                    if (!_snapshotCache.ContainsKey(file))
                        _snapshotCache[file] = FromFile(file);
                }
            }
        }

        string vanillaDir = Path.Combine(Application.streamingAssetsPath, regionFolder);
        if (Directory.Exists(vanillaDir))
        {
            foreach (string file in Directory.GetFiles(vanillaDir, searchPattern))
            {
                if (!_snapshotCache.ContainsKey(file))
                    _snapshotCache[file] = FromFile(file);
            }
        }
    }
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
    PSV,
    AUV,
    ORV
}

public enum RcType
{
    None,
    Static,
    Blend
}