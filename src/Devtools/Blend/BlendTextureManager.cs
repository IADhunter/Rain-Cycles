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

    internal static void Load(RoomCamera cam, SettingsSnapshot snapA, SettingsSnapshot snapB,
                              SettingsSnapshot snapOrigin = null, bool applyFade = true)
    {
        Destroy();

        // snapOrigin es el estado visual actual de la sala (puede ser distinto del archivo de disco
        // cuando el clock ya ha avanzado varias sub-fases). Si no se provee, usar roomSettings.
        var rs = cam.room?.roomSettings;
        int   realPalette   = snapOrigin?.Palette    ?? rs?.Palette    ?? snapA.Palette;
        int   realFadePal   = snapOrigin != null
                              ? (snapOrigin.FadePaletteID > 0 ? snapOrigin.FadePaletteID : realPalette)
                              : (rs?.fadePalette != null ? rs.fadePalette.palette : realPalette);
        float realFadeBlend = (rs?.fadePalette != null && cam.currentCameraPosition < rs.fadePalette.fades.Length)
                              ? rs.fadePalette.fades[cam.currentCameraPosition]
                              : 0f;
        int   realEffColorA = snapOrigin?.EffectColorA ?? rs?.EffectColorA ?? 0;
        int   realEffColorB = snapOrigin?.EffectColorB ?? rs?.EffectColorB ?? 0;

        Plugin.RSPlugin.log.LogInfo(
            $"[BlendTexMgr] Load: cam.room={cam.room?.abstractRoom?.name} applyFade={applyFade} " +
            $"realPalette={realPalette} snapA.Palette={snapA.Palette} snapB.Palette={snapB.Palette}");

        // snapA: hornear paleta + fade con sus EffectColors
        cam.ChangeBothPalettes(snapA.Palette,
            snapA.FadePaletteID > 0 ? snapA.FadePaletteID : snapA.Palette, 0f);
        cam.ApplyEffectColorsToAllPaletteTextures(snapA.EffectColorA, snapA.EffectColorB);
        TexA_s1  = Copy(cam.fadeTexA);
        TexB_s1  = Copy(cam.fadeTexB);

        // snapB: hornear paleta + fade con sus EffectColors
        cam.ChangeBothPalettes(snapB.Palette,
            snapB.FadePaletteID > 0 ? snapB.FadePaletteID : snapB.Palette, 0f);
        cam.ApplyEffectColorsToAllPaletteTextures(snapB.EffectColorA, snapB.EffectColorB);
        TexA_s2  = Copy(cam.fadeTexA);
        TexB_s2  = Copy(cam.fadeTexB);

        // Restaurar la paleta real de la cámara — sin esto el horneado deja rastros visibles
        cam.ChangeBothPalettes(realPalette, realFadePal, realFadeBlend);
        cam.ApplyEffectColorsToAllPaletteTextures(realEffColorA, realEffColorB);
        // applyFade=false durante transiciones de sala: la cámara aún renderiza la sala
        // anterior y subir texturas a GPU en ese momento causa un flash de un frame.
        // El caller es responsable de llamar ApplyFade cuando la sala ya es visible.
        if (applyFade) cam.ApplyFade();

        Ready = true;
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