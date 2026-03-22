using System.Collections.Generic;

namespace FilesSetting;

// ════════════════════════════════════════════════════════════════════════
// ESTRUCTURA DE DATOS — blend_settings.txt
//
// Un BlendSettings vive a nivel de región. Contiene la configuración del
// modo de blend activo y las secuencias de transición entre estados.
//
// Dividido en archivos parciales:
//   BlendSettings.cs        → estructura (este archivo)
//   BlendSettingsParser.cs  → FromFile, parsing por secciones
//   BlendSettingsLoader.cs  → carga automática por región + hook
// ════════════════════════════════════════════════════════════════════════

public enum BlendMode
{
    Loop,
    Cycle,
    EndCycle,
    Custom,
}

public partial class BlendSettings
{
    // ── [CONFIG] ──────────────────────────────────────────────────────────
    /// <summary>Modo activo de la región. El usuario puede cambiarlo desde el panel.</summary>
    public BlendMode Mode = BlendMode.Loop;

    /// <summary>
    /// Frames de cooldown entre cambios de cámara antes de arrancar un blend.
    /// Evita transiciones bruscas en Jolly Coop. -1 = no declarado (usa default).
    /// </summary>
    public int CameraCooldown = -1;

    // ── [CYCLE] ───────────────────────────────────────────────────────────
    /// <summary>Fracción del ciclo (0-1) en la que arranca el blend en modo Cycle.</summary>
    public float CycleTriggerPct = 0.90f;

    /// <summary>Duración del blend en segundos (modo Cycle).</summary>
    public float CycleDuration = 10f;

    // ── [ENDCYCLE] ────────────────────────────────────────────────────────
    /// <summary>Fracción del post-lluvia (0-1) en la que arranca el blend en modo EndCycle.</summary>
    public float EndCycleTriggerPct = 0.95f;

    /// <summary>Duración del blend en segundos (modo EndCycle).</summary>
    public float EndCycleDuration = 25f;

    /// <summary>
    /// Estado destino al terminar el EndCycle (1-based).
    /// -1 = no declarado (usa siguiente en secuencia).
    /// </summary>
    public int EndCycleTargetState = -1;

    // ── [CUSTOM] ──────────────────────────────────────────────────────────
    /// <summary>Identificador que otro mod envía para disparar el blend.</summary>
    public string CustomTriggerId = "";

    /// <summary>Duración del blend en segundos (modo Custom).</summary>
    public float CustomDuration = 15f;

    // ── [LOOP] ────────────────────────────────────────────────────────────
    /// <summary>Segundos de espera entre transiciones en modo Loop.</summary>
    public float LoopIdleTime = 45f;

    /// <summary>Duración del blend en segundos (modo Loop).</summary>
    public float LoopDuration = 10f;

    // ── [SEQUENCES] ───────────────────────────────────────────────────────
    /// <summary>
    /// Secuencia base por estado inicial (para Cycle/EndCycle/Custom).
    /// Ejemplo: { 1 → [1, 2, 3] }
    /// </summary>
    public Dictionary<int, List<int>> Sequences = new Dictionary<int, List<int>>();

    /// <summary>
    /// Carriles Loop por estado inicial.
    /// Ejemplo: { 1 → LoopLane(A=[1,2,3], B=[3,4,1]) }
    /// Solo presente si la línea tiene paréntesis (A=...~B=...).
    /// </summary>
    public Dictionary<int, LoopLane> LoopLanes = new Dictionary<int, LoopLane>();

    // ── [ROOMS] ───────────────────────────────────────────────────────────
    /// <summary>
    /// Conjunto de nombres de habitación que participan del sistema automático.
    /// Las habitaciones que no están en esta lista solo usan el slider manual.
    /// </summary>
    public HashSet<string> Rooms = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

    // ── Flags de secciones presentes ────────────────────────────────────
    // Permiten saber si una sección fue declarada explícitamente en el archivo.
    public bool _hasCycleSection;
    public bool _hasEndCycleSection;
    public bool _hasCustomSection;
    public bool _hasLoopSection;
    public bool _hasSequencesSection;
    public bool _hasRoomsSection;

