using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace FilesSetting;

// ════════════════════════════════════════════════════════════════════════
// PARSING
// Lee un settings file y rellena un SettingsSnapshot.
// ════════════════════════════════════════════════════════════════════════

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

<<<<<<< Updated upstream:src/Devtools/Blend/SettingsSnapshotParser.cs

            if (line.StartsWith("FadePalette: "))
            {
                string[] parts = line.Substring("FadePalette: ".Length).Trim().Split(',');
                if (parts.Length >= 1 && int.TryParse(parts[0].Trim(), out int fpid))
=======
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
>>>>>>> Stashed changes:src/Snapshot/SettingsSnapshotParser.cs
                {
                    string sv = val.Trim();
                    snap.Effects = sv;
                    ParseEffectAmounts(snap, sv);
                    break;
                }

<<<<<<< Updated upstream:src/Devtools/Blend/SettingsSnapshotParser.cs
            // Campo propio del mod — Rain World ignora líneas que no reconoce.
            // Formato: RC_TINT: #RRGGBB #RRGGBB
            if (line.StartsWith("RC_TINT: "))
            {
                string[] hexes = line.Substring("RC_TINT: ".Length).Trim().Split(' ');
                if (hexes.Length >= 1) snap.TintMultiply        = ParseHexColor(hexes[0]);
                if (hexes.Length >= 2) snap.TintAtmosphere      = ParseHexColor(hexes[1]);
                if (hexes.Length >= 3) snap.TintCloudAtmosphere = ParseHexColor(hexes[2]);
=======
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
>>>>>>> Stashed changes:src/Snapshot/SettingsSnapshotParser.cs
            }
        }
        return snap;
    }

    // ── Template fallback ────────────────────────────────────────────────

    public static SettingsSnapshot FromFileWithTemplate(string path, string roomName)
    {
        var snap = FromFile(path);
        FillFromTemplate(snap, roomName);
        return snap;
    }

    private static void FillFromTemplate(SettingsSnapshot snap, string roomName)
    {
        if (snap.Template.ToUpper() == "NONE") return;

        string region = roomName.Contains("_")
            ? roomName.Split('_')[0].ToLower()
            : roomName.ToLower();

        string templateName = "outside";
        if (!string.IsNullOrEmpty(snap.Template))
        {
            string t = snap.Template.ToLower();
            templateName = t.Contains("_")
                ? t.Split('_')[t.Split('_').Length - 1]
                : t;
        }

        string templateFile = region + "_settingstemplate_" + templateName + ".txt";
        string templateRelative = System.IO.Path.Combine("World", region, templateFile);

        string templatePath = null;
        for (int i = ModManager.ActiveMods.Count - 1; i >= 0; i--)
        {
<<<<<<< Updated upstream:src/Devtools/Blend/SettingsSnapshotParser.cs
            Plugin.RSPlugin.log.LogWarning("[SettingsSnapshot] Template not found: " + templatePath);
=======
            string candidate = System.IO.Path.Combine(ModManager.ActiveMods[i].path, templateRelative);
            if (System.IO.File.Exists(candidate)) { templatePath = candidate; break; }
        }
        // Fallback: buscar en todas las subcarpetas de StreamingAssets/mods/
        // cubre mods que Rain World carga pero BepInEx no registra (ej: moreslugcats built-in)
        if (templatePath == null)
        {
            string modsRoot = System.IO.Path.Combine(UnityEngine.Application.streamingAssetsPath, "mods");
            if (System.IO.Directory.Exists(modsRoot))
            {
                foreach (string found in System.IO.Directory.GetFiles(modsRoot, templateFile, System.IO.SearchOption.AllDirectories))
                { templatePath = found; break; }
            }
        }
        // Fallback final: StreamingAssets base (vanilla)
        if (templatePath == null)
        {
            string basePath = System.IO.Path.Combine(UnityEngine.Application.streamingAssetsPath, templateRelative);
            if (System.IO.File.Exists(basePath)) templatePath = basePath;
        }

        if (templatePath == null || !System.IO.File.Exists(templatePath))
        {
            RSPlugin.log.LogDebug("[SettingsSnapshot] Template not found: " + (templatePath ?? "null"));
>>>>>>> Stashed changes:src/Snapshot/SettingsSnapshotParser.cs
            return;
        }

        var tmpl = FromFile(templatePath);

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
    }

    // ── PlacedObjects ────────────────────────────────────────────────────

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
        // El alpha está siempre en parts[3] del header (split por '>'),
        // donde parts[0]="LightSource", parts[1]=posX, parts[2]=posY, parts[3]=alpha.
        // Se lee desde el header (antes del primer '~') para no confundirlo con
        // los campos numéricos que siguen (offsets de panel, radio, flags, etc.).
        int tildePos = obj.IndexOf('~');
        string header = tildePos >= 0 ? obj.Substring(0, tildePos) : obj;
        string[] parts = header.Split('>');

        // parts[3] es la posición fija del alpha. Iterar de atrás es frágil porque
        // versiones futuras del editor pueden agregar campos extra al header.
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
        // (formato no estándar), buscar el último float parseable hacia atrás.
        // Esto no debería dispararse con archivos generados por el editor de RW.
        for (int i = parts.Length - 1; i >= 0; i--)
        {
            string p = parts[i].TrimStart('<').Trim();
            float v;
            if (float.TryParse(p, NF, INV, out v))
            {
                Plugin.RSPlugin.log.LogWarning(
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

<<<<<<< Updated upstream:src/Devtools/Blend/SettingsSnapshotParser.cs
    // ── Helpers de parsing ───────────────────────────────────────────────

    private static bool TryParseInt(string line, string prefix, out int val)
    {
        val = 0;
        if (!line.StartsWith(prefix)) return false;
        return int.TryParse(line.Substring(prefix.Length).Trim(), out val);
    }

    private static bool TryParseFloat(string line, string prefix, out float val)
    {
        val = 0f;
        if (!line.StartsWith(prefix)) return false;
        return float.TryParse(line.Substring(prefix.Length).Trim(), NF, INV, out val);
    }

    private static bool TryParseValue(string line, string prefix, out string val)
    {
        val = "";
        if (!line.StartsWith(prefix)) return false;
        val = line.Substring(prefix.Length).Trim();
        return true;
    }

    // Parsea "R, G, B" como Color. Acepta valores 0..1.
    private static bool TryParseColor(string line, string prefix, out UnityEngine.Color color)
    {
        color = UnityEngine.Color.white;
        if (!line.StartsWith(prefix)) return false;
        string[] parts = line.Substring(prefix.Length).Trim().Split(',');
        if (parts.Length < 3) return false;
        float r, g, b;
        if (!float.TryParse(parts[0].Trim(), NF, INV, out r)) return false;
        if (!float.TryParse(parts[1].Trim(), NF, INV, out g)) return false;
        if (!float.TryParse(parts[2].Trim(), NF, INV, out b)) return false;
        color = new UnityEngine.Color(r, g, b);
        return true;
    }

=======
>>>>>>> Stashed changes:src/Snapshot/SettingsSnapshotParser.cs
    // Parsea la línea de Effects y extrae los amounts de los efectos escalares.
    // Formato del juego: "EffectName-amount-panelX-panelY, EffectName2-amount2-..."
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