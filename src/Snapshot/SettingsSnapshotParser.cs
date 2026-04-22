using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace RainCycles.Snapshot;

// PARSING

public partial class SettingsSnapshot
{
    private static readonly NumberStyles NF  = NumberStyles.Float;
    private static readonly CultureInfo  INV = CultureInfo.InvariantCulture;

    public static SettingsSnapshot FromFile(string path)
    {
        var snap = new SettingsSnapshot();
        if (!File.Exists(path)) return snap;

        string raw = File.ReadAllText(path, Encoding.UTF8);
        snap.RawText = raw;

        foreach (string rawLine in raw.Split('\n'))
        {
            string line = rawLine.TrimEnd('\r');

            int sep = line.IndexOf(": ", StringComparison.Ordinal);
            if (sep < 0) continue;

            string key = line.Substring(0, sep);
            string val = line.Substring(sep + 2);

            int iv; float fv;
            switch (key)
            {
                case "PlacedObjects":
                    ParsePlacedObjectsContent(snap, val);
                    break;

                case "Palette":
                    if (int.TryParse(val.Trim(), out iv)) { snap.Palette = iv; snap._hasPalette = true; }
                    break;
                case "Grime":
                    if (float.TryParse(val.Trim(), NF, INV, out fv)) { snap.Grime = fv; snap._hasGrime = true; }
                    break;
                case "Clouds":
                    if (float.TryParse(val.Trim(), NF, INV, out fv)) { snap.Clouds = fv; snap._hasClouds = true; }
                    break;
                case "CeilingDrips":
                    if (float.TryParse(val.Trim(), NF, INV, out fv)) { snap.CeilingDrips = fv; snap._hasCeilingDrips = true; }
                    break;
                case "BkgDroneVolume":
                    if (float.TryParse(val.Trim(), NF, INV, out fv)) { snap.BkgDroneVolume = fv; snap._hasBkgDroneVolume = true; }
                    break;
                case "RandomItemDensity":
                    if (float.TryParse(val.Trim(), NF, INV, out fv)) { snap.RandomItemDensity = fv; snap._hasRandomItemDensity = true; }
                    break;
                case "RandomItemSpearChance":
                    if (float.TryParse(val.Trim(), NF, INV, out fv)) { snap.RandomItemSpearChance = fv; snap._hasRandomItemSpearChance = true; }
                    break;
                case "WaterReflectionAlpha":
                    if (float.TryParse(val.Trim(), NF, INV, out fv)) snap.WaterReflectionAlpha = fv;
                    break;
                case "EffectColorA":
                    if (int.TryParse(val.Trim(), out iv)) { snap.EffectColorA = iv; snap._hasEffectColorA = true; }
                    break;
                case "EffectColorB":
                    if (int.TryParse(val.Trim(), out iv)) { snap.EffectColorB = iv; snap._hasEffectColorB = true; }
                    break;

                case "DangerType":     snap.DangerType    = val.Trim(); break;
                case "Template":       snap.Template      = val.Trim(); break;
                case "Triggers":       snap.Triggers      = val.Trim(); break;
                case "AmbientSounds":  snap.AmbientSounds = val.Trim(); break;

                case "Effects":
                {
                    string sv = val.Trim();
                    snap.Effects = sv;
                    ParseEffectAmounts(snap, sv);
                    break;
                }

                case "TerrainPalette":
                    snap.TerrainPaletteName = val.Trim();
                    snap._hasTerrainPalette = true;
                    break;
                case "TerrainWaves":
                    if (float.TryParse(val.Trim(), NF, INV, out fv)) snap.TerrainWaves = fv;
                    break;
                case "TerrainLight":
                    if (float.TryParse(val.Trim(), NF, INV, out fv)) snap.TerrainLight = fv;
                    break;
                case "TerrainGrain":
                    if (float.TryParse(val.Trim(), NF, INV, out fv)) snap.TerrainGrain = fv;
                    break;
                case "TerrainDepth":
                    if (float.TryParse(val.Trim(), NF, INV, out fv)) snap.TerrainDepth = fv;
                    break;
                case "TerrainSkyFade":
                    if (float.TryParse(val.Trim(), NF, INV, out fv)) snap.TerrainSkyFade = fv;
                    break;
                case "TerrainEdgeRadius":
                    if (float.TryParse(val.Trim(), NF, INV, out fv)) snap.TerrainEdgeRadius = fv;
                    break;
                case "TerrainGooHeight":
                    if (float.TryParse(val.Trim(), NF, INV, out fv)) snap.TerrainGooHeight = fv;
                    break;
                case "TerrainStainAmount":
                    if (float.TryParse(val.Trim(), NF, INV, out fv)) snap.TerrainStainAmount = fv;
                    break;
                case "TerrainStainBrightness":
                    if (float.TryParse(val.Trim(), NF, INV, out fv)) snap.TerrainStainBrightness = fv;
                    break;
                case "TerrainStainHeight":
                    if (float.TryParse(val.Trim(), NF, INV, out fv)) snap.TerrainStainHeight = fv;
                    break;

                case "TerrainFadePalette":
                {
                    string[] parts = val.Trim().Split(',');
                    if (parts.Length >= 1 && !string.IsNullOrEmpty(parts[0].Trim()))
                    {
                        snap.TerrainFadePaletteName = parts[0].Trim();
                        snap._hasTerrainFadePalette = true;
                        var ops = new List<float>();
                        for (int i = 1; i < parts.Length; i++)
                        {
                            float op;
                            if (float.TryParse(parts[i].Trim(), NF, INV, out op))
                                ops.Add(op);
                        }
                        snap.TerrainFadeOpacities = ops.ToArray();
                    }
                    break;
                }

                case "FadePalette":
                {
                    string[] parts = val.Trim().Split(',');
                    int fpid;
                    if (parts.Length >= 1 && int.TryParse(parts[0].Trim(), out fpid))
                    {
                        snap.FadePaletteID = fpid;
                        snap._hasFadePalette = true;
                        var ops = new List<float>();
                        for (int i = 1; i < parts.Length; i++)
                        {
                            float op;
                            if (float.TryParse(parts[i].Trim(), NF, INV, out op))
                                ops.Add(op);
                        }
                        snap.FadePaletteOpacities = ops.ToArray();
                    }
                    break;
                }

                // Campo propio del mod — Rain World ignora líneas que no reconoce.
                case "RC_TINT":
                {
                    string[] hexes = val.Trim().Split(' ');
                    if (hexes.Length >= 1) snap.TintMultiply        = ParseHexColor(hexes[0]);
                    if (hexes.Length >= 2) snap.TintAtmosphere      = ParseHexColor(hexes[1]);
                    if (hexes.Length >= 3) snap.TintCloudAtmosphere = ParseHexColor(hexes[2]);
                    break;
                }
            }
        }
        return snap;
    }

