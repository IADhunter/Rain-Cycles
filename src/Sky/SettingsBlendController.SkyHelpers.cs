using System.Collections.Generic;
using System.IO;
using UnityEngine;
using RainCycles.Settings;
using RainCycles.Clock;

namespace RainCycles.Core;

public static partial class SettingsBlendController
{
    private static List<BackgroundScene.Simple2DBackgroundIllustration> GetSlotsForSky(SkyType sky)
    {
        if (sky == SkyType.ACV) return _rcSlotsACV;
        if (sky == SkyType.RTV) return _rcSlotsRTV;
        if (sky == SkyType.PSV) return _rcSlotsPSV;
        return null;
    }

    private static void ForceSunShader(List<BackgroundScene.Simple2DBackgroundIllustration> sunSlots, RoomCamera cam)
    {
        if (sunSlots == null || cam?.spriteLeasers == null) return;
        var additiveShader = cam.game.rainWorld.Shaders["BackgroundAdditive"];
        foreach (var slot in sunSlots)
        {
            foreach (var sl in cam.spriteLeasers)
            {
                if (sl.drawableObject == slot && sl.sprites != null && sl.sprites.Length > 0)
                    sl.sprites[0].shader = additiveShader;
            }
        }
    }

    private static void PreApplySlotAlphas(List<BackgroundScene.Simple2DBackgroundIllustration> slots)
    {
        if (slots == null || slots.Count < 3) return;
        if (slots == _rcSlotsPSVFog || slots == _rcSlotsPSVSun) return;

        if (_active && _externalT)
        {
            float t = _forcedT;
            slots[0].alpha = 1f - t;
            slots[1].alpha = 1f;
            slots[2].alpha = 0f;
        }
        else if (BlendClock.IsRunning && BlendClock.CurrentPhase == BlendClock.Phase.Blending)
        {
            float t = BlendClock.SubPhaseLocalT;
            slots[0].alpha = 1f - t;
            slots[1].alpha = 1f;
            slots[2].alpha = 0f;
        }
        else if (_hasSavedAlphas && _detachedThisFrame)
        {
            slots[0].alpha = _savedDayAlpha;
            slots[1].alpha = _savedDuskAlpha;
            slots[2].alpha = _savedNightAlpha;
        }
        else
        {
            slots[0].alpha = 1f;
            slots[1].alpha = 0f;
            slots[2].alpha = 0f;
        }

        _savedDayAlpha = slots[0].alpha;
        _savedDuskAlpha = slots[1].alpha;
        _savedNightAlpha = slots[2].alpha;
        _hasSavedAlphas = true;
    }

    private static void UpdatePsvSlots(int stateA, int stateB, int stateC, RoomCamera cam)
    {
        var settings = BlendSettingsLoader.Active;
        if (settings == null) return;

        var skySlots = _rcSlotsPSV;
        if (skySlots != null && skySlots.Count >= 3)
        {
            string fileA = settings.GetBkgFileForState(stateA, ViewType.PSV);
            string fileB = settings.GetBkgFileForState(stateB, ViewType.PSV);
            string fileC = settings.GetBkgFileForState(stateC, ViewType.PSV);

            if (!string.IsNullOrEmpty(fileA) && skySlots[0].illustrationName != Path.GetFileNameWithoutExtension(fileA))
                RefreshSlotSprite(skySlots[0], Path.GetFileNameWithoutExtension(fileA), cam);
            if (!string.IsNullOrEmpty(fileB) && skySlots[1].illustrationName != Path.GetFileNameWithoutExtension(fileB))
                RefreshSlotSprite(skySlots[1], Path.GetFileNameWithoutExtension(fileB), cam);
            if (!string.IsNullOrEmpty(fileC) && skySlots[2].illustrationName != Path.GetFileNameWithoutExtension(fileC))
                RefreshSlotSprite(skySlots[2], Path.GetFileNameWithoutExtension(fileC), cam);
        }

        var fogSlots = _rcSlotsPSVFog;
        if (fogSlots != null && fogSlots.Count >= 3)
        {
            string fogA = settings.GetBkgFogForState(stateA);
            string fogB = settings.GetBkgFogForState(stateB);
            string fogC = settings.GetBkgFogForState(stateC);

            if (!string.IsNullOrEmpty(fogA) && fogSlots[0].illustrationName != Path.GetFileNameWithoutExtension(fogA))
                RefreshSlotSprite(fogSlots[0], Path.GetFileNameWithoutExtension(fogA), cam);
            if (!string.IsNullOrEmpty(fogB) && fogSlots[1].illustrationName != Path.GetFileNameWithoutExtension(fogB))
                RefreshSlotSprite(fogSlots[1], Path.GetFileNameWithoutExtension(fogB), cam);
            if (!string.IsNullOrEmpty(fogC) && fogSlots[2].illustrationName != Path.GetFileNameWithoutExtension(fogC))
                RefreshSlotSprite(fogSlots[2], Path.GetFileNameWithoutExtension(fogC), cam);
        }

        var sunSlots = _rcSlotsPSVSun;
        if (sunSlots != null && sunSlots.Count >= 3)
        {
            string sunA = settings.GetBkgSunForState(stateA);
            string sunB = settings.GetBkgSunForState(stateB);
            string sunC = settings.GetBkgSunForState(stateC);

            if (!string.IsNullOrEmpty(sunA) && sunSlots[0].illustrationName != Path.GetFileNameWithoutExtension(sunA))
                RefreshSlotSprite(sunSlots[0], Path.GetFileNameWithoutExtension(sunA), cam);
            if (!string.IsNullOrEmpty(sunB) && sunSlots[1].illustrationName != Path.GetFileNameWithoutExtension(sunB))
                RefreshSlotSprite(sunSlots[1], Path.GetFileNameWithoutExtension(sunB), cam);
            if (!string.IsNullOrEmpty(sunC) && sunSlots[2].illustrationName != Path.GetFileNameWithoutExtension(sunC))
                RefreshSlotSprite(sunSlots[2], Path.GetFileNameWithoutExtension(sunC), cam);
        }
    }

