using System;

namespace RainCycles.API;

public static class RainCyclesAPI
{
    public static event Action<RainCyclesRegionEventArgs> OnRegionEnter;
    public static event Action<RainCyclesStateEventArgs> OnStateChanged;

    public static string CurrentRegion { get; internal set; }
    public static int CurrentSetting => BlendClock.IsRunning ? BlendClock.StateA : 0;
    public static int NextSetting => BlendClock.IsRunning ? BlendClock.StateB : 0;
    public static float CurrentProgress =>
        BlendClock.IsRunning && BlendClock.CurrentPhase == BlendClock.Phase.Blending
            ? BlendClock.SubPhaseLocalT : 0f;
    public static bool IsIdle => !BlendClock.IsRunning || BlendClock.CurrentPhase == BlendClock.Phase.Idle;
    public static float CurrentGlobalT => BlendClock.IsRunning ? BlendClock.T : 0f;
    public static bool IsClockEnabled { get; internal set; }
    public static BlendMode? CurrentMode { get; internal set; }
    public static int InitialSetting { get; internal set; }

    public static void ForceNotify()
    {
        RainCyclesEventDispatcher.ForceDispatchCurrentState();
    }

    internal static void InvokeRegionEnter(RainCyclesRegionEventArgs args)
    {
        Delegate[] dlist = OnRegionEnter?.GetInvocationList();
        if (dlist == null) return;
        foreach (Delegate d in dlist)
        {
            try { ((Action<RainCyclesRegionEventArgs>)d).Invoke(args); }
            catch (Exception ex) { RSPlugin.log.LogWarning($"[RainCyclesAPI] Handler exception in OnRegionEnter: {ex.Message}"); }
        }
    }

    internal static void InvokeStateChanged(RainCyclesStateEventArgs args)
    {
        Delegate[] dlist = OnStateChanged?.GetInvocationList();
        if (dlist == null) return;
        foreach (Delegate d in dlist)
        {
            try { ((Action<RainCyclesStateEventArgs>)d).Invoke(args); }
            catch (Exception ex) { RSPlugin.log.LogWarning($"[RainCyclesAPI] Handler exception in OnStateChanged: {ex.Message}"); }
        }
    }
}

public class RainCyclesRegionEventArgs
{
    public string RegionCode { get; set; }
    public BlendMode? Mode { get; set; }
    public bool IsClockEnabled { get; set; }
    public int InitialSetting { get; set; }
}

public class RainCyclesStateEventArgs
{
    public int Setting { get; set; }
    public float Progress { get; set; }
    public bool IsIdle { get; set; }
    public float GlobalT { get; set; }
    public string Phase => IsIdle ? "Idle" : "Blending";
}
