using UnityEngine;
using RainCycles.Settings;
using RainCycles.Snapshot;
using RainCycles.Clock;
using RainCycles.Core;
using RainCycles.Blend;

namespace RainCycles.Core;

public static partial class SettingsBlendController
{

    public static void AttachWithExternalT(Room room, string pathA, string pathB, bool isAuto = false)
    {
        // ============================================================
        // VERIFICACIÓN: LA SALA DEBE TENER 4 ESTADOS PARA BLEND
        // ============================================================
        string roomName = room?.abstractRoom?.name;
        if (!string.IsNullOrEmpty(roomName) && !StateFileResolver.HasFullStates(roomName))
        {
            return;
        }

        bool isIdleRefresh = (pathA == pathB);

        if (isIdleRefresh)
        {
            _room = room;
            _pathA = pathA;
            _pathB = pathB;
            _snapA = SettingsSnapshot.GetCached(pathA, room.abstractRoom.name);
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
        _snapA = SettingsSnapshot.GetCached(pathA, room.abstractRoom.name);
        _snapB = SettingsSnapshot.GetCached(pathB, room.abstractRoom.name);
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

            RoomCameraExtensions.BuildLightIndex(room);

            string roomName2 = room.abstractRoom?.name;
            // ⭐ Ahora usa StateFileResolver.GetStateFromPath()
            int stateA = StateFileResolver.GetStateFromPath(pathA, roomName2);
            int stateB = StateFileResolver.GetStateFromPath(pathB, roomName2);
            
            if (stateA > 0) _manualStateA = stateA;
            if (stateB > 0) _manualStateB = stateB;

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
                // ⭐ Ahora usa StateFileResolver.ResolveSettingsPath()
                string pathA = StateFileResolver.ResolveSettingsPath(roomName, BlendClock.StateA);
                if (pathA != null)
                    _snapOriginal = SettingsSnapshot.GetCached(pathA, roomName);
                else
                    _snapOriginal = SettingsSnapshot.GetCached(room.roomSettings.filePath ?? "", roomName);
            }
            else
                _snapOriginal = SettingsSnapshot.FromFile(room.roomSettings.filePath ?? "");
        }
        else if (originPath != null)
        {
            string roomName = room.abstractRoom?.name ?? "";
            _snapOriginal = SettingsSnapshot.GetCached(originPath, roomName);
        }
        else if (_snapOriginal == null)
        {
            _snapOriginal = SettingsSnapshot.FromFile(room.roomSettings.filePath ?? "");
        }
    }

    public static void SetExternalT(float t)
    {
        if (!_active || !_externalT) return;
        
        _forcedT = t;

        if (_moveCameraThisFrame && _entryFrameT < 0f)
            _entryFrameT = t;

        if (_snapA != null && _snapB != null && _room != null)
        {
            bool lightTChanged = !Mathf.Approximately(t, _lastLightT);
            if (lightTChanged || _lastLightT < 0f)
            {
                _lastLightT = t;

                _room.ApplyLightSources(_snapA, _snapB, t);
                _room.ApplyLightBeams(_snapA, _snapB, t);
            }
        }

        _lastT = t;
        
        ApplyBlend(t);
        
        if (_room != null)
        {
            var skyType = GetViewFromLoadedSettings(_room);
            if (skyType == SkyType.PSV)
                ApplyPsvAlphas(t, isBlending: _manualStateA != _manualStateB);
        }
    }
}