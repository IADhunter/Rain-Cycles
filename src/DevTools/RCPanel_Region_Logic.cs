using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DevInterface;
using UnityEngine;
using RainCycles.Settings;
using RainCycles.Core;

namespace FilesSetting;

public class RegionLogic
{
    private RCPanel_RegionPage _page;
    
    public BlendMode CurrentMode { get; set; } = BlendMode.Loop;
    public bool ClockEnabled { get; set; } = false;
    public float IdleValue { get; set; } = 5f;
    public float DurationValue { get; set; } = 10f;
    public LoopTrigger CurrentTrigger { get; set; } = LoopTrigger.None;
    public float WaitTimeValue { get; set; } = 0f;
    public int SettingValue { get; set; } = 0;
    public ViewType CurrentViewType { get; set; } = ViewType.ACV;
    
    public string[] BkgValues { get; private set; }
    public string[] FogValues { get; private set; }
    public string[] SunValues { get; private set; }
    
    public string SelectedModName { get; set; } = "";
    public string SavedModName { get; set; } = "";
    
    private Dictionary<ViewType, string[]> _allBackgrounds = new Dictionary<ViewType, string[]>();
    private Dictionary<ViewType, string[]> _allFogs = new Dictionary<ViewType, string[]>();
    private Dictionary<ViewType, string[]> _allSuns = new Dictionary<ViewType, string[]>();
    private readonly ViewType[] _viewTypes = { ViewType.ACV, ViewType.RTV, ViewType.PSV };
    private int _viewTypeIndex = 0;
    
    public float BlendValue
    {
        get => DurationValue;
        set => DurationValue = value;
    }

    public RegionLogic(RCPanel_RegionPage page)
    {
        _page = page;
        BkgValues = new string[4];
        FogValues = new string[4];
        SunValues = new string[4];
        for (int i = 0; i < 4; i++)
        {
            BkgValues[i] = "";
            FogValues[i] = "";
            SunValues[i] = "";
        }
    }

    // ============================================================
    // HELPERS
    // ============================================================
    private string RegionCode => ExtractRegionCode(_page.ParentPanel.CurrentRoomName);
    private string BlendSettingsPath => GetBlendSettingsPath();
    
    private string GetBlendSettingsPath()
    {
        if (string.IsNullOrEmpty(RegionCode)) return null;
        return BlendSettingsLoader.ResolvePath(RegionCode);
    }
    
    private string ExtractRegionCode(string roomName)
    {
        if (string.IsNullOrEmpty(roomName)) return null;
        string[] parts = roomName.Split('_');
        return parts.Length >= 2 ? parts[0].ToUpperInvariant() : null;
    }
    
    // ============================================================
    // OBTENER NOMBRE DEL MOD DESDE MODINFO.JSON
    // ============================================================
    private string GetModNameFromModInfo(string modPath)
    {
        try
        {
            string modInfoPath = Path.Combine(modPath, "modinfo.json");
            if (!File.Exists(modInfoPath)) return null;

            string json = File.ReadAllText(modInfoPath);
            
            int nameIndex = json.IndexOf("\"name\"", StringComparison.OrdinalIgnoreCase);
            if (nameIndex < 0) return null;

            int colonIndex = json.IndexOf(':', nameIndex);
            if (colonIndex < 0) return null;

            int startQuote = json.IndexOf('"', colonIndex + 1);
            if (startQuote < 0) return null;

            int endQuote = json.IndexOf('"', startQuote + 1);
            if (endQuote < 0) return null;

            return json.Substring(startQuote + 1, endQuote - startQuote - 1);
        }
        catch
        {
            return null;
        }
    }
    
    // ============================================================
    // RESOLVER RUTA DEL MOD POR NOMBRE
    // ============================================================
    private string ResolveModPathFromName(string modName)
    {
        if (string.IsNullOrEmpty(modName)) return null;
        
        foreach (var mod in ModManager.ActiveMods)
        {
            string realName = GetModNameFromModInfo(mod.path);
            if (string.Equals(realName, modName, StringComparison.OrdinalIgnoreCase))
            {
                return mod.path;
            }
        }
        return null;
    }

