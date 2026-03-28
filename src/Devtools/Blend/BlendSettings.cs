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
    /// <summary>
    /// Segundos de espera post-lluvia antes de arrancar el blend (EndCycle).
    /// El clock espera este tiempo desde que el ciclo termina antes de iniciar la mezcla.
    /// </summary>
    public float EndCycleIdleTime = 10f;

    /// <summary>Duración del blend en segundos (modo EndCycle).</summary>
    public float EndCycleDuration = 25f;

    /// <summary>
    /// Comportamiento al terminar EndCycle:
    ///   1 = modo normal — hace el blend y se queda en el estado destino (fin de lluvia).
    ///   2 = puente a Loop — tras el blend, arranca Loop saltándose el primer idle
    ///       (entra directo a Blending desde el estado destino del EndCycle).
    /// </summary>
    public int EndCycleTargetState = 1;

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
    /// Salas que participan del sistema automático, con su tipo de cielo opcional.
    /// Formato en archivo: "UW_F01, acv" / "UW_H01, rtv" / "UW_B01" (sin cielo).
    /// </summary>
    public Dictionary<string, SkyType> Rooms =
        new Dictionary<string, SkyType>(System.StringComparer.OrdinalIgnoreCase);

    // ── [BACKGROUNDS] ────────────────────────────────────────────────────
    /// <summary>
    /// Tabla alias → par de archivos (ACV, RTV) declarada en [BACKGROUNDS].
    /// Formato: "bkg00: day.png, rtvday.png"
    ///   AcvFile = primera imagen (AboveCloudsView)
    ///   RtvFile = segunda imagen (RoofTopView) — null si no declarada
    /// </summary>
    public Dictionary<string, BkgEntry> BackgroundAliases =
        new Dictionary<string, BkgEntry>(System.StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Imagen asignada a cada estado inicial, via sufijo =bkgXX en [SEQUENCES].
    /// Ejemplo: { 1 → "bkg00", 2 → "bkg01", 3 → "bkg00" }
    /// Ausente = sin imagen asignada para ese estado.
    /// </summary>
    public Dictionary<int, string> StateBkgAlias = new Dictionary<int, string>();

    // ── Flags de secciones presentes ────────────────────────────────────
    // Permiten saber si una sección fue declarada explícitamente en el archivo.
    public bool _hasCycleSection;
    public bool _hasEndCycleSection;
    public bool _hasCustomSection;
    public bool _hasLoopSection;
    public bool _hasSequencesSection;
    public bool _hasRoomsSection;
    public bool _hasBackgroundsSection;

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
    /// Busca la secuencia que contiene a <paramref name="state"/> como elemento,
    /// independientemente de cuál sea la clave de la secuencia.
    /// Útil cuando solo hay una secuencia declarada (ej: 1: 1,2,3) y el estado
    /// actual no es el estado inicial de esa secuencia.
    /// </summary>
    public List<int> FindSequenceContaining(int state)
    {
        foreach (var seq in Sequences.Values)
            if (seq.Contains(state)) return seq;
        return null;
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
        return _hasRoomsSection && Rooms.ContainsKey(roomName);
    }

    /// <summary>
    /// Tipo de cielo declarado para esta sala.
    /// Devuelve None si la sala no está registrada o no tiene sufijo.
    /// </summary>
    public SkyType GetSkyType(string roomName)
    {
        SkyType t;
        return Rooms.TryGetValue(roomName, out t) ? t : SkyType.None;
    }

    /// <summary>
    /// Dado un estado y el tipo de cielo de la sala, resuelve el nombre de archivo.
    /// Devuelve null si el estado no tiene alias, el alias no está declarado,
    /// o el tipo de cielo no tiene archivo asignado.
    /// </summary>
    public string GetBkgFileForState(int state, SkyType sky)
    {
        string alias;
        if (!StateBkgAlias.TryGetValue(state, out alias)) return null;
        BkgEntry entry;
        if (!BackgroundAliases.TryGetValue(alias, out entry)) return null;
        return sky == SkyType.RTV ? entry.RtvFile : entry.AcvFile;
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
                case BlendMode.Cycle: return CycleTriggerPct;
                default:              return -1f;
            }
        }
    }
}

// ════════════════════════════════════════════════════════════════════════
// SKY TYPE — tipo de escena de cielo de una sala
// None = sin cielo gestionado por el mod
// RTV  = RoofTopView  (salas con efecto RoofTopView)
// ACV  = AboveCloudsView (salas con efecto AboveCloudsView)
// ════════════════════════════════════════════════════════════════════════
public enum SkyType { None, RTV, ACV }

// ════════════════════════════════════════════════════════════════════════
// BKG ENTRY — par de imágenes para un alias de background
// AcvFile = imagen para AboveCloudsView (primera en la declaración)
// RtvFile = imagen para RoofTopView (segunda, opcional — null si no declarada)
// Ejemplo en archivo: "bkg00: day.png, rtvday.png"
// ════════════════════════════════════════════════════════════════════════
public struct BkgEntry
{
    public string AcvFile;  // imagen ACV (siempre presente si el alias existe)
    public string RtvFile;  // imagen RTV (null si no declarada)
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