using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RainCycles.Core;

// ============================================================
// ÚNICA FUENTE DE VERDAD PARA ELEGIR EL ESTADO DEL CICLO
// ============================================================
// Reemplaza la lógica que antes estaba duplicada en ModResetter
// y BlendSettingsLoader. Elige el estado según el modo de Remix:
//   - ModeCycle          : (cycle % 4) + 1
//   - ModeProcedural\    : RandomByCycle (obedece al ciclo + seed)
//     + check activo     : RandomCoherent (anclado a (cycle, slugcat) en .txt)
//   - ModeRandom         : RNG real, ignora el archivo, cualquier estado
public static class CycleStateResolver
{
    public const int DefaultSeed = 1000003;

    private static readonly Dictionary<(int cycle, string slugcat), int> _anchors
        = new Dictionary<(int, string), int>();

    private static string _anchorPath = null;

    // ============================================================
    // RESOLUCIÓN PRINCIPAL
    // ============================================================
    public static int ResolveState(int cycle)
    {
        string mode = RSPlugin.cycleMode?.Value ?? RCOptions.ModeCycle;

        bool noCycle = RSPlugin.proceduralNoCycle != null
            && RSPlugin.proceduralNoCycle.Value;

        switch (mode)
        {
            case RCOptions.ModeProcedural:
                return noCycle
                    ? RandomCoherent(cycle, StateFileResolver.GetCurrentSlugcatName())
                    : RandomByCycle(cycle);
            case RCOptions.ModeRandom:
                return RandomFree();
            default:
                return Sequential(cycle);
        }
    }

    public static bool GetCustomSeed(out int seed)
    {
        string raw = RSPlugin.customSeed?.Value;
        if (!string.IsNullOrWhiteSpace(raw) &&
            int.TryParse(raw.Trim(), out seed))
            return true;
        seed = 0;
        return false;
    }

    // ============================================================
    // MODO CYCLE - SECUENCIA CONTINUA
    // ============================================================
    private static int Sequential(int cycle)
    {
        if (cycle == 0) return 1;
        return (cycle % 4) + 1;
    }

    // ============================================================
    // PROCEDURAL (obedece al ciclo) - ALEATORIO POR CICLO + SEED
    // ============================================================
    private static int RandomByCycle(int cycle)
    {
        int seed = GetCustomSeed(out int custom) ? custom : DefaultSeed;
        // Preserva la distribución actual: cycle * seed constante.
        return new System.Random(unchecked(cycle * seed)).Next(1, 5);
    }

    // ============================================================
    // PROCEDURAL (no obedece al ciclo) - ALEATORIO ANCLADO
    // ============================================================
    private static int RandomCoherent(int cycle, string slugcat)
    {
        LoadAnchors();

        if (_anchors.TryGetValue((cycle, slugcat), out int existing))
            return existing;

        int state = GetCustomSeed(out int custom)
            ? new System.Random(custom).Next(1, 5)
            : new System.Random(Environment.TickCount).Next(1, 5);

        _anchors[(cycle, slugcat)] = state;
        SaveAnchors();
        return state;
    }

    // ============================================================
    // RANDOM - ALEATORIO COMPLETO (ignora el archivo)
    // ============================================================
    private static int RandomFree()
    {
        // RNG real por arranque de partida: sin ancla, cualquier estado.
        int seed = GetCustomSeed(out int custom)
            ? custom
            : unchecked(Environment.TickCount * 7 + 13);
        return new System.Random(seed).Next(1, 5);
    }

    // ============================================================
    // ARCHIVO DE ANCLAJE (modo 3)
    // Formato: "cycle 4: state 2 yellow"
    // ============================================================
    private static string AnchorPath
    {
        get
        {
            if (_anchorPath == null)
            {
                string dir = Path.Combine(Application.persistentDataPath,
                    "ModConfigs", "RainCycles");
                try { Directory.CreateDirectory(dir); } catch { }
                _anchorPath = Path.Combine(dir, "cycle_anchors.txt");
            }
            return _anchorPath;
        }
    }

    private static void LoadAnchors()
    {
        _anchors.Clear();

        try
        {
            if (!File.Exists(AnchorPath)) return;

            foreach (string line in File.ReadAllLines(AnchorPath))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                string trimmed = line.Trim();

                // "cycle 4: state 2 yellow"
                int colon = trimmed.IndexOf(':');
                if (colon < 0) continue;

                string left  = trimmed.Substring(0, colon).Trim();
                string right = trimmed.Substring(colon + 1).Trim();

                if (!TryParseCyclePart(left, out int cycle)) continue;
                if (!TryParseStatePart(right, out int state, out string slugcat)) continue;

                _anchors[(cycle, slugcat ?? "")] = state;
            }
        }
        catch (Exception ex)
        {
            RSPlugin.log.LogWarning($"[CycleStateResolver] Error leyendo anclajes: {ex.Message}");
        }
    }

    private static void SaveAnchors()
    {
        try
        {
            var lines = new List<string>();
            foreach (var kv in _anchors)
                lines.Add($"cycle {kv.Key.cycle}: state {kv.Value} {kv.Key.slugcat}");
            File.WriteAllLines(AnchorPath, lines);
        }
        catch (Exception ex)
        {
            RSPlugin.log.LogWarning($"[CycleStateResolver] Error guardando anclajes: {ex.Message}");
        }
    }

    private static bool TryParseCyclePart(string text, out int cycle)
    {
        cycle = 0;
        if (!text.StartsWith("cycle")) return false;
        string num = text.Substring("cycle".Length).Trim();
        return int.TryParse(num, out cycle);
    }

    private static bool TryParseStatePart(string text, out int state, out string slugcat)
    {
        state = 0;
        slugcat = "";
        if (!text.StartsWith("state")) return false;
        string rest = text.Substring("state".Length).Trim();

        int space = rest.IndexOf(' ');
        string num = space < 0 ? rest : rest.Substring(0, space);
        if (!int.TryParse(num, out state)) return false;

        slugcat = space < 0 ? "" : rest.Substring(space + 1).Trim();
        return true;
    }

    // Permite a ModResetter cachear/limpiar estáticos entre partidas.
    public static void InvalidateCache()
    {
        _anchors.Clear();
        _anchorPath = null;
    }
}