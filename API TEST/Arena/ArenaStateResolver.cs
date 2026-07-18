using System.IO;
using UnityEngine;

namespace RainCycles.Core;

// Resuelve rutas de settings_N.txt y blend_settings.txt para salas de Arena.
//
// Búsqueda: raíz de raincycles/ primero, luego recursivo en todas las subcarpetas.
// No requiere que exista una carpeta con el nombre de la sala.
//
//   StreamingAssets/levels/raincycles/
//   ├── warehouse_settings_1.txt          ← válido (raíz)
//   ├── warehouse/
//   │   └── warehouse_settings_1.txt      ← válido (subcarpeta)
//   └── cualquier_subcarpeta/
//       └── warehouse_settings_1.txt      ← válido (recursivo)

public static class ArenaStateResolver
{
    // Raíz: StreamingAssets/levels/raincycles/
    private static string RootDir =>
        Path.Combine(Application.streamingAssetsPath, "levels", "raincycles");

    // ── API pública ───────────────────────────────────────────────────────

    // ¿Existe algún settings_N.txt para esta sala arena?
    public static bool HasSettings(string roomName) =>
        CountSettingsFiles(roomName) > 0;

    // ¿Existe blend_settings.txt para esta sala arena?
    public static bool HasBlendSettings(string roomName) =>
        GetBlendSettingsPath(roomName) != null;

    // Ruta de {roomName}_settings_{n}.txt. Null si no existe.
    // Busca en raíz de raincycles/ y recursivo en todas las subcarpetas.
    public static string GetSettingsPath(string roomName, int n)
    {
        string root     = RootDir;
        string fileName = $"{roomName.ToLowerInvariant()}_settings_{n}.txt";

        if (!Directory.Exists(root)) return null;

        // Raíz primero
        string direct = Path.Combine(root, fileName);
        if (File.Exists(direct)) return direct;

        // Recursivo en subcarpetas
        foreach (string found in Directory.GetFiles(root, fileName, SearchOption.AllDirectories))
            return found;

        return null;
    }

    // Cuenta cuántos settings_N.txt existen (consecutivos desde 1).
    public static int CountSettingsFiles(string roomName)
    {
        int count = 0;
        while (GetSettingsPath(roomName, count + 1) != null)
            count++;
        return count;
    }

    // Ruta de {roomName}_blend_settings.txt. Null si no existe.
    public static string GetBlendSettingsPath(string roomName)
    {
        string root     = RootDir;
        string fileName = $"{roomName.ToLowerInvariant()}_blend_settings.txt";

        if (!Directory.Exists(root)) return null;

        string direct = Path.Combine(root, fileName);
        if (File.Exists(direct)) return direct;

        foreach (string found in Directory.GetFiles(root, fileName, SearchOption.AllDirectories))
            return found;

        return null;
    }

    // Selecciona el número de state según sessionCount.
    // Modo secuencial: (sessionCount % n) + 1
    // Modo aleatorio:  seed dispersado con primo → mismo sessionCount → mismo state
    public static int SelectState(string roomName, int sessionCount)
    {
        int n = CountSettingsFiles(roomName);
        if (n == 0) return 1;

        if (RSPlugin.randomCycles != null && RSPlugin.randomCycles.Value)
        {
            // Seed combinado: sessionCount + ticks actuales → nunca repite el mismo state
            // consecutivamente mientras n > 1
            int seed = unchecked((int)(System.DateTime.Now.Ticks ^ (sessionCount * 1000003L)));
            return new System.Random(seed).Next(1, n + 1);
        }

        return (sessionCount % n) + 1;
    }

    // Ruta del settings_N.txt seleccionado para sessionCount. Null si no hay archivos.
    public static string GetSelectedSettingsPath(string roomName, int sessionCount)
    {
        int state = SelectState(roomName, sessionCount);
        return GetSettingsPath(roomName, state);
    }

    // ── Blend settings por level ──────────────────────────────────────────

