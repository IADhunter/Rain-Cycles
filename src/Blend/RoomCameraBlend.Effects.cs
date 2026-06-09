using UnityEngine;
using RainCycles.Snapshot;
using RWCustom;

namespace RainCycles.Blend;

public static partial class RoomCameraExtensions
{
    // ═════════════════════════════════════════════════════════════════════
    //  MODIFY EFFECT COLORS A/B - APLICACIÓN MANUAL
    // ═════════════════════════════════════════════════════════════════════

    private static readonly int[] EffectColorALightIndices = { (12 * 32) + 30, (12 * 32) + 31, (13 * 32) + 30, (13 * 32) + 31 };
    private static readonly int[] EffectColorADarkIndices = { (4 * 32) + 30, (4 * 32) + 31, (5 * 32) + 30, (5 * 32) + 31 };
    private static readonly int[] EffectColorBLightIndices = { (10 * 32) + 30, (10 * 32) + 31, (11 * 32) + 30, (11 * 32) + 31 };
    private static readonly int[] EffectColorBDarkIndices = { (2 * 32) + 30, (2 * 32) + 31, (3 * 32) + 30, (3 * 32) + 31 };

    public static void ApplyModifyToPalette(Color32[] palette, SettingsSnapshot snap)
    {
        if (palette == null || palette.Length != 512) return;
        if (snap == null) return;
        
        ApplyModifyToEffectColorInPalette(palette, snap, isEffectColorA: true);
        ApplyModifyToEffectColorInPalette(palette, snap, isEffectColorA: false);
    }

    private static void ApplyModifyToEffectColorInPalette(Color32[] palette, SettingsSnapshot snap, bool isEffectColorA)
    {
        bool hasModify;
        float hue, sat, val;
        
        if (isEffectColorA)
        {
            hasModify = snap.ModifyEffectColorA_Hue.HasValue || 
                        snap.ModifyEffectColorA_Saturation.HasValue || 
                        snap.ModifyEffectColorA_Value.HasValue;
            hue = snap.ModifyEffectColorA_Hue ?? 0f;
            sat = snap.ModifyEffectColorA_Saturation ?? 0.5f;
            val = snap.ModifyEffectColorA_Value ?? 0.5f;
        }
        else
        {
            hasModify = snap.ModifyEffectColorB_Hue.HasValue || 
                        snap.ModifyEffectColorB_Saturation.HasValue || 
                        snap.ModifyEffectColorB_Value.HasValue;
            hue = snap.ModifyEffectColorB_Hue ?? 0f;
            sat = snap.ModifyEffectColorB_Saturation ?? 0.5f;
            val = snap.ModifyEffectColorB_Value ?? 0.5f;
        }
        
        if (!hasModify) return;
        
        bool isNeutral = Mathf.Approximately(hue, 0f) && 
                         Mathf.Approximately(sat, 0.5f) && 
                         Mathf.Approximately(val, 0.5f);
        
        if (isNeutral) return;
        
        int[] lightIndices, darkIndices;
        if (isEffectColorA)
        {
            lightIndices = EffectColorALightIndices;
            darkIndices = EffectColorADarkIndices;
        }
        else
        {
            lightIndices = EffectColorBLightIndices;
            darkIndices = EffectColorBDarkIndices;
        }
        
        float sMultiplier = sat * 2f;
        float vMultiplier = val * 2f;
        
        for (int i = 0; i < 4; i++)
        {
            Color32 pixel = palette[lightIndices[i]];
            Color color = new Color(pixel.r / 255f, pixel.g / 255f, pixel.b / 255f);
            Vector3 hsl = Custom.RGB2HSL(color);
            
            hsl.x = (hsl.x + hue) % 1f;
            if (hsl.x < 0f) hsl.x += 1f;
            hsl.y = Mathf.Clamp01(hsl.y * sMultiplier);
            hsl.z = Mathf.Clamp01(hsl.z * vMultiplier);
            
            Color modified = Custom.HSL2RGB(hsl.x, hsl.y, hsl.z);
            palette[lightIndices[i]] = new Color32(
                (byte)(modified.r * 255), (byte)(modified.g * 255), (byte)(modified.b * 255), 255);
        }
        
        for (int i = 0; i < 4; i++)
        {
            Color32 pixel = palette[darkIndices[i]];
            Color color = new Color(pixel.r / 255f, pixel.g / 255f, pixel.b / 255f);
            Vector3 hsl = Custom.RGB2HSL(color);
            
            hsl.x = (hsl.x + hue) % 1f;
            if (hsl.x < 0f) hsl.x += 1f;
            hsl.y = Mathf.Clamp01(hsl.y * sMultiplier);
            hsl.z = Mathf.Clamp01(hsl.z * vMultiplier);
            
            Color modified = Custom.HSL2RGB(hsl.x, hsl.y, hsl.z);
            palette[darkIndices[i]] = new Color32(
                (byte)(modified.r * 255), (byte)(modified.g * 255), (byte)(modified.b * 255), 255);
        }
    }

    public static void ApplyRotLayerToPalette(Color32[] palette, RoomCamera cam)
    {
        if (palette == null || palette.Length != 512) return;
        if (RoomCamera.allEffectColorsTexture == null) return;
        if (cam.rotMode <= 0f) return;
        
        Color[] rotColors = RoomCamera.allEffectColorsTexture.GetPixels(21 * 2, 0, 2, 2);
        float rotStrength = cam.rotMode * 0.9f;
        
        for (int i = 0; i < 4; i++)
        {
            Color32 currentLight = palette[EffectColorBLightIndices[i]];
            Color target = rotColors[i];
            
            palette[EffectColorBLightIndices[i]] = new Color32(
                (byte)(currentLight.r + (target.r * 255 - currentLight.r) * rotStrength),
                (byte)(currentLight.g + (target.g * 255 - currentLight.g) * rotStrength),
                (byte)(currentLight.b + (target.b * 255 - currentLight.b) * rotStrength),
                255);
            
            Color32 currentDark = palette[EffectColorBDarkIndices[i]];
            palette[EffectColorBDarkIndices[i]] = new Color32(
                (byte)(currentDark.r + (target.r * 255 - currentDark.r) * rotStrength),
                (byte)(currentDark.g + (target.g * 255 - currentDark.g) * rotStrength),
                (byte)(currentDark.b + (target.b * 255 - currentDark.b) * rotStrength),
                255);
        }
    }

    public static void ClearEffectData(RoomCamera cam)
    {
        // No hay datos persistentes que limpiar
    }
}