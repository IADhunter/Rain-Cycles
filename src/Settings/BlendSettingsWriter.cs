using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace RainCycles.Settings;

// ════════════════════════════════════════════════════════════════════════

public static partial class BlendSettingsWriter
{
    // ────────────────────

    // Crea blend_settings.txt para la región de roomName si no existe. Devuelve la ruta del archivo (existente o recién creado), o null si falla.
    public static string EnsureFileExists(string roomName)
    {
        string regionCode = ExtractRegionCode(roomName);
        if (regionCode == null) return null;

        string path = ResolveWritablePath(regionCode);
        if (path == null) return null;

        if (File.Exists(path)) return path;

        // Crear directorio si hace falta
        string dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            try { Directory.CreateDirectory(dir); }
            catch (Exception ex)
            {
                RSPlugin.log.LogError(
                    $"[BlendSettingsWriter] Cannot create directory {dir}: {ex.Message}");
                return null;
            }
        }

        // Escribir plantilla default
        try
        {
            File.WriteAllText(path, BuildDefaultTemplate(regionCode), Encoding.UTF8);
            RSPlugin.log.LogInfo(
                $"[BlendSettingsWriter] Created blend_settings.txt for region {regionCode}: {path}");

            // Notificar al Loader para que cargue el nuevo archivo
            BlendSettingsLoader.InvalidateCache(regionCode);
        }
        catch (Exception ex)
        {
            RSPlugin.log.LogError(
                $"[BlendSettingsWriter] Cannot write {path}: {ex.Message}");
            return null;
        }

        // Generar entradas vacías en [SEQUENCES] según los settings disponibles
        UpdateSequences(roomName);

        return path;
    }

    // Detecta cuántos settings_N.txt hay para las salas de [ROOMS] y añade solo las entradas faltantes en [SEQUENCES]. Nunca modifica las existentes. Límite: 3 entradas para modos normales, 6 para Loop (con carriles A/B). Llamado automáticamente desde EnsureFileExists al abrir DevTools.
    public static void UpdateSequences(string roomName)
    {
        string regionCode = ExtractRegionCode(roomName);
        if (regionCode == null) return;

        string path = ResolveWritablePath(regionCode);
        if (path == null || !File.Exists(path)) return;

        // Contar cuántos settings_N.txt existen para esta sala
        int n = StateFileResolver.CountRainStateFiles(roomName);
        if (n == 0) return;

        // Leer el archivo actual
        string raw;
        try { raw = File.ReadAllText(path, System.Text.Encoding.UTF8); }
        catch { return; }

        // Detectar el modo activo leyendo el archivo raw (más fiable que el caché)
        bool isLoop = DetectLoopModeFromRaw(raw);
        int  limit  = isLoop ? 6 : 3;
        int  count   = System.Math.Min(n, limit);

        // Calcular qué entradas ya existen en [SEQUENCES]
        var existing = GetExistingSequenceKeys(raw);

        // Si todas las entradas hasta 'count' ya existen, nada que hacer
        bool anyMissing = false;
        for (int i = 1; i <= count; i++)
            if (!existing.Contains(i)) { anyMissing = true; break; }
        if (!anyMissing) return;

        // Construir las líneas nuevas a insertar según el modo activo
        var newLines = new System.Text.StringBuilder();
        for (int i = 1; i <= count; i++)
        {
            if (existing.Contains(i)) continue;
            if (isLoop)
            {
                // Generar carriles A y B con rotación circular entre los N states.
                int s1 = i;
                int s2 = (i % n) + 1;
                int s3 = ((i + 1) % n) + 1;  // anclaje
                int s4 = ((i + 2) % n) + 1;
                newLines.AppendLine($"{i}: {s1}, {s2}, {s3}, (A = {s1},{s2},{s3} ~ B = {s3},{s4},{s1})");
            }
            else
            {
                // Modos Cycle/EndCycle/Custom: secuencia lineal de 3 states rotando
                int s1 = i;
                int s2 = (i % n) + 1;
                int s3 = ((i + 1) % n) + 1;
                newLines.AppendLine($"{i}: {s1}, {s2}, {s3}");
            }
        }

        string newContent = InsertIntoSequencesSection(raw, newLines.ToString());
        if (newContent == raw) return;

        try
        {
            File.WriteAllText(path, newContent, System.Text.Encoding.UTF8);
            BlendSettingsLoader.InvalidateCache(regionCode);
            RSPlugin.log.LogInfo(
                $"[BlendSettingsWriter] Updated [SEQUENCES] for {regionCode} ({count} states, loop={isLoop})");
        }
        catch (Exception ex)
        {
            RSPlugin.log.LogError(
                $"[BlendSettingsWriter] Cannot write sequences: {ex.Message}");
        }
    }

