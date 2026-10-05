using System.Text;

namespace RainCycles.Snapshot;

// ================================================================
// BKG TAG — tag <Mod:mod,sky[,sun,fog]> de la línea RainCycles:
// Define la imagen de fondo por sala×estado. Formato:
//   <Mod:Nycthemeron,atc_day>                          (vista normal: sky)
//   <Mod:Nycthemeron,pnk_sky_1,pnk_sun_1,pnk_fog_1>    (PSV: sky,sun,fog)
// Helper único compartido por los 2 parsers (RoomSettingsPatches
// y SettingsSnapshotParser) y los 2 builders, para evitar divergencia.
// ================================================================
public static class BkgTag
{
    public readonly struct Data
    {
        public readonly string Mod;
        public readonly string Sky;
        public readonly string Sun;
        public readonly string Fog;

        public Data(string mod, string sky, string sun, string fog)
        {
            Mod = mod;
            Sky = sky;
            Sun = sun;
            Fog = fog;
        }

        public bool IsValid => !string.IsNullOrEmpty(Mod) && !string.IsNullOrEmpty(Sky);
    }

    // "mod,sky[,sun,fog]" → Data (strings vacíos → null)
    public static Data Parse(string value)
    {
        if (string.IsNullOrEmpty(value)) return default;

        string[] parts = value.Split(',');
        string mod = Clean(parts[0]);
        string sky = parts.Length > 1 ? Clean(parts[1]) : null;
        string sun = parts.Length > 2 ? Clean(parts[2]) : null;
        string fog = parts.Length > 3 ? Clean(parts[3]) : null;

        return new Data(mod, sky, sun, fog);
    }

    // Data → "mod,sky[,sun,fog]"; null si el dato no es válido.
    // Sun/Fog se emiten con padding posicional (sun vacío si solo hay fog).
    public static string Format(Data data)
    {
        if (!data.IsValid) return null;

        var sb = new StringBuilder(data.Mod);
        sb.Append(',').Append(data.Sky);

        if (!string.IsNullOrEmpty(data.Sun))
            sb.Append(',').Append(data.Sun);
        else if (!string.IsNullOrEmpty(data.Fog))
            sb.Append(',');

        if (!string.IsNullOrEmpty(data.Fog))
            sb.Append(',').Append(data.Fog);

        return sb.ToString();
    }

    private static string Clean(string s)
    {
        if (s == null) return null;
        s = s.Trim();
        return s.Length == 0 ? null : s;
    }
}