    // ¿El level tiene blend_settings y está registrado en [ROOMS]?
    public static bool IsLevelRegistered(string roomName)
    {
        string path = GetBlendSettingsPath(roomName);
        if (path == null) return false;
        try
        {
            string raw = File.ReadAllText(path, System.Text.Encoding.UTF8);
            return IsRoomInText(raw, roomName);
        }
        catch { return false; }
    }

    // Toggle: añade o quita el level de su blend_settings. Crea el archivo si no existe.
    // Devuelve true si quedó registrado, false si fue eliminado.
    public static bool ToggleLevel(string roomName)
    {
        string path = EnsureBlendSettingsExists(roomName);
        if (path == null) return false;

        string raw;
        try { raw = File.ReadAllText(path, System.Text.Encoding.UTF8); }
        catch { return false; }

        bool wasRegistered = IsRoomInText(raw, roomName);
        string newContent  = wasRegistered
            ? RemoveRoomFromText(raw, roomName)
            : AddRoomToText(raw, roomName);

        try
        {
            File.WriteAllText(path, newContent, System.Text.Encoding.UTF8);
            RSPlugin.log.LogInfo(
                $"[ArenaStateResolver] Level {roomName} {(!wasRegistered ? "added to" : "removed from")} blend_settings.");
            return !wasRegistered;
        }
        catch { return wasRegistered; }
    }

    // Crea {roomName}_blend_settings.txt en la raíz de raincycles/ si no existe.
    public static string EnsureBlendSettingsExists(string roomName)
    {
        string existing = GetBlendSettingsPath(roomName);
        if (existing != null) return existing;

        string root = RootDir;
        if (!Directory.Exists(root))
        {
            try { Directory.CreateDirectory(root); }
            catch { return null; }
        }

        string path = Path.Combine(root, $"{roomName.ToLowerInvariant()}_blend_settings.txt");
        try
        {
            File.WriteAllText(path, BuildArenaBlendSettingsTemplate(roomName), System.Text.Encoding.UTF8);
            RSPlugin.log.LogInfo($"[ArenaStateResolver] Created blend_settings for level {roomName}: {path}");
            return path;
        }
        catch { return null; }
    }

    // Template de blend_settings para un level de arena — mismo formato completo que blend_settings regional.
    // [ROOMS] ya relleno con el level. Secuencias generadas según settings_N.txt existentes.
    private static string BuildArenaBlendSettingsTemplate(string roomName)
    {
        int n = CountSettingsFiles(roomName);

        // Generar secuencias igual que BlendSettingsWriter hace para regiones:
        // Para n estados: cada estado i tiene secuencia circular completa + lanes A y B.
        // Ejemplo n=4:
        //   1: 1, 2, 3, 4, (A = 1,2,3 ~ B = 3,4,1)
        //   2: 2, 3, 4, 1, (A = 2,3,4 ~ B = 4,1,2)
        var seqLines = new System.Collections.Generic.List<string>();
        if (n >= 2)
        {
            int half = (n + 1) / 2; // mitad redondeada arriba
            for (int start = 1; start <= n; start++)
            {
                // Secuencia circular completa desde este inicio
                var seq = new System.Collections.Generic.List<int>();
                for (int i = 0; i < n; i++)
                    seq.Add((start - 1 + i) % n + 1);

                // Lane A: primera mitad (incluyendo el anchor)
                var laneA = new System.Collections.Generic.List<int>();
                for (int i = 0; i <= half; i++)
                    laneA.Add(seq[i % n]);

                // Lane B: segunda mitad (desde el anchor)
                var laneB = new System.Collections.Generic.List<int>();
                for (int i = half; i <= n; i++)
                    laneB.Add(seq[i % n]);

                string seqStr  = string.Join(", ", seq);
                string laneAStr = string.Join(",", laneA);
                string laneBStr = string.Join(",", laneB);
                seqLines.Add($"{start}: {seqStr}, (A = {laneAStr} ~ B = {laneBStr})");
            }
        }
        else
        {
            seqLines.Add("# 1: 1, 2, (A = 1,2 ~ B = 2,1)");
        }

        return string.Join("\n", new[]
        {
            $"# Rain Cycles — {roomName}",
            "",
            "[CONFIG]",
            "mode: loop",
            "",
            "[LOOP]",
            "idle_time: 10.0",
            "duration: 10.0",
            "",
            "# [CYCLE]",
            "# trigger_pct: 0.90",
            "# duration: 10.0",
            "",
            "# [ENDCYCLE]",
            "# idle: 10.0",
            "# duration: 25.0",
            "# target_state: 1",
            "",
            "# [CUSTOM]",
            $"# API: CustomModeState.Activate(\"{roomName}\", \"MY_TRIGGER\")",
            $"#      CustomModeState.Deactivate(\"{roomName}\", \"MY_TRIGGER\")",
            "# trigger_id: MY_TRIGGER",
            "",
            "[BACKGROUNDS]",
            "#   Cada subsección declara imágenes para un tipo de vista (ACV/RTV/PSV).",
            "#   Si una vista no se declara, no se cargan sus imágenes.",
            "# ACV",
            "# bkg00: day.png",
            "# bkg01: dusk.png",
            "# bkg02: night.png",
            "# ",
            "# RTV",
            "# bkg00: rtvday.png",
            "# bkg01: rtvdusk.png",
            "# bkg02: rtvnight.png",
            "",
            "[SEQUENCES]",
            string.Join("\n", seqLines),
            "",
            "[ROOMS]",
            roomName,
            ""
        });
    }

