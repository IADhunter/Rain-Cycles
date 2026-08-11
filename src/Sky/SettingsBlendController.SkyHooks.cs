using System.Collections.Generic;
using UnityEngine;
using RainCycles.Settings;
using RainCycles.Snapshot;
using RainCycles.Clock;
using RainCycles.Core;
using Watcher;

namespace RainCycles.Core;

public static partial class SettingsBlendController
{
    // ============================================================
    // ROOFTOPVIEW
    // ============================================================

    private static void OnRoofTopViewCtor(
        On.RoofTopView.orig_ctor orig, RoofTopView self,
        Room room, RoomSettings.RoomEffect effect)
    {
        string settingsPath = room?.roomSettings?.filePath;
        var snap = string.IsNullOrEmpty(settingsPath) ? null
            : SettingsSnapshot.GetCached(settingsPath, room?.abstractRoom?.name);

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

        if (isBlendManaged)
        {
            Color originalAtmo = Shader.GetGlobalVector(RainWorld.ShadPropAboveCloudsAtmosphereColor);
            Color originalMult = Shader.GetGlobalVector(RainWorld.ShadPropMultiplyColor);
            TintManager.SaveOriginalViewStateDirect(self, originalAtmo, originalMult);
        }

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

            UpdateRcSlots(SkyType.RTV, state, state, cam, room);
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
    }

    private static void OnRoofTopViewUpdate(
        On.RoofTopView.orig_Update orig, RoofTopView self, bool eu)
    {
        var cam = self.room?.game?.cameras?[0];
        bool camIsHere = cam != null && cam.room == self.room;

        string settingsPath = self.room?.roomSettings?.filePath;
        var snap = string.IsNullOrEmpty(settingsPath) ? null
            : SettingsSnapshot.GetCached(settingsPath, self.room?.abstractRoom?.name);

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
            
            UpdateRcSlots(SkyType.RTV, state, state, cam, self.room);
        }

        if (!hasRcType)
        {
            orig(self, eu);
            return;
        }

        if (camIsHere && _rcSlotsStaticRTV != null && _rcSlotsStaticRTV.Count > 0
            && _rcSlotsStaticRTV[0].illustrationName != "RC_Transparent")
        {
            RefreshSlotSprite(_rcSlotsStaticRTV[0], _rcSlotsStaticRTV[0].illustrationName, cam);
        }