    // ============================================================
    // VIEW DICTIONARY
    // ============================================================
    private void SaveCurrentViewToDictionary()
    {
        if (!_allBackgrounds.ContainsKey(CurrentViewType))
            _allBackgrounds[CurrentViewType] = new string[4];
        if (!_allFogs.ContainsKey(CurrentViewType))
            _allFogs[CurrentViewType] = new string[4];
        if (!_allSuns.ContainsKey(CurrentViewType))
            _allSuns[CurrentViewType] = new string[4];
        
        for (int i = 0; i < 4; i++)
        {
            _allBackgrounds[CurrentViewType][i] = BkgValues[i] ?? "";
            if (CurrentViewType == ViewType.PSV)
            {
                _allFogs[CurrentViewType][i] = FogValues[i] ?? "";
                _allSuns[CurrentViewType][i] = SunValues[i] ?? "";
            }
        }
    }
    
    private void LoadCurrentViewFromDictionary()
    {
        if (_allBackgrounds.ContainsKey(CurrentViewType))
        {
            var values = _allBackgrounds[CurrentViewType];
            for (int i = 0; i < 4; i++)
                BkgValues[i] = values[i] ?? "";
        }
        else
        {
            for (int i = 0; i < 4; i++)
                BkgValues[i] = "";
        }
        
        if (CurrentViewType == ViewType.PSV)
        {
            if (_allFogs.ContainsKey(CurrentViewType))
            {
                var fogValues = _allFogs[CurrentViewType];
                for (int i = 0; i < 4; i++)
                    FogValues[i] = fogValues[i] ?? "";
            }
            else
            {
                for (int i = 0; i < 4; i++)
                    FogValues[i] = "";
            }
            
            if (_allSuns.ContainsKey(CurrentViewType))
            {
                var sunValues = _allSuns[CurrentViewType];
                for (int i = 0; i < 4; i++)
                    SunValues[i] = sunValues[i] ?? "";
            }
            else
            {
                for (int i = 0; i < 4; i++)
                    SunValues[i] = "";
            }
        }
    }
    
    public void ClearAllBackgrounds()
    {
        _allBackgrounds.Clear();
        _allFogs.Clear();
        _allSuns.Clear();
        for (int i = 0; i < 4; i++)
        {
            BkgValues[i] = "";
            FogValues[i] = "";
            SunValues[i] = "";
        }
    }

    // ============================================================
    // PUBLIC API
    // ============================================================
    public void CycleViewType(int delta)
    {
        SaveCurrentViewToDictionary();
        SaveToBlendSettings();
        
        _viewTypeIndex += delta;
        if (_viewTypeIndex < 0) _viewTypeIndex = _viewTypes.Length - 1;
        if (_viewTypeIndex >= _viewTypes.Length) _viewTypeIndex = 0;
        CurrentViewType = _viewTypes[_viewTypeIndex];
        
        LoadCurrentViewFromDictionary();
    }
    
    private static readonly BlendMode[] _modes = { BlendMode.Loop, BlendMode.Cycle, BlendMode.EndCycle };
    private int _modeIndex = 0;

    private static readonly LoopTrigger[] _triggers = { LoopTrigger.None, LoopTrigger.Cycle, LoopTrigger.Rain };
    private int _triggerIndex = 0;

    public void CycleTrigger(int delta)
    {
        if (CurrentMode != BlendMode.Loop)
        {
            CurrentTrigger = LoopTrigger.None;
            _triggerIndex = 0;
            SaveToBlendSettings();
            return;
        }

        SaveToBlendSettings();

        _triggerIndex += delta;
        if (_triggerIndex < 0) _triggerIndex = _triggers.Length - 1;
        if (_triggerIndex >= _triggers.Length) _triggerIndex = 0;
        CurrentTrigger = _triggers[_triggerIndex];

        SaveToBlendSettings();
    }

