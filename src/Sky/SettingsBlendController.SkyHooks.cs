using System.Collections.Generic;
using UnityEngine;
using RainCycles.Settings;
using RainCycles.Snapshot;
using RainCycles.Clock;
using RainCycles.Core;

namespace RainCycles.Core;

public static partial class SettingsBlendController
{
    // ── RoofTopView ──────────────────────────────────────────────────────

    private static void OnRoofTopViewCtor(
        On.RoofTopView.orig_ctor orig, RoofTopView self,
        Room room, RoomSettings.RoomEffect effect)
    {
        string settingsPath = room?.roomSettings?.filePath;
        var snap = !string.IsNullOrEmpty(settingsPath) && System.IO.File.Exists(settingsPath)
            ? SettingsSnapshot.FromFile(settingsPath)
            : null;

        bool hasRcType = snap != null && snap.HasRcType;
        bool isBlendManaged = hasRcType && snap.RcType == RcType.Blend;
        bool isStaticManaged = hasRcType && snap.RcType == RcType.Static;

        if (!hasRcType)
        {
            orig(self, room, effect);
            return;
        }

        var savedMultiply = Shader.GetGlobalVector(RainWorld.ShadPropMultiplyColor);
        var savedAtmosphere = Shader.GetGlobalVector(RainWorld.ShadPropAboveCloudsAtmosphereColor);

        orig(self, room, effect);

        var cam = room.game?.cameras?[0];
        if (cam == null || cam.room != room)
        {
            Shader.SetGlobalVector(RainWorld.ShadPropMultiplyColor, savedMultiply);
            Shader.SetGlobalVector(RainWorld.ShadPropAboveCloudsAtmosphereColor, savedAtmosphere);
        }

        if (room == null) return;

        string roomName = room.abstractRoom?.name;
        if (string.IsNullOrEmpty(roomName)) return;

        string currentRegionCode = room.world?.region?.name?.ToUpperInvariant();
        var regionSettings = BlendSettingsLoader.GetForRegion(currentRegionCode);

        ViewType roomView = snap.ViewType;
        bool shouldCreateRTV = roomView == ViewType.RTV;

        if (shouldCreateRTV && isBlendManaged)
        {
            _rcSlotsRTV = CreateRcSlotsVanilla(self, room, SkyType.RTV);
            _rtvScene = self;

            int state = StateFileResolver.GetStateFromPath(room.roomSettings?.filePath, roomName);
            if (state < 1)
            {
                int n = StateFileResolver.CountRainStateFiles(roomName);
                int cycle = room.game?.GetStorySession?.saveState?.cycleNumber ?? 0;
                state = n > 0 ? (cycle % n) + 1 : 1;
            }
            int stateB = NextStateIn(regionSettings, state);
            int stateC = NextStateIn(regionSettings, stateB);

            UpdateRcSlots(SkyType.RTV, state, stateB, stateC, cam);
            SetSlotDay(SkyType.RTV, state);
            SetSlotDusk(SkyType.RTV, stateB);
            SetSlotNight(SkyType.RTV, stateC);
            ApplyRcSlotsAlpha(SkyType.RTV, 0f, false);
        }
        else if (shouldCreateRTV && isStaticManaged)
        {
            if (_rcSlotsStaticRTV == null)
            {
                _rcSlotsStaticRTV = CreateStaticSlotsVanilla(self, room, SkyType.RTV);
                int state = StateFileResolver.GetStateFromPath(room.roomSettings?.filePath, roomName);
                if (state < 1) state = 1;
                string file = regionSettings?.GetBkgFileForState(state, ViewType.RTV);
                if (!string.IsNullOrEmpty(file))
                    _rcSlotsStaticRTV[0].illustrationName = System.IO.Path.GetFileNameWithoutExtension(file);
                _rcSlotsStaticRTV[0].alpha = 1f;
            }
            _rtvScene = self;
        }

        if (!isBlendManaged && !isStaticManaged) return;
        _rtvScene = self;
        _rtvSlotDay = _rtvSlotDusk = _rtvSlotNight = -1;
    }