        orig(self, eu);
    }

    // ============================================================
    // ABOVECLOUDSVIEW
    // ============================================================

    private static void OnAboveCloudsViewCtor(
        On.AboveCloudsView.orig_ctor orig, AboveCloudsView self,
        Room room, RoomSettings.RoomEffect effect)
    {
        string settingsPath = room?.roomSettings?.filePath;
        var snap = string.IsNullOrEmpty(settingsPath) ? null
            : SettingsSnapshot.GetCached(settingsPath, room?.abstractRoom?.name);

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

        if (isBlendManaged)
        {
            Color originalAtmo = self.atmosphereColor;
            Color originalMult = Shader.GetGlobalVector(RainWorld.ShadPropMultiplyColor);
            TintManager.SaveOriginalViewStateDirect(self, originalAtmo, originalMult);
        }

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

                UpdateRcSlots(targetSky, state, state, cam, room);
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

                UpdateRcSlots(targetSky, state, state, cam, room);
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
    }

    private static void OnAboveCloudsViewUpdate(
        On.AboveCloudsView.orig_Update orig, AboveCloudsView self, bool eu)
    {
        var cam = self.room?.game?.cameras?[0];
        bool camIsHere = cam != null && cam.room == self.room;
        string roomName = self.room?.abstractRoom?.name;

        string settingsPath = self.room?.roomSettings?.filePath;
        var snap = string.IsNullOrEmpty(settingsPath) ? null
            : SettingsSnapshot.GetCached(settingsPath, self.room?.abstractRoom?.name);

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
            
            UpdateRcSlots(SkyType.ACV, state, state, cam, self.room);
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
            
            UpdateRcSlots(SkyType.PSV, state, state, cam, self.room);
        }

        if (!hasRcType)
        {
            orig(self, eu);
            return;
        }

        if (!isBlendManaged && !isStaticManaged)
        {
            orig(self, eu);
            if (camIsHere)
            {
                var snapLocal = SettingsSnapshot.GetCached(self.room.roomSettings?.filePath, self.room.abstractRoom?.name);
                bool isPsv = snapLocal != null && snapLocal.HasView && snapLocal.ViewType == ViewType.PSV;

                if (isPsv)
                {
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
                        {
                            abi.alpha = 0f;
                        }
                    }
                }

                var skyType = GetViewFromLoadedSettings(self.room);
                if (skyType != SkyType.None)
                {
                    var staticSlots = skyType == SkyType.ACV ? _rcSlotsStaticACV :
                                      skyType == SkyType.RTV ? _rcSlotsStaticRTV :
                                      skyType == SkyType.PSV ? _rcSlotsStaticPSV : _rcSlotsStaticORV;

                    if (staticSlots != null && staticSlots.Count > 0)
                    {
                        if (staticSlots[0].illustrationName == "RC_Transparent")
                        {
                            var settings = BlendSettingsLoader.Active;
                            int state = StateFileResolver.GetStateFromPath(self.room.roomSettings?.filePath, roomName);
                            if (state > 0 && settings != null)
                            {
                                var view = skyType == SkyType.ACV ? ViewType.ACV : (skyType == SkyType.RTV ? ViewType.RTV : (skyType == SkyType.PSV ? ViewType.PSV : ViewType.ORV));
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

        var snapLocal2 = SettingsSnapshot.GetCached(self.room.roomSettings?.filePath, self.room.abstractRoom?.name);
        bool isPsv2 = snapLocal2 != null && snapLocal2.HasView && snapLocal2.ViewType == ViewType.PSV;

        if (isPsv2)
        {
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
        }
    }

    // ============================================================
    // OUTERRIMVIEW (Watcher)
    // ============================================================

    private static void OnOuterRimViewCtor(
        On.Watcher.OuterRimView.orig_ctor orig, Watcher.OuterRimView self,
        Room room, RoomSettings.RoomEffect effect)
    {
        string settingsPath = room?.roomSettings?.filePath;
        var snap = string.IsNullOrEmpty(settingsPath) ? null
            : SettingsSnapshot.GetCached(settingsPath, room?.abstractRoom?.name);

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

        if (isBlendManaged)
        {
            Color originalAtmo = Shader.GetGlobalVector(RainWorld.ShadPropAboveCloudsAtmosphereColor);
            Color originalMult = Shader.GetGlobalVector(RainWorld.ShadPropMultiplyColor);
            TintManager.SaveOriginalViewStateDirect(self, originalAtmo, originalMult);
        }

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

        if (roomView == ViewType.ORV && isBlendManaged)
        {
            HideVanillaOuterRimSky(self);
            _rcSlotsORV = CreateRcSlotsVanilla(self, room, SkyType.ORV);
            _orvScene = self;
            DefaultOrvSlotsToVanillaSky(_rcSlotsORV);

            int state = StateFileResolver.GetStateFromPath(room.roomSettings?.filePath, roomName);
            if (state < 1)
            {
                int n = StateFileResolver.CountRainStateFiles(roomName);
                int cycle = room.game?.GetStorySession?.saveState?.cycleNumber ?? 0;
                state = n > 0 ? (cycle % n) + 1 : 1;
            }

            UpdateRcSlots(SkyType.ORV, state, state, cam, room);
        }
        else if (roomView == ViewType.ORV && isStaticManaged)
        {
            HideVanillaOuterRimSky(self);
            if (_rcSlotsStaticORV == null)
            {
                _rcSlotsStaticORV = CreateStaticSlotsVanilla(self, room, SkyType.ORV);
                int state = StateFileResolver.GetStateFromPath(room.roomSettings?.filePath, roomName);
                if (state < 1) state = 1;
                string file = regionSettings?.GetBkgFileForState(state, ViewType.ORV);
                _rcSlotsStaticORV[0].illustrationName = !string.IsNullOrEmpty(file)
                    ? System.IO.Path.GetFileNameWithoutExtension(file)
                    : "otr_sky";
                _rcSlotsStaticORV[0].alpha = 1f;
            }
            _orvScene = self;
        }
    }

    private static void OnOuterRimViewUpdate(
        On.Watcher.OuterRimView.orig_Update orig, Watcher.OuterRimView self, bool eu)
    {
        var cam = self.room?.game?.cameras?[0];
        bool camIsHere = cam != null && cam.room == self.room;

        string settingsPath = self.room?.roomSettings?.filePath;
        var snap = string.IsNullOrEmpty(settingsPath) ? null
            : SettingsSnapshot.GetCached(settingsPath, self.room?.abstractRoom?.name);

        bool hasRcType = snap != null && snap.HasRcType;
        bool isBlendManaged = hasRcType && snap.RcType == RcType.Blend;

        if (isBlendManaged && snap.ViewType == ViewType.ORV && (_rcSlotsORV == null || _rcSlotsORV.Count == 0))
        {
            HideVanillaOuterRimSky(self);
            _rcSlotsORV = CreateRcSlotsVanilla(self, self.room, SkyType.ORV);
            _orvScene = self;
            DefaultOrvSlotsToVanillaSky(_rcSlotsORV);

            int state = BlendClock.StateA;
            if (state < 1)
            {
                state = StateFileResolver.GetStateFromPath(self.room.roomSettings?.filePath, self.room?.abstractRoom?.name);
                if (state < 1) state = 1;
            }

            UpdateRcSlots(SkyType.ORV, state, state, cam, self.room);
        }

        if (!hasRcType)
        {
            orig(self, eu);
            return;
        }

        if (camIsHere && _rcSlotsStaticORV != null && _rcSlotsStaticORV.Count > 0
            && _rcSlotsStaticORV[0].illustrationName != "RC_Transparent")
        {
            RefreshSlotSprite(_rcSlotsStaticORV[0], _rcSlotsStaticORV[0].illustrationName, cam);
        }

        orig(self, eu);
    }

    // ============================================================
    // HELPERS OUTERRIMVIEW
    // ============================================================

    private static void HideVanillaOuterRimSky(OuterRimView scene)
    {
        if (scene?.elements == null) return;
        foreach (var el in scene.elements)
        {
            if (el is BackgroundScene.Simple2DBackgroundIllustration ill
                && ill.illustrationName == "otr_sky")
            {
                ill.alpha = 0f;
                break;
            }
        }
    }

    private static void DefaultOrvSlotsToVanillaSky(
        List<BackgroundScene.Simple2DBackgroundIllustration> slots)
    {
        if (slots == null) return;
        for (int i = 0; i < slots.Count; i++)
            slots[i].illustrationName = "otr_sky";
    }

    // ============================================================
    // ANCIENTURBANVIEW (Watcher)
    // ============================================================

    private static void OnAncientUrbanViewCtor(
        On.Watcher.AncientUrbanView.orig_ctor orig, Watcher.AncientUrbanView self,
        Room room, RoomSettings.RoomEffect effect)
    {
        string settingsPath = room?.roomSettings?.filePath;
        var snap = string.IsNullOrEmpty(settingsPath) ? null
            : SettingsSnapshot.GetCached(settingsPath, room?.abstractRoom?.name);

        bool hasRcType = snap != null && snap.HasRcType;
        bool isBlendManaged = hasRcType && snap.RcType == RcType.Blend;

        if (!hasRcType)
        {
            orig(self, room, effect);
            return;
        }

        var savedMultiply = Shader.GetGlobalVector(RainWorld.ShadPropMultiplyColor);
        var savedAtmosphere = Shader.GetGlobalVector(RainWorld.ShadPropAboveCloudsAtmosphereColor);

        orig(self, room, effect);

        if (isBlendManaged)
        {
            Color originalAtmo = Shader.GetGlobalVector(RainWorld.ShadPropAboveCloudsAtmosphereColor);
            Color originalMult = Shader.GetGlobalVector(RainWorld.ShadPropMultiplyColor);
            TintManager.SaveOriginalViewStateDirect(self, originalAtmo, originalMult);
        }

        var cam = room.game?.cameras?[0];
        if (cam == null || cam.room != room)
        {
            Shader.SetGlobalVector(RainWorld.ShadPropMultiplyColor, savedMultiply);
            Shader.SetGlobalVector(RainWorld.ShadPropAboveCloudsAtmosphereColor, savedAtmosphere);
        }
    }

    // ============================================================
    // HOOK: Ocultar DistantCloud con depth >= 195f en PSV (solo una vez)
    // ============================================================
    private static void OnDistantCloudInitiateSprites(
        On.AboveCloudsView.DistantCloud.orig_InitiateSprites orig,
        AboveCloudsView.DistantCloud self,
        RoomCamera.SpriteLeaser sLeaser,
        RoomCamera rCam)
    {
        orig(self, sLeaser, rCam);

        if (self.AboveCloudsScene.PinkSky && self.depth >= 195f)
        {
            foreach (var sprite in sLeaser.sprites)
            {
                if (sprite != null)
                {
                    sprite.isVisible = false;
                    sprite.alpha = 0f;
                }
            }
        }
    }
}