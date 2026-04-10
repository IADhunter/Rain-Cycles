using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace RainCycles.Settings;

// Partial: helpers de texto, lógica de secciones, template default y resolución de rutas.
public static partial class BlendSettingsWriter
{
    // ────────────────────

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
            if (!inRooms || line.StartsWith("#") || string.IsNullOrEmpty(line)) continue;
            // El nombre de sala puede ir seguido de ", acv" o ", rtv"
            string lineName = line.Split(',')[0].Trim();
            if (string.Equals(lineName, roomName, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    // Añade roomName al final de la sección [ROOMS]. Si la sección no existe, la crea al final del archivo.
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

// Elimina roomName de la sección [ROOMS] (case-insensitive).
    private static string RemoveRoomFromText(string fileText, string roomName)
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
                    continue;  // saltar esta línea
            }

            result.Add(rawLine.TrimEnd('\r'));
        }

        return string.Join("\n", result);
    }

    // Secciones que se controlan según el modo activo.
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

    // Procesa el texto del archivo línea a línea: - Actualiza "mode: xxx" en [CONFIG] - Descomenta las secciones activas del modo y comenta las demás - No toca [CONFIG] ni [ROOMS]
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

            // ────────────────────
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

            // ────────────────────
            if (inModeSection)
            {
                // Actualizar "mode:" dentro de [CONFIG] si aplica (no es sección controlada,

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

            // ────────────────────
            if (currentSection == "CONFIG" && trimmed.StartsWith("mode:"))
            {
                // Preservar indentación original
                int modeIdx = line.IndexOf("mode:");
                result.Append(line.Substring(0, modeIdx) + "mode: " + modeName);
                result.Append('\n');
                continue;
            }

            // ────────────────────
            result.Append(line);
            result.Append('\n');
        }

        // Quitar el último \n extra si el archivo original no terminaba con uno
        string final = result.ToString();
        if (!fileText.EndsWith("\n") && final.EndsWith("\n"))
            final = final.TrimEnd('\n');

        return final;
    }

    // ────────────────────

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

[LOOP]
idle_time: 60.0
duration: 10.0

# [CYCLE]
# Dispara un blend cuando el ciclo de lluvia alcanza trigger_pct (0-1).
# El blend dura 'duration' segundos y luego el sistema duerme hasta el próximo ciclo.
# trigger_pct: 0.90
# duration: 10.0

# [ENDCYCLE]
# Se activa cuando el ciclo de lluvia termina.
# 'idle' = segundos de espera post-lluvia antes de iniciar el blend.
# 'duration' = duración del blend en segundos.
# target_state:
#   1 = fin de lluvia normal — hace el blend y se queda en el estado destino.
#   2 = puente a Loop — tras el blend, arranca Loop saltándose el primer idle.
# idle: 10.0
# duration: 25.0
# target_state: 1

# [CUSTOM]
# El blend solo corre cuando otro mod activa el trigger vía la API:
#   CustomModeState.Activate(""REGION"", ""MY_TRIGGER"")
#   CustomModeState.Deactivate(""REGION"", ""MY_TRIGGER"")
# El modo declarado en [CONFIG] define qué sistema corre cuando Custom está activo.
# trigger_id: MY_TRIGGER

[BACKGROUNDS]
# Las imágenes se buscan en la carpeta 'illustrations' (vanilla o del mod).
# Formato: bkgXX: imagen_acv.png, imagen_rtv.png
#   Primera imagen → AboveCloudsView (ACV)
#   Segunda imagen → RoofTopView (RTV) — opcional
# Asigna una imagen a un estado con el sufijo =bkgXX en [SEQUENCES]:
#   1: 1, 2, 3, (A = 1,2,3 ~ B = 3,4,1) =bkg00
bkg00: day.png, rtvday.png
bkg01: dusk.png, rtvdusk.png
bkg02: night.png, rtvnight.png

[ROOMS]
";
    }

    // ────────────────────

    // Resuelve la ruta ESCRIBIBLE de blend_settings.txt para una región. A diferencia de BlendSettingsLoader.ResolvePath, devuelve la ruta aunque el archivo no exista todavía (para poder crearlo).

    // Lee la línea "mode:" del bloque [CONFIG] directamente del texto del archivo. Devuelve true si el modo es loop o no está declarado (default).
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
            string anchor = null;

            // Intentar con settings_1.txt de cualquier sala de la región
            var settings = BlendSettingsLoader.GetForRegion(upper);
            if (settings != null && settings._hasRoomsSection)
            {
                foreach (string room in settings.Rooms.Keys)
                {
                    string s = StateFileResolver.GetRainStateSettingsFile(room, 1);
                    if (s != null) { anchor = s; break; }
                }
            }

            string folder;
            if (anchor != null)
            {
                // El settings_1.txt vive en .../RainCycles/<room>/<room>_settings_1.txt
                folder = Path.GetDirectoryName(Path.GetDirectoryName(anchor));
            }
            else
            {
                // Fallback: construir la ruta relativa y dejar que AssetManager
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
                RSPlugin.log.LogError(
                    $"[BlendSettingsWriter] Cannot determine RainCycles folder for {upper}");
                return null;
            }

            // Construir el nombre del archivo con mayúsculas garantizadas
            string fileName = upper + "_blend_settings.txt";
            string result   = Path.Combine(folder, fileName);

            RSPlugin.log.LogInfo(
                $"[BlendSettingsWriter] Resolved writable path: {result}");

            return result;
        }
        catch (Exception ex)
        {
            RSPlugin.log.LogError(
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
