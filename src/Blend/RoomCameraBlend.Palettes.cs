using System.IO;
using UnityEngine;
using RainCycles.Snapshot;
using RainCycles.Clock;
using RainCycles.Core;
using System.Collections.Generic;

namespace RainCycles.Blend;

public static partial class RoomCameraExtensions
{
    private static readonly HashSet<int> EffectColorIndices = new HashSet<int>
    {
        (4 * 32) + 30, (4 * 32) + 31, (5 * 32) + 30, (5 * 32) + 31,
        (2 * 32) + 30, (2 * 32) + 31, (3 * 32) + 30, (3 * 32) + 31
    };

    public static void SetBlendActive(this RoomCamera cam, string roomName)
    {
        if (cam == null) return;
        if (!ShouldHaveBlendActive(cam)) return;

        var data = GetOrCreateData(cam);
        data.roomName = roomName;
        data.isBlendActive = true;

        if (cam.paletteTexture == null)
        {
            cam.paletteTexture = new Texture2D(32, 8, TextureFormat.RGBA32, false);
            cam.paletteTexture.filterMode = FilterMode.Point;
            cam.paletteTexture.wrapMode = TextureWrapMode.Clamp;
        }
    }

    public static void UpdateBlendPalette(this RoomCamera cam, float forcedT = -1f)
    {
        if (cam == null) return;

        if (!ShouldHaveBlendActive(cam))
        {
            var existing = GetBlendData(cam);
            if (existing != null && existing.isBlendActive)
                existing.isBlendActive = false;
            return;
        }

        var data = GetOrCreateData(cam);
        if (data == null || !data.isBlendActive)
        {
            data = GetOrCreateData(cam);
            data.isBlendActive = true;
            if (string.IsNullOrEmpty(data.roomName))
                data.roomName = cam.room?.abstractRoom?.name;
        }

        string roomName = data.roomName ?? cam.room?.abstractRoom?.name;
        if (string.IsNullOrEmpty(roomName)) return;

        // ============================================================
        // DETECTAR MODO MANUAL (SLIDER)
        // ============================================================
        bool isManualBlend = SettingsBlendController.IsActive && 
                             SettingsBlendController.IsExternalT &&
                             SettingsBlendController.ActiveRoom == cam.room;

        int stateA, stateB;
        float t;
        SettingsSnapshot snapA = null, snapB = null;
        bool isIdle;

        if (isManualBlend)
        {
            stateA = SettingsBlendController.ManualStateA;
            stateB = SettingsBlendController.ManualStateB;
            t = forcedT >= 0f ? forcedT : SettingsBlendController.ForcedT;
            isIdle = (stateA == stateB);
            
            snapA = SettingsBlendController.ManualSnapA;
            snapB = SettingsBlendController.ManualSnapB;
        }
        else
        {
            stateA = BlendClock.StateA;
            stateB = BlendClock.StateB;
            t = forcedT >= 0f ? forcedT : BlendClock.SubPhaseLocalT;
            isIdle = (BlendClock.CurrentPhase == BlendClock.Phase.Idle);
            
            if (isIdle)
            {
                string idlePath = StateFileResolver.GetRainStateSettingsFile(roomName, stateA);
                snapA = SettingsSnapshot.GetCached(idlePath, roomName);
            }
            else
            {
                string pathA = StateFileResolver.GetRainStateSettingsFile(roomName, stateA);
                string pathB = StateFileResolver.GetRainStateSettingsFile(roomName, stateB);
                snapA = SettingsSnapshot.GetCached(pathA, roomName);
                snapB = SettingsSnapshot.GetCached(pathB, roomName);
            }
        }

        data.lastStateA = stateA;
        data.lastStateB = stateB;
        data.lastBlendT = t;
        data.lastUpdateFrame = Time.frameCount;
        
        if (isIdle)
        {
            if (snapA != null)
            {
                GetOrLoadState(roomName, stateA, snapA, out data.mainPixelsA, out data.fadePixelsA, 
                    out data.lastFadePaletteA, out _);
                
                data.mainTexA = null;
                data.mainPixelsB = data.mainPixelsA;
                data.fadePixelsB = data.fadePixelsA;
                data.lastFadePaletteB = data.lastFadePaletteA;

                Color32[] paletteA = BuildFullPalette(cam, data, snapA);
                
                EnsureFadeTex(cam, ref cam.fadeTexA);
                cam.fadeTexA.SetPixels32(paletteA);
                cam.fadeTexA.Apply(false);
                
                cam.paletteB = -1;
                cam.paletteBlend = 0f;
            }
        }
        else
        {
            if (snapA == null || snapB == null) return;

            GetOrLoadState(roomName, stateA, snapA, out data.mainPixelsA, out data.fadePixelsA, 
                out data.lastFadePaletteA, out _);
            Color32[] paletteA = BuildFullPalette(cam, data, snapA);
            
            GetOrLoadState(roomName, stateB, snapB, out data.mainPixelsB, out data.fadePixelsB, 
                out data.lastFadePaletteB, out _);
            var dataB = new CameraBlendData();
            dataB.mainPixelsA = data.mainPixelsB;
            dataB.fadePixelsA = data.fadePixelsB;
            dataB.lastFadePaletteA = data.lastFadePaletteB;
            Color32[] paletteB = BuildFullPalette(cam, dataB, snapB);
            
            EnsureFadeTex(cam, ref cam.fadeTexA);
            EnsureFadeTex(cam, ref cam.fadeTexB);
            
            cam.fadeTexA.SetPixels32(paletteA);
            cam.fadeTexA.Apply(false);
            cam.fadeTexB.SetPixels32(paletteB);
            cam.fadeTexB.Apply(false);
            
            cam.paletteBlend = t;
            cam.paletteB = 1;
        }

        cam.ApplyFade();
        cam.ApplyPalette();
        cam.UpdateBlendTerrain(t, stateA, stateB, isIdle);
        cam.lastFadeCoord = cam.fadeCoord;
    }