    // ── Helpers privados de texto ─────────────────────────────────────────

    private static bool IsRoomInText(string text, string roomName)
    {
        bool inRooms = false;
        foreach (string rawLine in text.Split('\n'))
        {
            string line = rawLine.TrimEnd('\r').Trim();
            if (line.StartsWith("[") && line.EndsWith("]"))
            { inRooms = line.ToUpperInvariant() == "[ROOMS]"; continue; }
            if (!inRooms || line.StartsWith("#") || string.IsNullOrEmpty(line)) continue;
            if (string.Equals(line.Split(',')[0].Trim(), roomName, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static string AddRoomToText(string text, string roomName)
    {
        var lines = new System.Collections.Generic.List<string>(text.Split('\n'));
        int insertAfter = -1;
        int roomsStart  = -1;
        for (int i = 0; i < lines.Count; i++)
        {
            string l = lines[i].TrimEnd('\r').Trim();
            if (l.ToUpperInvariant() == "[ROOMS]") { roomsStart = i; insertAfter = i; continue; }
            if (roomsStart >= 0)
            {
                if (l.StartsWith("[") && l.EndsWith("]")) break;
                if (!string.IsNullOrEmpty(l) && !l.StartsWith("#")) insertAfter = i;
            }
        }
        if (roomsStart >= 0) lines.Insert(insertAfter + 1, roomName);
        else { lines.Add(""); lines.Add("[ROOMS]"); lines.Add(roomName); }
        return string.Join("\n", lines);
    }

    private static string RemoveRoomFromText(string text, string roomName)
    {
        bool inRooms = false;
        var result   = new System.Collections.Generic.List<string>();
        foreach (string rawLine in text.Split('\n'))
        {
            string line = rawLine.TrimEnd('\r').Trim();
            if (line.StartsWith("[") && line.EndsWith("]"))
                inRooms = line.ToUpperInvariant() == "[ROOMS]";
            if (inRooms && !line.StartsWith("#") && !string.IsNullOrEmpty(line) &&
                string.Equals(line.Split(',')[0].Trim(), roomName, System.StringComparison.OrdinalIgnoreCase))
                continue;
            result.Add(rawLine.TrimEnd('\r'));
        }
        return string.Join("\n", result);
    }
}