using System;
using System.Collections.Generic;
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
        string regionCode = ExtractRegionCode(roomName);
        if (regionCode == null) return null;

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

    // ============================================================
    // GUARDADO DE CONFIGURACIÓN
    // ============================================================
    public static void SaveSettings(string regionCode, BlendSettings settings)
    {
        string path = ResolveWritablePath(regionCode);
        if (path == null) return;

        var sb = new StringBuilder();
        
        sb.AppendLine($"Clock: {(settings.Clock ? "true" : "false")}");
        sb.AppendLine($"Mode: {ModeToString(settings.Mode)}");
        sb.AppendLine($"Idle_time: {settings.IdleTime:F1}");
        sb.AppendLine($"Duration: {settings.Duration:F1}");
        sb.AppendLine($"Trigger: {settings.Trigger.ToString().ToLowerInvariant()}");
        sb.AppendLine($"wait_time: {settings.WaitTime:F1}");
        sb.AppendLine($"Setting: {settings.Setting}");

        if (settings.HasBackgroundsSection && settings.BackgroundAliases.Count > 0)
        {
            sb.AppendLine();
            AppendBackgrounds(sb, settings);
        }

        try
        {
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }
        catch (Exception ex)
        {
            RSPlugin.log.LogError($"[BlendSettingsWriter] Cannot save {path}: {ex.Message}");
        }
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

    private static string ModeToString(BlendMode mode)
    {
        switch (mode)
        {
            case BlendMode.Cycle:    return "cycle";
            case BlendMode.EndCycle: return "endcycle";
            default:                 return "loop";
        }
    }

    private static void AppendBackgrounds(StringBuilder sb, BlendSettings s)
    {
        if (s.BackgroundAliases.TryGetValue(ViewType.ACV, out var acvDict) && acvDict.Count > 0)
        {
            sb.AppendLine("\nAcv");
            foreach (var kv in acvDict)
                if (!kv.Key.EndsWith("_fog") && !kv.Key.EndsWith("_sun"))
                    sb.AppendLine($"{kv.Key}: {kv.Value}.png");
        }

        if (s.BackgroundAliases.TryGetValue(ViewType.RTV, out var rtvDict) && rtvDict.Count > 0)
        {
            sb.AppendLine("\nRtv");
            foreach (var kv in rtvDict)
                if (!kv.Key.EndsWith("_fog") && !kv.Key.EndsWith("_sun"))
                    sb.AppendLine($"{kv.Key}: {kv.Value}.png");
        }

        if (s.BackgroundAliases.TryGetValue(ViewType.ORV, out var orvDict) && orvDict.Count > 0)
        {
            sb.AppendLine("\nOrv");
            foreach (var kv in orvDict)
                if (!kv.Key.EndsWith("_fog") && !kv.Key.EndsWith("_sun"))
                    sb.AppendLine($"{kv.Key}: {kv.Value}.png");
        }

        if (s.BackgroundAliases.TryGetValue(ViewType.PSV, out var psvDict) && psvDict.Count > 0)
        {
            sb.AppendLine("\nPsv");
            for (int state = 1; state <= 4; state++)
            {
                string bkgKey = $"bkg{state:00}";
                string fogKey = bkgKey + "_fog";
                string sunKey = bkgKey + "_sun";
                
                string bkg = psvDict.TryGetValue(bkgKey, out string b) ? b : "";
                string fog = psvDict.TryGetValue(fogKey, out string f) ? f : "";
                string sun = psvDict.TryGetValue(sunKey, out string s_) ? s_ : "";
                
                if (!string.IsNullOrEmpty(bkg) || !string.IsNullOrEmpty(fog) || !string.IsNullOrEmpty(sun))
                {
                    var parts = new List<string>();
                    if (!string.IsNullOrEmpty(bkg)) parts.Add($"{bkg}.png");
                    if (!string.IsNullOrEmpty(fog)) parts.Add($"{fog}.png");
                    if (!string.IsNullOrEmpty(sun)) parts.Add($"{sun}.png");
                    sb.AppendLine($"{bkgKey}: {string.Join(", ", parts)}");
                }
            }
        }
    }

    private static string ExtractRegionCode(string roomName)
    {
        if (string.IsNullOrEmpty(roomName)) return null;
        string[] parts = roomName.Split('_');
        return parts.Length >= 2 ? parts[0].ToUpperInvariant() : null;
    }

    private static string ResolveWritablePath(string regionCode)
    {
        try
        {
            string upper = regionCode.ToUpperInvariant();
            string existing = BlendSettingsLoader.ResolvePath(upper);
            if (existing != null) return existing;

            foreach (var mod in ModManager.ActiveMods)
            {
                string candidate = Path.Combine(mod.path, "World", upper + "-Rooms", "RainCycles", upper + "_blend_settings.txt");
                if (Directory.Exists(Path.GetDirectoryName(candidate)))
                    return candidate;
            }

            foreach (var mod in ModManager.ActiveMods)
            {
                if (mod.id != RSPlugin.ID) continue;
                return Path.Combine(mod.path, "World", upper + "-Rooms", "RainCycles", upper + "_blend_settings.txt");
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