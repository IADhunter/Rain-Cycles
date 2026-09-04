using System.IO;

namespace RainCycles.Core;

// ============================================================
// DESTINO DE GUARDADO CONFIGURABLE (pestaña Developer)
// ============================================================
// Permite elegir en Remix (saveModId) un mod activo como destino
// único de escritura de settings_N y blend_settings. Cuando hay
// mod elegido, la escritura SIEMPRE va a {mod}/World/{REGION}-Rooms/
// RainCycles creando las carpetas que falten. Cuando no hay mod
// elegido (default), la escritura usa la lógica actual (región
// propia si la tiene, si no vanilla).
//
// IMPORTANTE: esto solo afecta a la ESCRITURA. La lectura sigue
// la lógica actual de prioridad de mods (StateFileResolver).
public static class SaveModResolver
{
    // Devuelve el mod destino si saveModId apunta a un mod activo;
    // null si no hay elegido o ya no está activo.
    public static ModManager.Mod GetTargetMod()
    {
        string id = RSPlugin.saveModId?.Value;
        if (string.IsNullOrEmpty(id)) return null;

        foreach (var mod in ModManager.ActiveMods)
            if (mod.id == id)
                return mod;

        return null;
    }

    // Ruta destino para settings_N de una sala (con el mod elegido).
    // Devuelve null si no hay mod destino configurado.
    public static string DirectoryForRoom(string roomName)
    {
        ModManager.Mod mod = GetTargetMod();
        if (mod == null) return null;

        string regionCode = ExtractRegionCode(roomName);
        if (regionCode == null) return null;

        return Path.Combine(mod.path, "world", regionCode + "-rooms", "raincycles");
    }

    // Ruta destino para blend_settings de una región (con el mod elegido).
    // Devuelve null si no hay mod destino configurado.
    public static string PathForRegionBlend(string regionCode)
    {
        ModManager.Mod mod = GetTargetMod();
        if (mod == null) return null;

        string lower = regionCode.ToLowerInvariant();
        return Path.Combine(mod.path, "world", lower + "-rooms", "raincycles", lower + "_blend_settings.txt");
    }

    // Ruta destino para el creador por lotes de una región (con el mod elegido).
    // Devuelve null si no hay mod destino configurado.
    public static string DirectoryForRegion(string regionCode)
    {
        ModManager.Mod mod = GetTargetMod();
        if (mod == null) return null;

        string lower = regionCode.ToLowerInvariant();
        return Path.Combine(mod.path, "world", lower + "-rooms", "raincycles");
    }

    private static string ExtractRegionCode(string roomName)
    {
        if (string.IsNullOrEmpty(roomName)) return null;
        string[] parts = roomName.Split('_');
        return parts.Length >= 2 ? parts[0].ToLowerInvariant() : null;
    }
}