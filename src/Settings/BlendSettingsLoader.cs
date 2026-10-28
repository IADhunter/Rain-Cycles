using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RainCycles.Settings;

public static class BlendSettingsLoader
{
    private static readonly Dictionary<string, BlendSettings> _cache
        = new Dictionary<string, BlendSettings>();

    private static string       _activeRegion   = null;
    private static BlendSettings _activeSettings = null;

    public static BlendSettings Active => _activeSettings;
    public static string ActiveRegion => _activeRegion;

    public static void Init()
    {
    }

    public static void InvalidateCache(string regionCode)
    {
        if (string.IsNullOrEmpty(regionCode)) return;
        regionCode = regionCode.ToUpperInvariant();
        _cache.Remove(regionCode);

        if (_activeRegion == regionCode)
        {
            _activeSettings = null;
        }
    }

    public static void LoadRegion(string regionCode)
    {
        if (RCGate.Suspended)
        {
            _activeRegion = null;
            _activeSettings = null;
            return;
        }

        regionCode = regionCode.ToUpperInvariant();

        if (!_cache.TryGetValue(regionCode, out var settings))
        {
            settings = LoadFromDisk(regionCode);
            _cache[regionCode] = settings;
        }

        _activeRegion   = regionCode;
        _activeSettings = settings;

        int cycle = GetCurrentCycleNumber();

        int state = Core.CycleStateResolver.ResolveState(cycle);

        Core.StateFileResolver.SetCurrentCycleState(state);
    }

    public static BlendSettings GetForRegion(string regionCode)
    {
        if (RCGate.Suspended) return null;
        regionCode = regionCode.ToUpperInvariant();
        if (!_cache.TryGetValue(regionCode, out var s))
        {
            s = LoadFromDisk(regionCode);
            _cache[regionCode] = s;
        }
        return s;
    }

    private static BlendSettings LoadFromDisk(string regionCode)
    {
        string path = ResolvePath(regionCode);
        if (path == null || !File.Exists(path)) return null;
        return LoadFile(path);
    }

    public static BlendSettings LoadFile(string path)
    {
        if (path == null || !File.Exists(path)) return null;
        return ParseContent(File.ReadAllText(path, System.Text.Encoding.UTF8));
    }

    public static void SetActiveBlend(BlendSettings settings, string key)
    {
        _activeRegion = key ?? _activeRegion;
        _activeSettings = settings;
    }

    public static bool IsGateRoom(string roomName)
    {
        return !string.IsNullOrEmpty(roomName)
            && roomName.StartsWith("GATE_", System.StringComparison.OrdinalIgnoreCase);
    }

    public static readonly string GateVanillaDir  = Path.Combine("world", "gates");
    public static readonly string GateRelativeDir = Path.Combine(GateVanillaDir, "raincycles");

    private static BlendSettings ParseContent(string content)
    {
        var settings = new BlendSettings();
        
        foreach (string line in content.Split('\n'))
        {
            string trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#")) continue;
            
            int sep = trimmed.IndexOf(':');
            if (sep > 0)
            {
                string key = trimmed.Substring(0, sep).Trim().ToLowerInvariant();
                string val = trimmed.Substring(sep + 1).Trim();
                
                switch (key)
                {
                    case "clock":
                        if (bool.TryParse(val, out bool clock)) settings.Clock = clock;
                        break;
                    case "mode":
                        settings.Mode = ParseMode(val);
                        break;
                    case "idle_time":
                        if (float.TryParse(val, out float idle))
                            settings.IdleTime = idle;
                        break;
                    case "duration":
                        if (float.TryParse(val, out float dur))
                            settings.Duration = dur;
                        break;
                    case "trigger":
                        settings.Trigger = val.Trim().ToLowerInvariant() switch
                        {
                            "cycle" => LoopTrigger.Cycle,
                            "rain"  => LoopTrigger.Rain,
                            _       => LoopTrigger.None
                        };
                        break;
                    case "wait_time":
                        if (float.TryParse(val, out float wt))
                            settings.WaitTime = wt;
                        break;
                }
            }
        }
        
        return settings;
    }
    
    private static BlendMode ParseMode(string val)
    {
        switch (val.ToLowerInvariant())
        {
            case "cycle": return BlendMode.Cycle;
            case "rain": return BlendMode.Rain;
            default: return BlendMode.Loop;
        }
    }

    public static string ResolvePath(string regionCode)
    {
        string lower    = regionCode.ToLowerInvariant();
        string relative = Path.Combine("world", lower + "-rooms", "raincycles",
            lower + "_blend_settings.txt");

        for (int i = ModManager.ActiveMods.Count - 1; i >= 0; i--)
        {
            string candidate = Path.Combine(ModManager.ActiveMods[i].path, relative);
            if (File.Exists(candidate)) return candidate;
        }

        string basePath = Path.Combine(Application.streamingAssetsPath, relative);
        return File.Exists(basePath) ? basePath : null;
    }

    private static int GetCurrentCycleNumber()
    {
        var rw = UnityEngine.Object.FindObjectOfType<RainWorld>();
        var game = rw?.processManager?.currentMainLoop as RainWorldGame;
        return game?.GetStorySession?.saveState?.cycleNumber ?? 0;
    }
}
