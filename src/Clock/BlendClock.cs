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
        public bool LoopActivated;
        public float IdleDuration;
        public float BlendDuration;
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
    private const float TicksPerSecond = 40f;

    private static string    _regionCode = null;
    private static int       _lastEmittedSetting = -1;
    private static bool      _lastEmittedIsIdle = false;

    // ============================================================
    // TRIGGER DE ACTIVACIÓN PARA LOOP
    // ============================================================
    private static bool      _waitingForThreshold  = false;
    private static bool      _waitingForDeathRain  = false;
    private static bool      _waitingPostRainDelay = false;
    private static bool      _loopActivated        = false;
    private static bool      _deathRainTriggered   = false;

    // ============================================================
    // TRIGGER DE ACTIVACIÓN PARA CYCLE / ENDCYCLE
    // ============================================================
    private static bool      _waitingForCycleThreshold  = false;
    private static bool      _waitingForCycleDeathRain  = false;
    private static bool      _waitingCyclePostRainDelay = false;

    public static void OnDeathRainTriggered()
    {
        _deathRainTriggered = true;
    }

    private static float ResolveEffectiveDuration(float configValue, float fallbackDefault)
    {
        if (configValue > 0f)
            return configValue;

        if (_rainCycleLen > 0)
            return _rainCycleLen / 40f;

        return fallbackDefault;
    }

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
            LoopActivated = _loopActivated,
            IdleDuration = _idleDuration,
            BlendDuration = _blendDuration,
        };
    }

    public static void RestoreState(ClockState state, bool rainCycleEnded)
    {
        if (state.Mode != _mode) return;

        if (state.Mode == BlendMode.Loop)
        {
            if (!state.LoopActivated) return;
        }
        else if (state.Mode == BlendMode.EndCycle)
        {
            if (!rainCycleEnded) return;
        }
        else
        {
            return;
        }

        _mode = state.Mode;
        IsRunning = state.IsRunning;
        T = state.T;
        CurrentPhase = state.CurrentPhase;
        StateA = state.StateA;
        StateB = state.StateB;

        if (state.CurrentPhase == Phase.Blending)
        {
            float progress = T < 0.5f ? T / 0.5f : (T - 0.5f) / 0.5f;
            progress = Mathf.Clamp01(progress);
            _timer = progress * _blendDuration;
        }
        else if (state.CurrentPhase == Phase.Idle)
        {
            float oldIdle = state.IdleDuration > 0f ? state.IdleDuration : 1f;
            _timer = Mathf.Clamp(state.Timer * (_idleDuration / oldIdle), 0f, _idleDuration);
        }
        else
        {
            _timer = 0f;
        }

        _loopActivated = state.LoopActivated;

        _waitingForThreshold = false;
        _waitingForDeathRain = false;
        _waitingPostRainDelay = false;
        _waitingForCycleThreshold = false;
        _waitingForCycleDeathRain = false;
        _waitingCyclePostRainDelay = false;
    }

    public static void Start(string regionCode, int initialState = 1, float rainTimer = 0f, int rainCycleLen = 1)
    {
        var s = BlendSettingsLoader.Active;
        if (s == null) return;

        _regionCode = regionCode?.ToUpperInvariant();
        _mode = s.Mode;

        _idleDuration = ResolveEffectiveDuration(s.IdleTime, 5f);
        _blendDuration = ResolveEffectiveDuration(s.Duration, 10f);

        _rainTimer = rainTimer;
        _rainCycleLen = Mathf.Max(1, rainCycleLen);

        _deathRainTriggered = false;
        _waitingForThreshold = false;
        _waitingForDeathRain = false;
        _waitingPostRainDelay = false;
        _waitingForCycleThreshold = false;
        _waitingForCycleDeathRain = false;
        _waitingCyclePostRainDelay = false;

        switch (_mode)
        {
            case BlendMode.Loop:
                if (s.Trigger == LoopTrigger.None)
                {
                    _loopActivated = true;
                    StartLoop(s, initialState);
                }
                else
                {
                    _loopActivated = false;
                    StateA = initialState;
                    StateB = initialState;
                    T = 0f;
                    CurrentPhase = Phase.Idle;
                    IsRunning = true;

                    if (s.Trigger == LoopTrigger.Cycle)
                    {
                        _waitingForThreshold = true;
                    }
                    else if (s.Trigger == LoopTrigger.Rain)
                    {
                        _waitingForDeathRain = true;
                    }
                }
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

        _waitingForThreshold = false;
        _waitingForDeathRain = false;
        _waitingPostRainDelay = false;
        _loopActivated = false;
        _deathRainTriggered = false;
        _waitingForCycleThreshold = false;
        _waitingForCycleDeathRain = false;
        _waitingCyclePostRainDelay = false;
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
        var seq = new List<int>(4);
        for (int i = 0; i < 4; i++)
            seq.Add(((initialState - 1 + i) % 4) + 1);
        return seq;
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
        int segments = (_mode == BlendMode.Loop) ? _sequence.Count : (_sequence.Count - 1);
        if (segments < 1) segments = 1;
        float stepSize = 1f / segments;
        return Mathf.Min(Mathf.FloorToInt(T / stepSize), segments - 1);
    }

    private static float CalculateLocalT()
    {
        if (_sequence == null || _sequence.Count < 2) return 0f;
        int segments = (_mode == BlendMode.Loop) ? _sequence.Count : (_sequence.Count - 1);
        if (segments < 1) segments = 1;
        float stepSize = 1f / segments;
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
        int nextIdx = (_mode == BlendMode.Loop)
            ? (transIdx + 1) % _sequence.Count
            : Mathf.Min(transIdx + 1, _sequence.Count - 1);
        
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

    private static void ActivateLoopDirectToBlend(int initialState)
    {
        _loopActivated = true;
        _sequence = BuildLoopSequence(initialState);
        if (_sequence == null || _sequence.Count < 4)
            _sequence = new List<int> { 1, 2, 3, 4 };

        T = 0f;
        _timer = 0f;
        CurrentPhase = Phase.Blending;
        UpdateStatesFromT();
        IsRunning = true;
    }

    private static void TickLoop(BlendSettings s)
    {
        if (_waitingForThreshold)
        {
            if (_rainCycleLen <= 0) return;
            float progress = (_rainTimer / _rainCycleLen) * 100f;

            if (progress >= s.WaitTime)
            {
                _waitingForThreshold = false;
                ActivateLoopDirectToBlend(StateA);
            }
            return;
        }

        if (_waitingForDeathRain)
        {
            if (_deathRainTriggered)
            {
                _waitingForDeathRain = false;

                if (s.WaitTime <= 0f)
                {
                    _loopActivated = true;
                    StartLoop(s, StateA);
                }
                else
                {
                    _waitingPostRainDelay = true;
                    _timer = 0f;
                }
            }
            return;
        }

        if (_waitingPostRainDelay)
        {
            if (_timer < s.WaitTime) return;

            _waitingPostRainDelay = false;
            ActivateLoopDirectToBlend(StateA);
            return;
        }

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

        StateA = _sequence[0];
        StateB = _sequence[0];
        CurrentPhase = Phase.Idle;
        IsRunning = true;

        if (s.IdleTime <= 0f || _rainCycleLen <= 0)
        {
            _waitingForCycleThreshold = false;
            ActivateCycleDirectToBlend(initialState, 0f);
            return;
        }

        float globalProgressPct = (_rainTimer / _rainCycleLen) * 100f;

        if (globalProgressPct < s.IdleTime)
        {
            _waitingForCycleThreshold = true;
            _timer = 0f;
        }
        else
        {
            float activationTicks = (s.IdleTime / 100f) * _rainCycleLen;
            float elapsedTicks   = _rainTimer - activationTicks;
            float elapsedSeconds = Mathf.Max(0f, elapsedTicks / TicksPerSecond);

            _waitingForCycleThreshold = false;
            ActivateCycleDirectToBlend(initialState, elapsedSeconds);
        }
    }

    private static void ActivateCycleDirectToBlend(int initialState, float startTimer)
    {
        _sequence = BuildCycleSequence(initialState);
        if (_sequence == null || _sequence.Count < 3)
            _sequence = new List<int> { 1, 2, 3 };

        _timer = startTimer;
        IsRunning = true;

        if (_timer >= _blendDuration)
        {
            int finalState = _sequence[_sequence.Count - 1];
            StateA = finalState;
            StateB = finalState;
            T = 1f;
            CurrentPhase = Phase.Idle;
        }
        else
        {
            T = Mathf.Clamp01(_timer / _blendDuration);
            CurrentPhase = Phase.Blending;
            UpdateStatesFromT();
        }
    }

    private static void TickCycle(BlendSettings s)
    {
        if (_waitingForCycleThreshold)
        {
            if (_rainCycleLen <= 0) return;
            float progress = (_rainTimer / _rainCycleLen) * 100f;

            if (progress >= s.IdleTime)
            {
                _waitingForCycleThreshold = false;
                ActivateCycleDirectToBlend(StateA, 0f);
            }
            return;
        }

        if (CurrentPhase != Phase.Blending) return;

        float progress2 = Mathf.Clamp01(_timer / _blendDuration);
        T = progress2;
        UpdateStatesFromT();

        if (_timer >= _blendDuration)
        {
            T = 1f;
            int finalState = _sequence[_sequence.Count - 1];
            StateA = finalState;
            StateB = finalState;
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

        StateA = _sequence[0];
        StateB = _sequence[0];
        T = 0f;
        _timer = 0f;
        CurrentPhase = Phase.Idle;
        IsRunning = true;

        _waitingForCycleDeathRain  = true;
        _waitingCyclePostRainDelay = false;
    }

    private static void TickEndCycle(BlendSettings s)
    {
        if (_waitingForCycleDeathRain)
        {
            if (_deathRainTriggered)
            {
                _waitingForCycleDeathRain = false;

                if (s.IdleTime <= 0f)
                {
                    ActivateCycleDirectToBlend(StateA, 0f);
                }
                else
                {
                    _waitingCyclePostRainDelay = true;
                    _timer = 0f;
                }
            }
            return;
        }

        if (_waitingCyclePostRainDelay)
        {
            if (_timer < s.IdleTime) return;

            _waitingCyclePostRainDelay = false;
            ActivateCycleDirectToBlend(StateA, 0f);
            return;
        }

        if (CurrentPhase == Phase.Blending)
        {
            float progress = Mathf.Clamp01(_timer / _blendDuration);
            T = progress;
            UpdateStatesFromT();

            if (_timer >= _blendDuration)
            {
                T = 1f;
                int finalState = _sequence[_sequence.Count - 1];
                StateA = finalState;
                StateB = finalState;
                CurrentPhase = Phase.Idle;
                CheckAndDispatchThresholds();
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