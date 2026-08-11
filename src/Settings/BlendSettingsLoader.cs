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
    
    public static string ActiveModName => _activeSettings?.SelectedModName ?? "";

    public static void Init()
    {
        On.RainWorldGame.ShutDownProcess += OnShutDown;
    }

    private static void OnShutDown(On.RainWorldGame.orig_ShutDownProcess orig, RainWorldGame self)
    {
        orig(self);
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
        regionCode = regionCode.ToUpperInvariant();

        if (!_cache.TryGetValue(regionCode, out var settings))
        {
            settings = LoadFromDisk(regionCode);
            _cache[regionCode] = settings;
        }

        _activeRegion   = regionCode;
        _activeSettings = settings;
        
        int cycle = GetCurrentCycleNumber();
        
        int state;
        if (RSPlugin.randomCycles != null && RSPlugin.randomCycles.Value)
        {
            int seed = unchecked(cycle * 1000003);
            state = new System.Random(seed).Next(1, 5);
        }
        else
        {
            state = (cycle % 4) + 1;
            if (cycle == 0) state = 1;
        }
        
        Core.StateFileResolver.SetCurrentCycleState(state);
    }

    public static BlendSettings GetForRegion(string regionCode)
    {
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

    // Parseo de un archivo de blend settings (reutilizable por ruta arbitraria,
    // p. ej. los blend settings per-level de Arena).
    public static BlendSettings LoadFile(string path)
    {
        if (path == null || !File.Exists(path)) return null;
        return ParseContent(File.ReadAllText(path, System.Text.Encoding.UTF8));
    }

    // Establece el blend activo desde una fuente externa (Arena).
    // key = identificador (para arena, el nombre del level).
    public static void SetActiveBlend(BlendSettings settings, string key)
    {
        _activeRegion = key ?? _activeRegion;
        _activeSettings = settings;
    }

    private static BlendSettings ParseContent(string content)
    {
        var settings = new BlendSettings();
        ViewType currentView = ViewType.None;
        
        foreach (string line in content.Split('\n'))
        {
            string trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#")) continue;
            
            if (trimmed == "Acv") { currentView = ViewType.ACV; continue; }
            if (trimmed == "Rtv") { currentView = ViewType.RTV; continue; }
            if (trimmed == "Psv") { currentView = ViewType.PSV; continue; }
            if (trimmed == "Auv") { currentView = ViewType.AUV; continue; }
            if (trimmed == "Orv") { currentView = ViewType.ORV; continue; }
            
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
                    case "setting":
                        if (int.TryParse(val, out int set) && set >= 0 && set <= 4) settings.Setting = set;
                        break;
                    case "mod":
                        settings.SelectedModName = val.Trim();
                        break;
                    default:
                        if (key.StartsWith("bkg") && currentView != ViewType.None)
                        {
                            int state = ParseStateFromKey(key);
                            if (state >= 1 && state <= 4)
                            {
                                if (val.StartsWith("<") && val.EndsWith(">"))
                                {
                                    string inner = val.Substring(1, val.Length - 2);
                                    string[] parts = inner.Split(new[] { "><" }, System.StringSplitOptions.None);
                                    
                                    settings.HasBackgroundsSection = true;
                                    
                                    if (!settings.BackgroundAliases.ContainsKey(currentView))
                                        settings.BackgroundAliases[currentView] = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
                                    
                                    string alias = key;
                                    
                                    if (currentView == ViewType.PSV)
                                    {
                                        if (parts.Length >= 1 && !string.IsNullOrEmpty(parts[0]))
                                            settings.BackgroundAliases[currentView][alias] = parts[0];
                                        if (parts.Length >= 2 && !string.IsNullOrEmpty(parts[1]))
                                            settings.BackgroundAliases[currentView][alias + "_fog"] = parts[1];
                                        if (parts.Length >= 3 && !string.IsNullOrEmpty(parts[2]))
                                            settings.BackgroundAliases[currentView][alias + "_sun"] = parts[2];
                                    }
                                    else
                                    {
                                        if (parts.Length >= 1 && !string.IsNullOrEmpty(parts[0]))
                                            settings.BackgroundAliases[currentView][alias] = parts[0];
                                    }
                                }
                            }
                        }
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
            case "endcycle": return BlendMode.EndCycle;
            default: return BlendMode.Loop;
        }
    }
    
    private static int ParseStateFromKey(string key)
    {
        if (key.Length >= 5 && int.TryParse(key.Substring(3), out int state))
            return state;
        return -1;
    }

    public static string ResolvePath(string regionCode)
    {
        string upper    = regionCode.ToUpperInvariant();
        string relative = Path.Combine("World", upper + "-Rooms", "RainCycles",
            upper + "_blend_settings.txt");

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