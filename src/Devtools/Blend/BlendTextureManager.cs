using UnityEngine;

namespace FilesSetting;

// ════════════════════════════════════════════════════════════════════════
// Gestiona las 4 texturas de paleta necesarias para el blend:
//   texA_s1 / texB_s1 → paleta base y fade de snapA
//   texA_s2 / texB_s2 → paleta base y fade de snapB
//
// Los EffectColors se bakean en las texturas al cargarlas, así la mezcla
// de píxeles los incluye automáticamente sin cálculo extra por frame.
// ════════════════════════════════════════════════════════════════════════
internal static class BlendTextureManager
{
    internal static Texture2D TexA_s1;
    internal static Texture2D TexA_s2;
    internal static Texture2D TexB_s1;
    internal static Texture2D TexB_s2;
    internal static bool      Ready;

<<<<<<< Updated upstream:src/Devtools/Blend/BlendTextureManager.cs
=======
    // Arrays de píxeles cacheados — evitan GetPixel por pixel en el hot path.
    internal static Color32[] PxA_s1;
    internal static Color32[] PxA_s2;
    internal static Color32[] PxB_s1;
    internal static Color32[] PxB_s2;

    // ── Terrain palette blend ─────────────────────────────────────────────
    // Píxeles horneados de terrain A y B al inicio del blend.
    // Incluyen el fade palette de cada snapshot aplicado con su opacidad.
    internal static Color[] TerrainPxA = null;
    internal static Color[] TerrainPxB = null;
    internal static bool    TerrainReady => TerrainPxA != null && TerrainPxB != null;

    // Hornea los texturePixels de terrain para snapA y snapB.
    // Llamar desde Attach/AttachWithExternalT igual que Load.
    internal static void LoadTerrain(RoomCamera cam,
        RainCycles.Snapshot.SettingsSnapshot snapA,
        RainCycles.Snapshot.SettingsSnapshot snapB)
    {
        TerrainPxA = null;
        TerrainPxB = null;

        if (cam?.room == null) return;

        TerrainPxA = BakeTerrainPixels(cam, snapA);
        TerrainPxB = BakeTerrainPixels(cam, snapB);
    }

    // Interpola los píxeles de terrain entre A y B según t y sube al shader.
    // Opera sobre cam.terrainPalette.texturePixels que vanilla ya construyó con fade.
    // Lerpa hasta Mathf.Min(PxA, PxB, cam) para tolerar tamaños distintos entre snaps.
    internal static void MixTerrainPalette(RoomCamera cam, float t)
    {
        if (!TerrainReady) return;
        if (cam?.terrainPalette == null) return;
        if (cam.terrainPalette.texturePixels == null) return;

        int len = Mathf.Min(TerrainPxA.Length,
                  Mathf.Min(TerrainPxB.Length,
                            cam.terrainPalette.texturePixels.Length));

        for (int i = 0; i < len; i++)
        {
            Color a = TerrainPxA[i];
            Color b = TerrainPxB[i];
            cam.terrainPalette.texturePixels[i] = new Color(
                a.r + (b.r - a.r) * t,
                a.g + (b.g - a.g) * t,
                a.b + (b.b - a.b) * t,
                1f);
        }

        cam.terrainPalette.texture.SetPixels(cam.terrainPalette.texturePixels);
        cam.terrainPalette.texture.Apply(false);
        Shader.SetGlobalTexture("_terrainPalette", cam.terrainPalette.texture);
    }

    // Hornea los texturePixels de un snapshot de terrain:
    // construye TerrainPalette temporal, llama UpdateFade con la opacidad del screen,
    // captura texturePixels y destruye el objeto temporal.
    private static Color[] BakeTerrainPixels(RoomCamera cam,
        RainCycles.Snapshot.SettingsSnapshot snap)
    {
        if (snap == null || !snap._hasTerrainPalette ||
            string.IsNullOrEmpty(snap.TerrainPaletteName)) return null;

        // Hornear con fade palette del snap (igual que vanilla construye cam.terrainPalette)
        // → texturePixels tiene el mismo tamaño y composición que cam.terrainPalette.texturePixels.
        // Si A y B tienen distinto fade → distinto tamaño → MixTerrainPalette toma Mathf.Min,
        // el exceso de pixels queda como estaba en cam.terrainPalette (fade parcial visible).
        string fadeName = snap._hasTerrainFadePalette ? snap.TerrainFadePaletteName : null;

        TerrainPalette tp = null;
        try { tp = new TerrainPalette(snap.TerrainPaletteName, fadeName); }
        catch { return null; }

        int   camIdx = cam.currentCameraPosition;
        float fade   = (fadeName != null && camIdx < snap.TerrainFadeOpacities.Length)
                       ? snap.TerrainFadeOpacities[camIdx] : 0f;

        tp.UpdateFade(fade, cam.mushroomMode, cam.DarkPalette, cam.ghostMode, cam.rotMode);

        var pixels = (Color[])tp.texturePixels.Clone();
        tp.Dispose();
        return pixels;
    }

