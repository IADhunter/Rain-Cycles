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
        _snapA = SettingsSnapshot.GetCached(pathA, room.abstractRoom.name);
        _snapB = SettingsSnapshot.GetCached(pathB, room.abstractRoom.name);
        _active = true;
        _lastT = -1f;

        var cam = room.game?.cameras?[0];
        if (cam != null)
        {
            cam.ClearBlendSnapshots();
            cam.SetBlendActive(room.abstractRoom.name);
            cam.UpdateBlendPalette(0f);
            cam.ApplyFade();
        }
        RoomCameraExtensions.BuildLightIndex(room);
        ApplyBlend(0f);
    }

    public static void Detach()
    {
        if (_room != null)
        {
            string roomName = _room.abstractRoom?.name;
            if (!string.IsNullOrEmpty(roomName))
            {
                RoomCameraExtensions.InvalidateRoomCache(roomName);
            }
            
            var cam = _room.game?.cameras?[0];
            if (cam != null && cam.room == _room)
            {
                cam.paletteB = -1;
                cam.ClearBlendSnapshots();
            }
        }

        _active = false;
        _externalT = false;
        _detachedThisFrame = true;
        _room = null;
        _pathA = null;
        _pathB = null;
        _snapOriginal = null;
        _lastLightT = -1f;
        _forceSkyRefresh = false;
        RoomCameraExtensions.ClearLightIndex();
    }

    public static void ResetFull()
    {
        _pendingOrigin = null;
        _lastRoomWasManaged = false;
        if (_active) Detach();
        _rtvScene = null; _acvScene = null; _psvScene = null;
        _forceSkyRefresh = false;
        _entryFrameT = -1f;
        
        _rcSlotsStaticACV = null;
        _rcSlotsStaticRTV = null;
        _rcSlotsStaticPSV = null;
        
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