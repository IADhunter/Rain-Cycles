using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace RainCycles.Snapshot;

public partial class SettingsSnapshot
{
    private static readonly CultureInfo SINV = CultureInfo.InvariantCulture;

    public string ToFileText()
    {
        if (string.IsNullOrEmpty(RawText)) return "";
        string[] lines = RawText.Split('\n');
        var sb = new StringBuilder();
        bool poWritten = false;

        string rainCyclesLine = BuildRainCyclesLine();
        bool hasRainCycles = !string.IsNullOrEmpty(rainCyclesLine);

        bool templateWritten = false;

        foreach (string rawLine in lines)
        {
            string line = rawLine.TrimEnd('\r');

            if (line.StartsWith("RC_TYPE:") || line.StartsWith("RC_VIEW:") || line.StartsWith("RC_TINT:"))
                continue;

            if (line.StartsWith("PlacedObjects: ") && !poWritten)
            {
                poWritten = true;
                sb.AppendLine(BuildPlacedObjectsLine());
                continue;
            }

            if (line.StartsWith("Template:") && hasRainCycles && !templateWritten)
            {
                templateWritten = true;
                sb.AppendLine(line);
                sb.AppendLine(rainCyclesLine);
                continue;
            }

            if      (line.StartsWith("Palette: "))               sb.AppendLine("Palette: " + Palette);
            else if (line.StartsWith("Grime: "))                 sb.AppendLine(SFL("Grime", Grime));
            else if (line.StartsWith("Clouds: "))                sb.AppendLine(SFL("Clouds", Clouds));
            else if (line.StartsWith("CeilingDrips: "))          sb.AppendLine(SFL("CeilingDrips", CeilingDrips));
            else if (line.StartsWith("BkgDroneVolume: "))        sb.AppendLine(SFL("BkgDroneVolume", BkgDroneVolume));
            else if (line.StartsWith("RandomItemDensity: "))     sb.AppendLine(SFL("RandomItemDensity", RandomItemDensity));
            else if (line.StartsWith("RandomItemSpearChance: ")) sb.AppendLine(SFL("RandomItemSpearChance", RandomItemSpearChance));
            else if (line.StartsWith("EffectColorA: "))          sb.AppendLine("EffectColorA: " + EffectColorA);
            else if (line.StartsWith("EffectColorB: "))          sb.AppendLine("EffectColorB: " + EffectColorB);
            else if (line.StartsWith("DangerType: "))            sb.AppendLine("DangerType: " + DangerType);
            else if (line.StartsWith("Template: "))              
            {
                if (hasRainCycles && !templateWritten)
                {
                    templateWritten = true;
                    sb.AppendLine(line);
                    sb.AppendLine(rainCyclesLine);
                }
                else
                {
                    sb.AppendLine(line);
                }
            }
            else if (line.StartsWith("Effects: "))               sb.AppendLine("Effects: " + Effects);
            else if (line.StartsWith("Triggers: "))              sb.AppendLine("Triggers: " + Triggers);
            else if (line.StartsWith("AmbientSounds: "))         sb.AppendLine("AmbientSounds: " + AmbientSounds);
            else if (line.StartsWith("FadePalette: "))           sb.AppendLine(BuildFadePaletteLine());
            else                                                sb.AppendLine(line);
        }

        if (hasRainCycles && !templateWritten)
        {
            sb.AppendLine(rainCyclesLine);
        }

        return sb.ToString();
    }

    // ============================================================
    // BUILD RAINCYCLES LINE - NUEVO FORMATO MODULAR
    // ============================================================
    private string BuildRainCyclesLine()
    {
        if (!HasRcType) return null;

        var parts = new List<string> { $"Type:{RcType}" };

        if (HasView)
        {
            parts.Add($"View:{ViewType}");

            if (HasTint)
            {
                string mul = TintMultiply.HasValue ? ColorToHex(TintMultiply.Value) : "FFFFFF";
                string atm = TintAtmosphere.HasValue ? ColorToHex(TintAtmosphere.Value) : "FFFFFF";
                parts.Add($"Tint:#{mul} #{atm}");
            }
        }

        return $"RainCycles: <{string.Join("><", parts)}>";
    }

    private string BuildFadePaletteLine()
    {
        var sb = new StringBuilder("FadePalette: " + FadePaletteID);
        foreach (float op in FadePaletteOpacities)
            sb.Append(", " + op.ToString("F7", SINV));
        return sb.ToString();
    }

    private string BuildPlacedObjectsLine()
    {
        var patched = new List<string>(PlacedObjectLines);
        foreach (var kv in DecalOpacities)
            if (kv.Key < patched.Count) patched[kv.Key] = PatchDecal(patched[kv.Key], kv.Value);
        foreach (var kv in LightIntensities)
            if (kv.Key < patched.Count) patched[kv.Key] = PatchLightIntensity(patched[kv.Key], kv.Value);
        return "PlacedObjects: " + string.Join(", ", patched);
    }

    private static string PatchDecal(string obj, float[] ops)
    {
        string[] t = obj.Split('~');
        int[] oi = { 12, 14, 16, 18 };
        for (int i = 0; i < 4; i++)
            if (oi[i] < t.Length)
                t[oi[i]] = ops[i].ToString("F7", SINV);
        return string.Join("~", t);
    }

    private static string PatchLightIntensity(string obj, float intensity)
    {
        int tp = obj.IndexOf('~');
        if (tp < 0) return obj;
        string header = obj.Substring(0, tp);
        string rest = obj.Substring(tp);
        string[] parts = header.Split('>');
        for (int i = parts.Length - 1; i >= 0; i--)
        {
            if (float.TryParse(parts[i].TrimStart('<').Trim(), NumberStyles.Float, SINV, out _))
            {
                parts[i] = "<" + intensity.ToString("F7", SINV);
                break;
            }
        }
        return string.Join(">", parts) + rest;
    }

    private static string SFL(string key, float v) =>
        key + ": " + v.ToString("F7", SINV);

    private static string ColorToHex(Color color)
    {
        return $"#{Mathf.RoundToInt(color.r * 255f):X2}{Mathf.RoundToInt(color.g * 255f):X2}{Mathf.RoundToInt(color.b * 255f):X2}";
    }
}