// Devuelve los números de estado ya declarados en [SEQUENCES].
    private static System.Collections.Generic.HashSet<int> GetExistingSequenceKeys(string fileText)
    {
        var result = new System.Collections.Generic.HashSet<int>();
        bool inSeq = false;
        foreach (string rawLine in fileText.Split('\n'))
        {
            string line = rawLine.TrimEnd('\r').Trim();
            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                inSeq = line.ToUpperInvariant() == "[SEQUENCES]";
                continue;
            }
            if (!inSeq || line.StartsWith("#") || string.IsNullOrEmpty(line)) continue;
            int sep = line.IndexOf(':');
            if (sep <= 0) continue;
            int key;
            if (int.TryParse(line.Substring(0, sep).Trim(), out key))
                result.Add(key);
        }
        return result;
    }

    // Inserta líneas al final de la sección [SEQUENCES]. Si la sección no existe, la crea antes de [ROOMS].
    private static string InsertIntoSequencesSection(string fileText, string linesToAdd)
    {
        var lines = new System.Collections.Generic.List<string>(fileText.Split('\n'));
        int seqStart  = -1;
        int insertIdx = -1;

        for (int i = 0; i < lines.Count; i++)
        {
            string line = lines[i].TrimEnd('\r').Trim();
            if (line.ToUpperInvariant() == "[SEQUENCES]")
            {
                seqStart  = i;
                insertIdx = i;
                continue;
            }
            if (seqStart >= 0)
            {
                if (line.StartsWith("[") && line.EndsWith("]")) break;
                if (!string.IsNullOrEmpty(line)) insertIdx = i;
            }
        }

        if (seqStart >= 0)
        {
            // Insertar después del último elemento existente de [SEQUENCES]
            int at = insertIdx + 1;
            foreach (string newLine in linesToAdd.Split('\n'))
            {
                string trimmed = newLine.TrimEnd('\r');
                if (!string.IsNullOrEmpty(trimmed))
                    lines.Insert(at++, trimmed);
            }
        }
        else
        {
            // Sección no existe — crearla antes de [ROOMS]
            int roomsIdx = -1;
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].TrimEnd('\r').Trim().ToUpperInvariant() == "[ROOMS]")
                { roomsIdx = i; break; }
            }

            var seqBlock = new System.Collections.Generic.List<string>();
            seqBlock.Add("");
            seqBlock.Add("[SEQUENCES]");
            foreach (string newLine in linesToAdd.Split('\n'))
            {
                string trimmed = newLine.TrimEnd('\r');
                if (!string.IsNullOrEmpty(trimmed))
                    seqBlock.Add(trimmed);
            }
            seqBlock.Add("");

            int insertAt = roomsIdx >= 0 ? roomsIdx : lines.Count;
            lines.InsertRange(insertAt, seqBlock);
        }

        return string.Join("\n", lines);
    }

    // Añade o quita roomName de la sección [ROOMS] del blend_settings.txt de su región. Crea el archivo si no existe. Devuelve true si la sala quedó registrada, false si fue eliminada.
    public static bool ToggleRoom(string roomName)
    {
        string path = EnsureFileExists(roomName);
        if (path == null) return false;

        string regionCode = ExtractRegionCode(roomName);

        // Leer el archivo actual
        string raw;
        try { raw = File.ReadAllText(path, Encoding.UTF8); }
        catch (Exception ex)
        {
            RSPlugin.log.LogError(
                $"[BlendSettingsWriter] Cannot read {path}: {ex.Message}");
            return false;
        }

        bool isRegistered = IsRoomRegistered(raw, roomName);

        string newContent = isRegistered
            ? RemoveRoomFromText(raw, roomName)
            : AddRoomToText(raw, roomName);

        try
        {
            File.WriteAllText(path, newContent, Encoding.UTF8);
            BlendSettingsLoader.InvalidateCache(regionCode);

            bool nowRegistered = !isRegistered;
            RSPlugin.log.LogInfo(
                $"[BlendSettingsWriter] Room {roomName} {(nowRegistered ? "added to" : "removed from")} [ROOMS] in {path}");
            return nowRegistered;
        }
        catch (Exception ex)
        {
            RSPlugin.log.LogError(
                $"[BlendSettingsWriter] Cannot write {path}: {ex.Message}");
            return isRegistered;  // sin cambio
        }
    }

    // Devuelve true si roomName está actualmente en la sección [ROOMS] del blend_settings.txt de su región.
    public static bool IsRoomRegistered(string roomName)
    {
        // Consultar primero el BlendSettings cacheado (más rápido)
        var settings = BlendSettingsLoader.Active;
        if (settings != null && settings._hasRoomsSection)
            return settings.Rooms.ContainsKey(roomName);

        // Fallback: leer el archivo directamente
        string regionCode = ExtractRegionCode(roomName);
        if (regionCode == null) return false;
        string path = BlendSettingsLoader.ResolvePath(regionCode);
        if (path == null) return false;

        try
        {
            string raw = File.ReadAllText(path, Encoding.UTF8);
            return IsRoomRegistered(raw, roomName);
        }
        catch { return false; }
    }

    // Cambia el tipo de cielo de roomName en la sección [ROOMS]. Si sky == None elimina el sufijo. Si la sala no está registrada, no hace nada.
    public static void SetSkyType(string roomName, SkyType sky)
    {
        string regionCode = ExtractRegionCode(roomName);
        if (regionCode == null) return;

        string path = BlendSettingsLoader.ResolvePath(regionCode);
        if (path == null) return;

        string raw;
        try { raw = File.ReadAllText(path, Encoding.UTF8); }
        catch (Exception ex)
        {
            RSPlugin.log.LogError(
                $"[BlendSettingsWriter] Cannot read {path}: {ex.Message}");
            return;
        }

        string newContent = ApplySkyTypeToText(raw, roomName, sky);
        if (newContent == raw) return;

        try
        {
            File.WriteAllText(path, newContent, Encoding.UTF8);
            BlendSettingsLoader.InvalidateCache(regionCode);
            RSPlugin.log.LogInfo(
                $"[BlendSettingsWriter] SkyType for {roomName} → {sky} in {path}");
        }
        catch (Exception ex)
        {
            RSPlugin.log.LogError(
                $"[BlendSettingsWriter] Cannot write {path}: {ex.Message}");
        }
    }

    // Devuelve el SkyType actual de roomName leyendo el BlendSettings cacheado.
    public static SkyType GetSkyType(string roomName)
    {
        var settings = BlendSettingsLoader.Active;
        if (settings != null && settings._hasRoomsSection)
            return settings.GetSkyType(roomName);
        return SkyType.None;
    }

    // Reescribe la línea de roomName en [ROOMS] con el nuevo sufijo sky. None → quita el sufijo. ACV → ", acv". RTV → ", rtv".
    private static string ApplySkyTypeToText(string fileText, string roomName, SkyType sky)
    {
        bool inRooms = false;
        var result = new List<string>();

        foreach (string rawLine in fileText.Split('\n'))
        {
            string line = rawLine.TrimEnd('\r').Trim();

            if (line.StartsWith("[") && line.EndsWith("]"))
                inRooms = line.ToUpperInvariant() == "[ROOMS]";

            if (inRooms && !line.StartsWith("#") && !string.IsNullOrEmpty(line))
            {
                string lineName = line.Split(',')[0].Trim();
                if (string.Equals(lineName, roomName, StringComparison.OrdinalIgnoreCase))
                {
                    string suffix = sky == SkyType.ACV ? ", acv"
                                  : sky == SkyType.RTV ? ", rtv"
                                  : "";
                    result.Add(roomName + suffix);
                    continue;
                }
            }

            result.Add(rawLine.TrimEnd('\r'));
        }

        return string.Join("\n", result);
    }

    // Cambia el modo activo en blend_settings.txt de la región de roomName. Solo descomenta la sección del nuevo modo; comenta todas las demás. No borra el contenido de ninguna sección.
    public static void SetMode(string roomName, BlendMode mode)
    {
        string regionCode = ExtractRegionCode(roomName);
        if (regionCode == null) return;

        string path = EnsureFileExists(roomName);
        if (path == null) return;

        string raw;
        try { raw = File.ReadAllText(path, Encoding.UTF8); }
        catch (Exception ex)
        {
            RSPlugin.log.LogError(
                $"[BlendSettingsWriter] Cannot read {path}: {ex.Message}");
            return;
        }

        string newContent = ApplyModeToText(raw, mode);

        try
        {
            File.WriteAllText(path, newContent, Encoding.UTF8);
            BlendSettingsLoader.InvalidateCache(regionCode);
            RSPlugin.log.LogInfo(
                $"[BlendSettingsWriter] Mode set to {mode} in {path}");
        }
        catch (Exception ex)
        {
            RSPlugin.log.LogError(
                $"[BlendSettingsWriter] Cannot write {path}: {ex.Message}");
        }
    }

}