    private static int _lastApplyFrame = -1;
    private static float _lastAppliedT = -1f;

    private static void ApplyPsvAlphas(float t, bool isBlending)
    {
        bool isManualMode = _active && _externalT;
        
        if (!isManualMode && Time.frameCount == _lastApplyFrame) 
            return;
        
        var allSlots = new[] { _rcSlotsPSV, _rcSlotsPSVFog, _rcSlotsPSVSun };
        
        foreach (var slots in allSlots)
        {
            if (slots == null || slots.Count < 3) continue;
            
            float alphaDay, alphaDusk, alphaNight;
            
            if (isBlending)
            {
                if (slots == _rcSlotsPSV)
                {
                    alphaDay = 1f - t;
                    alphaDusk = 1f;
                    alphaNight = 0f;
                }
                else
                {
                    alphaDay = 1f - t;
                    alphaDusk = t;
                    alphaNight = 0f;
                }
            }
            else
            {
                alphaDay = 1f;
                alphaDusk = 0f;
                alphaNight = 0f;
            }
            
            slots[0].alpha = alphaDay;
            slots[1].alpha = alphaDusk;
            slots[2].alpha = alphaNight;
        }
        
        _lastApplyFrame = Time.frameCount;
        _lastAppliedT = t;
    }

    private static void UpdateRcSlots(SkyType sky, int stateA, int stateB, int stateC, RoomCamera forcedCam = null, Room targetRoom = null)
    {
        var slots = GetSlotsForSky(sky);
        if (slots == null || slots.Count < 3) return;

        string regionCode = null;
        if (targetRoom != null)
            regionCode = targetRoom.world?.region?.name?.ToUpperInvariant();
        else if (_room != null)
            regionCode = _room.world?.region?.name?.ToUpperInvariant();
        
        BlendSettings effectiveSettings = BlendSettingsLoader.Active;
        if (effectiveSettings == null && !string.IsNullOrEmpty(regionCode))
            effectiveSettings = BlendSettingsLoader.GetForRegion(regionCode);
        
        if (effectiveSettings == null) return;

        var cam = forcedCam ?? _room?.game?.cameras?[0];
        if (cam == null && targetRoom != null)
            cam = targetRoom.game?.cameras?[0];

        if (cam == null || (_room != null && cam.room != _room && (targetRoom == null || cam.room != targetRoom)))
        {
            _pendingSkySync = true;
            _pendingSyncSky = sky == SkyType.ACV ? 0 : (sky == SkyType.RTV ? 1 : 2);
            _pendingStateA = stateA;
            _pendingStateB = stateB;
            return;
        }

        ViewType view = sky == SkyType.ACV ? ViewType.ACV :
                        sky == SkyType.RTV ? ViewType.RTV : ViewType.PSV;

        string fileA = effectiveSettings.GetBkgFileForState(stateA, view);
        string fileB = effectiveSettings.GetBkgFileForState(stateB, view);
        string fileC = effectiveSettings.GetBkgFileForState(stateC, view);

        if (!string.IsNullOrEmpty(fileA) && slots[0].illustrationName != Path.GetFileNameWithoutExtension(fileA))
            RefreshSlotSprite(slots[0], Path.GetFileNameWithoutExtension(fileA), cam);
        if (!string.IsNullOrEmpty(fileB) && slots[1].illustrationName != Path.GetFileNameWithoutExtension(fileB))
            RefreshSlotSprite(slots[1], Path.GetFileNameWithoutExtension(fileB), cam);
        if (!string.IsNullOrEmpty(fileC) && slots[2].illustrationName != Path.GetFileNameWithoutExtension(fileC))
            RefreshSlotSprite(slots[2], Path.GetFileNameWithoutExtension(fileC), cam);

        if (BlendClock.IsRunning && BlendClock.CurrentPhase == BlendClock.Phase.Blending)
        {
            float t = BlendClock.SubPhaseLocalT;
            slots[0].alpha = 1f - t;
            slots[1].alpha = 1f;
            slots[2].alpha = 0f;
        }
        else
        {
            slots[0].alpha = 1f;
            slots[1].alpha = 0f;
            slots[2].alpha = 0f;
        }

        SetSlotDay(sky, stateA);
        SetSlotDusk(sky, stateB);
        SetSlotNight(sky, stateC);

        if (sky == SkyType.PSV)
        {
            UpdatePsvSlots(stateA, stateB, stateC, cam);
            ApplyPsvAlphas(BlendClock.IsRunning && BlendClock.CurrentPhase == BlendClock.Phase.Blending ? BlendClock.SubPhaseLocalT : 0f, BlendClock.IsRunning && BlendClock.CurrentPhase == BlendClock.Phase.Blending);
        }

        _pendingSkySync = false;
    }

