using System;
using UnityEngine;

namespace RainCycles.Core;

// Gestiona las 4 texturas de paleta necesarias para el blend:
internal static class BlendTextureManager
{
    internal static Texture2D TexA_s1;
    internal static Texture2D TexA_s2;
    internal static Texture2D TexB_s1;
    internal static Texture2D TexB_s2;
    internal static bool      Ready;

    // Arrays de píxeles cacheados — evitan GetPixel por pixel en el hot path.
    internal static Color32[] PxA_s1;
    internal static Color32[] PxA_s2;
    internal static Color32[] PxB_s1;
    internal static Color32[] PxB_s2;

    // ── Terrain palette blend ─────────────────────────────────────────────
    internal static TerrainPalette TerrainPalA = null;
    internal static TerrainPalette TerrainPalB = null;
    internal static bool TerrainReady => TerrainPalA != null && TerrainPalB != null;

    internal static void LoadTerrainTextures(
        RainCycles.Snapshot.SettingsSnapshot snapA,
        RainCycles.Snapshot.SettingsSnapshot snapB)
    {
        TerrainPalette newA = null;
        TerrainPalette newB = null;

        if (snapA._hasTerrainPalette && !string.IsNullOrEmpty(snapA.TerrainPaletteName))
        {
            string fadeA = snapA._hasTerrainFadePalette ? snapA.TerrainFadePaletteName : null;
            try { newA = new TerrainPalette(snapA.TerrainPaletteName, fadeA); }
            catch { newA = null; }
            if (newA != null && newA.fadePal != null)
            {
                var basePxA = (Color[])newA.texturePixels.Clone();
                newA.UpdateFade(1f, 0f, 0f, 0f, 0f);
                Array.Copy(basePxA, newA.texturePixels, basePxA.Length);
            }
        }

        if (snapB._hasTerrainPalette && !string.IsNullOrEmpty(snapB.TerrainPaletteName))
        {
            string fadeB = snapB._hasTerrainFadePalette ? snapB.TerrainFadePaletteName : null;
            try { newB = new TerrainPalette(snapB.TerrainPaletteName, fadeB); }
            catch { newB = null; }
            if (newB != null && newB.fadePal != null)
            {
                var basePxB = (Color[])newB.texturePixels.Clone();
                newB.UpdateFade(1f, 0f, 0f, 0f, 0f);
                Array.Copy(basePxB, newB.texturePixels, basePxB.Length);
            }
        }

        var oldA = TerrainPalA;
        var oldB = TerrainPalB;
        TerrainPalA = newA;
        TerrainPalB = newB;

        if (oldA != null) oldA.Dispose();
        if (oldB != null) oldB.Dispose();
    }

    internal static void MixTerrainPalette(RoomCamera cam, float t,
        RainCycles.Snapshot.SettingsSnapshot snapA,
        RainCycles.Snapshot.SettingsSnapshot snapB)
    {
        if (!TerrainReady || cam?.terrainPalette == null) return;
        if (TerrainPalA.texturePixels == null || TerrainPalB.texturePixels == null) return;

        var tp = cam.terrainPalette;
        if (tp.texturePixels == null) return;

        int camIdx = cam.currentCameraPosition;

        float fadeA = camIdx < snapA.TerrainFadeOpacities.Length
                      ? snapA.TerrainFadeOpacities[camIdx] : 0f;
        float fadeB = camIdx < snapB.TerrainFadeOpacities.Length
                      ? snapB.TerrainFadeOpacities[camIdx] : 0f;

        var pxA = TerrainPalA.texturePixels;
        var pxB = TerrainPalB.texturePixels;

        if (fadeA > 0f && TerrainPalA.fadePixels != null)
        {
            var fadedA = (Color[])pxA.Clone();
            TerrainPalette.LerpColors(fadedA, TerrainPalA.fadePixels, fadeA);
            pxA = fadedA;
        }
        if (fadeB > 0f && TerrainPalB.fadePixels != null)
        {
            var fadedB = (Color[])pxB.Clone();
            TerrainPalette.LerpColors(fadedB, TerrainPalB.fadePixels, fadeB);
            pxB = fadedB;
        }

        int len = Mathf.Min(pxA.Length, Mathf.Min(pxB.Length, tp.texturePixels.Length));

        for (int i = 0; i < len; i++)
        {
            Color a = pxA[i];
            Color b = pxB[i];
            tp.texturePixels[i] = new Color(
                a.r + (b.r - a.r) * t,
                a.g + (b.g - a.g) * t,
                a.b + (b.b - a.b) * t,
                1f);
        }

        tp.texture.SetPixels(tp.texturePixels);
        tp.texture.Apply(false);
        Shader.SetGlobalTexture("_terrainPalette", tp.texture);
    }

