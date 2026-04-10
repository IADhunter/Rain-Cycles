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

            if (line.StartsWith("PlacedObjects: "))
            {
                ParsePlacedObjectsContent(snap, line.Substring("PlacedObjects: ".Length));
                continue;
            }

            int iv; float fv; string sv;
            if (TryParseInt(line,   "Palette: ",               out iv))  { snap.Palette = iv;               snap._hasPalette = true; }
            if (TryParseFloat(line, "Grime: ",                 out fv))  { snap.Grime = fv;                 snap._hasGrime = true; }
            if (TryParseFloat(line, "Clouds: ",                out fv))  { snap.Clouds = fv;                snap._hasClouds = true; }
            if (TryParseFloat(line, "CeilingDrips: ",          out fv))  { snap.CeilingDrips = fv;          snap._hasCeilingDrips = true; }
            if (TryParseFloat(line, "BkgDroneVolume: ",        out fv))  { snap.BkgDroneVolume = fv;        snap._hasBkgDroneVolume = true; }
            if (TryParseFloat(line, "RandomItemDensity: ",     out fv))  { snap.RandomItemDensity = fv;     snap._hasRandomItemDensity = true; }
            if (TryParseFloat(line, "RandomItemSpearChance: ", out fv))  { snap.RandomItemSpearChance = fv; snap._hasRandomItemSpearChance = true; }
            if (TryParseFloat(line, "WaterReflectionAlpha: ",  out fv))  snap.WaterReflectionAlpha = fv;
            if (TryParseInt(line,   "EffectColorA: ",          out iv))  { snap.EffectColorA = iv;          snap._hasEffectColorA = true; }
            if (TryParseInt(line,   "EffectColorB: ",          out iv))  { snap.EffectColorB = iv;          snap._hasEffectColorB = true; }
            if (TryParseValue(line, "DangerType: ",     out sv))  snap.DangerType = sv;
            if (TryParseValue(line, "Template: ",       out sv))  snap.Template = sv;
            if (TryParseValue(line, "Triggers: ",       out sv))  snap.Triggers = sv;
            if (TryParseValue(line, "AmbientSounds: ",  out sv))  snap.AmbientSounds = sv;

            if (TryParseValue(line, "Effects: ", out sv))
            {
                snap.Effects = sv;
                ParseEffectAmounts(snap, sv);
            }

            if (line.StartsWith("FadePalette: "))
            {
                string[] parts = line.Substring("FadePalette: ".Length).Trim().Split(',');
                if (parts.Length >= 1 && int.TryParse(parts[0].Trim(), out int fpid))
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
            }

            // Campo propio del mod — Rain World ignora líneas que no reconoce.
            if (line.StartsWith("RC_TINT: "))
            {
                string[] hexes = line.Substring("RC_TINT: ".Length).Trim().Split(' ');
                if (hexes.Length >= 1) snap.TintMultiply        = ParseHexColor(hexes[0]);
                if (hexes.Length >= 2) snap.TintAtmosphere      = ParseHexColor(hexes[1]);
                if (hexes.Length >= 3) snap.TintCloudAtmosphere = ParseHexColor(hexes[2]);
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

        string templatePath = AssetManager.ResolveFilePath(
            "World" + System.IO.Path.DirectorySeparatorChar +
            region + System.IO.Path.DirectorySeparatorChar +
            region + "_settingstemplate_" + templateName + ".txt");

        if (!System.IO.File.Exists(templatePath))
        {
            RSPlugin.log.LogWarning("[SettingsSnapshot] Template not found: " + templatePath);
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

            if      (typeName == "CustomDecal") ExtractDecalOpacities(snap, idx, obj);
            else if (typeName == "LightSource") ExtractLightIntensity(snap, idx, obj);
            else if (typeName == "LightBeam")   ExtractLightBeam(snap, idx, obj);
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

    // ── Helpers de parsing

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
                case "Darkness":     snap.EffectDarkness     = amount; break;
                case "Brightness":   snap.EffectBrightness   = amount; break;
                case "Contrast":     snap.EffectContrast     = amount; break;
                case "Desaturation": snap.EffectDesaturation = amount; break;
                case "Hue":          snap.EffectHue          = amount; break;
                case "DarkenLights": snap.EffectDarkenLights = amount; break;
                case "Fog":          snap.EffectFog          = amount; break;
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