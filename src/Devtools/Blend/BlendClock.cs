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

    /// <summary>
    /// En Edit Mode el clock está suspendido y los sliders funcionan libremente
    /// independientemente del modo configurado (Loop/Cycle/EndCycle/Custom).
    /// Se activa/desactiva desde el botón Edit Mode del RCPanel.
    /// </summary>
    public static bool EditMode { get; private set; } = false;

    public static void SetEditMode(bool value)
    {
        EditMode = value;
        if (value && IsRunning)
            Stop();
        Plugin.RSPlugin.log.LogInfo($"[BlendClock] EditMode = {value}");
    }

    private static float      _timer       = 0f;
    private static List<int>  _currentLane = null;
    private static List<int>  _otherLane   = null;

    // ── Cycle / EndCycle: referencia al ciclo de lluvia ─────────────────
    // Se pasa desde BlendClockUpdater en cada Tick para que el clock pueda
    // leer timer y cycleLength sin acoplarse a RainWorldGame directamente.
    private static float _rainTimer       = 0f;
    private static int   _rainCycleLength = 1;

    // ── API ───────────────────────────────────────────────────────────────

    // true = Deactivate fue llamado mientras el blend corría — al llegar a Done no reiniciar
    private static bool _customPendingStop = false;

    /// <summary>
    /// Llamado por CustomModeState.Deactivate cuando hay un blend en curso.
    /// El blend termina normalmente pero al llegar a Done el clock se detiene
    /// en lugar de continuar con el Loop normal.
    /// </summary>
    public static void SetCustomPendingStop()
    {
        _customPendingStop = true;
        Plugin.RSPlugin.log.LogInfo("[BlendClock] Custom pending stop set — clock will stop after current blend.");
    }

    public static void Start(int initialStateA = 1)
    {
        var settings = BlendSettingsLoader.Active;
        if (settings == null)
        {
            Plugin.RSPlugin.log.LogWarning("[BlendClock] Cannot start: no BlendSettings loaded.");
            return;
        }

        switch (settings.Mode)
        {
            case BlendMode.Loop:     StartLoop(settings, initialStateA);     break;
            case BlendMode.Cycle:    StartCycle(settings, initialStateA);    break;
            case BlendMode.EndCycle: StartEndCycle(settings, initialStateA); break;
            case BlendMode.Custom:   StartLoop(settings, initialStateA);     break;
        }
    }

    public static void Stop()
    {
        IsRunning          = false;
        CurrentT           = 0f;
        CurrentPhase       = Phase.Idle;
        SubPhaseIndex      = 0;
        SubPhaseLocalT     = 0f;
        _timer             = 0f;
        _currentLane       = null;
        _otherLane         = null;
        _customPendingStop = false;
        Plugin.RSPlugin.log.LogInfo("[BlendClock] Stopped.");
    }

    public static void ForceStates(int a, int b)
    {
        StateA = a;
        StateB = b;
    }

    /// <summary>
    /// Avanza el clock un frame. Para Cycle y EndCycle se debe pasar
    /// rainTimer y rainCycleLength leídos desde world.rainCycle.
    /// Para Loop no se usan (se ignoran).
    /// </summary>
    public static void Tick(float deltaTime, float rainTimer = 0f, int rainCycleLength = 1)
    {
        if (!IsRunning) return;
        var settings = BlendSettingsLoader.Active;
        if (settings == null) return;

        _rainTimer       = rainTimer;
        _rainCycleLength = Mathf.Max(1, rainCycleLength);
        _timer          += deltaTime;

        switch (settings.Mode)
        {
            case BlendMode.Loop:
            case BlendMode.Custom:
                switch (CurrentPhase)
                {
                    case Phase.Idle:     TickIdle(settings);     break;
                    case Phase.Blending: TickBlending(settings); break;
                    case Phase.Done:     DoRelay(settings);      break;
                }
                break;

            case BlendMode.Cycle:
                TickCycle(settings);
                break;

            case BlendMode.EndCycle:
                TickEndCycle(settings);
                break;
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // LOOP (y Custom — mismo mecanismo, Custom solo lo activa externamente)
    // ════════════════════════════════════════════════════════════════════

    private static void StartLoop(BlendSettings settings, int initialState)
    {
        int resolvedA = FindFirstValidState(settings, initialState);
        if (resolvedA < 0)
        {
            Plugin.RSPlugin.log.LogWarning("[BlendClock] Cannot start: no valid state files found.");
            return;
        }

        // Buscar carriles Loop: solo por clave exacta del estado inicial.
        // REGLA DE ORO: no buscar por pertenencia ni en otras entradas.
        var laneData = settings.GetLoopLane(resolvedA);
        if (laneData.HasValue && laneData.Value.IsValid)
        {
            var lane = laneData.Value;
            if (lane.LaneA[0] == resolvedA)
                SetupLane(lane.LaneA, lane.LaneB, true);
            else if (lane.LaneB[0] == resolvedA)
                SetupLane(lane.LaneB, lane.LaneA, false);
            else
            {
                // El estado inicial no es el primer elemento de ningún carril.
                // Fallo determinista: no intentar "arreglar" la configuración.
                Plugin.RSPlugin.log.LogWarning(
                    $"[BlendClock] StartLoop: resolvedA={resolvedA} is not at position 0 of either lane. " +
                    $"LaneA[0]={lane.LaneA[0]}, LaneB[0]={lane.LaneB[0]}. Starting from LaneA as declared.");
                SetupLane(lane.LaneA, lane.LaneB, true);
            }
        }
        else
        {
            // Sin carriles Loop: usar la secuencia base declarada con clave exacta.
            var seq = BuildSequenceFrom(settings, resolvedA);
            if (seq.Count < 2)
            {
                // Una sola entrada → sin transición declarada. El mod se queda quieto.
                Plugin.RSPlugin.log.LogInfo(
                    $"[BlendClock] StartLoop: state {resolvedA} has no transition declared (single-element seq). Staying idle.");
                // No arrancar el clock: permanece parado, reflejo fiel del archivo.
                return;
            }
            SetupLane(seq, null, true);
        }

        IsRunning = true;
        Plugin.RSPlugin.log.LogInfo(
            $"[BlendClock] Started Loop/Custom. lane={(IsLaneA ? "A" : "B")} [{string.Join(",", _currentLane)}]");
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
        int   subCount = _currentLane != null ? _currentLane.Count - 1 : 0;

        // Guard: lane de un solo elemento no tiene transición que animar.
        // No debería llegar aquí (StartLoop ya lo filtra), pero defensivo.
        if (subCount <= 0)
        {
            Plugin.RSPlugin.log.LogWarning("[BlendClock] TickBlending: lane has < 2 elements, stopping.");
            Stop();
            return;
        }

        float subSize  = 1f / subCount;

        CurrentT = Mathf.Clamp01(_timer / duration);

        int   newSub = Mathf.Min(Mathf.FloorToInt(CurrentT / subSize), subCount - 1);
        float localT = Mathf.Clamp01((CurrentT - newSub * subSize) / subSize);

        if (newSub != SubPhaseIndex)
        {
            SubPhaseIndex = newSub;
            UpdateStatesFromSubPhase();
        }
        SubPhaseLocalT = localT;

        if (_timer < duration) return;

        CurrentT       = 1f;
        SubPhaseIndex  = subCount - 1;
        SubPhaseLocalT = 1f;
        UpdateStatesFromSubPhase();
        _timer        = 0f;
        CurrentPhase  = Phase.Done;
        Plugin.RSPlugin.log.LogInfo($"[BlendClock] Lane {(IsLaneA ? "A" : "B")} complete → Done" +
            (_customPendingStop ? " (Custom stop pending)" : ""));
    }

    private static void DoRelay(BlendSettings settings)
    {
        // Si Custom fue desactivado mientras corría, detener aquí en lugar de relay.
        if (_customPendingStop)
        {
            Plugin.RSPlugin.log.LogInfo("[BlendClock] Custom stop — halting after blend completion.");
            Stop();
            return;
        }

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
        Plugin.RSPlugin.log.LogInfo($"[BlendClock] Entering Idle for lane {(IsLaneA ? "A" : "B")} StateA={StateA} StateB={StateB}");
    }

    // ════════════════════════════════════════════════════════════════════
    // CYCLE — hijo de Loop
    // Idéntico a Loop Slide A: usa la secuencia base como lane, hereda
    // sub-fases y TickBlending. Sin relay (_otherLane = null).
    // La única diferencia: arranca desde Phase.Idle esperando que
    // rainTimer >= trigger_pct * cycleLength antes de pasar a Blending.
    // Al llegar a Done se queda quieto (no hay relay).
    // ════════════════════════════════════════════════════════════════════

    private static void StartCycle(BlendSettings settings, int initialState)
    {
        int resolvedA = FindFirstValidState(settings, initialState);
        if (resolvedA < 0)
        {
            Plugin.RSPlugin.log.LogWarning("[BlendClock] StartCycle: no valid state found.");
            return;
        }

        var seq = BuildSequenceFrom(settings, resolvedA);
        if (seq.Count < 2)
        {
            Plugin.RSPlugin.log.LogInfo(
                $"[BlendClock] StartCycle: state {resolvedA} has no transition declared. Staying put.");
            return;
        }

        // Mismo SetupLane que Loop pero sin otherLane → no hay relay
        SetupLane(seq, null, true);
        IsRunning = true;
        Plugin.RSPlugin.log.LogInfo(
            $"[BlendClock] Started Cycle. lane=[{string.Join(",", _currentLane)}] " +
            $"trigger={settings.CycleTriggerPct:P0} dur={settings.CycleDuration}s");
    }

    private static void TickCycle(BlendSettings settings)
    {
        switch (CurrentPhase)
        {
            case Phase.Idle:
                // Esperar trigger_pct antes de arrancar — única diferencia con Loop
                float triggerTime = settings.CycleTriggerPct * _rainCycleLength;
                if (_rainTimer < triggerTime) return;

                _timer         = 0f;
                CurrentT       = 0f;
                SubPhaseIndex  = 0;
                SubPhaseLocalT = 0f;
                CurrentPhase   = Phase.Blending;
                UpdateStatesFromSubPhase();
                Plugin.RSPlugin.log.LogInfo(
                    $"[BlendClock] Cycle triggered at {_rainTimer:F1}/{_rainCycleLength} " +
                    $"({_rainTimer / _rainCycleLength:P0}). Blending [{string.Join("→", _currentLane)}]");
                break;

            case Phase.Blending:
                TickBlendingCycle(settings);
                break;

            // Done: quedarse quieto, no hay relay
        }
    }

    // TickBlending adaptado para Cycle/EndCycle: usa CycleDuration en lugar de LoopDuration.
    // Idéntico en lógica a TickBlending — sub-fases, SubPhaseLocalT, etc.
    private static void TickBlendingCycle(BlendSettings settings)
    {
        float duration = settings.CycleDuration > 0f ? settings.CycleDuration : 1f;
        int   subCount = _currentLane != null ? _currentLane.Count - 1 : 0;

        if (subCount <= 0)
        {
            Plugin.RSPlugin.log.LogWarning("[BlendClock] TickBlendingCycle: lane < 2 elements, stopping.");
            Stop();
            return;
        }

        float subSize = 1f / subCount;
        CurrentT = Mathf.Clamp01(_timer / duration);

        int   newSub = Mathf.Min(Mathf.FloorToInt(CurrentT / subSize), subCount - 1);
        float localT = Mathf.Clamp01((CurrentT - newSub * subSize) / subSize);

        if (newSub != SubPhaseIndex)
        {
            SubPhaseIndex = newSub;
            UpdateStatesFromSubPhase();
        }
        SubPhaseLocalT = localT;

        if (_timer < duration) return;

        CurrentT       = 1f;
        SubPhaseIndex  = subCount - 1;
        SubPhaseLocalT = 1f;
        UpdateStatesFromSubPhase();
        _timer       = 0f;
        CurrentPhase = Phase.Done;
        Plugin.RSPlugin.log.LogInfo($"[BlendClock] Cycle complete. Resting at state {StateB}.");
    }

    // ════════════════════════════════════════════════════════════════════
    // ENDCYCLE — clon exacto de Cycle
    // Mismo comportamiento: Loop Slide A una sola vez, sin relay.
    // Diferencia de activación: deathRainHasHit en lugar de trigger_pct.
    // El clock lo arranca OnDeathRainHit (BlendClockUpdater) — cuando llega
    // aquí ya está en Phase.Idle esperando EndCycleIdleTime antes de blend.
    // target_state 2: al terminar, puente a Loop via StartLoopSkipIdle.
    // ════════════════════════════════════════════════════════════════════

    private static void StartEndCycle(BlendSettings settings, int initialState)
    {
        int resolvedA = FindFirstValidState(settings, initialState);
        if (resolvedA < 0)
        {
            Plugin.RSPlugin.log.LogWarning("[BlendClock] StartEndCycle: no valid state found.");
            return;
        }

        var seq = BuildSequenceFrom(settings, resolvedA);
        if (seq.Count < 2)
        {
            Plugin.RSPlugin.log.LogInfo(
                $"[BlendClock] StartEndCycle: state {resolvedA} has no transition declared. Staying put.");
            return;
        }

        // Igual que Cycle: SetupLane sin otherLane, IsLaneA = true
        SetupLane(seq, null, true);
        IsRunning = true;
        Plugin.RSPlugin.log.LogInfo(
            $"[BlendClock] Started EndCycle. lane=[{string.Join(",", _currentLane)}] " +
            $"idle={settings.EndCycleIdleTime}s dur={settings.EndCycleDuration}s " +
            $"target={settings.EndCycleTargetState}");
    }

    private static void TickEndCycle(BlendSettings settings)
    {
        switch (CurrentPhase)
        {
            case Phase.Idle:
                // Esperar el idle declarado desde que deathRainHasHit disparó
                if (_timer < settings.EndCycleIdleTime) return;

                _timer         = 0f;
                CurrentT       = 0f;
                SubPhaseIndex  = 0;
                SubPhaseLocalT = 0f;
                CurrentPhase   = Phase.Blending;
                UpdateStatesFromSubPhase();
                Plugin.RSPlugin.log.LogInfo(
                    $"[BlendClock] EndCycle idle complete. Blending [{string.Join("→", _currentLane)}] " +
                    $"dur={settings.EndCycleDuration}s");
                break;

            case Phase.Blending:
                TickBlendingEndCycle(settings);
                break;

            // Done: target 1 = quedarse quieto, target 2 = puente a Loop (ya procesado en Tick)
        }
    }

    // TickBlending para EndCycle: usa EndCycleDuration.
    // Al completar, si target_state==2 arranca Loop desde StateB.
    private static void TickBlendingEndCycle(BlendSettings settings)
    {
        float duration = settings.EndCycleDuration > 0f ? settings.EndCycleDuration : 1f;
        int   subCount = _currentLane != null ? _currentLane.Count - 1 : 0;

        if (subCount <= 0)
        {
            Plugin.RSPlugin.log.LogWarning("[BlendClock] TickBlendingEndCycle: lane < 2 elements, stopping.");
            Stop();
            return;
        }

        float subSize = 1f / subCount;
        CurrentT = Mathf.Clamp01(_timer / duration);

        int   newSub = Mathf.Min(Mathf.FloorToInt(CurrentT / subSize), subCount - 1);
        float localT = Mathf.Clamp01((CurrentT - newSub * subSize) / subSize);

        if (newSub != SubPhaseIndex)
        {
            SubPhaseIndex = newSub;
            UpdateStatesFromSubPhase();
        }
        SubPhaseLocalT = localT;

        if (_timer < duration) return;

        CurrentT       = 1f;
        SubPhaseIndex  = subCount - 1;
        SubPhaseLocalT = 1f;
        UpdateStatesFromSubPhase();
        _timer       = 0f;
        CurrentPhase = Phase.Done;

        if (settings.EndCycleTargetState == 2)
        {
            Plugin.RSPlugin.log.LogInfo(
                $"[BlendClock] EndCycle complete → bridging to Loop from state {StateB}.");
            int bridgeState = StateB;
            Stop();
            StartLoopSkipIdle(settings, bridgeState);
        }
        else
        {
            Plugin.RSPlugin.log.LogInfo(
                $"[BlendClock] EndCycle complete. Resting at state {StateB}.");
        }
    }

    /// <summary>
    /// Arranca Loop desde <paramref name="fromState"/> saltándose el primer idle.
    /// Usado por EndCycle como puente: el blend empieza inmediatamente.
    /// </summary>
    private static void StartLoopSkipIdle(BlendSettings settings, int fromState)
    {
        int resolvedA = FindFirstValidState(settings, fromState);
        if (resolvedA < 0)
        {
            Plugin.RSPlugin.log.LogWarning("[BlendClock] StartLoopSkipIdle: no valid state found.");
            return;
        }

        // Solo clave exacta — REGLA DE ORO: no buscar por Contains ni por pertenencia.
        var laneData = settings.GetLoopLane(resolvedA);
        if (laneData.HasValue && laneData.Value.IsValid)
        {
            var lane = laneData.Value;
            if (lane.LaneA[0] == resolvedA)
                SetupLane(lane.LaneA, lane.LaneB, true);
            else if (lane.LaneB[0] == resolvedA)
                SetupLane(lane.LaneB, lane.LaneA, false);
            else
            {
                Plugin.RSPlugin.log.LogWarning(
                    $"[BlendClock] StartLoopSkipIdle: state {resolvedA} not at lane[0]. Using LaneA as declared.");
                SetupLane(lane.LaneA, lane.LaneB, true);
            }
        }
        else
        {
            var seq = BuildSequenceFrom(settings, resolvedA);
            if (seq.Count < 2)
            {
                // Una sola entrada → sin transición declarada. Fallo determinista.
                Plugin.RSPlugin.log.LogInfo(
                    $"[BlendClock] StartLoopSkipIdle: state {resolvedA} has no transition declared. Staying idle.");
                return;
            }
            SetupLane(seq, null, true);
        }

        // Saltar el idle: ir directo a Blending
        CurrentPhase = Phase.Blending;
        IsRunning    = true;
        Plugin.RSPlugin.log.LogInfo(
            $"[BlendClock] Loop started (skip-idle) from state {resolvedA}. " +
            $"lane=[{string.Join(",", _currentLane)}]");
    }

    // ════════════════════════════════════════════════════════════════════
    // HELPERS compartidos
    // ════════════════════════════════════════════════════════════════════

    private static void UpdateStatesFromSubPhase()
    {
        if (_currentLane == null || _currentLane.Count < 2) return;
        int idxA = Mathf.Clamp(SubPhaseIndex,     0, _currentLane.Count - 1);
        int idxB = Mathf.Clamp(SubPhaseIndex + 1, 0, _currentLane.Count - 1);
        StateA = _currentLane[idxA];
        StateB = _currentLane[idxB];
    }

    /// <summary>
    /// Devuelve el estado inicial a usar, verificando que exista su archivo en disco.
    /// REGLA DE ORO: solo busca dentro de la secuencia declarada con clave exacta.
    /// Si el estado inicial tiene archivo → lo devuelve directamente.
    /// Si no, recorre su secuencia declarada buscando el primero válido.
    /// Si tampoco → devuelve -1 (el clock no arranca). No inventa estados.
    /// </summary>
    private static int FindFirstValidState(BlendSettings settings, int startState)
    {
        if (StateExists(settings, startState)) return startState;

        // Buscar solo en la secuencia declarada con clave exacta para este estado.
        var seq = settings.GetSequenceFor(startState);
        if (seq != null)
        {
            foreach (int s in seq)
                if (StateExists(settings, s)) return s;
        }

        // Sin declaración exacta y el estado inicial no existe → fallo determinista.
        Plugin.RSPlugin.log.LogWarning(
            $"[BlendClock] FindFirstValidState: state {startState} not found and no declared sequence. Cannot start.");
        return -1;
    }

    /// <summary>
    /// Devuelve la secuencia declarada exactamente para stateA como clave.
    /// REGLA DE ORO: si no existe declaración exacta, devuelve una lista de un solo
    /// elemento [stateA] — el sistema no asume nada ni construye secuencias ficticias.
    /// El caso de 1 elemento es el indicador de "sin transición declarada".
    /// </summary>
    private static List<int> BuildSequenceFrom(BlendSettings settings, int stateA)
    {
        var defined = settings.GetSequenceFor(stateA);
        if (defined != null && defined.Count > 0) return defined;
        // No hay declaración exacta: lista de un elemento = sin transición
        return new List<int> { stateA };
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
}