    internal static void DestroyTerrain()
    {
        TerrainPxA = null;
        TerrainPxB = null;
    }

>>>>>>> Stashed changes:src/Core/BlendTextureManager.cs
    internal static void Load(RoomCamera cam, SettingsSnapshot snapA, SettingsSnapshot snapB,
                              SettingsSnapshot snapOrigin = null, bool applyFade = true)
    {
        Destroy();

        // snapOrigin es el estado visual actual de la sala (puede ser distinto del archivo de disco
        // cuando el clock ya ha avanzado varias sub-fases). Si no se provee, usar roomSettings.
        var rs = cam.room?.roomSettings;
        int   realPalette   = snapOrigin?.Palette    ?? rs?.Palette    ?? snapA.Palette;
        int   realFadePal   = snapOrigin != null
                              ? (snapOrigin._hasFadePalette ? snapOrigin.FadePaletteID : realPalette)
                              : (rs?.fadePalette != null ? rs.fadePalette.palette : realPalette);
        float realFadeBlend = (rs?.fadePalette != null && cam.currentCameraPosition < rs.fadePalette.fades.Length)
                              ? rs.fadePalette.fades[cam.currentCameraPosition]
                              : 0f;
        int   realEffColorA = snapOrigin?.EffectColorA ?? rs?.EffectColorA ?? 0;
        int   realEffColorB = snapOrigin?.EffectColorB ?? rs?.EffectColorB ?? 0;

        // Preservar terrain palette durante horneado — ChangeBothPalettes→ApplyPalette
        // vanilla reconstruye cam.terrainPalette desde rs → no debe quedar contaminado.
        string savedTerrainName = rs?.TerrainPalette;
        var    savedTerrainFade = rs?.terrainFadePalette;

        // snapA: hornear paleta + fade con sus EffectColors
        // Usar _hasFadePalette para detectar si el fade está declarado —
        // FadePaletteID puede ser 0, que es una paleta válida en Rain World.
        // La condición "> 0" trataba ID=0 como "no declarado", perdiendo el fade.
        cam.ChangeBothPalettes(snapA.Palette,
            snapA._hasFadePalette ? snapA.FadePaletteID : snapA.Palette, 0f);
        cam.ApplyEffectColorsToAllPaletteTextures(snapA.EffectColorA, snapA.EffectColorB);
        TexA_s1  = Copy(cam.fadeTexA);
        TexB_s1  = Copy(cam.fadeTexB);

        // snapB: hornear paleta + fade con sus EffectColors
        cam.ChangeBothPalettes(snapB.Palette,
            snapB._hasFadePalette ? snapB.FadePaletteID : snapB.Palette, 0f);
        cam.ApplyEffectColorsToAllPaletteTextures(snapB.EffectColorA, snapB.EffectColorB);
        TexA_s2  = Copy(cam.fadeTexA);
        TexB_s2  = Copy(cam.fadeTexB);

        // Restaurar la paleta real de la cámara — sin esto el horneado deja rastros visibles
        cam.ChangeBothPalettes(realPalette, realFadePal, realFadeBlend);
        cam.ApplyEffectColorsToAllPaletteTextures(realEffColorA, realEffColorB);

        // Restaurar terrain palette — el horneado puede haberla contaminado via ApplyPalette
        if (rs != null)
        {
            rs.TerrainPalette     = savedTerrainName;
            rs.terrainFadePalette = savedTerrainFade;
        }

        // applyFade=false durante transiciones de sala: la cámara aún renderiza la sala
        // anterior y subir texturas a GPU en ese momento causa un flash de un frame.
        // El caller es responsable de llamar ApplyFade cuando la sala ya es visible.
        if (applyFade) cam.ApplyFade();

        Ready = true;

        // Hornear terrain palette A y B para blend pixel-a-pixel
        LoadTerrain(cam, snapA, snapB);
    }

    // Mezcla los píxeles de las 4 texturas según t y aplica el resultado
    // a fadeTexA/B de la cámara antes de que el juego llame ApplyFade.
    internal static void MixPalettes(RoomCamera cam, float t)
    {
        if (!Ready || TexA_s1 == null || TexA_s2 == null) return;

        for (int x = 0; x < 32; x++)
        {
            for (int y = 0; y < 16; y++)
            {
                cam.fadeTexA.SetPixel(x, y,
                    Color.Lerp(TexA_s1.GetPixel(x, y), TexA_s2.GetPixel(x, y), t));

                if (TexB_s1 != null && TexB_s2 != null)
                    cam.fadeTexB.SetPixel(x, y,
                        Color.Lerp(TexB_s1.GetPixel(x, y), TexB_s2.GetPixel(x, y), t));
            }
        }

        cam.fadeTexA.Apply(false);
        if (TexB_s1 != null) cam.fadeTexB.Apply(false);
    }

    /// <summary>
    /// Restaura los píxeles de fadeTexA/B de la cámara al estado del snapshot A
    /// (el estado "antes del blend"). Llamar antes de Destroy() en DetachAndRestore.
    /// </summary>
    internal static void RestoreOriginalTextures(RoomCamera cam)
    {
        if (!Ready || TexA_s1 == null || cam == null) return;

        // Copiar los píxeles originales (snapA) directamente en fadeTexA/B de la cámara.
        // Esto evita el problema de ChangeBothPalettes haciendo early-return cuando
        // la paleta ID es la misma que ya está cargada.
        if (cam.fadeTexA != null && TexA_s1 != null)
        {
            cam.fadeTexA.SetPixels(TexA_s1.GetPixels());
            cam.fadeTexA.Apply(false);
        }
        if (cam.fadeTexB != null && TexB_s1 != null)
        {
            cam.fadeTexB.SetPixels(TexB_s1.GetPixels());
            cam.fadeTexB.Apply(false);
        }
    }

    internal static void Destroy()
    {
        if (TexA_s1 != null) Object.Destroy(TexA_s1);
        if (TexA_s2 != null) Object.Destroy(TexA_s2);
        if (TexB_s1 != null) Object.Destroy(TexB_s1);
        if (TexB_s2 != null) Object.Destroy(TexB_s2);
        TexA_s1 = TexA_s2 = TexB_s1 = TexB_s2 = null;
        Ready = false;
        DestroyTerrain();
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