    // ── Template fallback

    public static SettingsSnapshot FromFileWithTemplate(string path, string roomName)
    {
        var snap = FromFile(path);
        FillFromTemplate(snap, roomName);
        return snap;
    }

    private static void FillFromTemplate(SettingsSnapshot snap, string roomName)
    {
        // Si el template es "NONE", no aplicar herencia
        if (snap.Template.ToUpper() == "NONE") return;

        // Determinar el nombre del template a usar
        string templateName;
        bool useWildcard = false;
        
        if (string.IsNullOrEmpty(snap.Template))
        {
            // Si no hay template declarado, usar "outside" como default (comportamiento vanilla)
            templateName = "outside";
            useWildcard = true;  // Buscar cualquier template que contenga "outside"
            RSPlugin.log.LogDebug($"[SettingsSnapshot] No template declared for {roomName}, using default 'outside' (wildcard)");
        }
        else
        {
            templateName = snap.Template.ToLower();
            // Si el template contiene "_", tomar la parte después del último "_"
            int lastUnderscore = templateName.LastIndexOf('_');
            if (lastUnderscore >= 0)
                templateName = templateName.Substring(lastUnderscore + 1);
        }

        // Extraer región del nombre de la sala (ej: "WPTA_F03" → "wpta")
        string region = roomName.Contains("_")
            ? roomName.Split('_')[0].ToLower()
            : roomName.ToLower();

        string templatePath = null;

        // Si usamos wildcard, buscar cualquier archivo que contenga "outside"
        if (useWildcard)
        {
            string searchPattern = region + "_settingstemplate_*outside*.txt";
            string modsRoot = Path.Combine(UnityEngine.Application.streamingAssetsPath, "mods");
            
            // Buscar en mods activos
            for (int i = ModManager.ActiveMods.Count - 1; i >= 0; i--)
            {
                string searchDir = Path.Combine(ModManager.ActiveMods[i].path, "World", region);
                if (Directory.Exists(searchDir))
                {
                    string[] matches = Directory.GetFiles(searchDir, searchPattern);
                    if (matches.Length > 0)
                    {
                        templatePath = matches[0];
                        break;
                    }
                }
            }
            
            // Buscar en StreamingAssets/mods/ (fallback para mods no registrados en BepInEx)
            if (templatePath == null && Directory.Exists(modsRoot))
            {
                foreach (string found in Directory.GetFiles(modsRoot, searchPattern, SearchOption.AllDirectories))
                {
                    templatePath = found;
                    break;
                }
            }
            
            // Buscar en vanilla
            if (templatePath == null)
            {
                string vanillaDir = Path.Combine(UnityEngine.Application.streamingAssetsPath, "World", region);
                if (Directory.Exists(vanillaDir))
                {
                    string[] matches = Directory.GetFiles(vanillaDir, searchPattern);
                    if (matches.Length > 0)
                        templatePath = matches[0];
                }
            }
        }
        else
        {
            // Búsqueda normal con nombre exacto
            string templateFile = region + "_settingstemplate_" + templateName + ".txt";
            string templateRelative = Path.Combine("World", region, templateFile);

            // Buscar en mods activos en orden inverso (mayor prioridad primero)
            for (int i = ModManager.ActiveMods.Count - 1; i >= 0; i--)
            {
                string candidate = Path.Combine(ModManager.ActiveMods[i].path, templateRelative);
                if (File.Exists(candidate)) { templatePath = candidate; break; }
            }

            // Fallback: buscar en todas las subcarpetas de StreamingAssets/mods/
            if (templatePath == null)
            {
                string modsRoot = Path.Combine(UnityEngine.Application.streamingAssetsPath, "mods");
                if (Directory.Exists(modsRoot))
                {
                    foreach (string found in Directory.GetFiles(modsRoot, templateFile, SearchOption.AllDirectories))
                    { templatePath = found; break; }
                }
            }

            // Fallback final: StreamingAssets base (vanilla)
            if (templatePath == null)
            {
                string basePath = Path.Combine(UnityEngine.Application.streamingAssetsPath, templateRelative);
                if (File.Exists(basePath)) templatePath = basePath;
            }
        }

        if (templatePath == null || !File.Exists(templatePath))
        {
            RSPlugin.log.LogDebug($"[SettingsSnapshot] Template not found: {(useWildcard ? "wildcard *outside*" : templateName)}");
            return;
        }

        RSPlugin.log.LogDebug($"[SettingsSnapshot] Template found: {templatePath}");
        
        var tmpl = FromFile(templatePath);

        // Solo copiar valores que el snapshot original NO declaró explícitamente
        if (!snap._hasPalette)               snap.Palette               = tmpl.Palette;
        if (!snap._hasGrime)                 snap.Grime                 = tmpl.Grime;
        if (!snap._hasClouds)                snap.Clouds                = tmpl.Clouds;
        if (!snap._hasCeilingDrips)          snap.CeilingDrips          = tmpl.CeilingDrips;
        if (!snap._hasBkgDroneVolume)        snap.BkgDroneVolume        = tmpl.BkgDroneVolume;
        if (!snap._hasRandomItemDensity)     snap.RandomItemDensity     = tmpl.RandomItemDensity;
        if (!snap._hasRandomItemSpearChance) snap.RandomItemSpearChance = tmpl.RandomItemSpearChance;
        if (!snap._hasEffectColorA)          snap.EffectColorA          = tmpl.EffectColorA;
        if (!snap._hasEffectColorB)          snap.EffectColorB          = tmpl.EffectColorB;
        
        if (!snap._hasFadePalette)
        {
            snap.FadePaletteID        = tmpl.FadePaletteID;
            snap.FadePaletteOpacities = tmpl.FadePaletteOpacities;
        }

        // Terrain palette y fade palette también heredan si no fueron declarados
        if (!snap._hasTerrainPalette)
        {
            snap.TerrainPaletteName = tmpl.TerrainPaletteName;
        }
        if (!snap._hasTerrainFadePalette)
        {
            snap.TerrainFadePaletteName = tmpl.TerrainFadePaletteName;
            snap.TerrainFadeOpacities   = tmpl.TerrainFadeOpacities;
        }

        // Terrain scalars (float? - null significa no declarado)
        if (snap.TerrainWaves == null && tmpl.TerrainWaves != null)
            snap.TerrainWaves = tmpl.TerrainWaves;
        if (snap.TerrainLight == null && tmpl.TerrainLight != null)
            snap.TerrainLight = tmpl.TerrainLight;
        if (snap.TerrainGrain == null && tmpl.TerrainGrain != null)
            snap.TerrainGrain = tmpl.TerrainGrain;
        if (snap.TerrainDepth == null && tmpl.TerrainDepth != null)
            snap.TerrainDepth = tmpl.TerrainDepth;
        if (snap.TerrainSkyFade == null && tmpl.TerrainSkyFade != null)
            snap.TerrainSkyFade = tmpl.TerrainSkyFade;
        if (snap.TerrainEdgeRadius == null && tmpl.TerrainEdgeRadius != null)
            snap.TerrainEdgeRadius = tmpl.TerrainEdgeRadius;
        if (snap.TerrainGooHeight == null && tmpl.TerrainGooHeight != null)
            snap.TerrainGooHeight = tmpl.TerrainGooHeight;
        if (snap.TerrainStainAmount == null && tmpl.TerrainStainAmount != null)
            snap.TerrainStainAmount = tmpl.TerrainStainAmount;
        if (snap.TerrainStainBrightness == null && tmpl.TerrainStainBrightness != null)
            snap.TerrainStainBrightness = tmpl.TerrainStainBrightness;
        if (snap.TerrainStainHeight == null && tmpl.TerrainStainHeight != null)
            snap.TerrainStainHeight = tmpl.TerrainStainHeight;

        // Efectos escalares (sentinel -1 = no declarado)
        if (snap.EffectDarkness < 0f && tmpl.EffectDarkness >= 0f)
            snap.EffectDarkness = tmpl.EffectDarkness;
        if (snap.EffectBrightness < 0f && tmpl.EffectBrightness >= 0f)
            snap.EffectBrightness = tmpl.EffectBrightness;
        if (snap.EffectContrast < 0f && tmpl.EffectContrast >= 0f)
            snap.EffectContrast = tmpl.EffectContrast;
        if (snap.EffectDesaturation < 0f && tmpl.EffectDesaturation >= 0f)
            snap.EffectDesaturation = tmpl.EffectDesaturation;
        if (snap.EffectHue < 0f && tmpl.EffectHue >= 0f)
            snap.EffectHue = tmpl.EffectHue;
        if (snap.EffectDarkenLights < 0f && tmpl.EffectDarkenLights >= 0f)
            snap.EffectDarkenLights = tmpl.EffectDarkenLights;
        if (snap.EffectFog < 0f && tmpl.EffectFog >= 0f)
            snap.EffectFog = tmpl.EffectFog;
        if (snap.EffectSkyBloom < 0f && tmpl.EffectSkyBloom >= 0f)
            snap.EffectSkyBloom = tmpl.EffectSkyBloom;
        if (snap.EffectSkyAndLightBloom < 0f && tmpl.EffectSkyAndLightBloom >= 0f)
            snap.EffectSkyAndLightBloom = tmpl.EffectSkyAndLightBloom;
        if (snap.EffectLightBurn < 0f && tmpl.EffectLightBurn >= 0f)
            snap.EffectLightBurn = tmpl.EffectLightBurn;
        if (snap.EffectBloom < 0f && tmpl.EffectBloom >= 0f)
            snap.EffectBloom = tmpl.EffectBloom;
        if (snap.EffectSurfaceSandstorm < 0f && tmpl.EffectSurfaceSandstorm >= 0f)
            snap.EffectSurfaceSandstorm = tmpl.EffectSurfaceSandstorm;
    }

