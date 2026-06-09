using System.Collections.Generic;

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

        foreach (var viewDict in settings.BackgroundAliases)
        {
            foreach (var aliasKv in viewDict.Value)
            {
                string file = aliasKv.Value;
                if (!string.IsNullOrEmpty(file))
                    PreloadFile(file, key);
            }
        }
    }

    private static void PreloadFile(string file, string regionKey)
    {
        if (string.IsNullOrEmpty(file)) return;
        string name = System.IO.Path.GetFileNameWithoutExtension(file);
        if (Futile.atlasManager.GetAtlasWithName(name) != null)
        {
            Register(regionKey, name);
            return;
        }
        string path = AssetManager.ResolveFilePath(
            "Illustrations" + System.IO.Path.DirectorySeparatorChar + name + ".png");
        if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return;

        var tex = new UnityEngine.Texture2D(1, 1, UnityEngine.TextureFormat.RGBA32, false);
        AssetManager.SafeWWWLoadTexture(ref tex, "file:///" + path, true, true);
        HeavyTexturesCache.LoadAndCacheAtlasFromTexture(name, tex, false);
        Register(regionKey, name);
    }
}