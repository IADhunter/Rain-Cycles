using System.Collections.Generic;
using UnityEngine;

namespace FilesSetting;

public static class BlendClock
{
    public enum Phase { Idle, Blending, Done }

    // ── Public state ──────────────────────────────────────────────────────
    public static float CurrentT        { get; private set; } = 0f;
    public static Phase CurrentPhase    { get; private set; } = Phase.Idle;
    public static int   SubPhaseIndex   { get; private set; } = 0;
    public static float SubPhaseLocalT  { get; private set; } = 0f;
    public static int   StateA          { get; private set; } = 1;
    public static int   StateB          { get; private set; } = 2;
    public static bool  IsRunning       { get; private set; } = false;

    // Global slider position (0→1) across the full sequence.
    // First half  [0, anchorIndex]   maps to [0.0, 0.5]
    // Second half [anchorIndex, end] maps to [0.5, 1.0]
    public static float GlobalT         { get; private set; } = 0f;

    // True while running the first half of the sequence (before anchor pause).
    // Kept as a named property so BlendClockUpdater / RCPanel can query it
    // without knowing internal indices.
    public static bool  IsFirstHalf     { get; private set; } = true;

    // Legacy alias used by existing callers (BlendClockUpdater.UpdateSliders)
    public static bool  IsLaneA         => IsFirstHalf;

    public static bool EditMode { get; private set; } = false;
    public static void SetEditMode(bool value)
    {
        EditMode = value;
        if (value && IsRunning) Stop();
        Plugin.RSPlugin.log.LogInfo($"[BlendClock] EditMode = {value}");
    }

    // ── Private state ─────────────────────────────────────────────────────

    // Flat sequence built at Start().
    // Example: A=[1,2,3] B=[3,4,1] → _seq = [1,2,3,4,1]
    // The anchor is _seq[_anchorIdx] — the midpoint where the pause happens.
    private static List<int> _seq        = null;
    private static int       _anchorIdx  = 0;

    // Index of the first element of the current half within _seq.
    // First half:  _halfStart = 0,           _halfEnd = _anchorIdx
    // Second half: _halfStart = _anchorIdx,  _halfEnd = _seq.Count - 1
    private static int _halfStart = 0;
    private static int _halfEnd   = 0;

    private static float _timer = 0f;

    // Cycle / EndCycle use a separate plain sequence, not the loop flat list
    private static List<int> _cycleSeq      = null;
    private static float     _rainTimer     = 0f;
    private static int       _rainCycleLen  = 1;

    private static bool _customPendingStop = false;
    public static void SetCustomPendingStop()
    {
        _customPendingStop = true;
        Plugin.RSPlugin.log.LogInfo("[BlendClock] Custom pending stop set.");
    }

    // ── Public API ────────────────────────────────────────────────────────

    public static void Start(int initialStateA = 1)
    {
        var s = BlendSettingsLoader.Active;
        if (s == null) { Plugin.RSPlugin.log.LogWarning("[BlendClock] Cannot start: no settings."); return; }
        switch (s.Mode)
        {
            case BlendMode.Loop:     StartLoop(s, initialStateA);     break;
            case BlendMode.Cycle:    StartCycle(s, initialStateA);    break;
            case BlendMode.EndCycle: StartEndCycle(s, initialStateA); break;
            case BlendMode.Custom:   StartLoop(s, initialStateA);     break;
        }
    }

    public static void Stop()
    {
        IsRunning          = false;
        CurrentT           = 0f;
        GlobalT            = 0f;
        CurrentPhase       = Phase.Idle;
        SubPhaseIndex      = 0;
        SubPhaseLocalT     = 0f;
        IsFirstHalf        = true;
        _timer             = 0f;
        _seq               = null;
        _cycleSeq          = null;
        _customPendingStop = false;
        Plugin.RSPlugin.log.LogInfo("[BlendClock] Stopped.");
    }

    public static void ForceStates(int a, int b) { StateA = a; StateB = b; }

