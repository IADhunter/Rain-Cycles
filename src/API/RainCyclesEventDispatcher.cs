using UnityEngine;
using RainCycles.Clock;
using RainCycles.Core;
using RainCycles.Settings;

namespace RainCycles.API;

public static class RainCyclesEventDispatcher
{
    private static int _lastEmittedSetting = -1;
    private static float _lastEmittedGlobalT = -1f;
    private static string _lastDispatchedRegion = null;

    internal static bool TransferApplied = false;

    // Placeholder intencional: aquí se suscribirían hooks propios
    // si el dispatcher necesitara reaccionar a eventos del juego.
    public static void Init()
    {
    }

    public static void Terminate()
    {
        _lastDispatchedRegion = null;
        _lastEmittedSetting = -1;
        _lastEmittedGlobalT = -1f;
        TransferApplied = false;
    }

    internal static void DispatchRegionEnter(
        string regionCode, BlendMode? mode, bool isClockEnabled, int initialSetting)
    {
        if (_lastDispatchedRegion == regionCode) return;

        BlendMode? prevMode = RainCyclesAPI.CurrentMode;
        int prevSetting = _lastEmittedSetting;
        float prevGlobalT = _lastEmittedGlobalT;
        float prevProgress = RainCyclesAPI.CurrentProgress;
        bool prevIsIdle = RainCyclesAPI.IsIdle;

        _lastDispatchedRegion = regionCode;
        _lastEmittedSetting = -1;
        _lastEmittedGlobalT = -1f;
        TransferApplied = false;

        RainCyclesAPI.InitialSetting = initialSetting;
        RainCyclesAPI.CurrentRegion = regionCode;
        RainCyclesAPI.CurrentMode = mode;
        RainCyclesAPI.IsClockEnabled = isClockEnabled;
        RainCyclesAPI.CurrentSetting = initialSetting;
        RainCyclesAPI.CurrentProgress = 0f;
        RainCyclesAPI.IsIdle = true;

        var args = new RainCyclesRegionEventArgs
        {
            RegionCode = regionCode,
            Mode = mode,
            IsClockEnabled = isClockEnabled,
            InitialSetting = initialSetting
        };

        string modeStr = mode?.ToString() ?? "OFF";
        RSPlugin.log.LogInfo(
            $"[RainCycles] Región: {regionCode}, Modo: {modeStr}, " +
            $"Clock: {(isClockEnabled ? "ON" : "OFF")}, Setting inicial: {initialSetting}");

        try
        {
            RainCyclesAPI.InvokeRegionEnter(args);
        }
        catch (System.Exception ex)
        {
            RSPlugin.log.LogWarning($"[RainCyclesAPI] Excepción en OnRegionEnter: {ex.Message}");
        }

        if (prevSetting >= 1 && prevSetting <= 4 && prevMode != null && mode != null && prevMode == mode)
        {
            TransferApplied = true;
            DispatchStateChanged(prevSetting, prevProgress, prevIsIdle, prevGlobalT);
        }
    }

    internal static void DispatchStateChanged(
        int setting, float progress, bool isIdle, float globalT)
    {
        if (setting == _lastEmittedSetting &&
            Mathf.Abs(globalT - _lastEmittedGlobalT) < 0.0001f)
            return;

        _lastEmittedSetting = setting;
        _lastEmittedGlobalT = globalT;

        RainCyclesAPI.CurrentSetting = setting;
        RainCyclesAPI.CurrentProgress = progress;
        RainCyclesAPI.IsIdle = isIdle;

        var args = new RainCyclesStateEventArgs
        {
            Setting = setting,
            Progress = progress,
            IsIdle = isIdle,
            GlobalT = globalT
        };

        string phase = isIdle ? "Idle" : "Blending";
        RSPlugin.log.LogDebug(
            $"[RainCycles] T={globalT:F2} \u2192 Setting: {setting}, " +
            $"Progress: {progress:F2}, Phase: {phase}");

        try
        {
            RainCyclesAPI.InvokeStateChanged(args);
        }
        catch (System.Exception ex)
        {
            RSPlugin.log.LogWarning($"[RainCyclesAPI] Excepción en OnStateChanged: {ex.Message}");
        }
    }

    internal static void ForceDispatchCurrentState()
    {
        if (!BlendClock.IsRunning) return;

        float t = BlendClock.T;
        float globalT = t % 1f;
        if (globalT < 0f) globalT += 1f;

        BlendMode mode = RainCyclesAPI.CurrentMode ?? BlendMode.Loop;
        int setting = ThresholdToSetting(globalT, mode);
        bool isIdle = IsThresholdIdle(globalT, mode);
        float progress = isIdle ? 0f : globalT;

        DispatchStateChanged(setting, progress, isIdle, globalT);
    }

    internal static int ThresholdToSetting(float threshold, BlendMode mode)
    {
        int initial = RainCyclesAPI.InitialSetting;
        if (initial < 1 || initial > 4) initial = 1;

        int steps = (mode == BlendMode.Loop) ? 4 : 3;
        int index = Mathf.RoundToInt(threshold * steps);
        index = Mathf.Clamp(index, 0, steps - 1);

        return ((initial - 1 + index) % 4) + 1;
    }

    internal static bool IsThresholdIdle(float threshold, BlendMode mode)
    {
        if (Mathf.Abs(threshold) < 0.0001f) return true;
        if (mode == BlendMode.Loop && Mathf.Abs(threshold - 0.50f) < 0.0001f) return true;
        return false;
    }
}
