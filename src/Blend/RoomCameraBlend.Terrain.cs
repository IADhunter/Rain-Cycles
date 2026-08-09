using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using RWCustom;
using RainCycles.Snapshot;
using RainCycles.Core;

namespace RainCycles.Blend;

public static partial class RoomCameraExtensions
{
    // ================================================================
    // CACHE: GRID POR SALA + ESTADOS
    // ================================================================

    private class TerrainGridInfo
    {
        public IntVector2 targetSize;
        public TerrainPalette.PaletteInfo rotInfo;
    }

    private class CachedTerrainState
    {
        public TerrainPalette terrainPalette;
        public float[] fadeOpacities;
    }

    private static readonly Dictionary<string, TerrainGridInfo> _terrainGridCache =
        new Dictionary<string, TerrainGridInfo>(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<(string roomName, int state), CachedTerrainState> _terrainStateCache =
        new Dictionary<(string, int), CachedTerrainState>();

    // ================================================================
    // LIMPIEZA DE CACHE
    // ================================================================

    internal static void UnloadRoomTerrainCache(string roomName)
    {
        if (string.IsNullOrEmpty(roomName)) return;

        _terrainGridCache.Remove(roomName);

        var keysToRemove = new List<(string, int)>();
        foreach (var key in _terrainStateCache.Keys)
        {
            if (key.roomName == roomName) keysToRemove.Add(key);
        }
        foreach (var key in keysToRemove)
        {
            if (_terrainStateCache.TryGetValue(key, out var state))
            {
                state.terrainPalette?.Dispose();
            }
            _terrainStateCache.Remove(key);
        }
    }

    internal static void ClearAllTerrainCaches()
    {
        foreach (var kv in _terrainStateCache)
        {
            kv.Value.terrainPalette?.Dispose();
        }
        _terrainGridCache.Clear();
        _terrainStateCache.Clear();
    }

    // ================================================================
    // LECTURA DE NOMBRES/OPACIDADES DESDE SETTINGSSNAPSHOT
    // ================================================================

    private static string GetTerrainMainName(string roomName, int state)
    {
        string path = StateFileResolver.GetRainStateSettingsFile(roomName, state);
        if (path == null) return null;
        var snap = SettingsSnapshot.GetCached(path, roomName);
        return (snap != null && snap._hasTerrainPalette) ? snap.TerrainPaletteName : null;
    }

    private static string GetTerrainFadeName(string roomName, int state)
    {
        string path = StateFileResolver.GetRainStateSettingsFile(roomName, state);
        if (path == null) return null;
        var snap = SettingsSnapshot.GetCached(path, roomName);
        return (snap != null && snap._hasTerrainFadePalette) ? snap.TerrainFadePaletteName : null;
    }

    private static float[] GetTerrainFadeOpacities(string roomName, int state)
    {
        string path = StateFileResolver.GetRainStateSettingsFile(roomName, state);
        if (path == null) return null;
        var snap = SettingsSnapshot.GetCached(path, roomName);
        return snap?.TerrainFadeOpacities;
    }

    private static float GetFadeOpacityForCamera(float[] fadeOpacities, int camIdx)
    {
        if (fadeOpacities != null && camIdx >= 0 && camIdx < fadeOpacities.Length)
            return fadeOpacities[camIdx];
        return 0f;
    }

    // ================================================================
    // BUG DE VANILLA - NORMALIZAR PALETTESIZE
    // ================================================================

    private static void NormalizeMainSize(TerrainPalette.PaletteInfo info, string name)
    {
        if (info == null || string.IsNullOrEmpty(name)) return;

        int expectedLen = info.PaletteSize.x * info.PaletteSize.y;
        if (info.main.Length == expectedLen) return;

        Texture2D rawMainTex = info.LoadTex(name);
        if (rawMainTex == null)
        {
            RSPlugin.log.LogWarning($"[TerrainBlend] No se pudo verificar tamaño real de '{name}', se omite normalización.");
            return;
        }

        var trueSize = new IntVector2(rawMainTex.width, rawMainTex.height);
        if (trueSize.x * trueSize.y != info.main.Length)
        {
            RSPlugin.log.LogWarning($"[TerrainBlend] Tamaño inconsistente para '{name}' (main={info.main.Length}px, textura={trueSize.x}x{trueSize.y}), se omite normalización.");
            return;
        }

        info.PaletteSize = trueSize;
    }

    // ================================================================
    // CONSTRUCCIÓN DEL GRID (solo main de los 4 estados)
    // ================================================================

    private static TerrainGridInfo GetOrCreateTerrainGrid(string roomName)
    {
        if (string.IsNullOrEmpty(roomName)) return null;

        if (_terrainGridCache.TryGetValue(roomName, out var existing))
            return existing;

        var sw = Stopwatch.StartNew();

        var builtStates = new Dictionary<int, (TerrainPalette pal, float[] fadeOp)>();
        IntVector2 maxSize = default;
        bool found = false;

        for (int state = 1; state <= 4; state++)
        {
            string mainName = GetTerrainMainName(roomName, state);
            if (string.IsNullOrEmpty(mainName)) continue;

            var swState = Stopwatch.StartNew();
            try
            {
                string fadeName = GetTerrainFadeName(roomName, state);
                var terrainPal = new TerrainPalette(mainName, fadeName);
                swState.Stop();

                NormalizeMainSize(terrainPal.mainPal, mainName);
                if (terrainPal.fadePal != null) NormalizeMainSize(terrainPal.fadePal, fadeName);
                NormalizeMainSize(terrainPal.rotPal, "rot");

                var thisSize = terrainPal.mainPal.PaletteSize;
                if (!found) { maxSize = thisSize; found = true; }
                else maxSize = new IntVector2(Mathf.Max(maxSize.x, thisSize.x), Mathf.Max(maxSize.y, thisSize.y));

                float[] fadeOp = GetTerrainFadeOpacities(roomName, state);
                builtStates[state] = (terrainPal, fadeOp);
            }
            catch (Exception ex)
            {
                swState.Stop();
                RSPlugin.log.LogWarning($"[TerrainBlend] No se pudo cargar terrain de {roomName} estado {state} ({swState.ElapsedMilliseconds} ms antes del fallo): {ex.Message}");
            }
        }

        if (!found)
        {
            sw.Stop();
            return null;
        }

        var grid = new TerrainGridInfo { targetSize = maxSize, rotInfo = builtStates.Values.First().pal.rotPal };
        _terrainGridCache[roomName] = grid;

        var swResize = Stopwatch.StartNew();
        foreach (var kvp in builtStates)
        {
            int state = kvp.Key;
            var (terrainPal, fadeOp) = kvp.Value;

            terrainPal.mainPal.Resize(maxSize);
            terrainPal.fadePal?.Resize(maxSize);
            terrainPal.rotPal.Resize(maxSize);

            int gridTotal = maxSize.x * maxSize.y;
            terrainPal.texturePixels = new Color[gridTotal];
            terrainPal.fadePixels = new Color[gridTotal];
            terrainPal.rotPixels = null;

            if (terrainPal.texture.width != maxSize.x || terrainPal.texture.height != maxSize.y)
            {
                UnityEngine.Object.Destroy(terrainPal.texture);
                terrainPal.texture = new Texture2D(maxSize.x, maxSize.y, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp
                };
            }

            _terrainStateCache[(roomName, state)] = new CachedTerrainState
            {
                terrainPalette = terrainPal,
                fadeOpacities = fadeOp
            };
        }
        swResize.Stop();
        sw.Stop();

        return grid;
    }

    private static CachedTerrainState GetCachedTerrainState(string roomName, int state)
    {
        _terrainStateCache.TryGetValue((roomName, state), out var cached);
        return cached;
    }

    // ================================================================
    // BLEND A↔B EN CPU
    // ================================================================

    private static void BlendTerrainPixels(Color[] pixelsA, Color[] pixelsB, float t, int width, Color[] result)
    {
        int len = pixelsA.Length;
        for (int i = 0; i < len; i++)
        {
            if (i < width && i == 4)
                result[i] = BlendSandstormPixel(pixelsA[i], pixelsB[i], t);
            else
                result[i] = Color.Lerp(pixelsA[i], pixelsB[i], t);
        }
    }

    private static Color BlendSandstormPixel(Color a, Color b, float t)
    {
        bool aIsBlack = a.r < 0.001f && a.g < 0.001f && a.b < 0.001f;
        bool bIsBlack = b.r < 0.001f && b.g < 0.001f && b.b < 0.001f;

        if (aIsBlack && !bIsBlack) return (t < 0.5f) ? Color.black : b;
        if (bIsBlack && !aIsBlack) return (t < 0.5f) ? a : Color.black;

        return Color.Lerp(a, b, t);
    }

    // ================================================================
    // TEXTURA FINAL POR CÁMARA
    // ================================================================

    private static void EnsureTerrainTexture(CameraBlendData camData, int w, int h)
    {
        if (camData.terrainBlendedTexture != null &&
            camData.terrainBlendedTexture.width == w &&
            camData.terrainBlendedTexture.height == h)
            return;

        if (camData.terrainBlendedTexture != null)
            UnityEngine.Object.Destroy(camData.terrainBlendedTexture);

        camData.terrainBlendedTexture = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp
        };
    }

