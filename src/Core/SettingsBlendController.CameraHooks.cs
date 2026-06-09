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
    private static SkyType GetViewFromLoadedSettings(Room room)
    {
        if (room?.roomSettings?.filePath == null) return SkyType.None;
        var snap = SettingsSnapshot.FromFile(room.roomSettings.filePath);
        return snap.ViewType == ViewType.ACV ? SkyType.ACV
            : snap.ViewType == ViewType.RTV ? SkyType.RTV
            : snap.ViewType == ViewType.PSV ? SkyType.PSV
            : SkyType.None;
    }

    private static void OnMoveCamera(On.RoomCamera.orig_MoveCamera_Room_int orig, RoomCamera self, Room newRoom, int camPos)
    {
        _moveCameraThisFrame = true;
        if (BlendClock.EditMode)
            BlendClock.SetEditMode(false);

        string prevRoomName = self.room?.abstractRoom?.name;
        bool prevWasManaged = prevRoomName != null && IsBlendRoom(self.room);

        string nextRoomName = newRoom?.abstractRoom?.name;
        bool nextIsManaged  = nextRoomName != null && IsBlendRoom(newRoom);

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

            if (newRoom?.roomSettings != null)
            {
                int correctPal = newRoom.roomSettings.Palette;
                self.ChangeMainPalette(correctPal);
                self.ApplyFade();
            }

            _activeSnapshot = null;
            _psvScene = null;
            _acvScene = null;
            _lastIdleRotatedState = -1;

            if (BlendClock.IsRunning && BlendClock.CurrentPhase == BlendClock.Phase.Idle && self.room != null)
            {
                var prevSkyType = GetViewFromLoadedSettings(self.room);
                if (prevSkyType != SkyType.None && GetSlotDay(prevSkyType) != BlendClock.StateA)
                    ApplySkyForState(BlendClock.StateA, self.room);
            }
        }

        if (prevWasManaged)
            _lastRoomWasManaged = true;

        orig(self, newRoom, camPos);

        if (_active && _room != null && newRoom != _room)
            Detach();

        if (prevWasManaged && !nextIsManaged)
        {
            _activeSnapshot = null;
            _psvScene = null;
            _acvScene = null;
            _lastIdleRotatedState = -1;
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
                var newSkyType = GetViewFromLoadedSettings(newRoom);
                if (newSkyType != SkyType.None && GetSlotDay(newSkyType) != BlendClock.StateA)
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
                        if (lerped2.TintCloudAtmosphere.HasValue)
                            _lastAtmosphereColor = lerped2.TintCloudAtmosphere.Value;
                    }
                }
            }
        }
    }

    private static void OnChangeRoom(
        On.RoomCamera.orig_ChangeRoom orig, RoomCamera self,
        Room newRoom, int cameraPosition)
    {
        if (BlendClock.IsRunning && newRoom != null)
        {
            string newRoomName = newRoom.abstractRoom?.name;

        }

        if (BlendClock.IsRunning && newRoom != null)
        {
            string newRoomName = newRoom.abstractRoom?.name;
            if (newRoomName != null && IsBlendRoom(newRoom))
            {
                var skyType = GetViewFromLoadedSettings(newRoom);
                var acv = _acvScene;
                var rtv = _rtvScene;
                bool isAcv = skyType == SkyType.ACV && acv != null && acv.room?.abstractRoom?.name == newRoomName;
                bool isRtv = skyType == SkyType.RTV && rtv != null && rtv.room?.abstractRoom?.name == newRoomName;
                if (isAcv || isRtv)
                {
                    var thisSky = isAcv ? SkyType.ACV : SkyType.RTV;
                    var phase = BlendClock.CurrentPhase;
                    if (phase == BlendClock.Phase.Blending && GetSlotDay(thisSky) != BlendClock.StateA)
                        SyncSkySlots(newRoom, BlendClock.StateA, BlendClock.StateB);
                    else if (phase == BlendClock.Phase.Idle && GetSlotDay(thisSky) != BlendClock.StateA)
                        SyncSkySlots(newRoom, BlendClock.StateA, NextStateIn(BlendSettingsLoader.Active, BlendClock.StateA));
                }
            }
        }

        orig(self, newRoom, cameraPosition);

        var rcSlots = GetRcSlotsForRoom(newRoom);
        if (rcSlots != null)
        {
            var waterContainer = self.ReturnFContainer("Water");
            for (int i = 0; i < rcSlots.Count && i < 3; i++)
            {
                var slot = rcSlots[i];
                var sLeaser = self.spriteLeasers?.Find(s => s.drawableObject == slot);
                if (sLeaser?.sprites != null && sLeaser.sprites.Length > 0)
                {
                    var sprite = sLeaser.sprites[0];
                    sprite.RemoveFromContainer();
                    waterContainer.AddChildAtIndex(sprite, 2 - i);
                }
            }
        }

        if (BlendClock.IsRunning && newRoom != null)
        {
            string newRoomName = newRoom.abstractRoom?.name;
            if (newRoomName != null && IsBlendRoom(newRoom))
            {
                var skyType = GetViewFromLoadedSettings(newRoom);
                if (skyType != SkyType.None)
                {
                    var phase = BlendClock.CurrentPhase;
                    int syncA, syncB;
                    if (phase == BlendClock.Phase.Blending)
                    {
                        syncA = BlendClock.StateA;
                        syncB = BlendClock.StateB;
                    }
                    else
                    {
                        syncA = BlendClock.StateA;
                        syncB = NextStateIn(BlendSettingsLoader.Active, BlendClock.StateA);
                    }
                    _pendingSkySync = true;
                    _pendingSkyStateA = syncA;
                    _pendingSkyStateB = syncB;

                    var acvPost = _acvScene;
                    var rtvPost = _rtvScene;
                    bool isAcvPost = skyType == SkyType.ACV && acvPost != null && acvPost.room?.abstractRoom?.name == newRoomName;
                    bool isRtvPost = skyType == SkyType.RTV && rtvPost != null && rtvPost.room?.abstractRoom?.name == newRoomName;
                    if (isAcvPost || isRtvPost)
                        SyncSkySlots(newRoom, syncA, syncB);
                }
            }
        }

        string newRoomNameClean = newRoom?.abstractRoom?.name;
        bool newRoomManaged = newRoomNameClean != null && IsBlendRoom(newRoom);
        if (!newRoomManaged)
        {
            _activeSnapshot = null;
            _psvScene = null;
            _psvSlotDay = _psvSlotDusk = _psvSlotNight = -1;
            _pendingSkySync = false; _pendingSkyStateA = -1; _pendingSkyStateB = -1;
            _entryFrameT = -1f; _hasSavedAlphas = false;
            _lastIdleRotatedState = -1;
        }
    }

    private static void OnRoomCameraUpdate(On.RoomCamera.orig_Update orig, RoomCamera self)
    {
        orig(self);
        if (self.room == null) return;
        var settings = BlendSettingsLoader.Active;
        string roomName = self.room.abstractRoom?.name;
        if (roomName == null) return;

        bool isBlendRoom  = IsBlendRoom(self.room);
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

        if (_lastRoomWasManaged && self.room != null)
        {
            string currentRoom = self.room.abstractRoom?.name;
            bool stillManaged = currentRoom != null && (IsBlendRoom(self.room) || StaticTintManager.IsStaticViewRoom(self.room));

            if (!stillManaged)
            {
                _lastRoomWasManaged = false;
                Shader.SetGlobalVector(RainWorld.ShadPropMultiplyColor, new Vector4(1f, 1f, 1f, 1f));
                Shader.SetGlobalVector(RainWorld.ShadPropAboveCloudsAtmosphereColor,
                    new Vector4(0.16078432f, 0.23137255f, 0.31764707f, 1f));
                StaticTintManager.ApplyPSVDefaults(self.room);
            }
        }

        if (_lastRoomWasManaged && self.room != null)
        {
            string currentRoom = self.room.abstractRoom?.name;
            bool stillManaged = currentRoom != null && (IsBlendRoom(self.room) || StaticTintManager.IsStaticViewRoom(self.room));

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
            else if (!stillManaged)
            {
                Shader.SetGlobalVector(RainWorld.ShadPropMultiplyColor, new Vector4(1f, 1f, 1f, 1f));
                Shader.SetGlobalVector(RainWorld.ShadPropAboveCloudsAtmosphereColor,
                    new Vector4(0.16078432f, 0.23137255f, 0.31764707f, 1f));
                StaticTintManager.ApplyPSVDefaults(self.room);
                StaticTintManager.ApplyForRoom(self.room);
            }
        }

        if (self.room != null)
        {
            string currentRoom = self.room.abstractRoom?.name;
            bool isBlendManaged = currentRoom != null && IsBlendRoom(self.room);

            if (!isBlendManaged)
            {
                StaticTintManager.ApplyForRoom(self.room);
            }
            else if (BlendClock.IsRunning)
            {
                if (BlendClock.CurrentPhase == BlendClock.Phase.Blending)
                {
                    if (_acvScene != null && _acvScene.room == self.room)
                    {
                        _acvScene.atmosphereColor = _lastAtmosphereColor;
                        Shader.SetGlobalVector(RainWorld.ShadPropAboveCloudsAtmosphereColor,
                            new Vector4(_lastAtmosphereColor.r, _lastAtmosphereColor.g, _lastAtmosphereColor.b, 1f));
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