    internal static void DestroyTerrainTextures()
    {
        if (TerrainPalA != null) { TerrainPalA.Dispose(); TerrainPalA = null; }
        if (TerrainPalB != null) { TerrainPalB.Dispose(); TerrainPalB = null; }
    }

    internal static void Load(RoomCamera cam, SettingsSnapshot snapA, SettingsSnapshot snapB,
                              SettingsSnapshot snapOrigin = null, bool applyFade = true)
    {
        Destroy();

        var rs = cam.room?.roomSettings;
        int   realPalette   = snapOrigin?.Palette    ?? rs?.Palette    ?? snapA.Palette;
        int   realFadePal   = snapOrigin != null
                              ? (snapOrigin._hasFadePalette ? snapOrigin.FadePaletteID : realPalette)
                              : (rs?.fadePalette != null ? rs.fadePalette.palette : realPalette);
        float realFadeBlend = (rs?.fadePalette != null && cam.currentCameraPosition < rs.fadePalette.fades.Length)
                              ? rs.fadePalette.fades[cam.currentCameraPosition]
                              : 0f;

        string savedTerrainName = rs?.TerrainPalette;
        var    savedTerrainFade = rs?.terrainFadePalette;

        // snapA: hornear paleta + fade con sus EffectColors
        cam.ChangeBothPalettes(snapA.Palette,
            snapA._hasFadePalette ? snapA.FadePaletteID : snapA.Palette, 0f);
        TexA_s1  = Copy(cam.fadeTexA);
        TexB_s1  = Copy(cam.fadeTexB);
        PxA_s1   = TexA_s1?.GetPixels32();
        PxB_s1   = TexB_s1?.GetPixels32();

        // snapB: hornear paleta + fade con sus EffectColors
        cam.ChangeBothPalettes(snapB.Palette,
            snapB._hasFadePalette ? snapB.FadePaletteID : snapB.Palette, 0f);
        TexA_s2  = Copy(cam.fadeTexA);
        TexB_s2  = Copy(cam.fadeTexB);
        PxA_s2   = TexA_s2?.GetPixels32();
        PxB_s2   = TexB_s2?.GetPixels32();

        // Restaurar la paleta real de la cámara
        cam.ChangeBothPalettes(realPalette, realFadePal, realFadeBlend);

        if (rs != null)
        {
            rs.TerrainPalette     = savedTerrainName;
            rs.terrainFadePalette = savedTerrainFade;
        }

        if (applyFade) cam.ApplyFade();

        Ready = true;

        LoadTerrainTextures(snapA, snapB);
    }

    internal static void MixPalettes(RoomCamera cam, float t)
    {
        if (!Ready || PxA_s1 == null || PxA_s2 == null) return;

        int len = PxA_s1.Length;
        var outA = new Color32[len];

        for (int i = 0; i < len; i++)
        {
            Color32 a1 = PxA_s1[i];
            Color32 a2 = PxA_s2[i];
            outA[i] = new Color32(
                (byte)(a1.r + (a2.r - a1.r) * t),
                (byte)(a1.g + (a2.g - a1.g) * t),
                (byte)(a1.b + (a2.b - a1.b) * t),
                255);
        }

        cam.fadeTexA.SetPixels32(outA);
        cam.fadeTexA.Apply(false);

        if (PxB_s1 != null && PxB_s2 != null)
        {
            var outB = new Color32[len];
            for (int i = 0; i < len; i++)
            {
                Color32 b1 = PxB_s1[i];
                Color32 b2 = PxB_s2[i];
                outB[i] = new Color32(
                    (byte)(b1.r + (b2.r - b1.r) * t),
                    (byte)(b1.g + (b2.g - b1.g) * t),
                    (byte)(b1.b + (b2.b - b1.b) * t),
                    255);
            }
            cam.fadeTexB.SetPixels32(outB);
            cam.fadeTexB.Apply(false);
        }
    }

    internal static void Destroy()
    {
        if (TexA_s1 != null) UnityEngine.Object.Destroy(TexA_s1);
        if (TexA_s2 != null) UnityEngine.Object.Destroy(TexA_s2);
        if (TexB_s1 != null) UnityEngine.Object.Destroy(TexB_s1);
        if (TexB_s2 != null) UnityEngine.Object.Destroy(TexB_s2);
        TexA_s1 = TexA_s2 = TexB_s1 = TexB_s2 = null;
        PxA_s1  = PxA_s2  = PxB_s1  = PxB_s2  = null;
        Ready = false;
    }

    private static Texture2D Copy(Texture2D src)
    {
        if (src == null) return null;
        var copy = new Texture2D(src.width, src.height, src.format, false);
        copy.SetPixels(src.GetPixels());
        copy.Apply(false);
        return copy;
    }
}