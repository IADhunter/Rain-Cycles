using System;
using System.IO;
using System.Text;

namespace FilesSetting;

// ════════════════════════════════════════════════════════════════════════
// CUSTOM MODE STATE
//
// Gestiona el estado persistente del modo Custom: qué regiones tienen
// Custom activo y qué trigger_id lo activó.
//
// Persistencia: un archivo TXT externo al save del juego, ubicado en
//   <RainWorld>/ModConfigs/RainCycles/custom_state.txt
//
// Formato del archivo — una línea por región activa:
//   UW:HEAVY_STORM
//   SU:SPECIAL_EVENT
//
// Archivo ausente o vacío = Custom inactivo en todas las regiones.
// Rain World lo ignora completamente; el mod lo gestiona de forma autónoma.
//
// API pública (para que otros mods interactúen):
//   CustomModeState.Activate(regionCode, triggerId)
//   CustomModeState.Deactivate(regionCode, triggerId)
//   CustomModeState.IsActive(regionCode, triggerId)
// ════════════════════════════════════════════════════════════════════════

public static class CustomModeState
{
    private static string _filePath = null;

    // Cache en memoria: regionCode (mayúsculas) → triggerId activo
    // Solo una región puede tener un trigger activo a la vez por simplicidad.
    private static readonly System.Collections.Generic.Dictionary<string, string>
        _activeByRegion = new System.Collections.Generic.Dictionary<string, string>(
            System.StringComparer.OrdinalIgnoreCase);

    // ── Inicialización ────────────────────────────────────────────────────

    public static void Init()
    {
        _filePath = ResolveFilePath();
        Load();
    }

    // ── API pública ───────────────────────────────────────────────────────

    /// <summary>
    /// Activa el modo Custom para una región con el trigger dado.
    /// Si la región ya tiene un trigger activo, lo reemplaza.
    /// Persiste el estado al disco inmediatamente.
    /// </summary>
    public static void Activate(string regionCode, string triggerId)
    {
        if (string.IsNullOrEmpty(regionCode) || string.IsNullOrEmpty(triggerId))
        {
            Plugin.RSPlugin.log.LogWarning("[CustomMode] Activate: regionCode y triggerId no pueden ser vacíos.");
            return;
        }

        string key = regionCode.ToUpperInvariant();
        _activeByRegion[key] = triggerId;
        Save();

        Plugin.RSPlugin.log.LogInfo($"[CustomMode] Activated '{triggerId}' for region '{key}'.");

        // Si la región activa ya tiene un blend_settings con mode:custom,
        // arrancar el clock inmediatamente.
        TryStartClockForCurrentRegion(key, triggerId);
    }

    /// <summary>
    /// Desactiva el modo Custom para una región.
    /// Si el trigger no coincide con el activo, no hace nada (seguridad
    /// para que un mod no apague el trigger de otro).
    /// Si hay un blend en curso, lo deja terminar — el clock se para
    /// automáticamente al llegar a Phase.Done.
    /// </summary>
    public static void Deactivate(string regionCode, string triggerId)
    {
        if (string.IsNullOrEmpty(regionCode)) return;

        string key = regionCode.ToUpperInvariant();
        string current;
        if (!_activeByRegion.TryGetValue(key, out current)) return;

        if (!string.Equals(current, triggerId, StringComparison.OrdinalIgnoreCase))
        {
            Plugin.RSPlugin.log.LogWarning(
                $"[CustomMode] Deactivate ignored: active trigger for '{key}' is '{current}', not '{triggerId}'.");
            return;
        }

        _activeByRegion.Remove(key);
        Save();

        Plugin.RSPlugin.log.LogInfo($"[CustomMode] Deactivated '{triggerId}' for region '{key}'.");

        // Marcar el clock para que no reinicie después de Done.
        // Si hay un blend activo, se deja terminar — BlendClockUpdater
        // detecta que Custom ya no está activo y no reinicia el clock.
        BlendClock.SetCustomPendingStop();
    }

