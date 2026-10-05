using System;
using System.IO;
using System.Text;

namespace RainCycles.Settings;

public static partial class BlendSettingsWriter
{
    // ============================================================
    // GENERACIÓN DE ARCHIVO NUEVO
    // ============================================================
    public static string EnsureFileExists(string roomName)
    {
        string path = BlendSettingsLoader.IsGateRoom(roomName)
            ? ResolveWritablePath(null, roomName)
            : ResolveWritablePath(ExtractRegionCode(roomName));
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

    // ============================================================
    // HELPERS
    // ============================================================
    private static string GetDefaultTemplate()
    {
        return @"Clock: false
Mode: loop
Idle_time: 5.0
Duration: 10.0
Trigger: none
wait_time: 0.0
Setting: 0";
    }

    private static string ExtractRegionCode(string roomName)
    {
        if (string.IsNullOrEmpty(roomName)) return null;
        string[] parts = roomName.Split('_');
        return parts.Length >= 2 ? parts[0].ToLowerInvariant() : null;
    }

    private static string ResolveGateWritablePath(string roomName)
    {
        string lower = roomName.ToLowerInvariant();
        string fileName = lower + "_blend_settings.txt";

        // Si ya existe en algún mod, usar esa ruta
        string existing = BlendSettingsLoader.ResolveGateBlendPath(roomName);
        if (existing != null) return existing;

        // Mod destino elegido en Developer
        ModManager.Mod targetMod = SaveModResolver.GetTargetMod();
        if (targetMod != null)
            return Path.Combine(targetMod.path, BlendSettingsLoader.GateRelativeDir, fileName);

        // Primer mod que tenga la carpeta gates/raincycles
        foreach (var mod in ModManager.ActiveMods)
        {
            string candidate = Path.Combine(mod.path, BlendSettingsLoader.GateRelativeDir, fileName);
            if (Directory.Exists(Path.GetDirectoryName(candidate)))
                return candidate;
        }

        // Fallback: RainCycles mod
        foreach (var mod in ModManager.ActiveMods)
        {
            if (mod.id != RSPlugin.ID) continue;
            return Path.Combine(mod.path, BlendSettingsLoader.GateRelativeDir, fileName);
        }

        return null;
    }

    private static string ResolveWritablePath(string regionCode, string roomName = null)
    {
        try
        {
            // Gate rooms: resolución específica por gate
            if (BlendSettingsLoader.IsGateRoom(roomName))
            {
                return ResolveGateWritablePath(roomName);
            }

            if (string.IsNullOrEmpty(regionCode)) return null;
            string lower = regionCode.ToLowerInvariant();

            // Mod destino elegido en la pestaña Developer: escribe SIEMPRE ahí,
            // sin buscar carpetas existentes (se crean al guardar).
            string targetPath = SaveModResolver.PathForRegionBlend(lower);
            if (targetPath != null) return targetPath;

            string existing = BlendSettingsLoader.ResolvePath(lower);
            if (existing != null) return existing;

            foreach (var mod in ModManager.ActiveMods)
            {
                string candidate = Path.Combine(mod.path, "world", lower + "-rooms", "raincycles", lower + "_blend_settings.txt");
                if (Directory.Exists(Path.GetDirectoryName(candidate)))
                    return candidate;
            }

            foreach (var mod in ModManager.ActiveMods)
            {
                if (mod.id != RSPlugin.ID) continue;
                return Path.Combine(mod.path, "world", lower + "-rooms", "raincycles", lower + "_blend_settings.txt");
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