    public string GetTriggerDisplay()
    {
        return CurrentTrigger switch
        {
            LoopTrigger.Cycle => "Cycle",
            LoopTrigger.Rain  => "Rain",
            _                 => "None"
        };
    }

    public void CycleMode(int delta)
    {
        SaveToBlendSettings();

        _modeIndex += delta;
        if (_modeIndex < 0) _modeIndex = _modes.Length - 1;
        if (_modeIndex >= _modes.Length) _modeIndex = 0;
        CurrentMode = _modes[_modeIndex];

        if (CurrentMode != BlendMode.Loop)
        {
            CurrentTrigger = LoopTrigger.None;
            _triggerIndex = 0;
        }

        SaveToBlendSettings();
    }

    public string GetModeDisplay()
    {
        return CurrentMode switch
        {
            BlendMode.Cycle => "Cycle",
            BlendMode.EndCycle => "Rain",
            _ => "Loop"
        };
    }

    public void LoadImagesForCurrentView()
    {
        LoadCurrentViewFromDictionary();
    }
    
    // ============================================================
    // LOAD FROM FILE
    // ============================================================
    public void LoadFromBlendSettings()
    {
        string path = BlendSettingsPath;
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            SetDefaultValues();
            return;
        }
        
        string content = File.ReadAllText(path, Encoding.UTF8);
        
        for (int i = 0; i < 4; i++)
        {
            BkgValues[i] = "";
            FogValues[i] = "";
            SunValues[i] = "";
        }
        _allBackgrounds.Clear();
        _allFogs.Clear();
        _allSuns.Clear();
        
        ViewType currentView = ViewType.None;
        
