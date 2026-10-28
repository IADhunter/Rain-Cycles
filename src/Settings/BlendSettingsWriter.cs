using System;
using System.IO;
using System.Text;

namespace RainCycles.Settings;

public static partial class BlendSettingsWriter
{
    // GENERACIÓN DE ARCHIVO NUEVO
    public static string EnsureFileExists(string roomName)
    {
        string regionCode = BlendSettingsLoader.IsGateRoom(roomName)
            ? BlendSettingsLoader.ActiveRegion
            : ExtractRegionCode(roomName);

        string path = ResolveWritablePath(regionCode);
        if (path == null) return null;

        if (File.Exists(path)) return path;

        string dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            try { Directory.CreateDirectory(dir); }
            catch (Exception ex)
            {
                RSPlugin.log.LogError($"[BlendSettingsWriter] Cannot create directory {dir}: {ex.Message}");
                return null;
            }
        }

        try
        {
            File.WriteAllText(path, GetDefaultTemplate(), Encoding.UTF8);
        }
        catch (Exception ex)
        {
            RSPlugin.log.LogError($"[BlendSettingsWriter] Cannot write {path}: {ex.Message}");
            return null;
        }

        return path;
    }

    // HELPERS
    private static string GetDefaultTemplate()
    {
        return @"Clock: false
Mode: loop
Idle_time: 5.0
Duration: 10.0
Trigger: none
wait_time: 0.0";
    }

    private static string ExtractRegionCode(string roomName)
    {
        if (string.IsNullOrEmpty(roomName)) return null;
        string[] parts = roomName.Split('_');
        return parts.Length >= 2 ? parts[0].ToLowerInvariant() : null;
    }

    private static string ResolveWritablePath(string regionCode)
    {
        try
        {
            if (string.IsNullOrEmpty(regionCode)) return null;
            string lower = regionCode.ToLowerInvariant();

            string targetPath = SaveModResolver.PathForRegionBlend(lower);
            if (targetPath != null) return targetPath;

            string existing = BlendSettingsLoader.ResolvePath(lower);
            if (existing != null) return existing;

            foreach (var mod in ModManager.ActiveMods)
            {
                string candidate = Path.Combine(mod.path, "world", lower, "raincycles", lower + "_blend_settings.txt");
                if (Directory.Exists(Path.GetDirectoryName(candidate)))
                    return candidate;
            }

            foreach (var mod in ModManager.ActiveMods)
            {
                if (mod.id != RSPlugin.ID) continue;
                return Path.Combine(mod.path, "world", lower, "raincycles", lower + "_blend_settings.txt");
            }

            return null;
        }
        catch (Exception ex)
        {
            RSPlugin.log.LogError($"[BlendSettingsWriter] Cannot resolve path for region {regionCode}: {ex.Message}");
            return null;
        }
    }
}