    // ================================================================
    // BUFFERS DE TRABAJO (evitar allocs cada frame)
    // ================================================================

    private static void EnsureTerrainScratchBuffers(CameraBlendData camData, int total)
    {
        if (camData.terrainScratchTotal == total && camData.terrainScratchA != null)
            return;

        camData.terrainScratchA = new Color[total];
        camData.terrainScratchB = new Color[total];
        camData.terrainResultScratch = new Color[total];
        camData.terrainScratchTotal = total;
    }

    // ================================================================
    // API PÚBLICA - Llamado desde UpdateBlendPalette() cada frame
    // ================================================================

    public static void UpdateBlendTerrain(this RoomCamera cam, float t, int stateA, int stateB, bool isIdle)
    {
        if (cam?.room == null) return;

        string roomName = cam.room.abstractRoom?.name;
        if (string.IsNullOrEmpty(roomName)) return;

        var grid = GetOrCreateTerrainGrid(roomName);
        if (grid == null) return;

        var stateA_cached = GetCachedTerrainState(roomName, stateA);
        if (stateA_cached == null) return;

        var camData = GetOrCreateData(cam);

        float rain = cam.DarkPalette;
        float echo = cam.ghostMode;
        float rot = cam.rotMode;

        int w = grid.targetSize.x;
        int h = grid.targetSize.y;
        int total = w * h;

        EnsureTerrainScratchBuffers(camData, total);

        float fadeA = GetFadeOpacityForCamera(stateA_cached.fadeOpacities, cam.currentCameraPosition);
        var palA = stateA_cached.terrainPalette;
        palA.UpdateFade(fadeA, 0f, rain, echo, rot);
        Color[] pixelsA = palA.texturePixels;

        Color[] finalPixels = pixelsA;

        if (!isIdle && stateA != stateB)
        {
            var stateB_cached = GetCachedTerrainState(roomName, stateB);
            if (stateB_cached != null)
            {
                float fadeB = GetFadeOpacityForCamera(stateB_cached.fadeOpacities, cam.currentCameraPosition);
                var palB = stateB_cached.terrainPalette;
                palB.UpdateFade(fadeB, 0f, rain, echo, rot);
                Color[] pixelsB = palB.texturePixels;

                BlendTerrainPixels(pixelsA, pixelsB, t, w, camData.terrainResultScratch);
                finalPixels = camData.terrainResultScratch;
            }
        }

        EnsureTerrainTexture(camData, w, h);
        camData.terrainBlendedTexture.SetPixels(finalPixels);
        camData.terrainBlendedTexture.Apply(false);

        camData.lastTerrainStateA = stateA;
        camData.lastTerrainStateB = stateB;
        camData.lastTerrainT = t;
    }
}