    // ── PlacedObjects

    private static void ParsePlacedObjectsContent(SettingsSnapshot snap, string content)
    {
        foreach (string obj in SplitPlacedObjects(content))
        {
            if (string.IsNullOrEmpty(obj) || obj.Trim().Length == 0) continue;
            int idx = snap.PlacedObjectLines.Count;
            snap.PlacedObjectLines.Add(obj);

            int sep = obj.IndexOf("><");
            string typeName = sep >= 0 ? obj.Substring(0, sep) : obj;

            switch (typeName)
            {
                case "CustomDecal": ExtractDecalOpacities(snap, idx, obj); break;
                case "LightSource": ExtractLightIntensity(snap, idx, obj); break;
                case "LightBeam":   ExtractLightBeam(snap, idx, obj);      break;
            }
        }
    }

    private static List<string> SplitPlacedObjects(string content)
    {
        var result = new List<string>();
        int start = 0;
        for (int i = 0; i < content.Length - 1; i++)
        {
            if (content[i] != ',' || content[i + 1] != ' ') continue;
            int nameStart = i + 2;
            int nameEnd = content.IndexOf("><", nameStart);
            if (nameEnd <= nameStart) continue;
            string candidate = content.Substring(nameStart, nameEnd - nameStart);
            if (candidate.Length > 0 && char.IsUpper(candidate[0]) && candidate.IndexOf(' ') < 0)
            {
                result.Add(content.Substring(start, i - start));
                start = nameStart;
                i = nameStart - 1;
            }
        }
        if (start < content.Length)
            result.Add(content.Substring(start));
        return result;
    }