    /// <summary>
    /// Devuelve true si Custom está activo para esta región con este trigger.
    /// Si triggerId es null, devuelve true si Custom está activo con cualquier trigger.
    /// </summary>
    public static bool IsActive(string regionCode, string triggerId = null)
    {
        if (string.IsNullOrEmpty(regionCode)) return false;

        string key = regionCode.ToUpperInvariant();
        string current;
        if (!_activeByRegion.TryGetValue(key, out current)) return false;

        return triggerId == null ||
               string.Equals(current, triggerId, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Devuelve el trigger activo para una región, o null si Custom está inactivo.
    /// </summary>
    public static string GetActiveTrigger(string regionCode)
    {
        if (string.IsNullOrEmpty(regionCode)) return null;
        string current;
        _activeByRegion.TryGetValue(regionCode.ToUpperInvariant(), out current);
        return current;
    }

    // ── Persistencia ──────────────────────────────────────────────────────

    private static void Load()
    {
        _activeByRegion.Clear();
        if (_filePath == null || !File.Exists(_filePath)) return;

        try
        {
            foreach (string raw in File.ReadAllLines(_filePath, Encoding.UTF8))
            {
                string line = raw.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;

                int sep = line.IndexOf(':');
                if (sep < 1) continue;

                string region  = line.Substring(0, sep).Trim().ToUpperInvariant();
                string trigger = line.Substring(sep + 1).Trim();
                if (!string.IsNullOrEmpty(region) && !string.IsNullOrEmpty(trigger))
                    _activeByRegion[region] = trigger;
            }
            Plugin.RSPlugin.log.LogInfo(
                $"[CustomMode] Loaded {_activeByRegion.Count} active region(s) from {_filePath}");
        }
        catch (Exception ex)
        {
            Plugin.RSPlugin.log.LogWarning($"[CustomMode] Error loading state: {ex.Message}");
        }
    }

    private static void Save()
    {
        if (_filePath == null) return;

        try
        {
            string dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var sb = new StringBuilder();
            sb.AppendLine("# Rain Cycles — Custom mode state");
            sb.AppendLine("# Format: REGION:trigger_id");
            sb.AppendLine("# Do not edit manually unless you know what you're doing.");
            sb.AppendLine();
            foreach (var kv in _activeByRegion)
                sb.AppendLine($"{kv.Key}:{kv.Value}");

            File.WriteAllText(_filePath, sb.ToString(), Encoding.UTF8);
        }
        catch (Exception ex)
        {
            Plugin.RSPlugin.log.LogWarning($"[CustomMode] Error saving state: {ex.Message}");
        }
    }

    private static string ResolveFilePath()
    {
        try
        {
            // Custom.exe vive en <RainWorld>/RainWorld.exe
            // AssetManager.ResolveFilePath trabaja relativo a StreamingAssets,
            // así que resolvemos manualmente desde Application.dataPath.
            string dataPath = UnityEngine.Application.dataPath; // .../RainWorld_Data
            string root     = Path.GetDirectoryName(dataPath);   // .../RainWorld
            return Path.Combine(root, "ModConfigs", "RainCycles", "custom_state.txt");
        }
        catch
        {
            return null;
        }
    }

    // ── Helper interno ────────────────────────────────────────────────────

    private static void TryStartClockForCurrentRegion(string regionCode, string triggerId)
    {
        var settings = BlendSettingsLoader.Active;
        if (settings == null || settings.Mode != BlendMode.Custom) return;

        // Verificar que la región activa coincide
        string activeRegion = BlendSettingsLoader.ActiveRegion;
        if (!string.Equals(activeRegion, regionCode, StringComparison.OrdinalIgnoreCase)) return;

        // Verificar que el trigger_id declarado en blend_settings coincide
        if (!string.Equals(settings.CustomTriggerId, triggerId, StringComparison.OrdinalIgnoreCase))
        {
            Plugin.RSPlugin.log.LogWarning(
                $"[CustomMode] Trigger '{triggerId}' activado pero blend_settings declara '{settings.CustomTriggerId}'. Ignorando.");
            return;
        }

        if (BlendClock.IsRunning) return; // ya está corriendo

        var game = UnityEngine.Object.FindObjectOfType<RainWorld>()
                   ?.processManager?.currentMainLoop as RainWorldGame;
        int cycle = game?.GetStorySession?.saveState?.cycleNumber ?? 0;

        int n = 2;
        if (settings._hasRoomsSection)
            foreach (string room in settings.Rooms)
            {
                int count = ReadStateReadFiles.CountRainStateFiles(room);
                if (count > 0) { n = count; break; }
            }

        int initialA = (cycle % n) + 1;
        BlendClock.Start(initialA);
        Plugin.RSPlugin.log.LogInfo(
            $"[CustomMode] Clock started immediately for region '{regionCode}' trigger='{triggerId}'.");
    }
}
