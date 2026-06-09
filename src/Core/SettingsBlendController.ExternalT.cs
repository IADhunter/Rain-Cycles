using UnityEngine;
using System.Diagnostics;
using RainCycles.Settings;
using RainCycles.Snapshot;
using RainCycles.Clock;
using RainCycles.Core;
using RainCycles.Blend;

namespace RainCycles.Core;

public static partial class SettingsBlendController
{
    public static void OverrideLightColorsPostOrig()
    {
        if (!_active || !_externalT || _room == null) return;
        if (!BlendTextureManager.Ready) return;

        var cam = _room.game?.cameras?[0];
        if (cam == null) return;
        if (_snapA == null || _snapB == null) return;

        var lerped = SettingsSnapshot.Lerp(_snapA, _snapB, _forcedT);
        RoomEffectsApplier.ApplyLightSources(_room, lerped);
    }

    public static void AttachWithExternalT(Room room, string pathA, string pathB, bool isAuto = false)
    {
        bool isIdleRefresh = (pathA == pathB);

        if (isIdleRefresh)
        {
            _room = room;
            _pathA = pathA;
            _pathB = pathB;
            _snapA = StaticTintManager.GetCachedSnapshot(pathA, room.abstractRoom.name);
            _snapB = _snapA;
            _isAutoBlend = isAuto;

            var currentCam = room.game?.cameras?[0];
            if (currentCam != null)
            {
                currentCam.SetBlendActive(room.abstractRoom.name);
                ApplyIdleTintsAndEffects(room, _snapA);
            }
            return;
        }

        _room = room;
        _pathA = pathA;
        _pathB = pathB;
        ConsumePendingOrigin(room, pathA);
        _snapA = StaticTintManager.GetCachedSnapshot(pathA, room.abstractRoom.name);
        _snapB = StaticTintManager.GetCachedSnapshot(pathB, room.abstractRoom.name);
        _active = true;
        _externalT = true;
        _isAutoBlend = isAuto;
        _lastT = -1f;
        _lastLightT = -1f;

        var cam = room.game?.cameras?[0];
        if (cam != null)
        {
            cam.ClearBlendSnapshots();
            cam.SetBlendActive(room.abstractRoom.name);

            BlendTextureManager.Load(cam, _snapA, _snapB, _snapOriginal, applyFade: false);
            RoomEffectsApplier.BuildLightIndex(room);

            string roomName = room.abstractRoom?.name;
            int stateA = StateFileResolver.GetStateFromPath(pathA, roomName);
            int stateB = StateFileResolver.GetStateFromPath(pathB, roomName);
            
            if (stateA > 0) _manualStateA = stateA;
            if (stateB > 0) _manualStateB = stateB;
            RSPlugin.log.LogDebug($"[AttachWithExternalT] isAuto={isAuto}, states set: A={_manualStateA}, B={_manualStateB}");

            if (stateA > 0 && stateB > 0)
                SyncSkySlots(room, stateA, stateB);

            if (_externalT && BlendClock.IsRunning &&
                BlendClock.CurrentPhase == BlendClock.Phase.Blending && !isAuto)
            {
                _lastT = -1f;
                ApplyBlend(BlendClock.SubPhaseLocalT);
            }
        }
    }

    private static SettingsSnapshot _pendingOrigin = null;

    public static void AdvanceOriginToB()
    {
        if (_snapB != null)
            _pendingOrigin = _snapB;
    }

    public static void ClearPendingOrigin()
    {
        _pendingOrigin = null;
    }

    private static void ConsumePendingOrigin(Room room, string originPath = null)
    {
        if (_pendingOrigin != null)
        {
            _snapOriginal = _pendingOrigin;
            _pendingOrigin = null;
        }
        else if (BlendClock.IsRunning && BlendClock.CurrentPhase == BlendClock.Phase.Blending)
        {
            var settings = BlendSettingsLoader.Active;
            string roomName = room.abstractRoom?.name;
            if (settings != null && roomName != null)
            {
                string pathA = StateFileResolver.GetRainStateSettingsFile(roomName, BlendClock.StateA);
                if (pathA != null)
                    _snapOriginal = StaticTintManager.GetCachedSnapshot(pathA, roomName);
                else
                    _snapOriginal = StaticTintManager.GetCachedSnapshot(room.roomSettings.filePath ?? "", roomName);
            }
            else
                _snapOriginal = SettingsSnapshot.FromFile(room.roomSettings.filePath ?? "");
        }
        else if (originPath != null)
        {
            string roomName = room.abstractRoom?.name ?? "";
            _snapOriginal = StaticTintManager.GetCachedSnapshot(originPath, roomName);
        }
        else if (_snapOriginal == null)
        {
            _snapOriginal = SettingsSnapshot.FromFile(room.roomSettings.filePath ?? "");
        }
    }

    public static void SetExternalT(float t)
    {
        if (!_active || !_externalT) return;
        
        float delta = t - _forcedT;
        if (Mathf.Abs(delta) > 0.0001f && Mathf.Abs(delta) < 0.005f)
        {
            RSPlugin.log.LogDebug($"[SetExternalT] Micro-movement: {_forcedT:F4} → {t:F4} (delta={delta:F4})");
        }
        else if (Mathf.Abs(delta) > 0.001f)
        {
            RSPlugin.log.LogDebug($"[SetExternalT] Called with t={t:F3}, _forcedT={_forcedT:F3}, delta={delta:F3}, isAuto={_isAutoBlend}");
        }
        
        _forcedT = t;

        if (_moveCameraThisFrame && _entryFrameT < 0f)
            _entryFrameT = t;

        if (_snapA != null && _snapB != null && _room != null)
        {
            bool lightTChanged = !Mathf.Approximately(t, _lastLightT);
            if (lightTChanged || _lastLightT < 0f)
            {
                _lastLightT = t;
                var lerpedForLights = SettingsSnapshot.Lerp(_snapA, _snapB, t);
                RoomEffectsApplier.ApplyLightSources(_room, lerpedForLights);
                RoomEffectsApplier.ApplyLightBeams(_room, lerpedForLights);
            }
        }

        float lastT = _lastT;
        _lastT = t;
        
        ApplyBlend(t);

        if (_acvScene != null)
            _acvScene.atmosphereColor = _lastAtmosphereColor;
        
        if (lastT >= 0f && Mathf.Abs(t - lastT) < 0.005f && Mathf.Abs(t - lastT) > 0.0001f)
        {
            RSPlugin.log.LogDebug($"[SetExternalT] Previously throttled! delta={t - lastT:F4} would have been ignored.");
        }
    }
}