    private static void OnRoofTopViewUpdate(
        On.RoofTopView.orig_Update orig, RoofTopView self, bool eu)
    {
        var cam = self.room?.game?.cameras?[0];
        bool camIsHere = cam != null && cam.room == self.room;

        string settingsPath = self.room?.roomSettings?.filePath;
        var snap = !string.IsNullOrEmpty(settingsPath) && System.IO.File.Exists(settingsPath)
            ? SettingsSnapshot.FromFile(settingsPath)
            : null;

        bool hasRcType = snap != null && snap.HasRcType;
        bool isBlendManaged = hasRcType && snap.RcType == RcType.Blend;
        bool isStaticManaged = hasRcType && snap.RcType == RcType.Static;

        if (isBlendManaged && snap.ViewType == ViewType.RTV && (_rcSlotsRTV == null || _rcSlotsRTV.Count == 0))
        {
            _rcSlotsRTV = CreateRcSlotsVanilla(self, self.room, SkyType.RTV);
            _rtvScene = self;
            
            int state = BlendClock.StateA;
            if (state < 1)
            {
                state = StateFileResolver.GetStateFromPath(self.room.roomSettings?.filePath, self.room?.abstractRoom?.name);
                if (state < 1) state = 1;
            }
            
            string currentRegionCode = self.room.world?.region?.name?.ToUpperInvariant();
            var regionSettings = BlendSettingsLoader.GetForRegion(currentRegionCode);
            int stateB = NextStateIn(regionSettings, state);
            int stateC = NextStateIn(regionSettings, stateB);
            
            UpdateRcSlots(SkyType.RTV, state, stateB, stateC, cam, self.room);
            SetSlotDay(SkyType.RTV, state);
            SetSlotDusk(SkyType.RTV, stateB);
            SetSlotNight(SkyType.RTV, stateC);
            
            bool isBlending = BlendClock.IsRunning && BlendClock.CurrentPhase == BlendClock.Phase.Blending;
            ApplyRcSlotsAlpha(SkyType.RTV, isBlending ? BlendClock.SubPhaseLocalT : 0f, isBlending);
        }

        if (!hasRcType)
        {
            orig(self, eu);
            return;
        }

        if (_pendingSkySync && camIsHere)
        {
            var settings = BlendSettingsLoader.Active;
            SkyType pendingSky = _pendingSyncSky == 0 ? SkyType.ACV :
                                (_pendingSyncSky == 1 ? SkyType.RTV : SkyType.PSV);
            int stateC = settings != null ? NextStateIn(settings, _pendingStateB) : _pendingStateB;
            UpdateRcSlots(pendingSky, _pendingStateA, _pendingStateB, stateC);
            _pendingSkySync = false;
            _pendingSyncSky = -1;
        }

        if (_rcSlotsRTV != null)
            PreApplySlotAlphas(_rcSlotsRTV);

        if (camIsHere && _rcSlotsStaticRTV != null && _rcSlotsStaticRTV.Count > 0
            && _rcSlotsStaticRTV[0].illustrationName != "Futile_White")
        {
            RefreshSlotSprite(_rcSlotsStaticRTV[0], _rcSlotsStaticRTV[0].illustrationName, cam);
        }

        orig(self, eu);
    }

    // ── AboveCloudsView ──────────────────────────────────────────────────

