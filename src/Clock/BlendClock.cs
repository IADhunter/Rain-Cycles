using System.Collections.Generic;
using UnityEngine;
using RainCycles.Settings;
using RainCycles.Core;
using RainCycles.Blend;

namespace RainCycles.Clock;

public static class BlendClock
{
    public enum Phase { Idle, Blending }

    public struct ClockState
    {
        public BlendMode Mode;
        public bool IsRunning;
        public float T;
        public Phase CurrentPhase;
        public int StateA;
        public int StateB;
        public float Timer;
    }

    public static Phase CurrentPhase { get; private set; } = Phase.Idle;
    public static int   StateA       { get; private set; } = 1;
    public static int   StateB       { get; private set; } = 1;
    public static float T            { get; private set; } = 0f;
    public static bool  IsRunning    { get; private set; } = false;

    public static float CurrentT => T;
    public static float GlobalT => T;
    public static float SubPhaseLocalT => CalculateLocalT();
    public static int   SubPhaseIndex => CalculateTransitionIndex();
    public static bool  IsFirstHalf => T < 0.5f;

    public static bool EditMode { get; private set; } = false;
    public static void SetEditMode(bool value)
    {
        EditMode = value;
        if (value && IsRunning) Stop();
    }

    private static BlendMode _mode;
    private static List<int> _sequence;
    private static float     _idleDuration;
    private static float     _blendDuration;
    private static float     _timer = 0f;
    private static float     _rainTimer = 0f;
    private static int       _rainCycleLen = 1;
    private static bool      _customPendingStop = false;
    private static string    _regionCode = null;
    private static int       _lastEmittedSetting = -1;
    private static float     _subIdleDuration;
    private static float     _subBlendDuration;
    private static bool     _lastEmittedIsIdle = false;

    public static void SetCustomPendingStop()
    {
        _customPendingStop = true;
    }

    public static ClockState SaveState()
    {
        return new ClockState
        {
            Mode = _mode,
            IsRunning = IsRunning,
            T = T,
            CurrentPhase = CurrentPhase,
            StateA = StateA,
            StateB = StateB,
            Timer = _timer,
        };
    }

    public static void RestoreState(ClockState state)
    {
        if (state.Mode != _mode) return;
        _mode = state.Mode;
        IsRunning = state.IsRunning;
        T = state.T;
        CurrentPhase = state.CurrentPhase;
        StateA = state.StateA;
        StateB = state.StateB;
        _timer = state.Timer;
    }

    public static void Start(string regionCode, int initialState = 1)
    {
        var s = BlendSettingsLoader.Active;
        if (s == null) return;

        _regionCode = regionCode?.ToUpperInvariant();
        _mode = s.Mode;

        _idleDuration = s.IdleTime;
        _blendDuration = s.Duration;
        _subIdleDuration = s.SubIdleTime;
        _subBlendDuration = s.SubDuration;

        switch (_mode)
        {
            case BlendMode.Loop:
                StartLoop(s, initialState);
                break;
            case BlendMode.Cycle:
                StartCycle(s, initialState);
                break;
            case BlendMode.EndCycle:
                StartEndCycle(s, initialState);
                break;
        }
        
        _lastEmittedSetting = -1;
        _lastEmittedIsIdle = false;

        if (IsRunning)
        {
            if (RainCyclesEventDispatcher.TransferApplied)
            {
                RainCyclesEventDispatcher.TransferApplied = false;
            }
            else
            {
                RainCyclesEventDispatcher.DispatchStateChanged(StateA, 0f, CurrentPhase == Phase.Idle, T);
            }
            _lastEmittedSetting = StateA;
            _lastEmittedIsIdle = CurrentPhase == Phase.Idle;
        }

        RSPlugin.log.LogInfo($"[BlendClock] Iniciado - Región: {_regionCode}, Modo: {_mode}, Estado inicial: {initialState}");
    }

    public static void Start(int initialState = 1)
        => Start(BlendSettingsLoader.ActiveRegion, initialState);

    public static void Stop()
    {
        if (!IsRunning) return;

        _lastEmittedSetting = -1;
        _lastEmittedIsIdle = false;

        IsRunning = false;
        T = 0f;
        CurrentPhase = Phase.Idle;
        _timer = 0f;
        _sequence = null;
        _customPendingStop = false;
        _regionCode = null;

        RSPlugin.log.LogInfo("[BlendClock] Detenido");
    }

    public static void Tick(float dt, float rainTimer = 0f, int rainCycleLength = 1)
    {
        if (!IsRunning || EditMode) return;
        var s = BlendSettingsLoader.Active;
        if (s == null) return;

        _rainTimer = rainTimer;
        _rainCycleLen = Mathf.Max(1, rainCycleLength);
        _timer += dt;

        switch (_mode)
        {
            case BlendMode.Loop:
                TickLoop(s);
                break;
            case BlendMode.Cycle:
                TickCycle(s);
                break;
            case BlendMode.EndCycle:
                TickEndCycle(s);
                break;
        }

        CheckAndDispatchThresholds();
    }

    // ============================================================
    // SECUENCIAS INTRÍNSECAS
    // ============================================================
    private static List<int> BuildLoopSequence(int initialState)
    {
        var laneA = new List<int>();
        var laneB = new List<int>();
        
        for (int i = 0; i < 3; i++)
            laneA.Add(((initialState - 1 + i) % 4) + 1);
        
        for (int i = 2; i < 5; i++)
            laneB.Add(((initialState - 1 + i) % 4) + 1);
        
        var flat = new List<int>(laneA);
        for (int i = 1; i < laneB.Count; i++)
            flat.Add(laneB[i]);
        
        if (flat.Count > 1 && flat[flat.Count - 1] == flat[0])
            flat.RemoveAt(flat.Count - 1);
        
        return flat;
    }

