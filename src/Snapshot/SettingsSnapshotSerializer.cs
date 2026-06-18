using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RainCycles.Snapshot;

// Serialización a texto
public partial class SettingsSnapshot
{
    private static readonly CultureInfo SINV = CultureInfo.InvariantCulture;

    public string ToFileText()
    {
        if (string.IsNullOrEmpty(RawText)) return "";
        string[] lines = RawText.Split('\n');
        var sb = new StringBuilder();
        bool poWritten = false;

        // PRIMERO: Escribir RC_TYPE si existe (antes que cualquier otra línea RC_*)
        if (HasRcType && RcType != RcType.None)
        {
            string typeValue = RcType == RcType.Static ? "Static" : "Blend";
            sb.AppendLine($"RC_TYPE: {typeValue}");
        }

        foreach (string rawLine in lines)
        {
            string line = rawLine.TrimEnd('\r');

            // Saltar líneas RC_TYPE, RC_VIEW, RC_TINT originales
            // porque las vamos a reescribir con el orden correcto
            if (line.StartsWith("RC_TYPE:") || line.StartsWith("RC_VIEW:") || line.StartsWith("RC_TINT:"))
                continue;

            if (line.StartsWith("PlacedObjects: ") && !poWritten)
            {
                poWritten = true;
                sb.AppendLine(BuildPlacedObjectsLine());
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
            else if (line.StartsWith("Template: "))              sb.AppendLine("Template: " + Template);
            else if (line.StartsWith("Effects: "))               sb.AppendLine("Effects: " + Effects);
            else if (line.StartsWith("Triggers: "))              sb.AppendLine("Triggers: " + Triggers);
            else if (line.StartsWith("AmbientSounds: "))         sb.AppendLine("AmbientSounds: " + AmbientSounds);
            else if (line.StartsWith("FadePalette: "))           sb.AppendLine(BuildFadePaletteLine());
            else                                                sb.AppendLine(line);
        }

        // DESPUÉS: Escribir RC_VIEW y RC_TINT si hay RC_TYPE
        if (HasRcType && RcType != RcType.None)
        {
            if (ViewType != ViewType.None)
            {
                string viewValue = ViewType == ViewType.ACV ? "ACV" :
                                   ViewType == ViewType.RTV ? "RTV" : "PSV";
                sb.AppendLine($"RC_VIEW: {viewValue}");
            }
            sb.AppendLine(BuildRcTintLine());
        }
        else if (!RawText.Contains("RC_TINT:"))
        {
            // Solo por compatibilidad con archivos antiguos que no tienen RC_TYPE
            // pero ya existía RC_TINT. Normalmente esto no debería ocurrir.
            sb.AppendLine(BuildRcTintLine());
        }

        return sb.ToString();
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
        string rest   = obj.Substring(tp);
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

    private string BuildRcTintLine()
    {
        string mul = TintMultiply.HasValue        ? ColorToHex(TintMultiply.Value)        : "#FFFFFF";
        string atm = TintAtmosphere.HasValue      ? ColorToHex(TintAtmosphere.Value)      : "#FFFFFF";
        // Solo 2 colores: Multiply y Atmosphere
        return $"RC_TINT: {mul} {atm}";
    }

    private static string ColorToHex(Color c)
    {
        return $"#{Mathf.RoundToInt(c.r * 255f):X2}{Mathf.RoundToInt(c.g * 255f):X2}{Mathf.RoundToInt(c.b * 255f):X2}";
    }
}