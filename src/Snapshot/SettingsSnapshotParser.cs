using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace RainCycles.Snapshot;

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

            if (key == "RainCycles")
            {
                ParseRainCyclesContent(snap, val.Trim());
                continue;
            }

            switch (key)
            {
                case "PlacedObjects": ParsePlacedObjectsContent(snap, val); break;
                case "Palette": if (int.TryParse(val.Trim(), out int p)) { snap.Palette = p; snap._hasPalette = true; } break;
                case "Grime": if (float.TryParse(val.Trim(), NF, INV, out float g)) { snap.Grime = g; snap._hasGrime = true; } break;
                case "Clouds": if (float.TryParse(val.Trim(), NF, INV, out float c)) { snap.Clouds = c; snap._hasClouds = true; } break;
                case "CeilingDrips": if (float.TryParse(val.Trim(), NF, INV, out float cd)) { snap.CeilingDrips = cd; snap._hasCeilingDrips = true; } break;
                case "BkgDroneVolume": if (float.TryParse(val.Trim(), NF, INV, out float bv)) { snap.BkgDroneVolume = bv; snap._hasBkgDroneVolume = true; } break;
                case "RandomItemDensity": if (float.TryParse(val.Trim(), NF, INV, out float rd)) { snap.RandomItemDensity = rd; snap._hasRandomItemDensity = true; } break;
                case "RandomItemSpearChance": if (float.TryParse(val.Trim(), NF, INV, out float rs)) { snap.RandomItemSpearChance = rs; snap._hasRandomItemSpearChance = true; } break;
                case "WaterReflectionAlpha": if (float.TryParse(val.Trim(), NF, INV, out float wa)) snap.WaterReflectionAlpha = wa; break;
                case "EffectColorA": if (int.TryParse(val.Trim(), out int eca)) { snap.EffectColorA = eca; snap._hasEffectColorA = true; } break;
                case "EffectColorB": if (int.TryParse(val.Trim(), out int ecb)) { snap.EffectColorB = ecb; snap._hasEffectColorB = true; } break;
                case "DangerType": snap.DangerType = val.Trim(); break;
                case "Template": snap.Template = val.Trim(); break;
                case "Triggers": snap.Triggers = val.Trim(); break;
                case "AmbientSounds": snap.AmbientSounds = val.Trim(); break;
                case "Effects": snap.Effects = val.Trim(); ParseEffectAmounts(snap, snap.Effects); break;
                case "TerrainPalette": snap.TerrainPaletteName = val.Trim(); snap._hasTerrainPalette = true; break;
                case "TerrainWaves": if (float.TryParse(val.Trim(), NF, INV, out float tw)) snap.TerrainWaves = tw; break;
                case "TerrainLight": if (float.TryParse(val.Trim(), NF, INV, out float tl)) snap.TerrainLight = tl; break;
                case "TerrainGrain": if (float.TryParse(val.Trim(), NF, INV, out float tg)) snap.TerrainGrain = tg; break;
                case "TerrainSkyFade": if (float.TryParse(val.Trim(), NF, INV, out float ts)) snap.TerrainSkyFade = ts; break;
                case "TerrainStainAmount": if (float.TryParse(val.Trim(), NF, INV, out float ta)) snap.TerrainStainAmount = ta; break;
                case "TerrainStainBrightness": if (float.TryParse(val.Trim(), NF, INV, out float tb)) snap.TerrainStainBrightness = tb; break;
                case "TerrainStainHeight": if (float.TryParse(val.Trim(), NF, INV, out float tsh)) snap.TerrainStainHeight = tsh; break;

                case "TerrainFadePalette":
                {
                    string[] parts = val.Trim().Split(',');
                    if (parts.Length >= 1 && !string.IsNullOrEmpty(parts[0].Trim()))
                    {
                        snap.TerrainFadePaletteName = parts[0].Trim();
                        snap._hasTerrainFadePalette = true;
                        var ops = new List<float>();
                        for (int i = 1; i < parts.Length; i++)
                            if (float.TryParse(parts[i].Trim(), NF, INV, out float op))
                                ops.Add(op);
                        snap.TerrainFadeOpacities = ops.ToArray();
                    }
                    break;
                }

                case "FadePalette":
                {
                    string[] parts = val.Trim().Split(',');
                    if (parts.Length >= 1 && int.TryParse(parts[0].Trim(), out int fpid))
                    {
                        snap.FadePaletteID = fpid;
                        snap._hasFadePalette = true;
                        var ops = new List<float>();
                        for (int i = 1; i < parts.Length; i++)
                            if (float.TryParse(parts[i].Trim(), NF, INV, out float op))
                                ops.Add(op);
                        snap.FadePaletteOpacities = ops.ToArray();
                    }
                    break;
                }
            }
        }

        return snap;
    }

    // ============================================================
    // PARSEAR RAINCYCLES - FORMATO MODULAR
    // ============================================================
    private static void ParseRainCyclesContent(SettingsSnapshot snap, string content)
    {
        int pos = 0;
        while (pos < content.Length)
        {
            int start = content.IndexOf('<', pos);
            if (start < 0) break;
            int end = content.IndexOf('>', start);
            if (end < 0) break;

            string segment = content.Substring(start + 1, end - start - 1);
            int sep = segment.IndexOf(':');
            if (sep > 0)
            {
                string field = segment.Substring(0, sep).Trim();
                string value = segment.Substring(sep + 1).Trim();

                switch (field)
                {
                    case "Type":
                        snap.RcType = value.ToUpperInvariant() switch
                        {
                            "STATIC" => RcType.Static,
                            "BLEND" => RcType.Blend,
                            _ => RcType.None
                        };
                        break;
                    case "View":
                        if (snap.HasRcType)
                        {
                            snap.ViewType = value.ToUpperInvariant() switch
                            {
                                "ACV" => ViewType.ACV,
                                "RTV" => ViewType.RTV,
                                "PSV" => ViewType.PSV,
                                "AUV" => ViewType.AUV,
                                "ORV" => ViewType.ORV,
                                _ => ViewType.None
                            };
                        }
                        break;
                    case "Tint":
                        if (snap.HasView)
                        {
                            string[] hexes = value.Split(' ');
                            if (hexes.Length >= 1) snap.TintMultiply = ParseHexColor(hexes[0]);
                            if (hexes.Length >= 2) snap.TintAtmosphere = ParseHexColor(hexes[1]);
                        }
                        break;
                }
            }
            pos = end + 1;
        }
    }

    public static SettingsSnapshot FromFileWithTemplate(string path, string roomName)
    {
        var snap = FromFile(path);
        FillFromTemplate(snap, roomName, path);
        return snap;
    }

    private static void FillFromTemplate(SettingsSnapshot snap, string roomName, string settingsPath)
    {
        if (snap.Template.ToUpperInvariant() == "NONE") return;

        string region = roomName.Contains("_")
            ? roomName.Split('_')[0].ToLower()
            : roomName.ToLower();

        string settingsModDir = GetSettingsModDirectory(settingsPath, region);
        string templateName;

        if (string.IsNullOrEmpty(snap.Template))
        {
            templateName = GetFirstTemplateFromProperties(region, settingsModDir);
            if (string.IsNullOrEmpty(templateName))
            {
                return;
            }
        }
        else
        {
            templateName = snap.Template.ToLower();
            int lastUnderscore = templateName.LastIndexOf('_');
            if (lastUnderscore >= 0)
                templateName = templateName.Substring(lastUnderscore + 1);
        }

        string templatePath = ResolveTemplatePath(region, templateName, settingsModDir);
        if (templatePath == null || !File.Exists(templatePath))
        {
            RSPlugin.log.LogWarning($"[Template] {roomName}: template '{templateName}' no encontrado en región {region}");
            return;
        }

        SettingsSnapshot tmpl;
        if (!TryGetCached(templatePath, out tmpl))
        {
            tmpl = FromFile(templatePath);
            _snapshotCache[templatePath] = tmpl;
        }

        if (!snap._hasPalette) snap.Palette = tmpl.Palette;
        if (!snap._hasGrime) snap.Grime = tmpl.Grime;
        if (!snap._hasClouds) snap.Clouds = tmpl.Clouds;
        if (!snap._hasCeilingDrips) snap.CeilingDrips = tmpl.CeilingDrips;
        if (!snap._hasBkgDroneVolume) snap.BkgDroneVolume = tmpl.BkgDroneVolume;
        if (!snap._hasRandomItemDensity) snap.RandomItemDensity = tmpl.RandomItemDensity;
        if (!snap._hasRandomItemSpearChance) snap.RandomItemSpearChance = tmpl.RandomItemSpearChance;
        if (!snap._hasEffectColorA) snap.EffectColorA = tmpl.EffectColorA;
        if (!snap._hasEffectColorB) snap.EffectColorB = tmpl.EffectColorB;
        if (!snap._hasFadePalette) { snap.FadePaletteID = tmpl.FadePaletteID; snap.FadePaletteOpacities = tmpl.FadePaletteOpacities; }
        if (!snap._hasTerrainPalette) snap.TerrainPaletteName = tmpl.TerrainPaletteName;
        if (!snap._hasTerrainFadePalette) { snap.TerrainFadePaletteName = tmpl.TerrainFadePaletteName; snap.TerrainFadeOpacities = tmpl.TerrainFadeOpacities; }

        if (snap.TerrainWaves == null) snap.TerrainWaves = tmpl.TerrainWaves;
        if (snap.TerrainLight == null) snap.TerrainLight = tmpl.TerrainLight;
        if (snap.TerrainGrain == null) snap.TerrainGrain = tmpl.TerrainGrain;
        if (snap.TerrainSkyFade == null) snap.TerrainSkyFade = tmpl.TerrainSkyFade;
        if (snap.TerrainStainAmount == null) snap.TerrainStainAmount = tmpl.TerrainStainAmount;
        if (snap.TerrainStainBrightness == null) snap.TerrainStainBrightness = tmpl.TerrainStainBrightness;
        if (snap.TerrainStainHeight == null) snap.TerrainStainHeight = tmpl.TerrainStainHeight;

        FillEffectFromTemplate(snap, tmpl);
    }

    private static string GetFirstTemplateFromProperties(string region, string settingsModDir)
    {
        try
        {
            string upperRegion = region.ToUpperInvariant();

            if (!string.IsNullOrEmpty(settingsModDir))
            {
                string modRoot = GetModRoot(settingsModDir);
                if (!string.IsNullOrEmpty(modRoot))
                {
                    string candidate = Path.Combine(modRoot, "world", upperRegion, "properties.txt");
                    if (File.Exists(candidate))
                    {
                        string first = ParseFirstTemplate(candidate);
                        if (first != null)
                        {
                            return first;
                        }
                    }
                }
            }

            for (int i = ModManager.ActiveMods.Count - 1; i >= 0; i--)
            {
                if (ModManager.ActiveMods[i] == null) continue;
                string candidate = Path.Combine(ModManager.ActiveMods[i].path, "world", upperRegion, "properties.txt");
                if (File.Exists(candidate))
                {
                    string first = ParseFirstTemplate(candidate);
                    if (first != null)
                    {
                        return first;
                    }
                }
            }

            string vanillaPath = Path.Combine(Application.streamingAssetsPath, "world", upperRegion, "properties.txt");
            if (File.Exists(vanillaPath))
            {
                string first = ParseFirstTemplate(vanillaPath);
                if (first != null)
                {
                    return first;
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            RSPlugin.log.LogWarning($"[Template] {region}: error al leer properties.txt: {ex.Message}");
            return null;
        }
    }

    private static string ParseFirstTemplate(string propertiesPath)
    {
        try
        {
            foreach (string rawLine in File.ReadAllLines(propertiesPath, Encoding.UTF8))
            {
                string line = rawLine.Trim();
                if (!line.StartsWith("Room Setting Templates:")) continue;
                int sep = line.IndexOf(':');
                if (sep < 0) continue;
                string templatesLine = line.Substring(sep + 1).Trim();
                string[] templates = templatesLine.Split(',');

                var templateList = new List<string>();
                foreach (string t in templates)
                {
                    string clean = t.Trim();
                    if (!string.IsNullOrEmpty(clean))
                        templateList.Add(clean);
                }

                if (templateList.Count > 0)
                {
                    return templateList[0];
                }
            }
        }
        catch (Exception ex)
        {
            RSPlugin.log.LogWarning($"[Template] Error al parsear {Path.GetFileName(propertiesPath)}: {ex.Message}");
        }
        return null;
    }

    private static string ResolveTemplatePath(string region, string templateName, string settingsModDir)
    {
        string templateFile = $"{region}_settingstemplate_{templateName}.txt";

        if (!string.IsNullOrEmpty(settingsModDir) && Directory.Exists(settingsModDir))
        {
            string candidate = Path.Combine(settingsModDir, templateFile);
            if (File.Exists(candidate)) return candidate;

            foreach (string file in Directory.GetFiles(settingsModDir, "*.txt"))
            {
                if (string.Equals(Path.GetFileName(file), templateFile, StringComparison.OrdinalIgnoreCase))
                    return file;
            }
        }

        string templateRelative = Path.Combine("world", region, templateFile);
        for (int i = ModManager.ActiveMods.Count - 1; i >= 0; i--)
        {
            if (ModManager.ActiveMods[i] == null) continue;
            string candidate = Path.Combine(ModManager.ActiveMods[i].path, templateRelative);
            if (File.Exists(candidate)) return candidate;

            string modDir = Path.Combine(ModManager.ActiveMods[i].path, "world", region);
            if (Directory.Exists(modDir))
            {
                foreach (string file in Directory.GetFiles(modDir, "*.txt"))
                {
                    if (string.Equals(Path.GetFileName(file), templateFile, StringComparison.OrdinalIgnoreCase))
                        return file;
                }
            }
        }

        string basePath = Path.Combine(Application.streamingAssetsPath, templateRelative);
        if (File.Exists(basePath)) return basePath;

        string vanillaDir = Path.Combine(Application.streamingAssetsPath, "world", region);
        if (Directory.Exists(vanillaDir))
        {
            foreach (string file in Directory.GetFiles(vanillaDir, "*.txt"))
            {
                if (string.Equals(Path.GetFileName(file), templateFile, StringComparison.OrdinalIgnoreCase))
                    return file;
            }
        }

        return null;
    }

    private static void FillEffectFromTemplate(SettingsSnapshot snap, SettingsSnapshot tmpl)
    {
        if (snap.EffectDarkness < 0f && tmpl.EffectDarkness >= 0f) snap.EffectDarkness = tmpl.EffectDarkness;
        if (snap.EffectBrightness < 0f && tmpl.EffectBrightness >= 0f) snap.EffectBrightness = tmpl.EffectBrightness;
        if (snap.EffectContrast < 0f && tmpl.EffectContrast >= 0f) snap.EffectContrast = tmpl.EffectContrast;
        if (snap.EffectDesaturation < 0f && tmpl.EffectDesaturation >= 0f) snap.EffectDesaturation = tmpl.EffectDesaturation;
        if (snap.EffectHue < 0f && tmpl.EffectHue >= 0f) snap.EffectHue = tmpl.EffectHue;
        if (snap.EffectDarkenLights < 0f && tmpl.EffectDarkenLights >= 0f) snap.EffectDarkenLights = tmpl.EffectDarkenLights;
        if (snap.EffectFog < 0f && tmpl.EffectFog >= 0f) snap.EffectFog = tmpl.EffectFog;
        if (snap.EffectSkyBloom < 0f && tmpl.EffectSkyBloom >= 0f) snap.EffectSkyBloom = tmpl.EffectSkyBloom;
        if (snap.EffectSkyAndLightBloom < 0f && tmpl.EffectSkyAndLightBloom >= 0f) snap.EffectSkyAndLightBloom = tmpl.EffectSkyAndLightBloom;
        if (snap.EffectLightBurn < 0f && tmpl.EffectLightBurn >= 0f) snap.EffectLightBurn = tmpl.EffectLightBurn;
        if (snap.EffectBloom < 0f && tmpl.EffectBloom >= 0f) snap.EffectBloom = tmpl.EffectBloom;
        if (snap.EffectSurfaceSandstorm < 0f && tmpl.EffectSurfaceSandstorm >= 0f) snap.EffectSurfaceSandstorm = tmpl.EffectSurfaceSandstorm;
        if (snap.EffectSnowLight < 0f && tmpl.EffectSnowLight >= 0f) snap.EffectSnowLight = tmpl.EffectSnowLight;
        if (snap.EffectSnowSparkle < 0f && tmpl.EffectSnowSparkle >= 0f) snap.EffectSnowSparkle = tmpl.EffectSnowSparkle;

        if (tmpl.ModifyEffectColorA_Hue.HasValue && !snap.ModifyEffectColorA_Hue.HasValue)
            snap.ModifyEffectColorA_Hue = tmpl.ModifyEffectColorA_Hue;
        if (tmpl.ModifyEffectColorA_Saturation.HasValue && !snap.ModifyEffectColorA_Saturation.HasValue)
            snap.ModifyEffectColorA_Saturation = tmpl.ModifyEffectColorA_Saturation;
        if (tmpl.ModifyEffectColorA_Value.HasValue && !snap.ModifyEffectColorA_Value.HasValue)
            snap.ModifyEffectColorA_Value = tmpl.ModifyEffectColorA_Value;
        if (tmpl.ModifyEffectColorB_Hue.HasValue && !snap.ModifyEffectColorB_Hue.HasValue)
            snap.ModifyEffectColorB_Hue = tmpl.ModifyEffectColorB_Hue;
        if (tmpl.ModifyEffectColorB_Saturation.HasValue && !snap.ModifyEffectColorB_Saturation.HasValue)
            snap.ModifyEffectColorB_Saturation = tmpl.ModifyEffectColorB_Saturation;
        if (tmpl.ModifyEffectColorB_Value.HasValue && !snap.ModifyEffectColorB_Value.HasValue)
            snap.ModifyEffectColorB_Value = tmpl.ModifyEffectColorB_Value;

        if (!string.IsNullOrEmpty(tmpl.Effects))
        {
            if (string.IsNullOrEmpty(snap.Effects))
            {
                snap.Effects = tmpl.Effects;
                ParseEffectAmounts(snap, tmpl.Effects);
            }
            else
            {
                var existingNames = new HashSet<string>();
                foreach (string entry in snap.Effects.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string name = entry.Trim().Split('-')[0];
                    if (!string.IsNullOrEmpty(name)) existingNames.Add(name);
                }

                var toAdd = new List<string>();
                foreach (string entry in tmpl.Effects.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string name = entry.Trim().Split('-')[0];
                    if (!string.IsNullOrEmpty(name) && !existingNames.Contains(name))
                        toAdd.Add(entry.Trim());
                }

                if (toAdd.Count > 0)
                {
                    snap.Effects = snap.Effects.TrimEnd(' ', ',') + ", " + string.Join(", ", toAdd);
                    ParseEffectAmounts(snap, snap.Effects);
                }
            }
        }
    }

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
                case "LightBeam": ExtractLightBeam(snap, idx, obj); break;
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
        int[] oi = { 12, 14, 16, 18 };
        float[] ops = new float[4];
        bool any = false;
        for (int i = 0; i < 4; i++)
            if (oi[i] < t.Length && float.TryParse(t[oi[i]].Trim(), NF, INV, out ops[i])) any = true;
        if (any) snap.DecalOpacities[idx] = ops;
    }

    private static void ExtractLightIntensity(SettingsSnapshot snap, int idx, string obj)
    {
        int tildePos = obj.IndexOf('~');
        string header = tildePos >= 0 ? obj.Substring(0, tildePos) : obj;
        string[] parts = header.Split('>');

        if (parts.Length > 3 && float.TryParse(parts[3].TrimStart('<').Trim(), NF, INV, out float v))
        {
            snap.LightIntensities[idx] = v;
        }
    }

    private static void ExtractLightBeam(SettingsSnapshot snap, int idx, string obj)
    {
        string[] t = obj.Split('~');
        var d = new LightBeamData();
        if (8 < t.Length && float.TryParse(t[8].Trim(), NF, INV, out float v)) d.Opacity = v;
        if (9 < t.Length && float.TryParse(t[9].Trim(), NF, INV, out v)) d.ColorA = v;
        if (10 < t.Length && float.TryParse(t[10].Trim(), NF, INV, out v)) d.ColorB = v;
        snap.LightBeams[idx] = d;
    }

    private static void ParseEffectAmounts(SettingsSnapshot snap, string effectsLine)
    {
        foreach (string entry in effectsLine.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries))
        {
            string[] parts = entry.Trim().Split('-');
            if (parts.Length < 2) continue;

            string effectName = parts[0].Trim();
            string valuesStr = parts[1].Trim();

            if (effectName == "ModifyEffectColorA")
            {
                string[] valueParts = valuesStr.Split(',');
                if (valueParts.Length >= 1 && float.TryParse(valueParts[0].Trim(), NF, INV, out float h))
                    snap.ModifyEffectColorA_Hue = h;
                if (valueParts.Length >= 2 && float.TryParse(valueParts[1].Trim(), NF, INV, out float s))
                    snap.ModifyEffectColorA_Saturation = s;
                if (valueParts.Length >= 3 && float.TryParse(valueParts[2].Trim(), NF, INV, out float v))
                    snap.ModifyEffectColorA_Value = v;
                continue;
            }

            if (effectName == "ModifyEffectColorB")
            {
                string[] valueParts = valuesStr.Split(',');
                if (valueParts.Length >= 1 && float.TryParse(valueParts[0].Trim(), NF, INV, out float h))
                    snap.ModifyEffectColorB_Hue = h;
                if (valueParts.Length >= 2 && float.TryParse(valueParts[1].Trim(), NF, INV, out float s))
                    snap.ModifyEffectColorB_Saturation = s;
                if (valueParts.Length >= 3 && float.TryParse(valueParts[2].Trim(), NF, INV, out float v))
                    snap.ModifyEffectColorB_Value = v;
                continue;
            }

            if (!float.TryParse(valuesStr, NF, INV, out float amount)) continue;

            switch (effectName)
            {
                case "Darkness": snap.EffectDarkness = amount; break;
                case "Brightness": snap.EffectBrightness = amount; break;
                case "Contrast": snap.EffectContrast = amount; break;
                case "Desaturation": snap.EffectDesaturation = amount; break;
                case "Hue": snap.EffectHue = amount; break;
                case "DarkenLights": snap.EffectDarkenLights = amount; break;
                case "Fog": snap.EffectFog = amount; break;
                case "SkyBloom": snap.EffectSkyBloom = amount; break;
                case "SkyAndLightBloom": snap.EffectSkyAndLightBloom = amount; break;
                case "LightBurn": snap.EffectLightBurn = amount; break;
                case "Bloom": snap.EffectBloom = amount; break;
                case "SurfaceSandstorm": snap.EffectSurfaceSandstorm = amount; break;
                case "SnowLight": snap.EffectSnowLight = amount; break;
                case "SnowSparkle": snap.EffectSnowSparkle = amount; break;
            }
        }
    }

    private static Color? ParseHexColor(string hex)
    {
        hex = hex.Trim().TrimStart('#');
        if (hex.Length != 6) return null;
        try
        {
            return new Color(
                Convert.ToByte(hex.Substring(0, 2), 16) / 255f,
                Convert.ToByte(hex.Substring(2, 2), 16) / 255f,
                Convert.ToByte(hex.Substring(4, 2), 16) / 255f);
        }
        catch { return null; }
    }

    private static string GetModRoot(string folder)
    {
        if (string.IsNullOrEmpty(folder)) return null;
        string dir = folder;
        while (!string.IsNullOrEmpty(dir))
        {
            string parent = Path.GetDirectoryName(dir);
            if (string.IsNullOrEmpty(parent)) break;
            if (Path.GetFileName(parent).Equals("mods", StringComparison.OrdinalIgnoreCase))
                return dir;
            dir = parent;
        }
        return null;
    }

    private static string GetSettingsModDirectory(string settingsPath, string region)
    {
        if (!string.IsNullOrEmpty(settingsPath))
        {
            string dir = Path.GetDirectoryName(settingsPath);
            while (!string.IsNullOrEmpty(dir))
            {
                if (Path.GetFileName(dir).Equals("world", StringComparison.OrdinalIgnoreCase))
                {
                    string regionDir = Path.Combine(dir, region);
                    if (Directory.Exists(regionDir)) return regionDir;
                    string regionRoomsDir = Path.Combine(dir, region + "-Rooms");
                    if (Directory.Exists(regionRoomsDir)) return regionRoomsDir;
                    break;
                }
                dir = Path.GetDirectoryName(dir);
            }
        }

        for (int i = ModManager.ActiveMods.Count - 1; i >= 0; i--)
        {
            if (ModManager.ActiveMods[i] == null) continue;
            string regionRoomsDir = Path.Combine(ModManager.ActiveMods[i].path, "world", region + "-Rooms");
            if (Directory.Exists(regionRoomsDir))
            {
                string regionDir = Path.Combine(ModManager.ActiveMods[i].path, "world", region);
                return Directory.Exists(regionDir) ? regionDir : regionRoomsDir;
            }
        }
        return null;
    }
}