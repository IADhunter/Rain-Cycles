using System.Collections.Generic;

namespace RainCycles.Settings;

// ESTRUCTURA DE DATOS — blend_settings.txt

public enum BlendMode
{
    Loop,
    Cycle,
    EndCycle,
    Custom,
}

public partial class BlendSettings
{
    // [CONFIG]
// Modo activo de la región. El usuario puede cambiarlo desde el panel.
    public BlendMode Mode = BlendMode.Loop;

    // Frames de cooldown entre cambios de cámara antes de arrancar un blend. Evita transiciones bruscas en Jolly Coop. -1 = no declarado (usa default).
    public int CameraCooldown = -1;

    // [CYCLE]
// Fracción del ciclo (0-1) en la que arranca el blend en modo Cycle.
    public float CycleTriggerPct = 0.90f;

// Duración del blend en segundos (modo Cycle).
    public float CycleDuration = 10f;

    // [ENDCYCLE]
    // Segundos de espera post-lluvia antes de arrancar el blend (EndCycle). El clock espera este tiempo desde que el ciclo termina antes de iniciar la mezcla.
    public float EndCycleIdleTime = 10f;

// Duración del blend en segundos (modo EndCycle).
    public float EndCycleDuration = 25f;

    // Comportamiento al terminar EndCycle: 1 = modo normal — hace el blend y se queda en el estado destino (fin de lluvia). 2 = puente a Loop — tras el blend, arranca Loop saltándose el primer idle (entra directo a Blending desde el estado destino del EndCycle).
    public int EndCycleTargetState = 1;

    // [CUSTOM]
// Identificador que otro mod envía para disparar el blend.
    public string CustomTriggerId = "";

// Duración del blend en segundos (modo Custom).
    public float CustomDuration = 15f;

    // [LOOP]
// Segundos de espera entre transiciones en modo Loop.
    public float LoopIdleTime = 45f;

// Duración del blend en segundos (modo Loop).
    public float LoopDuration = 10f;

    // [SEQUENCES]
    // Secuencia base por estado inicial (para Cycle/EndCycle/Custom). Ejemplo: { 1 → [1, 2, 3] }
    public Dictionary<int, List<int>> Sequences = new Dictionary<int, List<int>>();

    // Carriles Loop por estado inicial. Ejemplo: { 1 → LoopLane(A=[1,2,3], B=[3,4,1]) } Solo presente si la línea tiene paréntesis (A=...~B=...).
    public Dictionary<int, LoopLane> LoopLanes = new Dictionary<int, LoopLane>();

    // [ROOMS]
    // Salas que participan del sistema automático, con su tipo de cielo opcional. Formato en archivo: "UW_F01, acv" / "UW_H01, rtv" / "UW_B01" (sin cielo).
    public Dictionary<string, SkyType> Rooms =
        new Dictionary<string, SkyType>(System.StringComparer.OrdinalIgnoreCase);

    // [BACKGROUNDS]
    // Tabla alias → par de archivos (ACV, RTV) declarada en [BACKGROUNDS]. Formato: "bkg00: day.png, rtvday.png" AcvFile = primera imagen (AboveCloudsView) RtvFile = segunda imagen (RoofTopView) — null si no declarada
    public Dictionary<string, BkgEntry> BackgroundAliases =
        new Dictionary<string, BkgEntry>(System.StringComparer.OrdinalIgnoreCase);

    // Imagen asignada a cada estado inicial, via sufijo =bkgXX en [SEQUENCES]. Ejemplo: { 1 → "bkg00", 2 → "bkg01", 3 → "bkg00" } Ausente = sin imagen asignada para ese estado.
    public Dictionary<int, string> StateBkgAlias = new Dictionary<int, string>();

    // ── Flags de secciones presentes
    // Permiten saber si una sección fue declarada explícitamente en el archivo.
    public bool _hasCycleSection;
    public bool _hasEndCycleSection;
    public bool _hasCustomSection;
    public bool _hasLoopSection;
    public bool _hasSequencesSection;
    public bool _hasRoomsSection;
    public bool _hasBackgroundsSection;

