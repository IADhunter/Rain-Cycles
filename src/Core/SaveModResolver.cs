using System.IO;

namespace RainCycles.Core;

// DESTINO DE GUARDADO CONFIGURABLE (pestaña Developer)
public static class SaveModResolver
{
    public static ModManager.Mod GetTargetMod()
    {
        string id = RSPlugin.saveModId?.Value;
        if (string.IsNullOrEmpty(id)) return null;

        foreach (var mod in ModManager.ActiveMods)
            if (mod.id == id)
                return mod;

        return null;
    }

    public static string DirectoryForRoom(string roomName)
    {
        ModManager.Mod mod = GetTargetMod();
        if (mod == null) return null;

        if (BlendSettingsLoader.IsGateRoom(roomName))
            return Path.Combine(mod.path, BlendSettingsLoader.GateRelativeDir);

        string regionCode = ExtractRegionCode(roomName);
        if (regionCode == null) return null;

        return Path.Combine(mod.path, "world", regionCode + "-rooms", "raincycles");
    }

    public static string PathForRegionBlend(string regionCode)
    {
        ModManager.Mod mod = GetTargetMod();
        if (mod == null) return null;

        string lower = regionCode.ToLowerInvariant();
        return Path.Combine(mod.path, "world", lower, "raincycles", lower + "_blend_settings.txt");
    }

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
