using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace RainCycles.Settings;

// LOADER — carga y caché de BlendSettings por región

public static class BlendSettingsLoader
{
    // ── Caché por región (clave = código de región en mayúsculas, ej: "SU")
    private static readonly Dictionary<string, BlendSettings> _cache
        = new Dictionary<string, BlendSettings>();

    // ── Región y settings actualmente activos
    private static string       _activeRegion   = null;
    private static BlendSettings _activeSettings = null;

    // BlendSettings de la región que el jugador tiene activa ahora mismo. Null si la región no tiene blend_settings.txt o aún no se cargó.
    public static BlendSettings Active => _activeSettings;

// Código de la región activa (ej: "SU"). Null si ninguna.
    public static string ActiveRegion => _activeRegion;

    // ── Init

    public static void Init()
    {
        // Hook al construir RoomSettings — ya existe en FilesSetting y se llama
        On.RoomSettings.ctor_Room_string_Region_bool_bool_Timeline_RainWorldGame
            += OnRoomSettingsCtor;

        // Limpiar caché al volver al menú principal
        On.RainWorldGame.ShutDownProcess += OnShutDown;
    }

    // ── Hooks

    private static void OnRoomSettingsCtor(
        On.RoomSettings.orig_ctor_Room_string_Region_bool_bool_Timeline_RainWorldGame orig,
        RoomSettings self,
        Room room, string name, Region region,
        bool template, bool firstTemplate,
        SlugcatStats.Timeline timelinePoint,
        RainWorldGame game)
    {
        orig(self, room, name, region, template, firstTemplate, timelinePoint, game);

        // Solo habitaciones reales con región asignada
        if (region == null) return;

        // Usar region.name en lugar del nombre de la sala para detectar la región.
        string regionCode = region.name?.ToUpperInvariant();
        if (string.IsNullOrEmpty(regionCode)) return;

        // Solo actualizamos si cambiamos de región
        if (regionCode == _activeRegion) return;

        LoadRegion(regionCode);
    }

    private static void OnShutDown(On.RainWorldGame.orig_ShutDownProcess orig, RainWorldGame self)
    {
        orig(self);
        ClearCache();
    }

    // ── Carga y caché

    // Carga (o recupera del caché) el BlendSettings para una región. Actualiza _activeRegion y _activeSettings.
    public static void LoadRegion(string regionCode)
    {
        regionCode = regionCode.ToUpperInvariant();

        BlendSettings settings;
        if (!_cache.TryGetValue(regionCode, out settings))
        {
            settings = LoadFromDisk(regionCode);
            _cache[regionCode] = settings;   // null también se cachea (región sin archivo)
        }

        _activeRegion   = regionCode;
        _activeSettings = settings;

        if (settings != null)
            RSPlugin.log.LogInfo(
                $"[BlendSettingsLoader] Region activated: {regionCode} | mode={settings.Mode}");
    }

    // Obtiene el BlendSettings para una región específica. Carga desde disco si no está en caché. Puede devolver null.
    public static BlendSettings GetForRegion(string regionCode)
    {
        regionCode = regionCode.ToUpperInvariant();
        BlendSettings s;
        if (!_cache.TryGetValue(regionCode, out s))
        {
            s = LoadFromDisk(regionCode);
            _cache[regionCode] = s;
        }
        return s;
    }

    // Invalida la entrada de caché para una región (fuerza recarga en disco). Útil cuando el usuario edita el archivo desde el panel DevTools.
    public static void InvalidateCache(string regionCode)
    {
        regionCode = regionCode.ToUpperInvariant();
        _cache.Remove(regionCode);

        // Si era la región activa, recargamos inmediatamente
        if (_activeRegion == regionCode)
        {
            _activeSettings = null;
            LoadRegion(regionCode);
        }
    }

// Vacía toda la caché.
    public static void ClearCache()
    {
        _cache.Clear();
        _activeRegion   = null;
        _activeSettings = null;
    }

    // ── Helpers

    private static BlendSettings LoadFromDisk(string regionCode)
    {
        string path = ResolvePath(regionCode);
        if (path == null) return null;

        return BlendSettings.FromFile(path);
    }

    // Devuelve la ruta resuelta de blend_settings.txt para una región, o null si el archivo no existe.
    public static string ResolvePath(string regionCode)
    {
        string candidate = AssetManager.ResolveFilePath(
            "World" +
            Path.DirectorySeparatorChar + regionCode.ToUpperInvariant() + "-Rooms" +
            Path.DirectorySeparatorChar + "RainCycles" +
            Path.DirectorySeparatorChar + regionCode.ToUpperInvariant() + "_blend_settings.txt");

        return File.Exists(candidate) ? candidate : null;
    }

    // Extrae el código de región de un nombre de habitación (ej: "SU_C01" → "SU"). Devuelve null si el nombre no tiene el formato esperado.
    private static string ExtractRegionCode(string roomName)
    {
        if (string.IsNullOrEmpty(roomName)) return null;
        string[] parts = Regex.Split(roomName, "_");
        return parts.Length >= 2 ? parts[0].ToUpperInvariant() : null;
    }
}