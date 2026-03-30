using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace FilesSetting;

// ════════════════════════════════════════════════════════════════════════
// SERIALIZACIÓN
// Reconstruye el texto de un settings file a partir del snapshot.
// Usado por el sistema de swap para persistir cambios a disco.
// El blend NO usa ToFileText() — aplica directamente en memoria.
// ════════════════════════════════════════════════════════════════════════

public partial class SettingsSnapshot
{
    private static readonly CultureInfo SINV = CultureInfo.InvariantCulture;

    public string ToFileText()
    {
        if (string.IsNullOrEmpty(RawText)) return "";
        string[] lines = RawText.Split('\n');
        var sb = new StringBuilder();
        bool poWritten = false;

        foreach (string rawLine in lines)
        {
            string line = rawLine.TrimEnd('\r');
            int colon = line.IndexOf(": ");

            // Solo necesitamos una llave
            // Objecto una llave unica, y obtengo la subcadena hasta ": "
            string key = colon >= 0? line.Substring(0, colon + 2) : "";
            
            if (!poWritten && key == "PlacedObjects: ")
            {
                poWritten = true;
                sb.AppendLine(BuildPlacedObjectsLine());
                continue;
            }
            
            
            
            string replacement = key switch
            {
                "Palette: "               => "Palette: " + Palette,
                "Grime: "                 => SFL("Grime", Grime),
                "Clouds: "                => SFL("Clouds", Clouds),
                "CeilingDrips: "          => SFL("CeilingDrips", CeilingDrips),
                "BkgDroneVolume: "        => SFL("BkgDroneVolume", BkgDroneVolume),
                "RandomItemDensity: "     => SFL("RandomItemDensity", RandomItemDensity),
                "RandomItemSpearChance: " => SFL("RandomItemSpearChance", RandomItemSpearChance),
                "EffectColorA: "          => "EffectColorA: " + EffectColorA,
                "EffectColorB: "          => "EffectColorB: " + EffectColorB,
                "DangerType: "            => "DangerType: " + DangerType,
                "Template: "              => "Template: " + Template,
                "Effects: "               => "Effects: " + Effects,
                "Triggers: "              => "Triggers: " + Triggers,
                "AmbientSounds: "         => "AmbientSounds: " + AmbientSounds,
                "FadePalette: "           => BuildFadePaletteLine(),
                "RC_TINT: "               => BuildRcTintLine(),
                _                         => line, //wtf, porque esto es un default
            };
            
            sb.AppendLine(replacement);
        }

        // Si RC_TINT no estaba en RawText (fue inyectado después de la carga),
        // agregarlo al final para que no se pierda en el próximo guardado.
        if (!RawText.Contains("RC_TINT:"))
            sb.AppendLine(BuildRcTintLine());

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
        // LightBeams NO se parchean aquí — su alpha lo gestiona exclusivamente
        // RoomEffectsApplier.ApplyLightBeams() con normalización por bandas.
        return "PlacedObjects: " + string.Join(", ", patched.ToArray());
    }

    private static string PatchDecal(string obj, float[] ops)
    {
        string[] t = obj.Split('~');
        int[] oi = new int[] { 12, 14, 16, 18 };
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
            float dummy;
            if (float.TryParse(parts[i].TrimStart('<').Trim(),
                System.Globalization.NumberStyles.Float, SINV, out dummy))
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
        string cld = TintCloudAtmosphere.HasValue ? ColorToHex(TintCloudAtmosphere.Value) : "#FFFFFF";
        return $"RC_TINT: {mul} {atm} {cld}";
    }

    private static string ColorToHex(UnityEngine.Color c)
    {
        int r = UnityEngine.Mathf.RoundToInt(c.r * 255f);
        int g = UnityEngine.Mathf.RoundToInt(c.g * 255f);
        int b = UnityEngine.Mathf.RoundToInt(c.b * 255f);
        return $"#{r:X2}{g:X2}{b:X2}";
    }
}
