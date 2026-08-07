using UnityEngine;
using System.Collections.Generic;
using RainCycles.Settings;
using RainCycles.Snapshot;
using RainCycles.Sky;
using RainCycles.Clock;
using RainCycles.Core;
using RainCycles.Blend;

namespace RainCycles.Core;

public static partial class SettingsBlendController
{
    private static void OnMoveCamera(On.RoomCamera.orig_MoveCamera_Room_int orig, RoomCamera self, Room newRoom, int camPos)
    {
        _moveCameraThisFrame = true;
        if (BlendClock.EditMode)
            BlendClock.SetEditMode(false);

        string prevRoomName = self.room?.abstractRoom?.name;
        bool prevWasManaged = prevRoomName != null && IsBlendRoom(self.room);

        string nextRoomName = newRoom?.abstractRoom?.name;
        bool nextIsManaged = nextRoomName != null && IsBlendRoom(newRoom);

        if (!string.IsNullOrEmpty(prevRoomName))
            RoomCameraExtensions.InvalidateRoomCache(prevRoomName);
        if (!string.IsNullOrEmpty(nextRoomName))
            RoomCameraExtensions.InvalidateRoomCache(nextRoomName);

        if (prevWasManaged && !nextIsManaged)
        {
            var blendData = self.GetBlendData();
            if (blendData != null)
            {
                blendData.isBlendActive = false;
            }

            if (!_active && _room != null)
            {
                _room = null;
                _pathA = null;
                _pathB = null;
                _snapA = null;
                _snapB = null;
            }

            if (newRoom?.roomSettings != null && !HasDayNightBlend(newRoom.roomSettings))
            {
                int correctPal = newRoom.roomSettings.Palette;
                self.ChangeMainPalette(correctPal);
                self.ApplyFade();
            }

            _activeSnapshot = null;
            _psvScene = null;
            _acvScene = null;
            ClearCachedVanillaFog();

            if (BlendClock.IsRunning && BlendClock.CurrentPhase == BlendClock.Phase.Idle && self.room != null)
            {
                ApplySkyForState(BlendClock.StateA, self.room);
            }
        }

        if (prevWasManaged)
            _lastRoomWasManaged = true;

        orig(self, newRoom, camPos);

        if (_active && _room != null && newRoom != _room)
        {
            Detach();
        }

        if (prevWasManaged && !nextIsManaged)
        {
            _activeSnapshot = null;
            _psvScene = null;
            _acvScene = null;
            ClearCachedVanillaFog();
        }

        if (newRoom == null || nextRoomName == null)
            return;

        bool newRoomInBlend = IsBlendRoom(newRoom);

        if (!newRoomInBlend)
            return;

        if (!BlendClock.IsRunning)
            return;

        if (BlendClock.CurrentPhase == BlendClock.Phase.Blending)
        {
            string pathA = StateFileResolver.ResolveSettingsPath(nextRoomName, BlendClock.StateA);
            string pathB = StateFileResolver.ResolveSettingsPath(nextRoomName, BlendClock.StateB);

            if (pathA != null && pathB != null && BlendClock.StateA != BlendClock.StateB)
            {
                SyncSkySlots(newRoom, BlendClock.StateA, BlendClock.StateB);

                if (self.room == newRoom)
                {
                    float t = BlendClock.SubPhaseLocalT;
                    AttachWithExternalT(newRoom, pathA, pathB);
                    SetExternalT(t);
                    _entryFrameT = t;
                }
                else
                {
                    _entryFrameT = BlendClock.SubPhaseLocalT;
                    SettingsSnapshot.GetCached(pathA, nextRoomName);
                    SettingsSnapshot.GetCached(pathB, nextRoomName);
                }
            }
        }
    }

    private static void OnChangeRoom(
        On.RoomCamera.orig_ChangeRoom orig, RoomCamera self,
        Room newRoom, int cameraPosition)
    {
        orig(self, newRoom, cameraPosition);

        string roomName = newRoom?.abstractRoom?.name;
        if (!string.IsNullOrEmpty(roomName))
            RoomCameraExtensions.InvalidateRoomCache(roomName);

        if (newRoom != null && !IsBlendRoom(newRoom))
        {
            var rs = newRoom.roomSettings;
            if (rs != null)
            {
                if (!HasDayNightBlend(rs))
                {
                    ForceLoadPalette(self, rs.Palette, ref self.fadeTexA);
                    
                    if (rs.fadePalette != null)
                    {
                        ForceLoadPalette(self, rs.fadePalette.palette, ref self.fadeTexB);
                        self.paletteB = rs.fadePalette.palette;
                        self.paletteBlend = (self.currentCameraPosition < rs.fadePalette.fades.Length) 
                            ? rs.fadePalette.fades[self.currentCameraPosition] 
                            : 0f;
                    }
                    else
                    {
                        self.paletteB = -1;
                        self.paletteBlend = 0f;
                    }
                }

                var terrainBlendDataReset = self.GetBlendData();
                if (terrainBlendDataReset != null)
                {
                    terrainBlendDataReset.isBlendActive = false;
                }
            }
        }

        var rcSlots = GetRcSlotsForRoom(newRoom);
        if (rcSlots != null)
        {
            var waterContainer = self.ReturnFContainer("Water");
            for (int i = 0; i < rcSlots.Count && i < 4; i++)
            {
                var slot = rcSlots[i];
                var sLeaser = self.spriteLeasers?.Find(s => s.drawableObject == slot);
                if (sLeaser?.sprites != null && sLeaser.sprites.Length > 0)
                {
                    var sprite = sLeaser.sprites[0];
                    sprite.RemoveFromContainer();
                    waterContainer.AddChildAtIndex(sprite, 3 - i);
                }
            }
        }

        if (BlendClock.IsRunning && newRoom != null && IsBlendRoom(newRoom))
        {
            SyncSkySlots(newRoom, BlendClock.StateA, BlendClock.StateB);
        }

        string newRoomNameClean = newRoom?.abstractRoom?.name;
        bool newRoomManaged = newRoomNameClean != null && IsBlendRoom(newRoom);
        if (!newRoomManaged)
        {
            _activeSnapshot = null;
            _psvScene = null;
            _entryFrameT = -1f;
            ClearCachedVanillaFog();
        }
    }

