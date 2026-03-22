using System.Collections.Generic;
using UnityEngine;

namespace FilesSetting;

public static class BlendClock
{
    public enum Phase { Idle, Blending, Done }

    public static float CurrentT        { get; private set; } = 0f;
    public static Phase CurrentPhase    { get; private set; } = Phase.Idle;
    public static int   SubPhaseIndex   { get; private set; } = 0;
    public static float SubPhaseLocalT  { get; private set; } = 0f;
    public static int   StateA          { get; private set; } = 1;
    public static int   StateB          { get; private set; } = 2;
    public static bool  IsLaneA         { get; private set; } = true;
    public static bool  IsRunning       { get; private set; } = false;

    private static float      _timer       = 0f;
    private static List<int>  _currentLane = null;
    private static List<int>  _otherLane   = null;

    public static void Start(int initialStateA = 1)
    {
        var settings = BlendSettingsLoader.Active;
        if (settings == null)
        {
            Plugin.RSPlugin.log.LogWarning("[BlendClock] Cannot start: no BlendSettings loaded.");
            return;
        }
        if (settings.Mode == BlendMode.Loop)
            StartLoop(settings, initialStateA);
    }

    public static void Stop()
    {
        IsRunning      = false;
        CurrentT       = 0f;
        CurrentPhase   = Phase.Idle;
        SubPhaseIndex  = 0;
        SubPhaseLocalT = 0f;
        _timer         = 0f;
        _currentLane   = null;
        _otherLane     = null;
        Plugin.RSPlugin.log.LogInfo("[BlendClock] Stopped.");
    }

    public static void ForceStates(int a, int b)
    {
        StateA = a;
        StateB = b;
    }

    public static void Tick(float deltaTime)
    {
        if (!IsRunning) return;
        var settings = BlendSettingsLoader.Active;
        if (settings == null) return;
        if (settings.Mode != BlendMode.Loop) return;

        _timer += deltaTime;

        switch (CurrentPhase)
        {
            case Phase.Idle:     TickIdle(settings);     break;
            case Phase.Blending: TickBlending(settings); break;
            case Phase.Done:     DoRelay(settings);      break;
        }
    }

    private static void StartLoop(BlendSettings settings, int initialState)
    {
        int resolvedA = FindFirstValidState(settings, initialState);
        if (resolvedA < 0)
        {
            Plugin.RSPlugin.log.LogWarning("[BlendClock] Cannot start: no valid state files found.");
            return;
        }

        var laneData = settings.GetLoopLane(resolvedA);
        if (laneData.HasValue && laneData.Value.IsValid)
        {
            var lane = laneData.Value;
            if (lane.LaneA[0] == resolvedA)
                SetupLane(lane.LaneA, lane.LaneB, true);
            else if (lane.LaneB[0] == resolvedA)
                SetupLane(lane.LaneB, lane.LaneA, false);
            else if (lane.LaneA.Contains(resolvedA))
                SetupLane(lane.LaneA, lane.LaneB, true);
            else
                SetupLane(lane.LaneB, lane.LaneA, false);
        }
        else
        {
            var seq = BuildSequenceFrom(settings, resolvedA);
            SetupLane(seq, null, true);
        }

        IsRunning = true;
        Plugin.RSPlugin.log.LogInfo(
            $"[BlendClock] Started Loop. lane={(IsLaneA ? "A" : "B")} [{string.Join(",", _currentLane)}]");
    }

    private static void SetupLane(List<int> active, List<int> other, bool isLaneA)
    {
        _currentLane   = active;
        _otherLane     = other;
        IsLaneA        = isLaneA;
        CurrentT       = 0f;
        SubPhaseIndex  = 0;
        SubPhaseLocalT = 0f;
        _timer         = 0f;
        CurrentPhase   = Phase.Idle;
        UpdateStatesFromSubPhase();
    }

    private static void TickIdle(BlendSettings settings)
    {
        if (_timer < settings.LoopIdleTime) return;
        _timer         = 0f;
        CurrentT       = 0f;
        SubPhaseIndex  = 0;
        SubPhaseLocalT = 0f;
        CurrentPhase   = Phase.Blending;
        UpdateStatesFromSubPhase();
        Plugin.RSPlugin.log.LogInfo(
            $"[BlendClock] Idle → Blending lane {(IsLaneA ? "A" : "B")} [{string.Join("→", _currentLane)}]");
    }