    private static void ExtractDecalOpacities(SettingsSnapshot snap, int idx, string obj)
    {
        string[] t = obj.Split('~');
        int[] oi = new int[] { 12, 14, 16, 18 };
        float[] ops = new float[4];
        bool any = false;
        for (int i = 0; i < 4; i++)
        {
            float v;
            if (oi[i] < t.Length && float.TryParse(t[oi[i]].Trim(), NF, INV, out v))
            { ops[i] = v; any = true; }
        }
        if (any) snap.DecalOpacities[idx] = ops;
    }

    private static void ExtractLightIntensity(SettingsSnapshot snap, int idx, string obj)
    {
        // Formato: LightSource><posX><posY><ALPHA~Environment~panelOffX~panelOffY~...
        int tildePos = obj.IndexOf('~');
        string header = tildePos >= 0 ? obj.Substring(0, tildePos) : obj;
        string[] parts = header.Split('>');

        // parts[3] es la posición fija del alpha. Iterar de atrás es frágil porque
        const int ALPHA_PART_INDEX = 3;
        if (parts.Length > ALPHA_PART_INDEX)
        {
            float v;
            if (float.TryParse(parts[ALPHA_PART_INDEX].TrimStart('<').Trim(), NF, INV, out v))
            {
                snap.LightIntensities[idx] = v;
                return;
            }
        }

        // Fallback defensivo: si el header tiene menos partes de lo esperado
        for (int i = parts.Length - 1; i >= 0; i--)
        {
            string p = parts[i].TrimStart('<').Trim();
            float v;
            if (float.TryParse(p, NF, INV, out v))
            {
                RSPlugin.log.LogWarning(
                    $"[SettingsSnapshot] LightSource idx={idx}: parts[{ALPHA_PART_INDEX}] " +
                    $"no existe o no es float (header='{header}'). " +
                    $"Fallback heurístico → v={v}. Revisar formato del archivo.");
                snap.LightIntensities[idx] = v;
                return;
            }
        }
    }