    /// <summary>
    /// Conjunto de estados (números de settings) que tienen el tag &lt;def&gt;
    /// en su línea de sequence. Cuando el estado activo o el destino del blend
    /// tiene este flag, el sistema de tinte de fondo se desactiva para ese lado
    /// y usa Color.white como si fuera el estado "sin tinte" (comportamiento vanilla).
    /// </summary>
    public HashSet<int> DefaultBackgroundStates = new HashSet<int>();

    // ── Helpers de consulta ──────────────────────────────────────────────

    /// <summary>
    /// Devuelve la secuencia base para un estado inicial dado (Cycle/EndCycle/Custom).
    /// </summary>
    public List<int> GetSequenceFor(int initialState)
    {
        List<int> seq;
        return Sequences.TryGetValue(initialState, out seq) ? seq : null;
    }

    /// <summary>
    /// Devuelve los carriles Loop para un estado inicial dado.
    /// Devuelve null si no hay carriles declarados (sin paréntesis).
    /// </summary>
    public LoopLane? GetLoopLane(int initialState)
    {
        LoopLane lane;
        return LoopLanes.TryGetValue(initialState, out lane) ? lane : (LoopLane?)null;
    }

    /// <summary>
    /// Dado un estado inicial y el índice de posición dentro de la secuencia,
    /// devuelve el siguiente estado destino. Cicla al final.
    /// </summary>
    public int GetNextState(int initialState, int currentStep)
    {
        var seq = GetSequenceFor(initialState);
        if (seq == null || seq.Count == 0) return initialState;
        int next = (currentStep + 1) % seq.Count;
        return seq[next];
    }

    /// <summary>¿Esta habitación participa del sistema automático?</summary>
    public bool IncludesRoom(string roomName)
    {
        return _hasRoomsSection && Rooms.Contains(roomName);
    }

    /// <summary>
    /// ¿Este número de settings tiene el tag &lt;def&gt;?
    /// Si true, el tinte de fondo se desactiva para ese estado (usa Color.white).
    /// </summary>
    public bool IsDefaultBackground(int state)
    {
        return DefaultBackgroundStates.Contains(state);
    }

    /// <summary>
    /// Duración activa según el modo actual.
    /// Útil para que BlendClock pregunte sin switch externo.
    /// </summary>
    public float ActiveDuration
    {
        get
        {
            switch (Mode)
            {
                case BlendMode.Cycle:    return CycleDuration;
                case BlendMode.EndCycle: return EndCycleDuration;
                case BlendMode.Custom:   return CustomDuration;
                default:                 return LoopDuration;   // Loop
            }
        }
    }

    /// <summary>
    /// Trigger pct activo según el modo actual.
    /// Solo relevante para Cycle y EndCycle — devuelve -1 en otros modos.
    /// </summary>
    public float ActiveTriggerPct
    {
        get
        {
            switch (Mode)
            {
                case BlendMode.Cycle:    return CycleTriggerPct;
                case BlendMode.EndCycle: return EndCycleTriggerPct;
                default:                 return -1f;
            }
        }
    }
}

// ════════════════════════════════════════════════════════════════════════
// LOOP LANE — carriles A y B para modo Loop
// Extraídos del paréntesis (A = ... ~ B = ...) en [SEQUENCES].
// ════════════════════════════════════════════════════════════════════════
public struct LoopLane
{
    /// <summary>Estados del carril A (ej: [1,2,3]). El último es el anclaje.</summary>
    public List<int> LaneA;

    /// <summary>Estados del carril B (ej: [3,4,1]). El primero debe ser el anclaje.</summary>
    public List<int> LaneB;

    /// <summary>
    /// Estado de anclaje — último de A == primero de B.
    /// Si no coinciden, el sistema logea un warning pero sigue funcionando.
    /// </summary>
    public int AnchorState => (LaneA != null && LaneA.Count > 0)
        ? LaneA[LaneA.Count - 1]
        : 0;

    public bool IsValid => LaneA != null && LaneA.Count >= 2
                        && LaneB != null && LaneB.Count >= 2;
}