    private static void TickBlending(BlendSettings settings)
    {
        float duration = settings.LoopDuration > 0f ? settings.LoopDuration : 1f;
        int   subCount = _currentLane.Count - 1;
        float subSize  = 1f / subCount;

        CurrentT = Mathf.Clamp01(_timer / duration);

        int   newSub   = Mathf.Min(Mathf.FloorToInt(CurrentT / subSize), subCount - 1);
        float localT   = Mathf.Clamp01((CurrentT - newSub * subSize) / subSize);

        if (newSub != SubPhaseIndex)
        {
            SubPhaseIndex = newSub;
            UpdateStatesFromSubPhase();
            Plugin.RSPlugin.log.LogInfo($"[BlendClock] Sub-phase {SubPhaseIndex}: {StateA}→{StateB}");
        }
        SubPhaseLocalT = localT;

        if (_timer < duration) return;

        CurrentT       = 1f;
        SubPhaseIndex  = subCount - 1;
        SubPhaseLocalT = 1f;
        UpdateStatesFromSubPhase();
        _timer        = 0f;
        CurrentPhase  = Phase.Done;
        Plugin.RSPlugin.log.LogInfo($"[BlendClock] Lane {(IsLaneA ? "A" : "B")} complete → Done");
    }

    private static void DoRelay(BlendSettings settings)
    {
        if (_otherLane != null && _otherLane.Count >= 2)
        {
            var temp     = _currentLane;
            _currentLane = _otherLane;
            _otherLane   = temp;
            IsLaneA      = !IsLaneA;
            Plugin.RSPlugin.log.LogInfo(
                $"[BlendClock] Relay → lane {(IsLaneA ? "A" : "B")} [{string.Join("→", _currentLane)}]");
        }

        var s = BlendSettingsLoader.Active;
        if (s != null)
        {
            for (int i = 1; i < _currentLane.Count; i++)
            {
                if (!StateExists(s, _currentLane[i]))
                {
                    Plugin.RSPlugin.log.LogWarning(
                        $"[BlendClock] No valid file for state {_currentLane[i]}. Stopping.");
                    Stop();
                    return;
                }
            }
        }

        CurrentT       = 0f;
        SubPhaseIndex  = 0;
        SubPhaseLocalT = 0f;
        _timer         = 0f;
        CurrentPhase   = Phase.Idle;
        UpdateStatesFromSubPhase();
        Plugin.RSPlugin.log.LogInfo($"[BlendClock] Entering Idle for lane {(IsLaneA ? "A" : "B")}");
    }

    private static void UpdateStatesFromSubPhase()
    {
        if (_currentLane == null || _currentLane.Count < 2) return;
        int idxA = Mathf.Clamp(SubPhaseIndex,     0, _currentLane.Count - 1);
        int idxB = Mathf.Clamp(SubPhaseIndex + 1, 0, _currentLane.Count - 1);
        StateA = _currentLane[idxA];
        StateB = _currentLane[idxB];
    }

    private static int FindFirstValidState(BlendSettings settings, int startState)
    {
        if (StateExists(settings, startState)) return startState;
        var seq = BuildSequenceFrom(settings, startState);
        foreach (int s in seq)
            if (StateExists(settings, s)) return s;
        int n = CountStatesForActiveRegion(settings);
        for (int i = 1; i <= n; i++)
            if (StateExists(settings, i)) return i;
        return -1;
    }

    private static List<int> BuildSequenceFrom(BlendSettings settings, int stateA)
    {
        var defined = settings.GetSequenceFor(stateA);
        if (defined != null && defined.Count > 0) return defined;
        int n = CountStatesForActiveRegion(settings);
        var fallback = new List<int>(n);
        for (int i = 0; i < n; i++)
            fallback.Add((stateA - 1 + i) % n + 1);
        return fallback;
    }

    private static bool StateExists(BlendSettings settings, int state)
    {
        if (state < 1) return false;
        if (!settings._hasRoomsSection || settings.Rooms.Count == 0) return true;
        foreach (string room in settings.Rooms)
            if (ReadStateReadFiles.GetRainStateSettingsFile(room, state) != null)
                return true;
        return false;
    }

    private static int CountStatesForActiveRegion(BlendSettings settings)
    {
        if (!settings._hasRoomsSection || settings.Rooms.Count == 0) return 2;
        foreach (string room in settings.Rooms)
        {
            int n = ReadStateReadFiles.CountRainStateFiles(room);
            if (n > 0) return n;
        }
        return 2;
    }
}