using System;

namespace RainCycles.API;

public static class RainCyclesAPI
{
    public static event EventHandler<RainCyclesRegionEventArgs> OnRegionEnter;
    public static event EventHandler<RainCyclesStateEventArgs> OnStateChanged;

    public static string CurrentRegion { get; internal set; }
    public static int CurrentSetting { get; internal set; }
    public static float CurrentProgress { get; internal set; }
    public static bool IsIdle { get; internal set; }
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
            try { ((EventHandler<RainCyclesRegionEventArgs>)d).Invoke(null, args); }
            catch (Exception ex) { RSPlugin.log.LogWarning($"[RainCyclesAPI] Handler exception in OnRegionEnter: {ex.Message}"); }
        }
    }

    internal static void InvokeStateChanged(RainCyclesStateEventArgs args)
    {
        Delegate[] dlist = OnStateChanged?.GetInvocationList();
        if (dlist == null) return;
        foreach (Delegate d in dlist)
        {
            try { ((EventHandler<RainCyclesStateEventArgs>)d).Invoke(null, args); }
            catch (Exception ex) { RSPlugin.log.LogWarning($"[RainCyclesAPI] Handler exception in OnStateChanged: {ex.Message}"); }
        }
    }
}

public class RainCyclesRegionEventArgs : EventArgs
{
    public string RegionCode { get; set; }
    public BlendMode? Mode { get; set; }
    public bool IsClockEnabled { get; set; }
    public int InitialSetting { get; set; }
}

public class RainCyclesStateEventArgs : EventArgs
{
    public int Setting { get; set; }
    public float Progress { get; set; }
    public bool IsIdle { get; set; }
    public float GlobalT { get; set; }
    public string Phase => IsIdle ? "Idle" : "Blending";
}
