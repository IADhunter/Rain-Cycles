using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace FilesSetting;

// ════════════════════════════════════════════════════════════════════════
// PARSER de blend_settings.txt
//
// Formato del archivo:
//
//   [CONFIG]
//   mode: loop
//   camera_cooldown: 30
//
//   [CYCLE]
//   trigger_pct: 0.90
//   duration: 10.0
//
//   [ENDCYCLE]
//   idle: 10.0
//   duration: 25.0
//   target_state: 1
//
//   [CUSTOM]
//   trigger_id: HEAVY_STORM
//   duration: 15.0
//
//   [LOOP]
//   idle_time: 45.0
//   duration: 10.0
//
//   [BACKGROUNDS]
//   bkg00: day.png
//   bkg01: dusk.png
//
//   [SEQUENCES]
//   1: 1, 2, 3, (A = 1,2,3 ~ B = 3,4,1) =bkg00
//   2: 2, 3, 4, (A = 2,3,4 ~ B = 4,1,2) =bkg01
//
//   [ROOMS]
//   SU_C01
//   SU_A02
//
// Reglas de parsing:
//   - Líneas vacías y las que empiezan con '#' se ignoran (comentarios).
//   - Las claves son case-insensitive.
//   - Los valores float usan InvariantCulture (punto decimal).
//   - Secciones desconocidas se saltan silenciosamente.
//   - Campos no declarados conservan los valores default de BlendSettings.
// ════════════════════════════════════════════════════════════════════════

public partial class BlendSettings
{
    private static readonly NumberStyles NF  = NumberStyles.Float;
    private static readonly CultureInfo  INV = CultureInfo.InvariantCulture;

    public static BlendSettings FromFile(string path)
    {
        var s = new BlendSettings();

        if (!File.Exists(path))
        {
            Plugin.RSPlugin.log.LogWarning($"[BlendSettings] File not found: {path}");
            return s;
        }

        string raw;
        try { raw = File.ReadAllText(path, Encoding.UTF8); }
        catch (Exception ex)
        {
            Plugin.RSPlugin.log.LogError($"[BlendSettings] Error reading {path}: {ex.Message}");
            return s;
        }

        string currentSection = "";

        foreach (string rawLine in raw.Split('\n'))
        {
            string line = rawLine.TrimEnd('\r').Trim();

            // Ignorar líneas vacías y comentarios
            if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;

            // Detectar cabecera de sección: [NOMBRE]
            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                currentSection = line.Substring(1, line.Length - 2).ToUpperInvariant();
                MarkSection(s, currentSection);
                continue;
            }

            // Sección [ROOMS]: cada línea es un nombre de sala con sufijo opcional
            // Formato: "UW_F01, acv" | "UW_H01, rtv" | "UW_B01"
            if (currentSection == "ROOMS")
            {
                if (!string.IsNullOrEmpty(line))
                {
                    string[] parts = line.Split(',');
                    string roomName = parts[0].Trim();
                    SkyType sky = SkyType.None;
                    if (parts.Length > 1)
                    {
                        string tag = parts[1].Trim().ToLowerInvariant();
                        if      (tag == "acv") sky = SkyType.ACV;
                        else if (tag == "rtv") sky = SkyType.RTV;
                    }
                    if (!string.IsNullOrEmpty(roomName))
                        s.Rooms[roomName] = sky;
                }
                continue;
            }

            // Separar clave: valor (primer ':' es el separador)
            int sep = line.IndexOf(':');
            if (sep < 0) continue;
            string key = line.Substring(0, sep).Trim().ToLowerInvariant();
            string val = line.Substring(sep + 1).Trim();

            ParseLine(s, currentSection, key, val);
        }

        ValidateSequences(s);

        Plugin.RSPlugin.log.LogInfo(
            $"[BlendSettings] Loaded from {path} | mode={s.Mode} rooms={s.Rooms.Count} " +
            $"sequences={s.Sequences.Count} backgrounds={s.BackgroundAliases.Count}");