    // ── Helpers de consulta

    // Devuelve la secuencia base para un estado inicial dado (Cycle/EndCycle/Custom).
    public List<int> GetSequenceFor(int initialState)
    {
        List<int> seq;
        return Sequences.TryGetValue(initialState, out seq) ? seq : null;
    }

    // Busca la secuencia que contiene a <paramref name="state"/> como elemento, independientemente de cuál sea la clave de la secuencia. Útil cuando solo hay una secuencia declarada (ej: 1: 1,2,3) y el estado actual no es el estado inicial de esa secuencia.
    public List<int> FindSequenceContaining(int state)
    {
        foreach (var seq in Sequences.Values)
            if (seq.Contains(state)) return seq;
        return null;
    }

    // Devuelve los carriles Loop para un estado inicial dado. Devuelve null si no hay carriles declarados (sin paréntesis).
    public LoopLane? GetLoopLane(int initialState)
    {
        LoopLane lane;
        return LoopLanes.TryGetValue(initialState, out lane) ? lane : (LoopLane?)null;
    }

    // Dado un estado inicial y el índice de posición dentro de la secuencia, devuelve el siguiente estado destino. Cicla al final.
    public int GetNextState(int initialState, int currentStep)
    {
        var seq = GetSequenceFor(initialState);
        if (seq == null || seq.Count == 0) return initialState;
        int next = (currentStep + 1) % seq.Count;
        return seq[next];
    }

// ¿Esta habitación participa del sistema automático?
    public bool IncludesRoom(string roomName)
    {
        return _hasRoomsSection && Rooms.ContainsKey(roomName);
    }

    // Tipo de cielo declarado para esta sala. Devuelve None si la sala no está registrada o no tiene sufijo.
    public SkyType GetSkyType(string roomName)
    {
        SkyType t;
        return Rooms.TryGetValue(roomName, out t) ? t : SkyType.None;
    }

    // Dado un estado y el tipo de cielo de la sala, resuelve el nombre de archivo. Devuelve null si el estado no tiene alias, el alias no está declarado, o el tipo de cielo no tiene archivo asignado.
    public string GetBkgFileForState(int state, SkyType sky)
    {
        string alias;
        if (!StateBkgAlias.TryGetValue(state, out alias)) return null;
        BkgEntry entry;
        if (!BackgroundAliases.TryGetValue(alias, out entry)) return null;
        return sky == SkyType.RTV ? entry.RtvFile : entry.AcvFile;
    }

    // Duración activa según el modo actual. Útil para que BlendClock pregunte sin switch externo.
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

    // Trigger pct activo según el modo actual. Solo relevante para Cycle y EndCycle — devuelve -1 en otros modos.
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

// SKY TYPE — tipo de escena de cielo de una sala
public enum SkyType { None, RTV, ACV }

// BKG ENTRY — par de imágenes para un alias de background
public struct BkgEntry
{
    public string AcvFile;  // imagen ACV (siempre presente si el alias existe)
    public string RtvFile;  // imagen RTV (null si no declarada)
}

// LOOP LANE — carriles A y B para modo Loop
public struct LoopLane
{
// Estados del carril A (ej: [1,2,3]). El último es el anclaje.
    public List<int> LaneA;

// Estados del carril B (ej: [3,4,1]). El primero debe ser el anclaje.
    public List<int> LaneB;

    // Estado de anclaje — último de A == primero de B. Si no coinciden, el sistema logea un warning pero sigue funcionando.
    public int AnchorState => (LaneA != null && LaneA.Count > 0)
        ? LaneA[LaneA.Count - 1]
        : 0;

    public bool IsValid => LaneA != null && LaneA.Count >= 2
                        && LaneB != null && LaneB.Count >= 2;
}