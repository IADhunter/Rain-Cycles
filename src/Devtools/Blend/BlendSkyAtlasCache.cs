using System.Collections.Generic;

namespace FilesSetting;

// ════════════════════════════════════════════════════════════════════════
// BLEND SKY ATLAS CACHE
//
// Registra qué atlas de ilustración de cielo cargó el mod por región.
// Al cambiar de región, descarga los atlas de la región anterior via
// Futile.atlasManager.UnloadAtlas — evita acumulación de texturas en VRAM.
//
// Ciclo de vida:
//   Register(region, name)   → llamado al cargar cada atlas en el ctor del hook
//   UnloadRegion(region)     → llamado en OnRegionChanged para la región anterior
//   UnloadAll()              → llamado en ShutDownProcess
// ════════════════════════════════════════════════════════════════════════

public static class BlendSkyAtlasCache
{
    // región → nombres de atlas que el mod cargó para esa región
    private static readonly Dictionary<string, HashSet<string>> _cache =
        new Dictionary<string, HashSet<string>>(System.StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registra un atlas como perteneciente a una región.
    /// Llamar justo después de LoadGraphic para que el atlas quede tracked.
    /// </summary>
    public static void Register(string regionCode, string atlasName)
    {
        if (string.IsNullOrEmpty(regionCode) || string.IsNullOrEmpty(atlasName)) return;
        string key = regionCode.ToUpperInvariant();
        if (!_cache.ContainsKey(key))
            _cache[key] = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        _cache[key].Add(atlasName);
    }

    /// <summary>
    /// Descarga todos los atlas registrados para una región.
    /// Llamar al salir de la región (OnRegionChanged con la región anterior).
    /// </summary>
    public static void UnloadRegion(string regionCode)
    {
        if (string.IsNullOrEmpty(regionCode)) return;
        string key = regionCode.ToUpperInvariant();
        if (!_cache.TryGetValue(key, out var names)) return;

        foreach (string name in names)
        {
            // Verificar que el atlas existe antes de intentar descargarlo
            if (Futile.atlasManager.GetAtlasWithName(name) != null)
            {
                Futile.atlasManager.UnloadAtlas(name);
                Plugin.RSPlugin.log.LogInfo($"[SkyAtlas] Unloaded '{name}' (region {key})");
            }
        }
        _cache.Remove(key);
    }

    /// <summary>
    /// Precarga todos los atlas de sky declarados en [BACKGROUNDS] para la región.
    /// Llamar en OnRegionChanged para que los atlas estén en Futile antes de que
    /// el jugador llegue a cualquier sala con sky — evita freeze al entrar.
    /// LoadGraphic tiene early-return si el atlas ya está cacheado — sin costo extra.
    /// </summary>
    public static void PreloadRegion(string regionCode)
    {
        if (string.IsNullOrEmpty(regionCode)) return;
        var settings = BlendSettingsLoader.GetForRegion(regionCode);
        if (settings == null || !settings._hasBackgroundsSection
            || settings.BackgroundAliases.Count == 0) return;

        string key = regionCode.ToUpperInvariant();

        foreach (var kv in settings.BackgroundAliases)
        {
            // Precargar ambas variantes (ACV y RTV) si están declaradas
            PreloadFile(kv.Value.AcvFile, key);
            PreloadFile(kv.Value.RtvFile, key);
        }

        Plugin.RSPlugin.log.LogInfo(
            $"[SkyAtlas] Preloaded sky atlases for region {key}");
    }

    private static void PreloadFile(string file, string regionKey)
    {
        if (string.IsNullOrEmpty(file)) return;
        string name = System.IO.Path.GetFileNameWithoutExtension(file);
        if (Futile.atlasManager.GetAtlasWithName(name) != null)
        {
            Register(regionKey, name);
            return;  // ya está en Futile
        }
        string path = AssetManager.ResolveFilePath(
            "Illustrations" + System.IO.Path.DirectorySeparatorChar + name + ".png");
        if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return;

        var tex = new UnityEngine.Texture2D(1, 1, UnityEngine.TextureFormat.RGBA32, false);
        AssetManager.SafeWWWLoadTexture(ref tex, "file:///" + path, true, true);
        HeavyTexturesCache.LoadAndCacheAtlasFromTexture(name, tex, false);
        Register(regionKey, name);
        Plugin.RSPlugin.log.LogInfo($"[SkyAtlas] Loaded '{name}' for region {regionKey}");
    }
    public static void UnloadAll()
    {
        foreach (var kv in _cache)
        {
            foreach (string name in kv.Value)
            {
                if (Futile.atlasManager.GetAtlasWithName(name) != null)
                    Futile.atlasManager.UnloadAtlas(name);
            }
        }
        _cache.Clear();
        Plugin.RSPlugin.log.LogInfo("[SkyAtlas] All sky atlases unloaded.");
    }

    /// <summary>
    /// Descarga todas las regiones excepto la indicada.
    /// Útil en OnRegionChanged para limpiar todo lo que no sea la región activa.
    /// </summary>
    public static void UnloadAllExcept(string keepRegion)
    {
        var toRemove = new List<string>();
        foreach (var key in _cache.Keys)
        {
            if (!string.Equals(key, keepRegion, System.StringComparison.OrdinalIgnoreCase))
                toRemove.Add(key);
        }
        foreach (string key in toRemove)
            UnloadRegion(key);
    }
}