using System;
using System.IO;
using System.Text;
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
    
    public float BlendValue
    {
        get => DurationValue;
        set => DurationValue = value;
    }

    public RegionLogic(RCPanel_RegionPage page)
    {
        _page = page;
    }

    public System.Action OnSaved;

    // ============================================================
    // HELPERS
    // ============================================================
    // Flag de arena: la pestaña edita el blend settings PER-LEVEL ({level}_blend_settings.txt)
    // en lugar del archivo regional de historia.
    private bool IsArena => StateFileResolver.IsArenaMode;

    private string RegionCode => ExtractRegionCode(_page.ParentPanel.CurrentRoomName);
    public string BlendSettingsPath => GetBlendSettingsPath();
    
    private string GetBlendSettingsPath()
    {
        if (IsArena)
            return ArenaBlendController.ResolveBlendSettingsPath(_page.ParentPanel.CurrentRoomName);

        string roomName = _page.ParentPanel.CurrentRoomName;
        if (BlendSettingsLoader.IsGateRoom(roomName))
            return BlendSettingsLoader.ResolveGateBlendPath(roomName);

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
    // PUBLIC API
    // ============================================================
    private static readonly BlendMode[] _modes = { BlendMode.Loop, BlendMode.Cycle, BlendMode.EndCycle };
    // En arena solo Loop y Cycle son válidos (EndCycle se fuerza a Loop en runtime).
    private static readonly BlendMode[] _arenaModes = { BlendMode.Loop, BlendMode.Cycle };
    private BlendMode[] Modes => IsArena ? _arenaModes : _modes;
    private int _modeIndex = 0;

    private static readonly LoopTrigger[] _triggers = { LoopTrigger.None, LoopTrigger.Cycle, LoopTrigger.Rain };
    // En arena los triggers de Loop no se usan (siempre none).
    private static readonly LoopTrigger[] _arenaTriggers = { LoopTrigger.None };
    private LoopTrigger[] Triggers => IsArena ? _arenaTriggers : _triggers;
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
        if (_triggerIndex < 0) _triggerIndex = Triggers.Length - 1;
        if (_triggerIndex >= Triggers.Length) _triggerIndex = 0;
        CurrentTrigger = Triggers[_triggerIndex];

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
        if (_modeIndex < 0) _modeIndex = Modes.Length - 1;
        if (_modeIndex >= Modes.Length) _modeIndex = 0;
        CurrentMode = Modes[_modeIndex];

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
        
        foreach (string line in content.Split('\n'))
        {
            string trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#")) continue;
            
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
                }
            }
        }
        
        // Coerciones arena (mismo criterio que ArenaBlendController.LoadBlendSettings):
        // EndCycle -> Loop, triggers -> none, Setting -> 0.
        if (IsArena)
        {
            if (CurrentMode == BlendMode.EndCycle)
            {
                CurrentMode = BlendMode.Loop;
            }
            CurrentTrigger = LoopTrigger.None;
            SettingValue = 0;
        }

        _modeIndex = Array.IndexOf(Modes, CurrentMode);
        if (_modeIndex < 0) _modeIndex = 0;

        if (CurrentMode != BlendMode.Loop)
        {
            CurrentTrigger = LoopTrigger.None;
        }
        
        _triggerIndex = Array.IndexOf(Triggers, CurrentTrigger);
        if (_triggerIndex < 0) _triggerIndex = 0;
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
    
    private void SetDefaultValues()
    {
        CurrentMode = BlendMode.Loop;
        ClockEnabled = false;
        IdleValue = 5f;
        DurationValue = 10f;
        CurrentTrigger = LoopTrigger.None;
        WaitTimeValue = 0f;
        SettingValue = 0;
        _modeIndex = 0;
        _triggerIndex = 0;
    }

    // ============================================================
    // SAVE TO FILE
    // ============================================================
    public void SaveToBlendSettings()
    {
        string path = BlendSettingsPath;
        if (string.IsNullOrEmpty(path))
        {
            if (IsArena)
            {
                // Crea {level}_blend_settings.txt en la carpeta del mod dueño del level
                // (o StreamingAssets si el level es vanilla).
                path = ArenaBlendController.EnsureBlendSettingsFile(_page.ParentPanel.CurrentRoomName);
                if (string.IsNullOrEmpty(path)) return;
            }
            else
            {
                path = BlendSettingsWriter.EnsureFileExists(_page.ParentPanel.CurrentRoomName);
                if (string.IsNullOrEmpty(path)) return;
            }
        }
        
        var sb = new StringBuilder();
        
        sb.AppendLine($"Clock: {(ClockEnabled ? "true" : "false")}");
        sb.AppendLine($"Mode: {ModeToString(CurrentMode)}");
        sb.AppendLine($"Idle_time: {IdleValue:F1}");
        sb.AppendLine($"Duration: {DurationValue:F1}");
        sb.AppendLine($"Trigger: {CurrentTrigger.ToString().ToLowerInvariant()}");
        sb.AppendLine($"wait_time: {WaitTimeValue:F1}");
        sb.AppendLine($"Setting: {SettingValue}");
        
        try
        {
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);

            if (IsArena)
            {
                ArenaBlendController.LoadBlendSettings(_page.ParentPanel.CurrentRoomName);
            }
            else
            {
                string roomName = _page.ParentPanel.CurrentRoomName;
                if (BlendSettingsLoader.IsGateRoom(roomName))
                {
                    string cacheKey = "GATE:" + roomName.ToUpperInvariant();
                    BlendSettingsLoader.InvalidateCache(cacheKey);
                    BlendSettingsLoader.LoadGateSettings(roomName);
                }
                else if (!string.IsNullOrEmpty(RegionCode))
                {
                    BlendSettingsLoader.InvalidateCache(RegionCode);
                    BlendSettingsLoader.LoadRegion(RegionCode);
                }
            }
        }
        catch
        {
        }

        OnSaved?.Invoke();
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