    public static void Tick(float dt, float rainTimer = 0f, int rainCycleLen = 1)
    {
        if (!IsRunning) return;
        if (EditMode) return;
        var s = BlendSettingsLoader.Active;
        if (s == null) return;

        _rainTimer    = rainTimer;
        _rainCycleLen = Mathf.Max(1, rainCycleLen);
        _timer       += dt;

        switch (s.Mode)
        {
            case BlendMode.Loop:
            case BlendMode.Custom:
                switch (CurrentPhase)
                {
                    case Phase.Idle:     TickIdle(s);    break;
                    case Phase.Blending: TickBlend(s);   break;
                    case Phase.Done:     TickDone(s);    break;
                }
                break;
            case BlendMode.Cycle:    TickCycle(s);    break;
            case BlendMode.EndCycle: TickEndCycle(s); break;
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // LOOP  (and Custom)
    //
    // File: 1: 1, 2, 3, (A = 1,2,3 ~ B = 3,4,1) =bkg00
    // The parser stores LaneA and LaneB in LoopLanes.
    // We read them, build a flat sequence [1,2,3,4,1], and run it as one
    // continuous pass with a pause at the midpoint (the anchor).
    //
    // Single slider (B) shows GlobalT 0→1.
    // First half  [0.0, 0.5] : states from index 0   to anchorIdx
    // Second half [0.5, 1.0] : states from anchorIdx to last
    //
    // Duration per half = LoopDuration  (independent of state count).
    // Pause at anchor and at end = idle_time.
    // ════════════════════════════════════════════════════════════════════

    private static void StartLoop(BlendSettings s, int initialState)
    {
        int resolved = FindFirstValidState(s, initialState);
        if (resolved < 0) { Plugin.RSPlugin.log.LogWarning("[BlendClock] StartLoop: no valid state."); return; }

        List<int> flat = BuildFlatLoop(s, resolved);
        if (flat == null || flat.Count < 2)
        {
<<<<<<< Updated upstream:src/Devtools/Blend/BlendClock.cs
            Plugin.RSPlugin.log.LogInfo($"[BlendClock] StartLoop: no transition for state {resolved}. Idle.");
=======
            RSPlugin.log.LogDebug($"[BlendClock] StartLoop: no transition for state {resolved}. Idle.");
>>>>>>> Stashed changes:src/Clock/BlendClock.cs
            return;
        }

        _seq       = flat;
        // Anchor = midpoint of the flat sequence.
        // For [1,2,3,4,1] (count=5) → anchorIdx = 2 → state 3.
        _anchorIdx = _seq.Count / 2;

        EnterFirstHalf();
        CurrentPhase = Phase.Idle;
        IsRunning    = true;

        Plugin.RSPlugin.log.LogInfo(
            $"[BlendClock] Loop started. seq=[{string.Join(",", _seq)}] " +
            $"anchor={_seq[_anchorIdx]}(idx={_anchorIdx}) " +
            $"idle={s.LoopIdleTime}s dur={s.LoopDuration}s");
    }

    // Builds [1,2,3,4,1] from LoopLane data or from the base sequence.
    private static List<int> BuildFlatLoop(BlendSettings s, int resolved)
    {
        var laneData = s.GetLoopLane(resolved);
        if (laneData.HasValue && laneData.Value.IsValid)
        {
            var ld = laneData.Value;
            // Determine which lane starts at resolved
            List<int> first, second;
            if (ld.LaneA.Count > 0 && ld.LaneA[0] == resolved)
            { first = ld.LaneA; second = ld.LaneB; }
            else if (ld.LaneB.Count > 0 && ld.LaneB[0] == resolved)
            { first = ld.LaneB; second = ld.LaneA; }
            else
            {
                Plugin.RSPlugin.log.LogWarning(
                    $"[BlendClock] resolved={resolved} not at [0] of either lane. Using LaneA.");
                first = ld.LaneA; second = ld.LaneB;
            }

            // Concatenate, removing the duplicate anchor (last of first = first of second)
            var flat = new List<int>(first);
            for (int i = 1; i < second.Count; i++) // skip second[0] — it's the anchor duplicate
                flat.Add(second[i]);
            return flat;
        }

        // No lane block: use the base sequence as the full pass.
        // If it has 2+ elements treat it as-is; add the start at the end to close the loop.
        var seq = BuildSeqFrom(s, resolved);
        if (seq.Count < 2) return null;

        // Close the loop: append the starting state at the end if not already there
        var result = new List<int>(seq);
        if (result[result.Count - 1] != resolved)
            result.Add(resolved);
        return result;
    }

    // Set internal half-range to first half and reset counters.
    private static void EnterFirstHalf()
    {
        IsFirstHalf    = true;
        _halfStart     = 0;
        _halfEnd       = _anchorIdx;
        CurrentT       = 0f;
        SubPhaseIndex  = 0;
        SubPhaseLocalT = 0f;
        _timer         = 0f;
        GlobalT        = 0f;
        UpdateSeqStates(_seq, _halfStart);
    }

    // Set internal half-range to second half and reset counters.
    private static void EnterSecondHalf()
    {
        IsFirstHalf    = false;
        _halfStart     = _anchorIdx;
        _halfEnd       = _seq.Count - 1;
        CurrentT       = 0f;
        SubPhaseIndex  = 0;
        SubPhaseLocalT = 0f;
        _timer         = 0f;
        GlobalT        = 0.5f;
        UpdateSeqStates(_seq, _halfStart);
    }

    private static void TickIdle(BlendSettings s)
    {
        if (_timer < s.LoopIdleTime) return;
<<<<<<< Updated upstream:src/Devtools/Blend/BlendClock.cs
=======
        RSPlugin.log.LogDebug(
            $"[BlendClock] Idle → Blending {(IsFirstHalf ? "first" : "second")} half " +
            $"[{string.Join("→", _seq.GetRange(_halfStart, _halfEnd - _halfStart + 1))}]");
>>>>>>> Stashed changes:src/Clock/BlendClock.cs
        _timer = 0f;
        CurrentPhase = Phase.Blending;
        // Reset T but keep half selection
        CurrentT = 0f; SubPhaseIndex = 0; SubPhaseLocalT = 0f;
        UpdateSeqStates(_seq, _halfStart);
        Plugin.RSPlugin.log.LogInfo(
            $"[BlendClock] Idle → Blending {(IsFirstHalf ? "first" : "second")} half " +
            $"[{string.Join("→", _seq.GetRange(_halfStart, _halfEnd - _halfStart + 1))}]");
    }

    private static void TickBlend(BlendSettings s)
    {
        float dur      = s.LoopDuration > 0f ? s.LoopDuration : 1f;
        int   subCount = _halfEnd - _halfStart; // number of transitions in this half

        if (subCount <= 0) { Plugin.RSPlugin.log.LogWarning("[BlendClock] TickBlend: half has 0 transitions."); Stop(); return; }

        float subSize = 1f / subCount;
        CurrentT = Mathf.Clamp01(_timer / dur);

        // GlobalT: first half → [0, 0.5], second half → [0.5, 1]
        GlobalT = IsFirstHalf
            ? CurrentT * 0.5f
            : 0.5f + CurrentT * 0.5f;

        int   newSub = Mathf.Min(Mathf.FloorToInt(CurrentT / subSize), subCount - 1);
        float locT   = Mathf.Clamp01((CurrentT - newSub * subSize) / subSize);

        if (newSub != SubPhaseIndex)
        {
            SubPhaseIndex = newSub;
            UpdateSeqStates(_seq, _halfStart);
        }
        SubPhaseLocalT = locT;

        if (_timer < dur) return;

        // Half complete
        CurrentT = 1f; SubPhaseIndex = subCount - 1; SubPhaseLocalT = 1f;
        UpdateSeqStates(_seq, _halfStart);
        _timer       = 0f;
        CurrentPhase = Phase.Done;
        GlobalT      = IsFirstHalf ? 0.5f : 1.0f;

<<<<<<< Updated upstream:src/Devtools/Blend/BlendClock.cs
        Plugin.RSPlugin.log.LogInfo(
=======
        RSPlugin.log.LogDebug(
>>>>>>> Stashed changes:src/Clock/BlendClock.cs
            $"[BlendClock] {(IsFirstHalf ? "First" : "Second")} half complete → Done" +
            (_customPendingStop ? " (custom stop pending)" : ""));
    }

    private static void TickDone(BlendSettings s)
    {
        if (_customPendingStop) { Stop(); return; }

        if (IsFirstHalf)
        {
            // Finished first half → pause at anchor, then start second half
            if (_timer < s.LoopIdleTime) return;
            EnterSecondHalf();
            CurrentPhase = Phase.Idle; // will fire immediately next TickIdle since _timer reset
            Plugin.RSPlugin.log.LogInfo($"[BlendClock] Done(first) → Idle before second half. Anchor={StateA}");
        }
        else
        {
            // Finished second half → validate, then restart from the beginning
            var settings = BlendSettingsLoader.Active;
            if (settings != null)
                foreach (int st in _seq)
                    if (!StateExists(settings, st))
                    {
                        Plugin.RSPlugin.log.LogWarning($"[BlendClock] State {st} missing. Stopping."); Stop(); return;
                    }

            EnterFirstHalf();
            CurrentPhase = Phase.Idle;
            Plugin.RSPlugin.log.LogInfo($"[BlendClock] Done(second) → Idle before first half. StateA={StateA}");
        }
    }

    // Updates StateA/StateB from a sequence and the current SubPhaseIndex.
    // halfStart offsets into the full _seq array.
    private static void UpdateSeqStates(List<int> seq, int halfStart)
    {
        if (seq == null || seq.Count < 2) return;
        int ia = Mathf.Clamp(halfStart + SubPhaseIndex,     0, seq.Count - 1);
        int ib = Mathf.Clamp(halfStart + SubPhaseIndex + 1, 0, seq.Count - 1);
        StateA = seq[ia];
        StateB = seq[ib];
    }

    // ════════════════════════════════════════════════════════════════════
    // CYCLE
    // ════════════════════════════════════════════════════════════════════

    private static void StartCycle(BlendSettings s, int initialState)
    {
        int resolved = FindFirstValidState(s, initialState);
        if (resolved < 0) { Plugin.RSPlugin.log.LogWarning("[BlendClock] StartCycle: no valid state."); return; }
        var seq = BuildSeqFrom(s, resolved);
        if (seq.Count < 2) { Plugin.RSPlugin.log.LogInfo("[BlendClock] StartCycle: no transition."); return; }
        _cycleSeq = seq;
        ResetCycleCounters();
        IsRunning = true;
        Plugin.RSPlugin.log.LogInfo($"[BlendClock] Cycle started. seq=[{string.Join(",", seq)}] trigger={s.CycleTriggerPct:P0} dur={s.CycleDuration}s");
    }

    private static void ResetCycleCounters()
    {
        CurrentT = 0f; SubPhaseIndex = 0; SubPhaseLocalT = 0f; _timer = 0f;
        CurrentPhase = Phase.Idle;
        UpdateCycleStates();
    }

    private static void UpdateCycleStates()
    {
        if (_cycleSeq == null || _cycleSeq.Count < 2) return;
        StateA = _cycleSeq[Mathf.Clamp(SubPhaseIndex,     0, _cycleSeq.Count - 1)];
        StateB = _cycleSeq[Mathf.Clamp(SubPhaseIndex + 1, 0, _cycleSeq.Count - 1)];
    }

    private static void TickCycle(BlendSettings s)
    {
        if (CurrentPhase == Phase.Idle)
        {
            if (_rainTimer < s.CycleTriggerPct * _rainCycleLen) return;
            _timer = 0f; CurrentT = 0f; SubPhaseIndex = 0; SubPhaseLocalT = 0f;
            CurrentPhase = Phase.Blending; UpdateCycleStates();
            Plugin.RSPlugin.log.LogInfo($"[BlendClock] Cycle triggered {_rainTimer:F1}/{_rainCycleLen}.");
        }
        else if (CurrentPhase == Phase.Blending)
        {
            TickLinearBlend(s.CycleDuration, _cycleSeq, UpdateCycleStates, () =>
            {
                CurrentPhase = Phase.Done;
                Plugin.RSPlugin.log.LogInfo($"[BlendClock] Cycle complete at state {StateB}.");
            });
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // ENDCYCLE
    // ════════════════════════════════════════════════════════════════════

    private static void StartEndCycle(BlendSettings s, int initialState)
    {
        int resolved = FindFirstValidState(s, initialState);
        if (resolved < 0) { Plugin.RSPlugin.log.LogWarning("[BlendClock] StartEndCycle: no valid state."); return; }
        var seq = BuildSeqFrom(s, resolved);
        if (seq.Count < 2) { Plugin.RSPlugin.log.LogInfo("[BlendClock] StartEndCycle: no transition."); return; }
        _cycleSeq = seq;
        ResetCycleCounters();
        IsRunning = true;
        Plugin.RSPlugin.log.LogInfo($"[BlendClock] EndCycle started. seq=[{string.Join(",", seq)}] idle={s.EndCycleIdleTime}s dur={s.EndCycleDuration}s target={s.EndCycleTargetState}");
    }

    private static void TickEndCycle(BlendSettings s)
    {
        if (CurrentPhase == Phase.Idle)
        {
            if (_timer < s.EndCycleIdleTime) return;
            _timer = 0f; CurrentT = 0f; SubPhaseIndex = 0; SubPhaseLocalT = 0f;
            CurrentPhase = Phase.Blending; UpdateCycleStates();
            Plugin.RSPlugin.log.LogInfo($"[BlendClock] EndCycle idle done. Blending {s.EndCycleDuration}s.");
        }
        else if (CurrentPhase == Phase.Blending)
        {
            TickLinearBlend(s.EndCycleDuration, _cycleSeq, UpdateCycleStates, () =>
            {
                CurrentPhase = Phase.Done;
                if (s.EndCycleTargetState == 2)
                {
                    int bridge = StateB;
                    Plugin.RSPlugin.log.LogInfo($"[BlendClock] EndCycle → bridging to Loop from {bridge}.");
                    Stop();
                    var settings = BlendSettingsLoader.Active;
                    if (settings != null) StartLoopSkipIdle(settings, bridge);
                }
                else Plugin.RSPlugin.log.LogInfo($"[BlendClock] EndCycle complete at state {StateB}.");
            });
        }
    }

    // Generic linear blend ticker for Cycle / EndCycle
    private static void TickLinearBlend(float duration, List<int> seq,
        System.Action updateStates, System.Action onDone)
    {
        if (duration <= 0f) duration = 1f;
        int subCount = seq != null ? seq.Count - 1 : 0;
        if (subCount <= 0) { Stop(); return; }

        float subSize = 1f / subCount;
        CurrentT = Mathf.Clamp01(_timer / duration);

        int   newSub = Mathf.Min(Mathf.FloorToInt(CurrentT / subSize), subCount - 1);
        float locT   = Mathf.Clamp01((CurrentT - newSub * subSize) / subSize);

        if (newSub != SubPhaseIndex) { SubPhaseIndex = newSub; updateStates(); }
        SubPhaseLocalT = locT;

        if (_timer < duration) return;
        CurrentT = 1f; SubPhaseIndex = subCount - 1; SubPhaseLocalT = 1f;
        updateStates(); _timer = 0f; onDone();
    }

    private static void StartLoopSkipIdle(BlendSettings s, int fromState)
    {
        int resolved = FindFirstValidState(s, fromState);
        if (resolved < 0) return;
        List<int> flat = BuildFlatLoop(s, resolved);
        if (flat == null || flat.Count < 2) return;

        _seq       = flat;
        _anchorIdx = _seq.Count / 2;
        EnterFirstHalf();
        CurrentPhase = Phase.Blending; // skip idle
        IsRunning    = true;
        Plugin.RSPlugin.log.LogInfo($"[BlendClock] Loop (skip-idle) from {resolved}. seq=[{string.Join(",", _seq)}]");
    }

    // ════════════════════════════════════════════════════════════════════
    // HELPERS
    // ════════════════════════════════════════════════════════════════════

    private static int FindFirstValidState(BlendSettings s, int start)
    {
        if (StateExists(s, start)) return start;
        var seq = s.GetSequenceFor(start);
        if (seq != null) foreach (int st in seq) if (StateExists(s, st)) return st;
        Plugin.RSPlugin.log.LogWarning($"[BlendClock] State {start} not found. Cannot start.");
        return -1;
    }

    private static List<int> BuildSeqFrom(BlendSettings s, int stateA)
    {
        var d = s.GetSequenceFor(stateA);
        return (d != null && d.Count > 0) ? d : new List<int> { stateA };
    }

    private static bool StateExists(BlendSettings s, int state)
    {
        if (state < 1) return false;
        if (!s._hasRoomsSection || s.Rooms.Count == 0) return true;
        foreach (string room in s.Rooms.Keys)
            if (ReadStateReadFiles.GetRainStateSettingsFile(room, state) != null) return true;
        return false;
    }
}