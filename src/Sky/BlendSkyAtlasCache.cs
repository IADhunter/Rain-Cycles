using System.Collections.Generic;
using System.IO;

namespace RainCycles.Sky;

// Registra y descarga atlas de cielo por región para evitar acumulación en VRAM.
public static class BlendSkyAtlasCache
{
    private static readonly Dictionary<string, HashSet<string>> _cache =
        new Dictionary<string, HashSet<string>>(System.StringComparer.OrdinalIgnoreCase);

    public static void Register(string regionCode, string atlasName)
    {
        if (string.IsNullOrEmpty(regionCode) || string.IsNullOrEmpty(atlasName)) return;
        string key = regionCode.ToUpperInvariant();
        if (!_cache.ContainsKey(key))
            _cache[key] = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        _cache[key].Add(atlasName);
    }

    public static void UnloadRegion(string regionCode)
    {
        if (string.IsNullOrEmpty(regionCode)) return;
        string key = regionCode.ToUpperInvariant();
        if (!_cache.TryGetValue(key, out var names)) return;

        foreach (string name in names)
        {
            if (Futile.atlasManager.GetAtlasWithName(name) != null)
                Futile.atlasManager.UnloadAtlas(name);
        }
        _cache.Remove(key);
    }

    public static void PreloadRegion(string regionCode)
    {
        if (string.IsNullOrEmpty(regionCode)) return;
        var settings = BlendSettingsLoader.GetForRegion(regionCode);
        if (settings == null || !settings.HasBackgroundsSection
            || settings.BackgroundAliases.Count == 0) return;

        string key = regionCode.ToUpperInvariant();
        string modName = settings.SelectedModName ?? "";

        foreach (var viewDict in settings.BackgroundAliases)
        {
            foreach (var aliasKv in viewDict.Value)
            {
                string file = aliasKv.Value;
                if (!string.IsNullOrEmpty(file))
                    PreloadFile(file, key, modName);
            }
        }
    }

    private static void PreloadFile(string file, string regionKey, string modName)
    {
        if (string.IsNullOrEmpty(file)) return;
        
        string baseName = Path.GetFileNameWithoutExtension(file);
        
        // ================================================================
        // CARGAR DIRECTAMENTE DESDE EL MOD ESPECIFICADO
        // Solo si el mod existe (tiene modinfo.json válido)
        // ================================================================
        string imagePath = ResolveIllustrationPath(modName, baseName);
        if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
        {
            RSPlugin.log.LogDebug($"[BlendSkyAtlasCache] No se encontró imagen: {baseName} en mod {modName}");
            return;
        }

        // Verificar si ya está cargado
        if (Futile.atlasManager.DoesContainAtlas(baseName))
        {
            Register(regionKey, baseName);
            RSPlugin.log.LogDebug($"[BlendSkyAtlasCache] Imagen ya cargada: {baseName}");
            return;
        }

        // Cargar textura directamente desde el archivo
        var tex = new UnityEngine.Texture2D(1, 1, UnityEngine.TextureFormat.RGBA32, false);
        AssetManager.SafeWWWLoadTexture(ref tex, "file:///" + imagePath, true, true);
        
        // Cargar en Futile con el nombre base
        HeavyTexturesCache.LoadAndCacheAtlasFromTexture(baseName, tex, false);
        Register(regionKey, baseName);
        
        RSPlugin.log.LogInfo($"[BlendSkyAtlasCache] Imagen cargada DESDE: {imagePath} → {baseName}");
    }

    // ================================================================
    // RESOLVER RUTA DE IMAGEN SEGÚN EL MOD
    // ================================================================
    private static string ResolveIllustrationPath(string modName, string imageName)
    {
        if (string.IsNullOrEmpty(modName) || string.IsNullOrEmpty(imageName))
            return null;

        RSPlugin.log.LogDebug($"[BlendSkyAtlasCache] Buscando {imageName}.png en mod: {modName}");

        // 1. Buscar por nombre del mod (modinfo.json "name")
        foreach (var mod in ModManager.ActiveMods)
        {
            string realModName = GetModNameFromModInfo(mod.path);
            if (string.IsNullOrEmpty(realModName)) continue;

            if (string.Equals(realModName, modName, System.StringComparison.OrdinalIgnoreCase))
            {
                string candidate = Path.Combine(mod.path, "Illustrations", imageName + ".png");
                if (File.Exists(candidate))
                {
                    RSPlugin.log.LogDebug($"[BlendSkyAtlasCache] Encontrada imagen en mod: {mod.path}");
                    return candidate;
                }
                
                // Buscar en subcarpetas de Illustrations
                string illustrationsDir = Path.Combine(mod.path, "Illustrations");
                if (Directory.Exists(illustrationsDir))
                {
                    foreach (string file in Directory.GetFiles(illustrationsDir, imageName + ".png", SearchOption.AllDirectories))
                    {
                        RSPlugin.log.LogDebug($"[BlendSkyAtlasCache] Encontrada imagen en subcarpeta: {file}");
                        return file;
                    }
                }
            }
        }

        RSPlugin.log.LogWarning($"[BlendSkyAtlasCache] No se encontró el mod '{modName}' con modinfo.json válido");
        return null;
    }

    // ================================================================
    // OBTENER NOMBRE REAL DEL MOD DESDE MODINFO.JSON
    // ================================================================
    private static string GetModNameFromModInfo(string modPath)
    {
        try
        {
            string modInfoPath = Path.Combine(modPath, "modinfo.json");
            if (!File.Exists(modInfoPath)) return null;

            string json = File.ReadAllText(modInfoPath);
            
            // Buscar "name": "xxx"
            int nameIndex = json.IndexOf("\"name\"", System.StringComparison.OrdinalIgnoreCase);
            if (nameIndex < 0) return null;

            int colonIndex = json.IndexOf(':', nameIndex);
            if (colonIndex < 0) return null;

            int startQuote = json.IndexOf('"', colonIndex + 1);
            if (startQuote < 0) return null;

            int endQuote = json.IndexOf('"', startQuote + 1);
            if (endQuote < 0) return null;

            return json.Substring(startQuote + 1, endQuote - startQuote - 1);
        }
        catch
        {
            return null;
        }
    }
}