        foreach (string line in content.Split('\n'))
        {
            string trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#")) continue;
            
            if (trimmed == "Acv") { currentView = ViewType.ACV; continue; }
            if (trimmed == "Rtv") { currentView = ViewType.RTV; continue; }
            if (trimmed == "Psv") { currentView = ViewType.PSV; continue; }
            
            int sep = trimmed.IndexOf(':');
            if (sep > 0)
            {
                string key = trimmed.Substring(0, sep).Trim();
                string val = trimmed.Substring(sep + 1).Trim();
                
                switch (key.ToLowerInvariant())
                {
                    case "clock":
                        if (bool.TryParse(val, out bool clock)) ClockEnabled = clock;
                        break;
                    case "mode":
                        CurrentMode = ParseModeFromString(val);
                        break;
                    case "idle_time":
                        if (float.TryParse(val, out float idle))
                            IdleValue = idle;
                        break;
                    case "duration":
                        if (float.TryParse(val, out float dur))
                            DurationValue = dur;
                        break;
                    case "trigger":
                        CurrentTrigger = val.Trim().ToLowerInvariant() switch
                        {
                            "cycle" => LoopTrigger.Cycle,
                            "rain"  => LoopTrigger.Rain,
                            _       => LoopTrigger.None
                        };
                        break;
                    case "wait_time":
                        if (float.TryParse(val, out float wt))
                            WaitTimeValue = wt;
                        break;
                    case "setting":
                        if (int.TryParse(val, out int set)) SettingValue = set;
                        break;
                    case "mod":
                        SelectedModName = val;
                        SavedModName = val;
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
                                    string[] parts = inner.Split(new[] { "><" }, StringSplitOptions.None);
                                    
                                    if (currentView == ViewType.PSV)
                                    {
                                        if (!_allBackgrounds.ContainsKey(ViewType.PSV))
                                            _allBackgrounds[ViewType.PSV] = new string[4];
                                        if (!_allFogs.ContainsKey(ViewType.PSV))
                                            _allFogs[ViewType.PSV] = new string[4];
                                        if (!_allSuns.ContainsKey(ViewType.PSV))
                                            _allSuns[ViewType.PSV] = new string[4];
                                        
                                        if (parts.Length >= 1) _allBackgrounds[ViewType.PSV][state - 1] = StripExt(parts[0]);
                                        if (parts.Length >= 2) _allFogs[ViewType.PSV][state - 1] = StripExt(parts[1]);
                                        if (parts.Length >= 3) _allSuns[ViewType.PSV][state - 1] = StripExt(parts[2]);
                                    }
                                    else
                                    {
                                        if (!_allBackgrounds.ContainsKey(currentView))
                                            _allBackgrounds[currentView] = new string[4];
                                        if (parts.Length >= 1) _allBackgrounds[currentView][state - 1] = StripExt(parts[0]);
                                    }
                                }
                            }
                        }
                        break;
                }
            }
        }
        
        CurrentViewType = DetermineCurrentViewType();
        _viewTypeIndex = Array.IndexOf(_viewTypes, CurrentViewType);
        if (_viewTypeIndex < 0) _viewTypeIndex = 0;
        
        _modeIndex = Array.IndexOf(_modes, CurrentMode);
        if (_modeIndex < 0) _modeIndex = 0;

        if (CurrentMode != BlendMode.Loop)
        {
            CurrentTrigger = LoopTrigger.None;
        }
        
        _triggerIndex = Array.IndexOf(_triggers, CurrentTrigger);
        if (_triggerIndex < 0) _triggerIndex = 0;
        
        LoadCurrentViewFromDictionary();
    }
    
    private BlendMode ParseModeFromString(string val)
    {
        switch (val.ToLowerInvariant())
        {
            case "cycle": return BlendMode.Cycle;
            case "endcycle": return BlendMode.EndCycle;
            default: return BlendMode.Loop;
        }
    }
    
    private int ParseStateFromKey(string key)
    {
        if (key.Length >= 5 && int.TryParse(key.Substring(3), out int state))
            return state;
        return -1;
    }
    
    private string StripExt(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        int dot = name.LastIndexOf('.');
        return dot > 0 ? name.Substring(0, dot) : name;
    }
    
    private ViewType DetermineCurrentViewType()
    {
        if (_allBackgrounds.ContainsKey(ViewType.ACV) && HasAnyImage(_allBackgrounds[ViewType.ACV]))
            return ViewType.ACV;
        if (_allBackgrounds.ContainsKey(ViewType.RTV) && HasAnyImage(_allBackgrounds[ViewType.RTV]))
            return ViewType.RTV;
        if (_allBackgrounds.ContainsKey(ViewType.PSV) && HasAnyImage(_allBackgrounds[ViewType.PSV]))
            return ViewType.PSV;
        return ViewType.ACV;
    }
    
    private bool HasAnyImage(string[] arr)
    {
        for (int i = 0; i < arr.Length; i++)
            if (!string.IsNullOrEmpty(arr[i])) return true;
        return false;
    }
    
    private bool HasAnyPsvContent()
    {
        if (!_allBackgrounds.ContainsKey(ViewType.PSV)) return false;
        for (int i = 0; i < 4; i++)
        {
            string sky = _allBackgrounds[ViewType.PSV][i] ?? "";
            string fog = _allFogs.ContainsKey(ViewType.PSV) ? (_allFogs[ViewType.PSV][i] ?? "") : "";
            string sun = _allSuns.ContainsKey(ViewType.PSV) ? (_allSuns[ViewType.PSV][i] ?? "") : "";
            if (!string.IsNullOrEmpty(sky) || !string.IsNullOrEmpty(fog) || !string.IsNullOrEmpty(sun))
                return true;
        }
        return false;
    }
    
    private void SetDefaultValues()
    {
        CurrentMode = BlendMode.Loop;
        ClockEnabled = false;
        IdleValue = 5f;
        DurationValue = 10f;
        CurrentTrigger = LoopTrigger.None;
        WaitTimeValue = 0f;
        SettingValue = 0;
        CurrentViewType = ViewType.ACV;
        _viewTypeIndex = 0;
        _modeIndex = 0;
        _triggerIndex = 0;
        _allBackgrounds.Clear();
        _allFogs.Clear();
        _allSuns.Clear();
        for (int i = 0; i < 4; i++)
        {
            BkgValues[i] = "";
            FogValues[i] = "";
            SunValues[i] = "";
        }
        SelectedModName = "";
        SavedModName = "";
    }

    // ============================================================
    // SAVE TO FILE
    // ============================================================
    public void SaveToBlendSettings()
    {
        SaveCurrentViewToDictionary();
        
        string path = BlendSettingsPath;
        if (string.IsNullOrEmpty(path))
        {
            path = BlendSettingsWriter.EnsureFileExists(_page.ParentPanel.CurrentRoomName);
            if (string.IsNullOrEmpty(path)) return;
        }
        
        var sb = new StringBuilder();
        
        sb.AppendLine($"Clock: {(ClockEnabled ? "true" : "false")}");
        sb.AppendLine($"Mode: {ModeToString(CurrentMode)}");
        sb.AppendLine($"Idle_time: {IdleValue:F1}");
        sb.AppendLine($"Duration: {DurationValue:F1}");
        sb.AppendLine($"Trigger: {CurrentTrigger.ToString().ToLowerInvariant()}");
        sb.AppendLine($"wait_time: {WaitTimeValue:F1}");
        sb.AppendLine($"Setting: {SettingValue}");
        
        if (!string.IsNullOrEmpty(SavedModName))
            sb.AppendLine($"Mod: {SavedModName}");

        if (_allBackgrounds.ContainsKey(ViewType.ACV) && HasAnyImage(_allBackgrounds[ViewType.ACV]))
        {
            sb.AppendLine("Acv");
            for (int i = 0; i < 4; i++)
            {
                string img = _allBackgrounds[ViewType.ACV][i] ?? "";
                if (!string.IsNullOrEmpty(img))
                    sb.AppendLine($"bkg{(i + 1):00}: <{img}>");
            }
        }
        
        if (_allBackgrounds.ContainsKey(ViewType.RTV) && HasAnyImage(_allBackgrounds[ViewType.RTV]))
        {
            sb.AppendLine("Rtv");
            for (int i = 0; i < 4; i++)
            {
                string img = _allBackgrounds[ViewType.RTV][i] ?? "";
                if (!string.IsNullOrEmpty(img))
                    sb.AppendLine($"bkg{(i + 1):00}: <{img}>");
            }
        }
        
        if (HasAnyPsvContent())
        {
            sb.AppendLine("Psv");
            for (int i = 0; i < 4; i++)
            {
                string sky = _allBackgrounds.ContainsKey(ViewType.PSV) ? (_allBackgrounds[ViewType.PSV][i] ?? "") : "";
                string fog = _allFogs.ContainsKey(ViewType.PSV) ? (_allFogs[ViewType.PSV][i] ?? "") : "";
                string sun = _allSuns.ContainsKey(ViewType.PSV) ? (_allSuns[ViewType.PSV][i] ?? "") : "";
                
                if (string.IsNullOrEmpty(sky) && string.IsNullOrEmpty(fog) && string.IsNullOrEmpty(sun))
                    continue;
                
                sb.AppendLine($"bkg{(i + 1):00}: <{sky}><{fog}><{sun}>");
            }
        }
        
        try
        {
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);

            if (!string.IsNullOrEmpty(RegionCode))
            {
                BlendSettingsLoader.InvalidateCache(RegionCode);
                BlendSettingsLoader.LoadRegion(RegionCode);
            }
        }
        catch (Exception ex)
        {
            RSPlugin.log.LogError($"[RegionLogic] Cannot save {path}: {ex.Message}");
        }
    }
    
    private string ModeToString(BlendMode mode)
    {
        switch (mode)
        {
            case BlendMode.Cycle: return "cycle";
            case BlendMode.EndCycle: return "endcycle";
            default: return "loop";
        }
    }
}