    private static void OnAboveCloudsViewCtor(
        On.AboveCloudsView.orig_ctor orig, AboveCloudsView self,
        Room room, RoomSettings.RoomEffect effect)
    {
        string settingsPath = room?.roomSettings?.filePath;
        var snap = !string.IsNullOrEmpty(settingsPath) && System.IO.File.Exists(settingsPath)
            ? SettingsSnapshot.FromFile(settingsPath)
            : null;

        bool hasRcType = snap != null && snap.HasRcType;
        bool isBlendManaged = hasRcType && snap.RcType == RcType.Blend;
        bool isStaticManaged = hasRcType && snap.RcType == RcType.Static;

        if (!hasRcType)
        {
            orig(self, room, effect);
            return;
        }

        var savedMultiply = Shader.GetGlobalVector(RainWorld.ShadPropMultiplyColor);
        var savedAtmosphere = Shader.GetGlobalVector(RainWorld.ShadPropAboveCloudsAtmosphereColor);

        orig(self, room, effect);

        var cam = room.game?.cameras?[0];
        if (cam == null || cam.room != room)
        {
            Shader.SetGlobalVector(RainWorld.ShadPropMultiplyColor, savedMultiply);
            Shader.SetGlobalVector(RainWorld.ShadPropAboveCloudsAtmosphereColor, savedAtmosphere);
        }

        if (room == null) return;

        string roomName = room.abstractRoom?.name;
        if (string.IsNullOrEmpty(roomName)) return;

        string currentRegionCode = room.world?.region?.name?.ToUpperInvariant();
        var regionSettings = BlendSettingsLoader.GetForRegion(currentRegionCode);

        ViewType roomView = snap.ViewType;
        SkyType targetSky = SkyType.None;
        if (roomView == ViewType.ACV) targetSky = SkyType.ACV;
        else if (roomView == ViewType.PSV) targetSky = SkyType.PSV;

        if (targetSky != SkyType.None && isBlendManaged)
        {
            if (targetSky == SkyType.ACV)
            {
                _rcSlotsACV = CreateRcSlotsVanilla(self, room, SkyType.ACV);
                _acvScene = self;
                
                int state = StateFileResolver.GetStateFromPath(room.roomSettings?.filePath, roomName);
                if (state < 1)
                {
                    int n = StateFileResolver.CountRainStateFiles(roomName);
                    int cycle = room.game?.GetStorySession?.saveState?.cycleNumber ?? 0;
                    state = n > 0 ? (cycle % n) + 1 : 1;
                }
                int stateB = NextStateIn(regionSettings, state);
                int stateC = NextStateIn(regionSettings, stateB);

                UpdateRcSlots(targetSky, state, stateB, stateC, cam);
                SetSlotDay(targetSky, state);
                SetSlotDusk(targetSky, stateB);
                SetSlotNight(targetSky, stateC);
                ApplyRcSlotsAlpha(targetSky, 0f, false);
            }
            else if (targetSky == SkyType.PSV)
            {
                _rcSlotsPSV = CreateRcSlotsVanilla(self, room, SkyType.PSV);
                _rcSlotsPSVFog = CreateRcSlotsVanilla(self, room, SkyType.PSV);
                _rcSlotsPSVSun = CreateSunSlots(self, room, SkyType.PSV, false);
                _psvScene = self;

                int state = StateFileResolver.GetStateFromPath(room.roomSettings?.filePath, roomName);
                if (state < 1)
                {
                    int n = StateFileResolver.CountRainStateFiles(roomName);
                    int cycle = room.game?.GetStorySession?.saveState?.cycleNumber ?? 0;
                    state = n > 0 ? (cycle % n) + 1 : 1;
                }
                int stateB = NextStateIn(regionSettings, state);
                int stateC = NextStateIn(regionSettings, stateB);

                UpdateRcSlots(targetSky, state, stateB, stateC, cam);
                SetSlotDay(targetSky, state);
                SetSlotDusk(targetSky, stateB);
                SetSlotNight(targetSky, stateC);
                ApplyRcSlotsAlpha(targetSky, 0f, false);
            }
        }
        else if (targetSky != SkyType.None && isStaticManaged)
        {
            if (targetSky == SkyType.ACV && _rcSlotsStaticACV == null)
            {
                _rcSlotsStaticACV = CreateStaticSlotsVanilla(self, room, SkyType.ACV);
                int state = StateFileResolver.GetStateFromPath(room.roomSettings?.filePath, roomName);
                if (state < 1) state = 1;
                string file = regionSettings?.GetBkgFileForState(state, ViewType.ACV);
                if (!string.IsNullOrEmpty(file))
                    _rcSlotsStaticACV[0].illustrationName = System.IO.Path.GetFileNameWithoutExtension(file);
                _rcSlotsStaticACV[0].alpha = 1f;
            }
            else if (targetSky == SkyType.PSV && _rcSlotsStaticPSV == null)
            {
                _rcSlotsStaticPSV = CreateStaticSlotsVanilla(self, room, SkyType.PSV);
                int state = StateFileResolver.GetStateFromPath(room.roomSettings?.filePath, roomName);
                if (state < 1) state = 1;
                string file = regionSettings?.GetBkgFileForState(state, ViewType.PSV);
                if (!string.IsNullOrEmpty(file))
                    _rcSlotsStaticPSV[0].illustrationName = System.IO.Path.GetFileNameWithoutExtension(file);
                _rcSlotsStaticPSV[0].alpha = 1f;
            }
        }

        if (!isBlendManaged && !isStaticManaged)
        {
            if (targetSky == SkyType.ACV && _acvScene == null) _acvScene = self;
            if (targetSky == SkyType.PSV && _psvScene == null) _psvScene = self;
            return;
        }

        _acvScene = self;
        _acvSlotDay = _acvSlotDusk = _acvSlotNight = -1;
    }

