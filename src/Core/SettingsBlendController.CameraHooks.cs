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

        // ════════════════════════════════════════════════════════════════════
        // INVALIDAR CACHE AL MOVER CÁMARA
        // ════════════════════════════════════════════════════════════════════
        if (!string.IsNullOrEmpty(prevRoomName))
            RoomCameraExtensions.InvalidateRoomCache(prevRoomName);
        if (!string.IsNullOrEmpty(nextRoomName))
            RoomCameraExtensions.InvalidateRoomCache(nextRoomName);

        RSPlugin.log.LogDebug($"[OnMoveCamera] Moviendo cámara: '{prevRoomName}' → '{nextRoomName}'");

        if (prevWasManaged && !nextIsManaged)
        {
            RSPlugin.log.LogDebug($"[OnMoveCamera] Saliendo de sala blend '{prevRoomName}' a no gestionada");
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

            if (newRoom?.roomSettings != null)
            {
                int correctPal = newRoom.roomSettings.Palette;
                self.ChangeMainPalette(correctPal);
                self.ApplyFade();
            }

            _activeSnapshot = null;
            _psvScene = null;
            _acvScene = null;

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
            RSPlugin.log.LogDebug($"[OnMoveCamera] Detectando cambio de sala mientras blend activo, llamando Detach");
            Detach();
        }

        if (prevWasManaged && !nextIsManaged)
        {
            _activeSnapshot = null;
            _psvScene = null;
            _acvScene = null;
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
            string pathA = StateFileResolver.GetRainStateSettingsFile(nextRoomName, BlendClock.StateA);
            string pathB = StateFileResolver.GetRainStateSettingsFile(nextRoomName, BlendClock.StateB);

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
                    var snapA2 = StaticTintManager.GetCachedSnapshot(pathA, nextRoomName);
                    var snapB2 = StaticTintManager.GetCachedSnapshot(pathB, nextRoomName);
                    if (snapA2 != null && snapB2 != null)
                    {
                        var lerped2 = SettingsSnapshot.Lerp(snapA2, snapB2, _entryFrameT);
                    }
                }
            }
        }
    }

    private static void OnChangeRoom(
        On.RoomCamera.orig_ChangeRoom orig, RoomCamera self,
        Room newRoom, int cameraPosition)
    {
        // Llamar al original PRIMERO - que ejecute ChangeBothPalettes con los valores de newRoom
        orig(self, newRoom, cameraPosition);

        // ════════════════════════════════════════════════════════════════════
        // INVALIDAR CACHE AL CAMBIAR DE SALA
        // ════════════════════════════════════════════════════════════════════
        string roomName = newRoom?.abstractRoom?.name;
        if (!string.IsNullOrEmpty(roomName))
            RoomCameraExtensions.InvalidateRoomCache(roomName);

        // ============================================================
        // FORZAR RECARGA DE PALETAS PARA SALAS NO BLEND
        // ============================================================
        if (newRoom != null && !IsBlendRoom(newRoom))
        {
            var rs = newRoom.roomSettings;
            if (rs != null)
            {
                RSPlugin.log.LogDebug($"[OnChangeRoom] Forzando recarga de paletas para sala NO blend: {newRoom.abstractRoom?.name}");
                
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
        }

        // ============================================================
        // SLOTS - Mover slots al contenedor Water si es necesario
        // ============================================================
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
        }
    }

    private static void OnRoomCameraUpdate(On.RoomCamera.orig_Update orig, RoomCamera self)
    {
        orig(self);
        if (self.room == null) return;

        string roomName = self.room.abstractRoom?.name;
        if (roomName == null) return;

        bool isBlendRoom = IsBlendRoom(self.room);
        bool isStaticRoom = StaticTintManager.IsStaticViewRoom(self.room);

        if (isBlendRoom)
        {
            _lastManagedRoomName = roomName;
            ForceHideVanillaSlots(self.room);
        }
        else if (isStaticRoom)
        {
            ForceHideVanillaSlots(self.room);
        }

        // Restaurar tintes en idle para salas blend
        if (_lastRoomWasManaged && self.room != null && IsBlendRoom(self.room))
        {
            bool stillManaged = IsBlendRoom(self.room) || StaticTintManager.IsStaticViewRoom(self.room);

            if (stillManaged && BlendClock.IsRunning &&
                BlendClock.CurrentPhase == BlendClock.Phase.Idle)
            {
                int state = BlendClock.StateA;

                if (_lastManagedRoomName != null)
                {
                    string path = StateFileResolver.GetRainStateSettingsFile(_lastManagedRoomName, state);
                    if (path != null)
                    {
                        var snap = StaticTintManager.GetCachedSnapshot(path, _lastManagedRoomName);
                        if (snap?.TintMultiply != null)
                            Shader.SetGlobalVector(RainWorld.ShadPropMultiplyColor,
                                new Vector4(snap.TintMultiply.Value.r, snap.TintMultiply.Value.g, snap.TintMultiply.Value.b, 1f));
                        if (snap?.TintAtmosphere != null)
                            Shader.SetGlobalVector(RainWorld.ShadPropAboveCloudsAtmosphereColor,
                                new Vector4(snap.TintAtmosphere.Value.r, snap.TintAtmosphere.Value.g, snap.TintAtmosphere.Value.b, 1f));
                    }
                }
            }
        }
    }

    private static void OnRoomSettingsSave(On.RoomSettings.orig_Save orig, RoomSettings self)
    {
        string filePath = self.filePath;
        string rcViewBlock = null;
        if (filePath != null)
        {
            bool hasRcData = BlendSettingsLoader.Active != null
                || System.IO.File.Exists(filePath) && System.IO.File.ReadAllText(filePath).Contains("RC_VIEW:");
            if (hasRcData) rcViewBlock = ExtractRcViewBlock(filePath);
        }
        orig(self);
        if (rcViewBlock != null) ReappendRcViewBlock(filePath, rcViewBlock);
    }

    private static void ForceHideVanillaSlots(Room room)
    {
        if (room == null) return;
        for (int i = 0; i < room.updateList.Count; i++)
        {
            if (room.updateList[i] is AboveCloudsView acv)
            {
                acv.daySky.alpha = 0f;
                acv.duskSky.alpha = 0f;
                acv.nightSky.alpha = 0f;
            }
            else if (room.updateList[i] is RoofTopView rtv)
            {
                rtv.daySky.alpha = 0f;
                rtv.duskSky.alpha = 0f;
                rtv.nightSky.alpha = 0f;
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