    // ============================================================
    // ROOMCAMERA.UPDATE - OCULTAR SLOTS SI HAY VIEW + SINCRONIZAR FOG
    // ============================================================
    private static void OnRoomCameraUpdate(On.RoomCamera.orig_Update orig, RoomCamera self)
    {
        orig(self);
        if (self.room == null) return;

        var blendData = self.GetBlendData();
        if (blendData != null && blendData.isBlendActive && blendData.terrainBlendedTexture != null)
        {
            Shader.SetGlobalTexture("_terrainPalette", blendData.terrainBlendedTexture);
        }

        string roomName = self.room.abstractRoom?.name;
        if (roomName == null) return;

        var rcState = RoomCameraExtensions.GetRoomBlendState(self.room);
        bool hasView = rcState.HasView;
        bool isBlendRoom = rcState.IsBlend;
        bool isStaticRoom = rcState.IsStatic;

        if ((isBlendRoom || isStaticRoom) && hasView)
        {
            _lastManagedRoomName = roomName;
            ForceHideVanillaSlots(self.room, rcState);
        }

        // ═══════════════════════════════════════════════════════════════
        // SINCRONIZAR POSICIÓN DEL FOG RC CON EL FOG VANILLA (PSV)
        // SOLO SI ESTAMOS EN UNA SALA PSV Y TENEMOS SLOTS DE FOG
        // ═══════════════════════════════════════════════════════════════
        if (_psvScene != null && _rcSlotsPSVFog != null && _rcSlotsPSVFog.Count > 0)
        {
            SyncFogSlotPosition(self);
        }
        // Si estamos en una sala que NO es PSV, limpiar la cache del fog
        else if (_psvScene == null && _cachedVanillaFog != null)
        {
            ClearCachedVanillaFog();
        }

        if (_lastRoomWasManaged && self.room != null && isBlendRoom)
        {
            bool stillManaged = isBlendRoom || isStaticRoom;

            if (stillManaged && BlendClock.IsRunning &&
                BlendClock.CurrentPhase == BlendClock.Phase.Idle)
            {
                int state = BlendClock.StateA;

                if (_lastManagedRoomName != null)
                {
                    string path = StateFileResolver.ResolveSettingsPath(_lastManagedRoomName, state);
                    if (path != null)
                    {
                        var snap2 = SettingsSnapshot.GetCached(path, _lastManagedRoomName);
                        if (snap2?.TintMultiply != null)
                            Shader.SetGlobalVector(RainWorld.ShadPropMultiplyColor,
                                new Vector4(snap2.TintMultiply.Value.r, snap2.TintMultiply.Value.g, snap2.TintMultiply.Value.b, 1f));
                        if (snap2?.TintAtmosphere != null)
                            Shader.SetGlobalVector(RainWorld.ShadPropAboveCloudsAtmosphereColor,
                                new Vector4(snap2.TintAtmosphere.Value.r, snap2.TintAtmosphere.Value.g, snap2.TintAtmosphere.Value.b, 1f));
                    }
                }
            }
        }
    }

    // ============================================================
    // FORCE HIDE VANILLA SLOTS - SOLO SI EL VIEW ESTÁ DECLARADO
    // ============================================================
    private static void ForceHideVanillaSlots(Room room, RoomCameraExtensions.RoomBlendState state)
    {
        if (room == null) return;
        if (!state.HasView) return;

        ViewType view = state.View;

        for (int i = 0; i < room.updateList.Count; i++)
        {
            if (room.updateList[i] is AboveCloudsView acv)
            {
                // PSV: ocultar sky
                if (view == ViewType.PSV || view == ViewType.ACV)
                {
                    acv.daySky.alpha = 0f;
                    acv.duskSky.alpha = 0f;
                    acv.nightSky.alpha = 0f;
                }
            }
            else if (room.updateList[i] is RoofTopView rtv)
            {
                // RTV: ocultar sky
                if (view == ViewType.RTV)
                {
                    rtv.daySky.alpha = 0f;
                    rtv.duskSky.alpha = 0f;
                    rtv.nightSky.alpha = 0f;
                }
            }
        }
    }

    private static List<BackgroundScene.Simple2DBackgroundIllustration> GetRcSlotsForRoom(Room room)
    {
        if (room == null) return null;
        for (int i = 0; i < room.updateList.Count; i++)
        {
            if (room.updateList[i] is AboveCloudsView)
            {
                var skyType = GetViewFromLoadedSettings(room);
                if (skyType == SkyType.ACV) return _rcSlotsACV;
                if (skyType == SkyType.PSV) return _rcSlotsPSV;
            }
            if (room.updateList[i] is RoofTopView)
                return _rcSlotsRTV;
        }
        return null;
    }
}