        return s;
    }

    // ── Marca qué secciones están presentes ────────────────────────────

    private static void MarkSection(BlendSettings s, string section)
    {
        switch (section)
        {
            case "CYCLE":       s._hasCycleSection       = true; break;
            case "ENDCYCLE":    s._hasEndCycleSection    = true; break;
            case "CUSTOM":      s._hasCustomSection      = true; break;
            case "LOOP":        s._hasLoopSection        = true; break;
            case "SEQUENCES":   s._hasSequencesSection   = true; break;
            case "ROOMS":       s._hasRoomsSection       = true; break;
            case "BACKGROUNDS": s._hasBackgroundsSection = true; break;
        }
    }

    // ── Despacha el parsing por sección y clave ─────────────────────────

    private static void ParseLine(BlendSettings s, string section, string key, string val)
    {
        switch (section)
        {
            case "CONFIG":      ParseConfig(s, key, val);     break;
            case "CYCLE":       ParseCycle(s, key, val);      break;
            case "ENDCYCLE":    ParseEndCycle(s, key, val);   break;
            case "CUSTOM":      ParseCustom(s, key, val);     break;
            case "LOOP":        ParseLoop(s, key, val);       break;
            case "BACKGROUNDS": ParseBackground(s, key, val); break;
            case "SEQUENCES":   ParseSequence(s, key, val);   break;
        }
    }

    // ── Sección [CONFIG] ────────────────────────────────────────────────

    private static void ParseConfig(BlendSettings s, string key, string val)
    {
        switch (key)
        {
            case "mode":
                s.Mode = ParseMode(val);
                break;

            case "camera_cooldown":
                int cd;
                if (int.TryParse(val, out cd)) s.CameraCooldown = cd;
                break;
        }
    }

    // ── Sección [CYCLE] ─────────────────────────────────────────────────

    private static void ParseCycle(BlendSettings s, string key, string val)
    {
        float f;
        switch (key)
        {
            case "trigger_pct":
                if (TryParseFloat(val, out f)) s.CycleTriggerPct = Clamp01(f);
                break;
            case "duration":
                if (TryParseFloat(val, out f) && f > 0f) s.CycleDuration = f;
                break;
        }
    }

    // ── Sección [ENDCYCLE] ──────────────────────────────────────────────

    private static void ParseEndCycle(BlendSettings s, string key, string val)
    {
        float f; int i;
        switch (key)
        {
            case "idle":
                if (TryParseFloat(val, out f) && f >= 0f) s.EndCycleIdleTime = f;
                break;
            case "duration":
                if (TryParseFloat(val, out f) && f > 0f) s.EndCycleDuration = f;
                break;
            case "target_state":
                // 1 = fin de lluvia normal, 2 = puente a Loop
                if (int.TryParse(val, out i) && i >= 1 && i <= 2) s.EndCycleTargetState = i;
                break;
        }
    }

    // ── Sección [CUSTOM] ────────────────────────────────────────────────

    private static void ParseCustom(BlendSettings s, string key, string val)
    {
        float f;
        switch (key)
        {
            case "trigger_id":
                if (!string.IsNullOrEmpty(val)) s.CustomTriggerId = val;
                break;
            case "duration":
                if (TryParseFloat(val, out f) && f > 0f) s.CustomDuration = f;
                break;
        }
    }

    // ── Sección [LOOP] ──────────────────────────────────────────────────

    private static void ParseLoop(BlendSettings s, string key, string val)
    {
        float f;
        switch (key)
        {
            case "idle_time":
                if (TryParseFloat(val, out f) && f >= 0f) s.LoopIdleTime = f;
                break;
            case "duration":
                if (TryParseFloat(val, out f) && f > 0f) s.LoopDuration = f;
                break;
        }
    }

    // ── Sección [BACKGROUNDS] ───────────────────────────────────────────

    private static void ParseBackground(BlendSettings s, string key, string val)
    {
        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(val)) return;
        if (!key.StartsWith("bkg"))
        {
            Plugin.RSPlugin.log.LogWarning(
                $"[BlendSettings] [BACKGROUNDS] clave inesperada '{key}' — se esperaba bkgNN.");
            return;
        }

        // Formato: "day.png" o "day.png, rtvday.png"
        string[] parts = val.Split(',');
        var entry = new BkgEntry
        {
            AcvFile = parts[0].Trim(),
            RtvFile = parts.Length > 1 ? parts[1].Trim() : null
        };

        if (string.IsNullOrEmpty(entry.AcvFile)) return;
        s.BackgroundAliases[key] = entry;
    }

    // ── Sección [SEQUENCES] ─────────────────────────────────────────────
    // Formato completo:
    //   1: 1, 2, 3, (A = 1,2,3 ~ B = 3,4,1) =bkg00
    //
    // La parte antes del '(' son los estados base (Cycle/EndCycle/Custom).
    // El bloque '(A=...~B=...)' son los carriles Loop (opcional).
    // El sufijo '=bkgXX' asigna una imagen de fondo a ese estado (opcional).

    private static void ParseSequence(BlendSettings s, string key, string val)
    {
        int initialState;
        if (!int.TryParse(key, out initialState) || initialState < 1)
        {
            Plugin.RSPlugin.log.LogWarning($"[BlendSettings] Invalid sequence key: '{key}'");
            return;
        }

        // ── 1. Consumir tag <> (descartado, ya no tiene función) ────────
        int tagOpen = val.LastIndexOf('<');
        if (tagOpen >= 0)
        {
            int tagClose = val.IndexOf('>', tagOpen);
            if (tagClose > tagOpen)
                val = val.Remove(tagOpen, tagClose - tagOpen + 1);
        }

        // ── 2. Consumir sufijo =bkgXX ───────────────────────────────────
        // Puede ir pegado al ')' o con espacios: ") =bkg00" o ")=bkg00"
        int eqIdx = val.LastIndexOf('=');
        if (eqIdx >= 0)
        {
            string candidate = val.Substring(eqIdx + 1).Trim().ToLowerInvariant();
            if (candidate.StartsWith("bkg"))
            {
                s.StateBkgAlias[initialState] = candidate;
                val = val.Substring(0, eqIdx);
            }
        }

        // ── 3. Separar parte base del bloque de carriles ─────────────────
        string basePart = val;
        string lanePart = null;

        int parenOpen = val.IndexOf('(');
        if (parenOpen >= 0)
        {
            int parenClose = val.IndexOf(')', parenOpen);
            basePart = val.Substring(0, parenOpen);
            if (parenClose > parenOpen)
                lanePart = val.Substring(parenOpen + 1, parenClose - parenOpen - 1);
        }

        // ── 4. Parsear secuencia base (estados para Cycle/EndCycle/Custom) ──
        var steps = new List<int>();
        foreach (string part in basePart.Split(','))
        {
            int step;
            if (int.TryParse(part.Trim(), out step) && step >= 1)
                steps.Add(step);
        }

        if (steps.Count == 0 && lanePart == null)
        {
            Plugin.RSPlugin.log.LogWarning($"[BlendSettings] Sequence {initialState} has no valid steps.");
            return;
        }

        if (steps.Count > 0)
        {
            // REGLA DE ORO: el parser es un reflejo fiel del archivo.
            // NO se inserta automáticamente el estado inicial si el autor no lo declaró.
            // Si el autor escribe "1: 3," el mod guarda [3] — sin transición (single-element).
            // Si escribe "1: 1, 3," el mod guarda [1, 3] — blend de 1 a 3.
            // Si escribe "1: 1," el mod guarda [1] — sin transición (mismo destino declarado).
            // El sistema interpreta la semántica en tiempo de ejecución, no aquí.
            s.Sequences[initialState] = steps;
        }

        // ── 5. Parsear carriles Loop (A=...~B=...) ───────────────────────
        if (lanePart != null)
        {
            var lane = ParseLoopLane(initialState, lanePart);
            if (lane.IsValid)
                s.LoopLanes[initialState] = lane;
        }
    }

    /// <summary>
    /// Parsea el contenido del bloque de carriles: "A = 1,2,3 ~ B = 3,4,1"
    /// </summary>
    private static LoopLane ParseLoopLane(int initialState, string lanePart)
    {
        var lane = new LoopLane();

        // Dividir por '~' para obtener los dos carriles
        string[] sides = lanePart.Split('~');
        if (sides.Length < 2)
        {
            Plugin.RSPlugin.log.LogWarning(
                $"[BlendSettings] Sequence {initialState}: lane block missing '~' separator.");
            return lane;
        }

        lane.LaneA = ParseLaneSide(sides[0]);
        lane.LaneB = ParseLaneSide(sides[1]);

        // Validar anclaje: último de A debe ser primero de B
        if (lane.IsValid)
        {
            int anchorA = lane.LaneA[lane.LaneA.Count - 1];
            int anchorB = lane.LaneB[0];
            if (anchorA != anchorB)
                Plugin.RSPlugin.log.LogWarning(
                    $"[BlendSettings] Sequence {initialState}: anchor mismatch — " +
                    $"last of A={anchorA}, first of B={anchorB}. System will use last of A as anchor.");
        }
        else
        {
            Plugin.RSPlugin.log.LogWarning(
                $"[BlendSettings] Sequence {initialState}: invalid lane (needs 2+ states each side).");
        }

        return lane;
    }

    /// <summary>
    /// Parsea un lado del carril: "A = 1,2,3" → [1,2,3]
    /// </summary>
    private static List<int> ParseLaneSide(string side)
    {
        // Quitar el prefijo "A =" o "B ="
        int eq = side.IndexOf('=');
        string nums = eq >= 0 ? side.Substring(eq + 1) : side;

        var result = new List<int>();
        foreach (string part in nums.Split(','))
        {
            int v;
            if (int.TryParse(part.Trim(), out v) && v >= 1)
                result.Add(v);
        }
        return result;
    }

    // ── Post-validación de sequences (solo warnings, nunca lanza) ────────

    private static void ValidateSequences(BlendSettings s)
    {
        if (!s._hasSequencesSection) return;

        foreach (var kv in s.Sequences)
        {
            int initial = kv.Key;
            var seq     = kv.Value;

            foreach (int step in seq)
            {
                if (step < 1)
                    Plugin.RSPlugin.log.LogWarning(
                        $"[BlendSettings] Sequence {initial} contains invalid state {step} (must be >= 1). Will be skipped at runtime.");
            }

            if (seq.Count <= 1)
                Plugin.RSPlugin.log.LogInfo(
                    $"[BlendSettings] Sequence {initial} has only one step — no transition declared (by design). Clock will stay idle for this state.");
        }

        // Validar que los LoopLanes tengan su estado inicial en posición 0 de LaneA
        foreach (var kv in s.LoopLanes)
        {
            int initial = kv.Key;
            var lane    = kv.Value;
            if (lane.LaneA.Count > 0 && lane.LaneA[0] != initial)
                Plugin.RSPlugin.log.LogWarning(
                    $"[BlendSettings] LoopLane {initial}: LaneA[0]={lane.LaneA[0]} != initialState={initial}. " +
                    $"The clock will start from LaneA[0].");
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private static BlendMode ParseMode(string val)
    {
        switch (val.ToLowerInvariant())
        {
            case "cycle":    return BlendMode.Cycle;
            case "endcycle": return BlendMode.EndCycle;
            case "custom":   return BlendMode.Custom;
            default:         return BlendMode.Loop;   // "loop" o cualquier desconocido
        }
    }

    private static bool TryParseFloat(string val, out float result)
        => float.TryParse(val, NF, INV, out result);

    private static float Clamp01(float v)
        => v < 0f ? 0f : v > 1f ? 1f : v;
}