    private static void ApplyRcSlotsAlpha(SkyType sky, float t, bool isBlending)
    {
        var slots = GetSlotsForSky(sky);
        if (slots == null || slots.Count < 3) return;

        if (isBlending)
        {
            slots[0].alpha = 1f - t;
            slots[1].alpha = 1f;
            slots[2].alpha = 0f;
        }
        else
        {
            slots[0].alpha = 1f;
            slots[1].alpha = 0f;
            slots[2].alpha = 0f;
        }

        if (sky == SkyType.PSV)
            ApplyPsvAlphas(t, isBlending);
    }

    public static void SyncSkySlots(Room room, int stateA, int stateB)
    {
        if (room == null) return;
        var settings = BlendSettingsLoader.Active;
        if (settings == null) return;

        string roomName = room.abstractRoom?.name;
        if (roomName == null) return;

        var skyType = GetViewFromLoadedSettings(room);
        if (skyType == SkyType.None) return;

        int stateC = NextStateIn(settings, stateB);
        SetSlotDay(skyType, stateA);
        SetSlotDusk(skyType, stateB);
        SetSlotNight(skyType, stateC);

        UpdateRcSlots(skyType, stateA, stateB, stateC, null, room);
    }

    public static void ApplySkyForState(int state, Room room)
    {
        if (room == null) return;
        var settings = BlendSettingsLoader.Active;
        if (settings == null) return;
        string roomName = room.abstractRoom?.name;
        if (roomName == null) return;

        var skyType = GetViewFromLoadedSettings(room);
        if (skyType == SkyType.None) return;
        if (GetSlotDay(skyType) == state) return;

        int stateB = NextStateIn(settings, state);
        int stateC = NextStateIn(settings, stateB);

        SetSlotDay(skyType, state);
        SetSlotDusk(skyType, stateB);
        SetSlotNight(skyType, stateC);

        UpdateRcSlots(skyType, state, stateB, stateC, null, room);
        ApplyRcSlotsAlpha(skyType, 0f, false);
    }

