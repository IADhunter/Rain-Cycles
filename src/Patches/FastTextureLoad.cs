using System;
using System.IO;

namespace RainCycles.Patches;

// FAST PATH PARA TEXTURAS LOCALES (05/10/2026)
public static class FastTextureLoad
{
    private static bool _initialized = false;
    private const string FilePrefix = "file:///";

    public static void Init()
    {
        if (_initialized) return;
        On.AssetManager.SafeWWWLoadTexture += OnSafeWWWLoadTexture;
        _initialized = true;
    }

    public static void Terminate()
    {
        if (!_initialized) return;
        On.AssetManager.SafeWWWLoadTexture -= OnSafeWWWLoadTexture;
        _initialized = false;
    }

    private static Texture2D OnSafeWWWLoadTexture(
        On.AssetManager.orig_SafeWWWLoadTexture orig,
        ref Texture2D texture2D, string path, bool clampWrapMode, bool crispPixels)
    {
        if (path != null && path.StartsWith(FilePrefix, StringComparison.Ordinal))
        {
            string local = path.Substring(FilePrefix.Length);
            if (local.Length > 0 && local[0] != '/' && File.Exists(local))
            {
                byte[] data = File.ReadAllBytes(local);

                texture2D.wrapMode = clampWrapMode ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
                if (crispPixels)
                {
                    texture2D.anisoLevel = 0;
                    texture2D.filterMode = FilterMode.Point;
                }

                texture2D.LoadImage(data);
                return texture2D;
            }
        }
        return orig(ref texture2D, path, clampWrapMode, crispPixels);
    }
}