    private static void OnAboveCloudsViewUpdate(
        On.AboveCloudsView.orig_Update orig, AboveCloudsView self, bool eu)
    {
        var cam = self.room?.game?.cameras?[0];
        bool camIsHere = cam != null && cam.room == self.room;
        string roomName = self.room?.abstractRoom?.name;

        string settingsPath = self.room?.roomSettings?.filePath;
        var snap = !string.IsNullOrEmpty(settingsPath) && System.IO.File.Exists(settingsPath)
            ? SettingsSnapshot.FromFile(settingsPath)
            : null;

        bool hasRcType = snap != null && snap.HasRcType;
        bool isBlendManaged = hasRcType && snap.RcType == RcType.Blend;
        bool isStaticManaged = hasRcType && snap.RcType == RcType.Static;

        if (isBlendManaged && snap.ViewType == ViewType.ACV && (_rcSlotsACV == null || _rcSlotsACV.Count == 0))
        {
            _rcSlotsACV = CreateRcSlotsVanilla(self, self.room, SkyType.ACV);
            _acvScene = self;
            
            int state = BlendClock.StateA;
            if (state < 1)
            {
                state = StateFileResolver.GetStateFromPath(self.room.roomSettings?.filePath, roomName);
                if (state < 1) state = 1;
            }
            
            string currentRegionCode = self.room.world?.region?.name?.ToUpperInvariant();
            var regionSettings = BlendSettingsLoader.GetForRegion(currentRegionCode);
            int stateB = NextStateIn(regionSettings, state);
            int stateC = NextStateIn(regionSettings, stateB);
            
            UpdateRcSlots(SkyType.ACV, state, stateB, stateC, cam, self.room);
            SetSlotDay(SkyType.ACV, state);
            SetSlotDusk(SkyType.ACV, stateB);
            SetSlotNight(SkyType.ACV, stateC);
            
            bool isBlending = BlendClock.IsRunning && BlendClock.CurrentPhase == BlendClock.Phase.Blending;
            ApplyRcSlotsAlpha(SkyType.ACV, isBlending ? BlendClock.SubPhaseLocalT : 0f, isBlending);
        }

        if (isBlendManaged && snap.ViewType == ViewType.PSV && (_rcSlotsPSV == null || _rcSlotsPSV.Count == 0))
        {
            _rcSlotsPSV = CreateRcSlotsVanilla(self, self.room, SkyType.PSV);
            _rcSlotsPSVFog = CreateRcSlotsVanilla(self, self.room, SkyType.PSV);
            _rcSlotsPSVSun = CreateSunSlots(self, self.room, SkyType.PSV, false);
            _psvScene = self;
            
            int state = BlendClock.StateA;
            if (state < 1)
            {
                state = StateFileResolver.GetStateFromPath(self.room.roomSettings?.filePath, roomName);
                if (state < 1) state = 1;
            }
            
            string currentRegionCode = self.room.world?.region?.name?.ToUpperInvariant();
            var regionSettings = BlendSettingsLoader.GetForRegion(currentRegionCode);
            int stateB = NextStateIn(regionSettings, state);
            int stateC = NextStateIn(regionSettings, stateB);
            
            UpdateRcSlots(SkyType.PSV, state, stateB, stateC, cam, self.room);
            SetSlotDay(SkyType.PSV, state);
            SetSlotDusk(SkyType.PSV, stateB);
            SetSlotNight(SkyType.PSV, stateC);
            
            bool isBlending = BlendClock.IsRunning && BlendClock.CurrentPhase == BlendClock.Phase.Blending;
            ApplyRcSlotsAlpha(SkyType.PSV, isBlending ? BlendClock.SubPhaseLocalT : 0f, isBlending);
        }

        if (!hasRcType)
        {
            orig(self, eu);
            return;
        }

        if (_pendingSkySync && camIsHere)
        {
            var settings = BlendSettingsLoader.Active;
            SkyType pendingSky = _pendingSyncSky == 0 ? SkyType.ACV :
                                (_pendingSyncSky == 1 ? SkyType.RTV : SkyType.PSV);
            int stateC = settings != null ? NextStateIn(settings, _pendingStateB) : _pendingStateB;
            UpdateRcSlots(pendingSky, _pendingStateA, _pendingStateB, stateC);
            _pendingSkySync = false;
            _pendingSyncSky = -1;
        }

        if (_rcSlotsACV != null)
            PreApplySlotAlphas(_rcSlotsACV);
        if (_rcSlotsPSV != null)
            PreApplySlotAlphas(_rcSlotsPSV);
        if (_rcSlotsPSVFog != null)
            PreApplySlotAlphas(_rcSlotsPSVFog);
        if (_rcSlotsPSVSun != null)
        {
            PreApplySlotAlphas(_rcSlotsPSVSun);
            ForceSunShader(_rcSlotsPSVSun, cam);
        }

        if (!isBlendManaged && !isStaticManaged)
        {
            orig(self, eu);
            if (camIsHere)
            {
                StaticTintManager.ApplyForRoom(self.room);
                StaticTintManager.ApplyCloudAtmosphereToInstance(self, self.room);

                foreach (var el in self.elements)
                {
                    if (el is AboveCloudsView.HorizonFog hf && hf.illustrationName?.StartsWith("pnk_") == true)
                    {
                        foreach (var sl in cam.spriteLeasers)
                        {
                            if (sl.drawableObject == hf && sl.sprites != null && sl.sprites.Length > 0)
                            {
                                sl.sprites[0].alpha = 0f;
                                sl.sprites[0].isVisible = false;
                            }
                        }
                    }
                    if (el is BackgroundScene.AdditiveBackgroundIllustration abi && abi.illustrationName?.StartsWith("pnk_") == true)
                        abi.alpha = 0f;
                }

                var skyType = GetViewFromLoadedSettings(self.room);
                if (skyType != SkyType.None)
                {
                    var staticSlots = skyType == SkyType.ACV ? _rcSlotsStaticACV :
                                      skyType == SkyType.RTV ? _rcSlotsStaticRTV : _rcSlotsStaticPSV;

                    if (staticSlots != null && staticSlots.Count > 0)
                    {
                        if (staticSlots[0].illustrationName == "Futile_White")
                        {
                            var settings = BlendSettingsLoader.Active;
                            int state = StateFileResolver.GetStateFromPath(self.room.roomSettings?.filePath, roomName);
                            if (state > 0 && settings != null)
                            {
                                var view = skyType == SkyType.ACV ? ViewType.ACV : (skyType == SkyType.RTV ? ViewType.RTV : ViewType.PSV);
                                string file = settings.GetBkgFileForState(state, view);
                                if (!string.IsNullOrEmpty(file))
                                    staticSlots[0].illustrationName = System.IO.Path.GetFileNameWithoutExtension(file);
                            }
                        }
                        RefreshSlotSprite(staticSlots[0], staticSlots[0].illustrationName, cam);
                    }
                }
            }
            return;
        }

        orig(self, eu);

        if (self.room == null) return;
        if (_acvScene == null) _acvScene = self;

        foreach (var el in self.elements)
            if (el is AboveCloudsView.DistantBuilding db) db.alpha = 1f;

        foreach (var el in self.elements)
        {
            if (el is AboveCloudsView.HorizonFog hf && hf.illustrationName?.StartsWith("pnk_") == true)
            {
                foreach (var sl in cam.spriteLeasers)
                {
                    if (sl.drawableObject == hf && sl.sprites != null && sl.sprites.Length > 0)
                    {
                        sl.sprites[0].alpha = 0f;
                        sl.sprites[0].isVisible = false;
                    }
                }
            }
            if (el is BackgroundScene.AdditiveBackgroundIllustration abi && abi.illustrationName?.StartsWith("pnk_") == true)
                abi.alpha = 0f;
        }

        if (isBlendManaged && camIsHere)
            self.atmosphereColor = _lastAtmosphereColor;
    }

