using UnityEngine;
using RainCycles.Clock;
using RainCycles.Core;
using RainCycles.Settings;

namespace RainCycles.API;

public static class RainCyclesEventDispatcher
{
    private static int _lastEmittedSetting = -1;
    private static float _lastEmittedGlobalT = -1f;
    private static float _lastEmittedProgress = 0f;
    private static bool _lastEmittedIsIdle = true;
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
        _lastEmittedProgress = 0f;
        _lastEmittedIsIdle = true;
        TransferApplied = false;
    }

    internal static void DispatchRegionEnter(
        string regionCode, BlendMode? mode, bool isClockEnabled, int initialSetting)
    {
        if (_lastDispatchedRegion == regionCode) return;

        BlendMode? prevMode = RainCyclesAPI.CurrentMode;
        int prevSetting = _lastEmittedSetting;
        float prevGlobalT = _lastEmittedGlobalT;
        float prevProgress = _lastEmittedProgress;
        bool prevIsIdle = _lastEmittedIsIdle;

        _lastDispatchedRegion = regionCode;
        _lastEmittedSetting = -1;
        _lastEmittedGlobalT = -1f;
        _lastEmittedProgress = 0f;
        _lastEmittedIsIdle = true;
        TransferApplied = false;

        RainCyclesAPI.InitialSetting = initialSetting;
        RainCyclesAPI.CurrentRegion = regionCode;
        RainCyclesAPI.CurrentMode = mode;
        RainCyclesAPI.IsClockEnabled = isClockEnabled;

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
        _lastEmittedProgress = progress;
        _lastEmittedIsIdle = isIdle;

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

        bool isIdle = BlendClock.CurrentPhase == BlendClock.Phase.Idle;
        float progress = isIdle ? 0f : BlendClock.SubPhaseLocalT;

        DispatchStateChanged(BlendClock.StateA, progress, isIdle, BlendClock.T);
    }

}
