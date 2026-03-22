using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace FilesSetting;

// ════════════════════════════════════════════════════════════════════════
// BLEND SETTINGS WRITER
//
// Responsabilidades:
//   1. Crear blend_settings.txt con contenido default si no existe,
//      para la región de una sala dada.
//   2. Toggle de sala en la sección [ROOMS]: añadir o quitar una sala
//      y sobreescribir el archivo manteniendo el resto intacto.
//   3. Notificar al Loader para que invalide su caché tras la escritura.
//
// El archivo se crea con una plantilla comentada que sirve de documentación
// al creador de la región. Las secciones opcionales están comentadas.
// ════════════════════════════════════════════════════════════════════════

public static class BlendSettingsWriter
{
    // ── API pública ───────────────────────────────────────────────────────

    /// <summary>
    /// Crea blend_settings.txt para la región de roomName si no existe.
    /// Devuelve la ruta del archivo (existente o recién creado), o null si falla.
    /// </summary>
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
                Plugin.RSPlugin.log.LogError(
                    $"[BlendSettingsWriter] Cannot create directory {dir}: {ex.Message}");
                return null;
            }
        }

        // Escribir plantilla default
        try
        {
            File.WriteAllText(path, BuildDefaultTemplate(regionCode), Encoding.UTF8);
            Plugin.RSPlugin.log.LogInfo(
                $"[BlendSettingsWriter] Created blend_settings.txt for region {regionCode}: {path}");

            // Notificar al Loader para que cargue el nuevo archivo
            BlendSettingsLoader.InvalidateCache(regionCode);
        }
        catch (Exception ex)
        {
            Plugin.RSPlugin.log.LogError(
                $"[BlendSettingsWriter] Cannot write {path}: {ex.Message}");
            return null;
        }

        // Generar entradas vacías en [SEQUENCES] según los settings disponibles
        UpdateSequences(roomName);

        return path;
    }

    /// <summary>
    /// Detecta cuántos settings_N.txt hay para las salas de [ROOMS] y añade
    /// solo las entradas faltantes en [SEQUENCES]. Nunca modifica las existentes.
    /// Límite: 3 entradas para modos normales, 6 para Loop (con carriles A/B).
    /// Llamado automáticamente desde EnsureFileExists al abrir DevTools.
    /// </summary>
    public static void UpdateSequences(string roomName)
    {
        string regionCode = ExtractRegionCode(roomName);
        if (regionCode == null) return;

        string path = ResolveWritablePath(regionCode);
        if (path == null || !File.Exists(path)) return;

        // Contar cuántos settings_N.txt existen para esta sala
        int n = ReadStateReadFiles.CountRainStateFiles(roomName);
        if (n == 0) return;

        // Leer el archivo actual
        string raw;
        try { raw = File.ReadAllText(path, System.Text.Encoding.UTF8); }
        catch { return; }

        // Detectar el modo activo leyendo el archivo raw (más fiable que el caché)
        // para evitar que settings==null fuerce isLoop=true en archivos recién creados.
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
                // Anclaje = último de A = primero de B.
                // Ejemplo con N=4, i=1: A=1,2,3  B=3,4,1
                int s1 = i;
                int s2 = (i % n) + 1;
                int s3 = ((i + 1) % n) + 1;  // anclaje
                int s4 = ((i + 2) % n) + 1;
                newLines.AppendLine($"{i}: {s1}, {s2}, {s3}, (A = {s1},{s2},{s3} ~ B = {s3},{s4},{s1})<>");
            }
            else
            {
                // Modos Cycle/EndCycle/Custom: secuencia lineal de 3 states rotando
                int s1 = i;
                int s2 = (i % n) + 1;
                int s3 = ((i + 1) % n) + 1;
                newLines.AppendLine($"{i}: {s1}, {s2}, {s3}<>");
            }
        }

        string newContent = InsertIntoSequencesSection(raw, newLines.ToString());
        if (newContent == raw) return;

        try
        {
            File.WriteAllText(path, newContent, System.Text.Encoding.UTF8);
            BlendSettingsLoader.InvalidateCache(regionCode);
            Plugin.RSPlugin.log.LogInfo(
                $"[BlendSettingsWriter] Updated [SEQUENCES] for {regionCode} ({count} states, loop={isLoop})");
        }
        catch (Exception ex)
        {
            Plugin.RSPlugin.log.LogError(
                $"[BlendSettingsWriter] Cannot write sequences: {ex.Message}");
        }
    }

    /// <summary>Devuelve los números de estado ya declarados en [SEQUENCES].</summary>
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

    /// <summary>
    /// Inserta líneas al final de la sección [SEQUENCES].
    /// Si la sección no existe, la crea antes de [ROOMS].
    /// </summary>
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

    /// <summary>
    /// Añade o quita roomName de la sección [ROOMS] del blend_settings.txt
    /// de su región. Crea el archivo si no existe.
    /// Devuelve true si la sala quedó registrada, false si fue eliminada.
    /// </summary>
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
            Plugin.RSPlugin.log.LogError(
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
            Plugin.RSPlugin.log.LogInfo(
                $"[BlendSettingsWriter] Room {roomName} {(nowRegistered ? "added to" : "removed from")} [ROOMS] in {path}");
            return nowRegistered;
        }
        catch (Exception ex)
        {
            Plugin.RSPlugin.log.LogError(
                $"[BlendSettingsWriter] Cannot write {path}: {ex.Message}");
            return isRegistered;  // sin cambio
        }
    }

    /// <summary>
    /// Devuelve true si roomName está actualmente en la sección [ROOMS]
    /// del blend_settings.txt de su región.
    /// </summary>
    public static bool IsRoomRegistered(string roomName)
    {
        // Consultar primero el BlendSettings cacheado (más rápido)
        var settings = BlendSettingsLoader.Active;
        if (settings != null && settings._hasRoomsSection)
            return settings.Rooms.Contains(roomName);

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


    /// <summary>
    /// Cambia el modo activo en blend_settings.txt de la región de roomName.
    /// Solo descomenta la sección del nuevo modo; comenta todas las demás.
    /// No borra el contenido de ninguna sección.
    /// </summary>
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
            Plugin.RSPlugin.log.LogError(
                $"[BlendSettingsWriter] Cannot read {path}: {ex.Message}");
            return;
        }

        string newContent = ApplyModeToText(raw, mode);

        try
        {
            File.WriteAllText(path, newContent, Encoding.UTF8);
            BlendSettingsLoader.InvalidateCache(regionCode);
            Plugin.RSPlugin.log.LogInfo(
                $"[BlendSettingsWriter] Mode set to {mode} in {path}");
        }
        catch (Exception ex)
        {
            Plugin.RSPlugin.log.LogError(
                $"[BlendSettingsWriter] Cannot write {path}: {ex.Message}");
        }
    }

    // ── Helpers de texto ─────────────────────────────────────────────────

    private static bool IsRoomRegistered(string fileText, string roomName)
    {
        bool inRooms = false;
        foreach (string rawLine in fileText.Split('\n'))
        {
            string line = rawLine.TrimEnd('\r').Trim();
            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                inRooms = line.ToUpperInvariant() == "[ROOMS]";
                continue;
            }
            if (!inRooms) continue;
            if (string.Equals(line, roomName, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Añade roomName al final de la sección [ROOMS].
    /// Si la sección no existe, la crea al final del archivo.
    /// </summary>
    private static string AddRoomToText(string fileText, string roomName)
    {
        var lines = new List<string>(fileText.Split('\n'));

        // Buscar el índice de la última línea de [ROOMS]
        int roomsSectionStart = -1;
        int insertAfter = -1;

        for (int i = 0; i < lines.Count; i++)
        {
            string line = lines[i].TrimEnd('\r').Trim();
            if (line.ToUpperInvariant() == "[ROOMS]")
            {
                roomsSectionStart = i;
                insertAfter = i;
                continue;
            }
            if (roomsSectionStart >= 0)
            {
                // Fin de sección: línea de otra sección o fin de archivo
                if (line.StartsWith("[") && line.EndsWith("]")) break;
                if (!string.IsNullOrEmpty(line) && !line.StartsWith("#"))
                    insertAfter = i;
            }
        }

        if (roomsSectionStart >= 0)
        {
            // Insertar después del último elemento de [ROOMS]
            lines.Insert(insertAfter + 1, roomName);
        }
        else
        {
            // Sección no existe: añadir al final
            if (lines.Count > 0 && !string.IsNullOrWhiteSpace(lines[lines.Count - 1]))
                lines.Add("");
            lines.Add("[ROOMS]");
            lines.Add(roomName);
        }

        return string.Join("\n", lines);
    }

    /// <summary>Elimina roomName de la sección [ROOMS] (case-insensitive).</summary>
    private static string RemoveRoomFromText(string fileText, string roomName)
    {
        bool inRooms = false;
        var result = new List<string>();

        foreach (string rawLine in fileText.Split('\n'))
        {
            string line = rawLine.TrimEnd('\r').Trim();

            if (line.StartsWith("[") && line.EndsWith("]"))
                inRooms = line.ToUpperInvariant() == "[ROOMS]";

            if (inRooms && string.Equals(line, roomName, StringComparison.OrdinalIgnoreCase))
                continue;  // saltar esta línea

            result.Add(rawLine.TrimEnd('\r'));
        }

        return string.Join("\n", result);
    }


    // ── Lógica de comentar/descomentar secciones ─────────────────────────

    // Secciones que se controlan según el modo activo.
    // [CONFIG] y [ROOMS] nunca se tocan.
    private static readonly string[] MODE_SECTIONS = { "LOOP", "CYCLE", "ENDCYCLE", "CUSTOM", "SEQUENCES" };

    // Qué secciones activa cada modo
    private static string[] ActiveSectionsFor(BlendMode mode)
    {
        switch (mode)
        {
            case BlendMode.Loop:     return new[] { "LOOP", "SEQUENCES" };
            case BlendMode.Cycle:    return new[] { "CYCLE", "SEQUENCES" };
            case BlendMode.EndCycle: return new[] { "ENDCYCLE", "SEQUENCES" };
            case BlendMode.Custom:   return new[] { "CUSTOM" };
            default:                 return new[] { "LOOP", "SEQUENCES" };
        }
    }

    /// <summary>
    /// Procesa el texto del archivo línea a línea:
    /// - Actualiza "mode: xxx" en [CONFIG]
    /// - Descomenta las secciones activas del modo y comenta las demás
    /// - No toca [CONFIG] ni [ROOMS]
    /// </summary>
    private static string ApplyModeToText(string fileText, BlendMode mode)
    {
        string modeName = mode.ToString().ToLowerInvariant();
        string[] activeSections = ActiveSectionsFor(mode);

        var result = new System.Text.StringBuilder();
        string currentSection = "";
        bool inModeSection = false;   // estamos dentro de una sección controlada
        bool sectionActive  = false;  // la sección actual debe estar descomentada

        foreach (string rawLine in fileText.Split('\n'))
        {
            string line = rawLine.TrimEnd('\r');
            string trimmed = line.Trim();

            // ── Cabecera de sección ──────────────────────────────────────
            if (trimmed.StartsWith("[") || (trimmed.StartsWith("#") && trimmed.TrimStart('#').Trim().StartsWith("[")))
            {
                // Extraer el nombre de sección (puede estar comentada: "# [LOOP]")
                string raw = trimmed.TrimStart('#').Trim();
                if (raw.StartsWith("[") && raw.EndsWith("]"))
                {
                    currentSection = raw.Substring(1, raw.Length - 2).ToUpperInvariant();
                    inModeSection  = System.Array.IndexOf(MODE_SECTIONS, currentSection) >= 0;
                    sectionActive  = System.Array.Exists(activeSections, s => s == currentSection);

                    if (inModeSection)
                    {
                        // Escribir la cabecera comentada o no según corresponda
                        result.Append(sectionActive ? $"[{currentSection}]" : $"# [{currentSection}]");
                        result.Append('\n');
                        continue;
                    }
                }
            }

            // ── Líneas dentro de sección controlada ──────────────────────
            if (inModeSection)
            {
                // Actualizar "mode:" dentro de [CONFIG] si aplica (no es sección controlada,
                // pero por seguridad lo manejamos aquí también vía el bloque CONFIG abajo)

                if (sectionActive)
                {
                    // Descomentar: quitar "# " del inicio si lo tiene
                    string uncommented = (trimmed.StartsWith("# ") || trimmed == "#")
                        ? line.Substring(line.IndexOf('#') + 1).TrimStart()
                        : line;
                    result.Append(uncommented);
                }
                else
                {
                    // Comentar: añadir "# " si no está ya comentada y no está vacía
                    if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#"))
                        result.Append(line);
                    else
                        result.Append("# " + line);
                }
                result.Append('\n');
                continue;
            }

            // ── Línea "mode:" en [CONFIG] ────────────────────────────────
            if (currentSection == "CONFIG" && trimmed.StartsWith("mode:"))
            {
                // Preservar indentación original
                int modeIdx = line.IndexOf("mode:");
                result.Append(line.Substring(0, modeIdx) + "mode: " + modeName);
                result.Append('\n');
                continue;
            }

            // ── Línea normal (no controlada) ─────────────────────────────
            result.Append(line);
            result.Append('\n');
        }

        // Quitar el último \n extra si el archivo original no terminaba con uno
        string final = result.ToString();
        if (!fileText.EndsWith("\n") && final.EndsWith("\n"))
            final = final.TrimEnd('\n');

        return final;
    }

    // ── Generación del template default ──────────────────────────────────

    private static string BuildDefaultTemplate(string regionCode)
    {
        return
$@"# blend_settings.txt — Rain Cycles
# Region: {regionCode}
# Generated automatically. Edit freely.
#
# Modes: loop, cycle, endcycle, custom
# Add rooms below via the DevTools panel (RC_Panel toggle button).

[CONFIG]
mode: loop
# camera_cooldown: 30

[LOOP]
idle_time: 60.0
duration: 10.0

# [CYCLE]
# trigger_pct: 0.90
# duration: 10.0

# [ENDCYCLE]
# trigger_pct: 0.95
# duration: 25.0
# target_state: 1

# [CUSTOM]
# trigger_id: MY_TRIGGER
# duration: 15.0

# [SEQUENCES] se genera automáticamente al abrir DevTools.
# El mod solo añade entradas vacías — los valores los pone el usuario.

[ROOMS]
";
    }

    // ── Helpers de ruta ───────────────────────────────────────────────────

    /// <summary>
    /// Resuelve la ruta ESCRIBIBLE de blend_settings.txt para una región.
    /// A diferencia de BlendSettingsLoader.ResolvePath, devuelve la ruta
    /// aunque el archivo no exista todavía (para poder crearlo).
    /// </summary>

    /// <summary>
    /// Lee la línea "mode:" del bloque [CONFIG] directamente del texto del archivo.
    /// Devuelve true si el modo es loop o no está declarado (default).
    /// </summary>
    private static bool DetectLoopModeFromRaw(string fileText)
    {
        bool inConfig = false;
        foreach (string rawLine in fileText.Split('\n'))
        {
            string line = rawLine.TrimEnd('\r').Trim();
            if (line.ToUpperInvariant() == "[CONFIG]") { inConfig = true; continue; }
            if (inConfig && line.StartsWith("[")) break;  // salimos de [CONFIG]
            if (inConfig && line.StartsWith("mode:"))
            {
                string val = line.Substring("mode:".Length).Trim().ToLowerInvariant();
                return val == "loop" || val == "";
            }
        }
        return true;  // default: loop
    }

    private static string ResolveWritablePath(string regionCode)
    {
        try
        {
            string upper = regionCode.ToUpperInvariant();

            // Si ya existe, devolver esa ruta
            string existing = BlendSettingsLoader.ResolvePath(upper);
            if (existing != null) return existing;

            // Resolver la ruta del directorio RainCycles usando un settings file
            // conocido como ancla (no creamos placeholder, usamos lo que ya existe).
            // Si no hay ningún settings file, usamos AssetManager directamente.
            string anchor = null;

            // Intentar con settings_1.txt de cualquier sala de la región
            // buscando en la lista de salas registradas
            var settings = BlendSettingsLoader.GetForRegion(upper);
            if (settings != null && settings._hasRoomsSection)
            {
                foreach (string room in settings.Rooms)
                {
                    string s = ReadStateReadFiles.GetRainStateSettingsFile(room, 1);
                    if (s != null) { anchor = s; break; }
                }
            }

            string folder;
            if (anchor != null)
            {
                // El settings_1.txt vive en .../RainCycles/<room>/<room>_settings_1.txt
                // Subir dos niveles para llegar a RainCycles/
                folder = Path.GetDirectoryName(Path.GetDirectoryName(anchor));
            }
            else
            {
                // Fallback: construir la ruta relativa y dejar que AssetManager
                // resuelva el directorio base del mod
                string relative =
                    "World" + Path.DirectorySeparatorChar +
                    upper + "-Rooms" + Path.DirectorySeparatorChar +
                    "RainCycles";
                // AssetManager trabaja con '/' en algunas versiones
                folder = AssetManager.ResolveFilePath(relative.Replace(Path.DirectorySeparatorChar, '/'));
                // Si devolvió una ruta con el nombre del archivo, quitar el último segmento
                if (!string.IsNullOrEmpty(Path.GetExtension(folder)))
                    folder = Path.GetDirectoryName(folder);
            }

            if (string.IsNullOrEmpty(folder))
            {
                Plugin.RSPlugin.log.LogError(
                    $"[BlendSettingsWriter] Cannot determine RainCycles folder for {upper}");
                return null;
            }

            // Construir el nombre del archivo con mayúsculas garantizadas
            string fileName = upper + "_blend_settings.txt";
            string result   = Path.Combine(folder, fileName);

            Plugin.RSPlugin.log.LogInfo(
                $"[BlendSettingsWriter] Resolved writable path: {result}");

            return result;
        }
        catch (Exception ex)
        {
            Plugin.RSPlugin.log.LogError(
                $"[BlendSettingsWriter] Cannot resolve path for region {regionCode}: {ex.Message}");
            return null;
        }
    }

    private static string ExtractRegionCode(string roomName)
    {
        if (string.IsNullOrEmpty(roomName)) return null;
        string[] parts = Regex.Split(roomName, "_");
        return parts.Length >= 2 ? parts[0].ToUpperInvariant() : null;
    }
}