    // ── Creación de slots ───────────────────────────────────────────────

    private static List<BackgroundScene.Simple2DBackgroundIllustration> CreateRcSlotsVanilla(
        BackgroundScene scene, Room room, SkyType sky)
    {
        var slots = new List<BackgroundScene.Simple2DBackgroundIllustration>();
        for (int i = 0; i < 3; i++)
        {
            var slot = new BackgroundScene.Simple2DBackgroundIllustration(
                scene, "Futile_White", new Vector2(683f, 384f));
            slot.alpha = 0f;
            scene.AddElement(slot);
            slots.Add(slot);
        }
        return slots;
    }

    private static List<BackgroundScene.Simple2DBackgroundIllustration> CreateStaticSlotsVanilla(
        BackgroundScene scene, Room room, SkyType sky)
    {
        var slots = new List<BackgroundScene.Simple2DBackgroundIllustration>();
        var slot = new BackgroundScene.Simple2DBackgroundIllustration(
            scene, "Futile_White", new Vector2(683f, 384f));
        slot.alpha = 0f;
        scene.AddElement(slot);
        slots.Add(slot);
        return slots;
    }

    private static List<BackgroundScene.Simple2DBackgroundIllustration> CreateSunSlots(
        BackgroundScene scene, Room room, SkyType sky, bool isStatic)
    {
        int count = isStatic ? 1 : 3;
        var slots = new List<BackgroundScene.Simple2DBackgroundIllustration>();
        for (int i = 0; i < count; i++)
        {
            var slot = new BackgroundScene.Simple2DBackgroundIllustration(
                scene, "Futile_White", new Vector2(683f, 384f));
            slot.depth = 22.5f;
            slot.alpha = 0f;
            scene.AddElement(slot);
            slots.Add(slot);
        }
        return slots;
    }
}