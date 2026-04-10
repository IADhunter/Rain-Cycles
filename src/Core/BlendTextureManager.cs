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

    internal static void Load(RoomCamera cam, SettingsSnapshot snapA, SettingsSnapshot snapB,
                              SettingsSnapshot snapOrigin = null, bool applyFade = true)
    {
        Destroy();

        // snapOrigin es el estado visual actual de la sala (puede ser distinto del archivo de disco
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

        // snapA: hornear paleta + fade con sus EffectColors
        cam.ChangeBothPalettes(snapA.Palette,
            snapA._hasFadePalette ? snapA.FadePaletteID : snapA.Palette, 0f);
        cam.ApplyEffectColorsToAllPaletteTextures(snapA.EffectColorA, snapA.EffectColorB);
        TexA_s1  = Copy(cam.fadeTexA);
        TexB_s1  = Copy(cam.fadeTexB);
        PxA_s1   = TexA_s1?.GetPixels32();
        PxB_s1   = TexB_s1?.GetPixels32();

        // snapB: hornear paleta + fade con sus EffectColors
        cam.ChangeBothPalettes(snapB.Palette,
            snapB._hasFadePalette ? snapB.FadePaletteID : snapB.Palette, 0f);
        cam.ApplyEffectColorsToAllPaletteTextures(snapB.EffectColorA, snapB.EffectColorB);
        TexA_s2  = Copy(cam.fadeTexA);
        TexB_s2  = Copy(cam.fadeTexB);
        PxA_s2   = TexA_s2?.GetPixels32();
        PxB_s2   = TexB_s2?.GetPixels32();

        // Restaurar la paleta real de la cámara — sin esto el horneado deja rastros visibles
        cam.ChangeBothPalettes(realPalette, realFadePal, realFadeBlend);
        cam.ApplyEffectColorsToAllPaletteTextures(realEffColorA, realEffColorB);
        // applyFade=false durante transiciones de sala: la cámara aún renderiza la sala
        if (applyFade) cam.ApplyFade();

        Ready = true;
    }

    // Mezcla los píxeles de las 4 texturas según t y aplica el resultado
    internal static void MixPalettes(RoomCamera cam, float t)
    {
        if (!Ready || PxA_s1 == null || PxA_s2 == null) return;

        int len = PxA_s1.Length; // 32 * 16 = 512
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

    // Restaura los píxeles de fadeTexA/B de la cámara al estado del snapshot A (el estado "antes del blend"). Llamar antes de Destroy() en DetachAndRestore.
    internal static void RestoreOriginalTextures(RoomCamera cam)
    {
        if (!Ready || TexA_s1 == null || cam == null) return;

        // Copiar los píxeles originales (snapA) directamente en fadeTexA/B de la cámara.
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