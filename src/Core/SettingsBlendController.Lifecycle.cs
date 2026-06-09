using UnityEngine;
using RainCycles.Settings;
using RainCycles.Snapshot;
using RainCycles.Sky;
using RainCycles.Clock;
using RainCycles.Blend;

namespace RainCycles.Core;

public static partial class SettingsBlendController
{
    public static void Attach(Room room, string pathA, string pathB)
    {
        _room = room;
        _pathA = pathA;
        _pathB = pathB;
        _snapOriginal = SettingsSnapshot.FromFile(room.roomSettings.filePath ?? "");
        _snapA = StaticTintManager.GetCachedSnapshot(pathA, room.abstractRoom.name);
        _snapB = StaticTintManager.GetCachedSnapshot(pathB, room.abstractRoom.name);
        _active = true;
        _lastT = -1f;

        var cam = room.game?.cameras?[0];
        if (cam != null)
        {
            // Activar modo blend (paletas autónomas)
            cam.ClearBlendSnapshots();
            cam.SetBlendActive(room.abstractRoom.name);
            cam.UpdateBlendPalette(0f);
            cam.ApplyFade();

            if (BlendTextureManager.TerrainReady)
                BlendTextureManager.MixTerrainPalette(cam, 0f, _snapA, _snapB);
        }
        RoomEffectsApplier.BuildLightIndex(room);
        ApplyBlend(0f);
    }

    public static void Detach()
    {
        if (_room != null)
        {
            var cam = _room.game?.cameras?[0];
            // CRÍTICO: solo tocar la cámara si todavía está en la sala que estamos
            // detacheando. Si ya cambió a otra sala, NO contaminar su estado vanilla.
            if (cam != null && cam.room == _room)
            {
                cam.paletteB = -1;
            }
        }

        if (_active && _externalT)
        {
            _savedDayAlpha = 1f - _forcedT;
            _savedDuskAlpha = 1f;
            _savedNightAlpha = 0f;
            _hasSavedAlphas = true;
        }
        else if (BlendClock.IsRunning && BlendClock.CurrentPhase == BlendClock.Phase.Blending
                 && (_room != null ? ActiveSlotDay(_room.game?.cameras?[0]) : -1) == BlendClock.StateA)
        {
            _savedDayAlpha = 1f - BlendClock.SubPhaseLocalT;
            _savedDuskAlpha = 1f;
            _savedNightAlpha = 0f;
            _hasSavedAlphas = true;
        }
        else
        {
            _hasSavedAlphas = false;
        }

        _active = false;
        _externalT = false;
        _detachedThisFrame = true;
        _room = null;
        _pathA = null;
        _pathB = null;
        _snapOriginal = null;
        _lastLightT = -1f;
        _hasSavedAlphas = false;
        _pendingSkySync = false;
        _rcSlotsStaticACV = null;
        _rcSlotsStaticRTV = null;
        _rcSlotsStaticPSV = null;
        RoomEffectsApplier.ClearLightIndex();

        BlendTextureManager.DestroyTerrainTextures();
    }

    public static void ResetFull()
    {
        _pendingOrigin = null;
        _lastRoomWasManaged = false;
        if (_active) Detach();
        _rtvScene = null; _acvScene = null; _psvScene = null;
        _rtvSlotDay = _rtvSlotDusk = _rtvSlotNight = -1;
        _acvSlotDay = _acvSlotDusk = _acvSlotNight = -1;
        _psvSlotDay = _psvSlotDusk = _psvSlotNight = -1;
        _hasSavedAlphas = false;
        _entryFrameT = -1f;
        _pendingSkySync = false;
        _pendingSkyStateA = -1;
        _pendingSkyStateB = -1;
        _rcSlotsStaticACV = null;
        _rcSlotsStaticRTV = null;
        _rcSlotsStaticPSV = null;
        
        // Limpiar referencias de slots PSV (fog y sun)
        _rcSlotsACV = null;
        _rcSlotsRTV = null;
        _rcSlotsPSV = null;
        _rcSlotsPSVFog = null;
        _rcSlotsPSVSun = null;
    }

    public static void ResetFullSoft()
    {
        ResetFull();
    }
}