    private static List<int> BuildCycleSequence(int initialState)
    {
        var seq = new List<int>();
        for (int i = 0; i < 3; i++)
            seq.Add(((initialState - 1 + i) % 4) + 1);
        return seq;
    }

    private static int CalculateTransitionIndex()
    {
        if (_sequence == null || _sequence.Count < 2) return 0;
        float stepSize = 1f / _sequence.Count;
        return Mathf.Min(Mathf.FloorToInt(T / stepSize), _sequence.Count - 1);
    }

    private static float CalculateLocalT()
    {
        if (_sequence == null || _sequence.Count < 2) return 0f;
        float stepSize = 1f / _sequence.Count;
        int idx = CalculateTransitionIndex();
        float tStart = idx * stepSize;
        float tEnd = (idx + 1) * stepSize;
        if (tEnd <= tStart) return 0f;
        return Mathf.Clamp01((T - tStart) / (tEnd - tStart));
    }

    private static void UpdateStatesFromT()
    {
        if (_sequence == null || _sequence.Count < 2) return;
        
        int transIdx = CalculateTransitionIndex();
        int nextIdx = (transIdx + 1) % _sequence.Count;
        
        StateA = _sequence[transIdx];
        StateB = _sequence[nextIdx];
    }

    // ============================================================
    // LOOP
    // ============================================================
    private static void StartLoop(BlendSettings s, int initialState)
    {
        _sequence = BuildLoopSequence(initialState);
        if (_sequence == null || _sequence.Count < 4)
            _sequence = new List<int> { 1, 2, 3, 4 };

        T = 0f;
        _timer = 0f;
        CurrentPhase = Phase.Idle;
        UpdateStatesFromT();
        IsRunning = true;
    }

    private static void TickLoop(BlendSettings s)
    {
        if (CurrentPhase == Phase.Idle)
        {
            if (_timer < _idleDuration) return;

            _timer = 0f;
            CurrentPhase = Phase.Blending;
            
            if (T >= 1.0f) T = 0f;
            UpdateStatesFromT();
        }
        else if (CurrentPhase == Phase.Blending)
        {
            float progress = Mathf.Clamp01(_timer / _blendDuration);
            
            float tStart = IsFirstHalf ? 0f : 0.5f;
            float tEnd = IsFirstHalf ? 0.5f : 1.0f;
            T = Mathf.Lerp(tStart, tEnd, progress);
            UpdateStatesFromT();

            if (_timer >= _blendDuration)
            {
                T = tEnd;
                UpdateStatesFromT();
                _timer = 0f;
                CurrentPhase = Phase.Idle;

                if (T >= 1.0f)
                {
                    if (_customPendingStop)
                    {
                        Stop();
                        return;
                    }
                    T = 0f;
                    UpdateStatesFromT();
                }
            }
        }
    }

    // ============================================================
    // CYCLE
    // ============================================================
    private static void StartCycle(BlendSettings s, int initialState)
    {
        _sequence = BuildCycleSequence(initialState);
        if (_sequence == null || _sequence.Count < 3)
            _sequence = new List<int> { 1, 2, 3 };

        T = 0f;
        _timer = 0f;
        CurrentPhase = Phase.Blending;
        UpdateStatesFromT();
        IsRunning = true;
    }

    private static void TickCycle(BlendSettings s)
    {
        if (CurrentPhase != Phase.Blending) return;

        float progress = Mathf.Clamp01(_timer / _blendDuration);
        T = progress;
        UpdateStatesFromT();

        if (_timer >= _blendDuration)
        {
            T = 1f;
            UpdateStatesFromT();
            CurrentPhase = Phase.Idle;
        }
    }

    // ============================================================
    // ENDCYCLE
    // ============================================================
    private static void StartEndCycle(BlendSettings s, int initialState)
    {
        _sequence = BuildCycleSequence(initialState);
        if (_sequence == null || _sequence.Count < 3)
            _sequence = new List<int> { 1, 2, 3 };

        T = 0f;
        _timer = 0f;

        if (_idleDuration > 0f)
        {
            CurrentPhase = Phase.Idle;
            UpdateStatesFromT();
        }
        else
        {
            CurrentPhase = Phase.Blending;
            UpdateStatesFromT();
        }

        IsRunning = true;
    }

    private static void TickEndCycle(BlendSettings s)
    {
        if (CurrentPhase == Phase.Idle)
        {
            if (_timer < _idleDuration) return;
            _timer = 0f;
            CurrentPhase = Phase.Blending;
            UpdateStatesFromT();
        }
        else if (CurrentPhase == Phase.Blending)
        {
            float progress = Mathf.Clamp01(_timer / _blendDuration);
            T = progress;
            UpdateStatesFromT();

            if (_timer >= _blendDuration)
            {
                T = 1f;
                UpdateStatesFromT();
                CurrentPhase = Phase.Idle;
                CheckAndDispatchThresholds();
                IsRunning = false;
                RSPlugin.log.LogInfo("[BlendClock] EndCycle completado - Blend detenido");
            }
        }
    }

    // ============================================================
    // DETECCIÓN DE UMBRALES PARA LA API
    // ============================================================

    private static void CheckAndDispatchThresholds()
    {
        if (!IsRunning || EditMode) return;

        bool isIdleNow = CurrentPhase == Phase.Idle;
        int settingNow = StateA;

        if (settingNow != _lastEmittedSetting || isIdleNow != _lastEmittedIsIdle)
        {
            float progress = isIdleNow ? 0f : SubPhaseLocalT;
            RainCyclesEventDispatcher.DispatchStateChanged(settingNow, progress, isIdleNow, T);
            _lastEmittedSetting = settingNow;
            _lastEmittedIsIdle = isIdleNow;
        }
    }
}