    public static void RotateSlotsOnIdle(Room room, int currentState)
    {
        if (room == null) return;
        var settings = BlendSettingsLoader.Active;
        if (settings == null) return;

        string roomName = room.abstractRoom?.name;
        if (roomName == null) return;

        var skyType = GetViewFromLoadedSettings(room);
        if (skyType == SkyType.None) return;

        var slots = GetSlotsForSky(skyType);
        if (slots == null || slots.Count < 3) return;
        if (currentState == _lastIdleRotatedState) return;

        int stateB = NextStateIn(settings, currentState);
        int stateC = NextStateIn(settings, stateB);

        ViewType view = skyType == SkyType.ACV ? ViewType.ACV
                      : skyType == SkyType.RTV ? ViewType.RTV : ViewType.PSV;

        string fileA = settings.GetBkgFileForState(currentState, view);
        string fileB = settings.GetBkgFileForState(stateB, view);
        string fileC = settings.GetBkgFileForState(stateC, view);

        var cam = room.game?.cameras?[0];

        if (!string.IsNullOrEmpty(fileA) && slots[0].illustrationName != fileA)
            RefreshSlotSprite(slots[0], fileA, cam);
        if (!string.IsNullOrEmpty(fileB) && slots[1].illustrationName != fileB)
            RefreshSlotSprite(slots[1], fileB, cam);
        if (!string.IsNullOrEmpty(fileC) && slots[2].illustrationName != fileC)
            RefreshSlotSprite(slots[2], fileC, cam);

        slots[0].alpha = 1f;
        slots[1].alpha = 0f;
        slots[2].alpha = 0f;

        SetSlotDay(skyType, currentState);
        SetSlotDusk(skyType, stateB);
        SetSlotNight(skyType, stateC);

        if (skyType == SkyType.PSV)
        {
            UpdatePsvSlots(currentState, stateB, stateC, cam);
            ApplyPsvAlphas(0f, false);
        }

        _lastIdleRotatedState = currentState;
    }

    /// <summary>
    /// Siguiente estado en rotación cíclica intrínseca: 1→2→3→4→1.
    /// No depende de secuencias configuradas en archivo.
    /// </summary>
    private static int NextStateIn(BlendSettings settings, int state)
    {
        if (settings == null || state < 1 || state > 4) return state;
        return (state % 4) + 1;
    }

    private static bool RefreshSlotSprite(
        BackgroundScene.Simple2DBackgroundIllustration slot,
        string newName, RoomCamera cam)
    {
        if (slot.illustrationName == newName) return true;

        float currentAlpha = slot.alpha;
        string oldName = slot.illustrationName;
        slot.illustrationName = newName;

        var cameras = cam?.room?.game?.cameras
                   ?? slot.scene?.room?.game?.cameras;
        if (cameras == null) return false;

        bool success = false;
        foreach (var anyCamera in cameras)
        {
            if (anyCamera?.spriteLeasers == null) continue;
            foreach (var sLeaser in anyCamera.spriteLeasers)
            {
                if (sLeaser.drawableObject != slot) continue;
                if (sLeaser.sprites == null || sLeaser.sprites.Length == 0) continue;

                var oldSprite = sLeaser.sprites[0];
                var container = oldSprite?.container;
                if (container == null) break;

                int childIndex = -1;
                for (int i = 0; i < container.GetChildCount(); i++)
                    if (container.GetChildAt(i) == oldSprite) { childIndex = i; break; }

                var newSprite = new FSprite(newName, true);
                newSprite.x = oldSprite.x;
                newSprite.y = oldSprite.y;
                bool isSunSlot = (_rcSlotsPSVSun != null && _rcSlotsPSVSun.Contains(slot));
                newSprite.shader = isSunSlot
                    ? cam.game.rainWorld.Shaders["BackgroundAdditive"]
                    : oldSprite.shader;
                newSprite.alpha = currentAlpha;

                newSprite.UpdateLocalVertices();

                oldSprite.RemoveFromContainer();
                sLeaser.sprites[0] = newSprite;

                if (childIndex >= 0 && childIndex <= container.GetChildCount())
                    container.AddChildAtIndex(newSprite, childIndex);
                else
                    container.AddChild(newSprite);

                success = true;
                break;
            }
            if (success) break;
        }

        return success;
    }

    public static void ClearAllSlots()
    {
        _rcSlotsACV = null;
        _acvScene = null;
        _acvSlotDay = _acvSlotDusk = _acvSlotNight = -1;

        _rcSlotsRTV = null;
        _rtvScene = null;
        _rtvSlotDay = _rtvSlotDusk = _rtvSlotNight = -1;

        _rcSlotsPSV = null;
        _rcSlotsPSVFog = null;
        _rcSlotsPSVSun = null;
        _psvScene = null;
        _psvSlotDay = _psvSlotDusk = _psvSlotNight = -1;

        _rcSlotsStaticACV = null;
        _rcSlotsStaticRTV = null;
        _rcSlotsStaticPSV = null;

        _lastIdleRotatedState = -1;
        _hasSavedAlphas = false;
        _pendingSkySync = false;
    }
}