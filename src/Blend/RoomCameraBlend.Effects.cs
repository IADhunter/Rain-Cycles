using UnityEngine;
using RainCycles.Snapshot;
using RWCustom;
using System.Collections.Generic;

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
        if (cam?.room?.roomSettings == null) return;
        string filePath = cam.room.roomSettings.filePath;
        if (string.IsNullOrEmpty(filePath)) return;

        var snap = SettingsSnapshot.GetCached(filePath, cam.room.abstractRoom?.name);
        if (snap == null) return;

        var rs = cam.room.roomSettings;

        void RestoreEffect(RoomSettings.RoomEffect.Type type, float original)
        {
            float amount = original >= 0f ? original : 0f;
            var existing = rs.GetEffect(type);
            if (existing != null)
                existing.amount = amount;
            else if (amount > 0f)
                rs.effects.Add(new RoomSettings.RoomEffect(type, amount, false));
        }

        RestoreEffect(RoomSettings.RoomEffect.Type.Darkness,         snap.EffectDarkness);
        RestoreEffect(RoomSettings.RoomEffect.Type.Brightness,       snap.EffectBrightness);
        RestoreEffect(RoomSettings.RoomEffect.Type.Contrast,         snap.EffectContrast);
        RestoreEffect(RoomSettings.RoomEffect.Type.Desaturation,     snap.EffectDesaturation);
        RestoreEffect(RoomSettings.RoomEffect.Type.Hue,              snap.EffectHue);
        RestoreEffect(RoomSettings.RoomEffect.Type.DarkenLights,     snap.EffectDarkenLights);
        RestoreEffect(RoomSettings.RoomEffect.Type.Fog,              snap.EffectFog);
        RestoreEffect(RoomSettings.RoomEffect.Type.SkyBloom,         snap.EffectSkyBloom);
        RestoreEffect(RoomSettings.RoomEffect.Type.SkyAndLightBloom, snap.EffectSkyAndLightBloom);
        RestoreEffect(RoomSettings.RoomEffect.Type.LightBurn,        snap.EffectLightBurn);
        RestoreEffect(RoomSettings.RoomEffect.Type.Bloom,            snap.EffectBloom);

        float sandstorm = snap.EffectSurfaceSandstorm >= 0f ? snap.EffectSurfaceSandstorm : 0f;
        var ssExisting = rs.GetEffect(new RoomSettings.RoomEffect.Type("SurfaceSandstorm"));
        if (ssExisting != null)
            ssExisting.amount = sandstorm;
        else if (sandstorm > 0f)
            rs.effects.Add(new RoomSettings.RoomEffect(new RoomSettings.RoomEffect.Type("SurfaceSandstorm"), sandstorm, false));
    }

    // ═════════════════════════════════════════════════════════════════════
    //  SCALAR EFFECTS - INTERPOLACIÓN Y APLICACIÓN DURANTE EL BLEND
    // ═════════════════════════════════════════════════════════════════════

    private static readonly RoomSettings.RoomEffect.Type[] _exclusivePriority = new[]
    {
        RoomSettings.RoomEffect.Type.Bloom,
        RoomSettings.RoomEffect.Type.VoidMelt,
        RoomSettings.RoomEffect.Type.Fog,
        RoomSettings.RoomEffect.Type.LightBurn,
        RoomSettings.RoomEffect.Type.SkyAndLightBloom,
        RoomSettings.RoomEffect.Type.SkyBloom,
    };

    public static void ApplyScalarEffects(this Room room, SettingsSnapshot a, SettingsSnapshot b, float t)
    {
        ApplyEffect(room, RoomSettings.RoomEffect.Type.Darkness,         LerpScalarEffect(a.EffectDarkness,         b.EffectDarkness,         t));
        ApplyEffect(room, RoomSettings.RoomEffect.Type.Brightness,       LerpScalarEffect(a.EffectBrightness,       b.EffectBrightness,       t));
        ApplyEffect(room, RoomSettings.RoomEffect.Type.Contrast,         LerpScalarEffect(a.EffectContrast,         b.EffectContrast,         t));
        ApplyEffect(room, RoomSettings.RoomEffect.Type.Desaturation,     LerpScalarEffect(a.EffectDesaturation,     b.EffectDesaturation,     t));
        ApplyEffect(room, RoomSettings.RoomEffect.Type.Hue,              LerpScalarEffect(a.EffectHue,              b.EffectHue,              t));
        ApplyEffect(room, RoomSettings.RoomEffect.Type.DarkenLights,     LerpScalarEffect(a.EffectDarkenLights,     b.EffectDarkenLights,     t));
        ApplyEffect(room, RoomSettings.RoomEffect.Type.Fog,              LerpScalarEffect(a.EffectFog,              b.EffectFog,              t));
        ApplyEffect(room, RoomSettings.RoomEffect.Type.SkyBloom,         LerpScalarEffect(a.EffectSkyBloom,         b.EffectSkyBloom,         t));
        ApplyEffect(room, RoomSettings.RoomEffect.Type.SkyAndLightBloom, LerpScalarEffect(a.EffectSkyAndLightBloom, b.EffectSkyAndLightBloom, t));
        ApplyEffect(room, RoomSettings.RoomEffect.Type.LightBurn,        LerpScalarEffect(a.EffectLightBurn,        b.EffectLightBurn,        t));
        ApplyEffect(room, RoomSettings.RoomEffect.Type.Bloom,            LerpScalarEffect(a.EffectBloom,            b.EffectBloom,            t));

        float sandstorm = LerpScalarEffect(a.EffectSurfaceSandstorm, b.EffectSurfaceSandstorm, t);
        if (sandstorm >= 0f)
            ApplyEffect(room, new RoomSettings.RoomEffect.Type("SurfaceSandstorm"), sandstorm);
    }

    private static float LerpScalarEffect(float va, float vb, float t)
    {
        float a = va < 0f ? 0f : va;
        float b = vb < 0f ? 0f : vb;
        return Mathf.Lerp(a, b, t);
    }

    private static bool IsOverriddenByHigherPriority(RoomSettings rs, RoomSettings.RoomEffect.Type type)
    {
        int myPriority = System.Array.IndexOf(_exclusivePriority, type);
        if (myPriority < 0) return false;
        for (int i = myPriority + 1; i < _exclusivePriority.Length; i++)
        {
            var higher = rs.GetEffect(_exclusivePriority[i]);
            if (higher != null && higher.amount > 0f) return true;
        }
        return false;
    }

    private static void ApplyEffect(Room room, RoomSettings.RoomEffect.Type type, float amount)
    {
        if (amount <= 0f)
        {
            var existing = room.roomSettings.GetEffect(type);
            if (existing != null) existing.amount = 0f;
            return;
        }
        if (IsOverriddenByHigherPriority(room.roomSettings, type)) return;
        var effect = room.roomSettings.GetEffect(type);
        if (effect != null)
            effect.amount = amount;
        else
            room.roomSettings.effects.Add(new RoomSettings.RoomEffect(type, amount, false));
    }

    // ═════════════════════════════════════════════════════════════════════
    //  DECALS - INTERPOLACIÓN Y APLICACIÓN DURANTE EL BLEND
    //  (Extraído de SettingsSnapshotLerp y RoomEffectsApplier)
    // ═════════════════════════════════════════════════════════════════════

    public static SettingsSnapshot LerpDecals(SettingsSnapshot a, SettingsSnapshot b, float t)
    {
        var snap = new SettingsSnapshot();
        snap.PlacedObjectLines = new List<string>(a.PlacedObjectLines);

        foreach (var kv in a.DecalOpacities)
        {
            if (!b.DecalOpacities.TryGetValue(kv.Key, out float[] opsB)) opsB = new float[4];
            float[] lerped = new float[4];
            for (int i = 0; i < 4; i++) lerped[i] = Mathf.Lerp(kv.Value[i], opsB[i], t);
            snap.DecalOpacities[kv.Key] = lerped;
        }

        return snap;
    }

    public static void ApplyDecalOpacities(this Room room, SettingsSnapshot lerped)
    {
        if (lerped.DecalOpacities.Count == 0) return;

        var decalSnapIndices = new List<int>();
        for (int i = 0; i < lerped.PlacedObjectLines.Count; i++)
            if (lerped.PlacedObjectLines[i].StartsWith("CustomDecal><"))
                decalSnapIndices.Add(i);

        int decalCount = 0;
        for (int i = 0; i < room.updateList.Count; i++)
        {
            var decal = room.updateList[i] as CustomDecal;
            if (decal == null) continue;

            if (decalCount < decalSnapIndices.Count)
            {
                int snapIdx = decalSnapIndices[decalCount];
                if (lerped.DecalOpacities.TryGetValue(snapIdx, out float[] ops))
                {
                    var data = decal.placedObject.data as PlacedObject.CustomDecalData;
                    if (data != null)
                    {
                        for (int j = 0; j < 4 && j < ops.Length; j++)
                        {
                            float op = Mathf.Clamp01(ops[j]);
                            data.vertices[j, 0] = op;
                        }
                        decal.meshDirty = true;
                    }
                }
            }
            decalCount++;
        }
    }
}