    private static Color32[] BuildFullPalette(RoomCamera cam, CameraBlendData data, SettingsSnapshot snap)
    {
        Color32[] palette = new Color32[512];
        for (int i = 0; i < 512; i++)
            palette[i] = data.mainPixelsA[i];
        
        ApplyVanillaEffectColorsNoModify(cam, ref palette, snap);
        ApplyModifyToPalette(palette, snap);
        
        float fadeOpac = GetFadeOpacity(snap, cam.currentCameraPosition);
        if (fadeOpac > 0f && data.fadePixelsA != null)
        {
            Color32[] fadeProcessed = new Color32[512];
            for (int i = 0; i < 512; i++)
                fadeProcessed[i] = data.fadePixelsA[i];
            
            ApplyVanillaEffectColorsNoModify(cam, ref fadeProcessed, snap);
            ApplyModifyToPalette(fadeProcessed, snap);
            
            for (int i = 0; i < 512; i++)
            {
                if (!EffectColorIndices.Contains(i))
                    palette[i] = LerpColor32(palette[i], fadeProcessed[i], fadeOpac);
            }
        }
        
        ApplyRotLayerToPalette(palette, cam);
        return palette;
    }
    
    private static void ApplyVanillaEffectColorsNoModify(RoomCamera cam, ref Color32[] palette, SettingsSnapshot snap)
    {
        if (snap == null || palette == null || palette.Length != 512) return;
        
        var roomSettings = cam.room.roomSettings;
        
        RoomSettings.RoomEffect oldModifyA = null;
        RoomSettings.RoomEffect oldModifyB = null;
        
        for (int i = 0; i < roomSettings.effects.Count; i++)
        {
            if (roomSettings.effects[i].type == RoomSettings.RoomEffect.Type.ModifyEffectColorA)
                oldModifyA = roomSettings.effects[i];
            if (roomSettings.effects[i].type == RoomSettings.RoomEffect.Type.ModifyEffectColorB)
                oldModifyB = roomSettings.effects[i];
        }
        
        if (oldModifyA != null) roomSettings.effects.Remove(oldModifyA);
        if (oldModifyB != null) roomSettings.effects.Remove(oldModifyB);
        
        Texture2D tempTex = new Texture2D(32, 16, TextureFormat.RGBA32, false);
        tempTex.SetPixels32(palette);
        tempTex.Apply(false);
        
        cam.ApplyEffectColorsToPaletteTexture(ref tempTex, 
            snap._hasEffectColorA ? snap.EffectColorA : -1,
            snap._hasEffectColorB ? snap.EffectColorB : -1);
        
        Color32[] result = tempTex.GetPixels32();
        for (int i = 0; i < 512; i++)
            palette[i] = result[i];
        
        if (oldModifyA != null) roomSettings.effects.Add(oldModifyA);
        if (oldModifyB != null) roomSettings.effects.Add(oldModifyB);
        
        Object.Destroy(tempTex);
    }
    
    private static void EnsureFadeTex(RoomCamera cam, ref Texture2D fadeTex)
    {
        if (fadeTex == null)
        {
            fadeTex = new Texture2D(32, 16, TextureFormat.RGBA32, false);
            fadeTex.filterMode = FilterMode.Point;
            fadeTex.wrapMode = TextureWrapMode.Clamp;
        }
    }

    private static Color32 LerpColor32(Color32 a, Color32 b, float t)
    {
        return new Color32(
            (byte)(a.r + (b.r - a.r) * t),
            (byte)(a.g + (b.g - a.g) * t),
            (byte)(a.b + (b.b - a.b) * t),
            255);
    }

    private static float GetFadeOpacity(SettingsSnapshot snap, int camIdx)
    {
        if (snap != null && snap._hasFadePalette && camIdx < snap.FadePaletteOpacities.Length)
            return snap.FadePaletteOpacities[camIdx];
        return 0f;
    }

    private static void ClearPaletteData(RoomCamera cam)
    {
        var data = GetBlendData(cam);
        if (data == null) return;

        if (data.mainTexA != null) { Object.Destroy(data.mainTexA); data.mainTexA = null; }
        if (data.mainTexB != null) { Object.Destroy(data.mainTexB); data.mainTexB = null; }
        data.lastMainPaletteA = -1;
        data.lastMainPaletteB = -1;
        data.mainPixelsA = null;
        data.mainPixelsB = null;

        if (data.fadeTexA != null) { Object.Destroy(data.fadeTexA); data.fadeTexA = null; }
        if (data.fadeTexB != null) { Object.Destroy(data.fadeTexB); data.fadeTexB = null; }
        data.lastFadePaletteA = -1;
        data.lastFadePaletteB = -1;
        data.fadePixelsA = null;
        data.fadePixelsB = null;

        data.lastBlendT = -1f;
        data.lastStateA = -1;
        data.lastStateB = -1;
    }
}