    private static void ExtractLightBeam(SettingsSnapshot snap, int idx, string obj)
    {
        string[] t = obj.Split('~');
        var d = new LightBeamData();
        float v;
        if (8  < t.Length && float.TryParse(t[8].Trim(),  NF, INV, out v)) d.Opacity = v;
        if (9  < t.Length && float.TryParse(t[9].Trim(),  NF, INV, out v)) d.ColorA  = v;
        if (10 < t.Length && float.TryParse(t[10].Trim(), NF, INV, out v)) d.ColorB  = v;
        snap.LightBeams[idx] = d;
    }

    // Parsea la línea de Effects y extrae los amounts de los efectos escalares.
    private static void ParseEffectAmounts(SettingsSnapshot snap, string effectsLine)
    {
        string[] entries = effectsLine.Split(new[] { ", " }, System.StringSplitOptions.RemoveEmptyEntries);
        foreach (string entry in entries)
        {
            string[] parts = entry.Trim().Split('-');
            if (parts.Length < 2) continue;
            float amount;
            if (!float.TryParse(parts[1].Trim(), NF, INV, out amount)) continue;

            switch (parts[0].Trim())
            {
                case "Darkness":          snap.EffectDarkness        = amount; break;
                case "Brightness":        snap.EffectBrightness      = amount; break;
                case "Contrast":          snap.EffectContrast        = amount; break;
                case "Desaturation":      snap.EffectDesaturation    = amount; break;
                case "Hue":               snap.EffectHue             = amount; break;
                case "DarkenLights":      snap.EffectDarkenLights    = amount; break;
                case "Fog":               snap.EffectFog             = amount; break;
                case "SkyBloom":          snap.EffectSkyBloom        = amount; break;
                case "SkyAndLightBloom":  snap.EffectSkyAndLightBloom = amount; break;
                case "LightBurn":         snap.EffectLightBurn       = amount; break;
                case "Bloom":             snap.EffectBloom           = amount; break;
                case "SurfaceSandstorm":  snap.EffectSurfaceSandstorm = amount; break;
            }
        }
    }

    // Parsea un color hexadecimal #RRGGBB o RRGGBB → Color, null si falla.
    private static UnityEngine.Color? ParseHexColor(string hex)
    {
        hex = hex.Trim().TrimStart('#');
        if (hex.Length != 6) return null;
        try
        {
            byte r = System.Convert.ToByte(hex.Substring(0, 2), 16);
            byte g = System.Convert.ToByte(hex.Substring(2, 2), 16);
            byte b = System.Convert.ToByte(hex.Substring(4, 2), 16);
            return new UnityEngine.Color(r / 255f, g / 255f, b / 255f);
        }
        catch { return null; }
    }
}