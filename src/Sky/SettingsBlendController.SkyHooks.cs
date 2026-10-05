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
            if (!_sceneSlots.TryGetValue(self, out var existing))
            {
                existing = new SkySlotSet { blend = CreateRcSlotsVanilla(self, room, SkyType.RTV) };
                _sceneSlots[self] = existing;
            }
            _activeSlots = existing;
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
            if (!_staticSlots.TryGetValue(self, out var staticSet) || staticSet.blend == null || staticSet.blend.Count < 4)
            {
                staticSet = new SkySlotSet { blend = CreateStaticSlotsVanilla(self, room, SkyType.RTV) };
                _staticSlots[self] = staticSet;
                int state = StateFileResolver.GetStateFromPath(room.roomSettings?.filePath, roomName);
                if (state < 1) state = 1;
                InitStaticSlotImages(staticSet.blend, room, ViewType.RTV, state, null, cam);
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

        if (isBlendManaged && snap.ViewType == ViewType.RTV && !_sceneSlots.TryGetValue(self, out _))
        {
            var slotSet = new SkySlotSet { blend = CreateRcSlotsVanilla(self, self.room, SkyType.RTV) };
            _sceneSlots[self] = slotSet;
            _activeSlots = slotSet;
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

        // Sin repair path aquí: InitStaticSlotImages (OnRoomCameraUpdate)
        // aplica el bkg por-sala con el tag del estado.

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
                if (!_sceneSlots.TryGetValue(self, out var existing))
                {
                    existing = new SkySlotSet { blend = CreateRcSlotsVanilla(self, room, SkyType.ACV) };
                    _sceneSlots[self] = existing;
                }
                _activeSlots = existing;
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
                if (!_sceneSlots.TryGetValue(self, out var existing))
                {
                    existing = new SkySlotSet
                    {
                        blend = CreateRcSlotsVanilla(self, room, SkyType.PSV),
                        fog = CreateRcSlotsVanilla(self, room, SkyType.PSV),
                        sun = CreateSunSlots(self, room, SkyType.PSV, false)
                    };
                    _sceneSlots[self] = existing;
                }
                _activeSlots = existing;
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
            if (targetSky == SkyType.ACV)
            {
                if (!_staticSlots.TryGetValue(self, out var staticSet) || staticSet.blend == null || staticSet.blend.Count < 4)
                {
                    staticSet = new SkySlotSet { blend = CreateStaticSlotsVanilla(self, room, SkyType.ACV) };
                    _staticSlots[self] = staticSet;
                    int state = StateFileResolver.GetStateFromPath(room.roomSettings?.filePath, roomName);
                    if (state < 1) state = 1;
                    InitStaticSlotImages(staticSet.blend, room, ViewType.ACV, state, null, cam);
                }
            }
            else if (targetSky == SkyType.PSV)
            {
                if (!_staticSlots.TryGetValue(self, out var staticSetPsv) || staticSetPsv.blend == null || staticSetPsv.blend.Count < 4)
                {
                    staticSetPsv = new SkySlotSet
                    {
                        blend = CreateStaticSlotsVanilla(self, room, SkyType.PSV),
                        fog = CreateRcSlotsVanilla(self, room, SkyType.PSV),
                        sun = CreateSunSlots(self, room, SkyType.PSV, false)
                    };
                    _staticSlots[self] = staticSetPsv;
                    int state = StateFileResolver.GetStateFromPath(room.roomSettings?.filePath, roomName);
                    if (state < 1) state = 1;
                    InitStaticSlotImages(staticSetPsv.blend, room, ViewType.PSV, state, null, cam);
                    InitStaticPsvSlotImages(staticSetPsv, room, state, cam);
                }
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

        if (isBlendManaged && snap.ViewType == ViewType.ACV && !_sceneSlots.TryGetValue(self, out _))
        {
            var slotSet = new SkySlotSet { blend = CreateRcSlotsVanilla(self, self.room, SkyType.ACV) };
            _sceneSlots[self] = slotSet;
            _activeSlots = slotSet;
            _acvScene = self;
            
            int state = BlendClock.StateA;
            if (state < 1)
            {
                state = StateFileResolver.GetStateFromPath(self.room.roomSettings?.filePath, roomName);
                if (state < 1) state = 1;
            }
            
            UpdateRcSlots(SkyType.ACV, state, state, cam, self.room);
        }

        if (isBlendManaged && snap.ViewType == ViewType.PSV && !_sceneSlots.TryGetValue(self, out _))
        {
            var slotSet = new SkySlotSet
            {
                blend = CreateRcSlotsVanilla(self, self.room, SkyType.PSV),
                fog = CreateRcSlotsVanilla(self, self.room, SkyType.PSV),
                sun = CreateSunSlots(self, self.room, SkyType.PSV, false)
            };
            _sceneSlots[self] = slotSet;
            _activeSlots = slotSet;
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
                    var staticSlots = ResolveStaticSlotsForRoom(self.room);

                    if (staticSlots != null && staticSlots.Count > 0)
                    {
                        if (staticSlots[0].illustrationName == "RC_Transparent")
                        {
                            int state = StateFileResolver.GetStateFromPath(self.room.roomSettings?.filePath, roomName);
                            if (state > 0)
                            {
                                // bkg por sala×estado (tag <Mod:...>); con sprite ya
                                // iniciado hace el swap real, si no solo setea el nombre
                                var bkg = BkgResolver.Get(roomName, state);
                                string atlas = BkgAtlasOrNull(bkg.Mod, bkg.Sky);
                                if (atlas != null)
                                    RefreshSlotSprite(staticSlots[0], atlas, bkg.Mod, bkg.Sky, cam);
                            }
                        }
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

        // Ocultación pnk_ (HorizonFog + abi) movida a ToggleVanillaSlots
        // (OnRoomCameraUpdate): ahora depende de tener <Mod> (regla 10/2026),
        // no solo de la view.

        // PSV estático: forzar shader BackgroundAdditive en los slots de sun.
        // InitiateSprites usa "Background"; en blend lo corrige UpdatePsvSlots,
        // que en modo estático nunca se ejecuta.
        if (isStaticManaged && isPsv2 && camIsHere &&
            _staticSlots.TryGetValue(self, out var staticPsvSet) && staticPsvSet.sun != null)
        {
            ForceSunShader(staticPsvSet.sun, cam);
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
            if (!_sceneSlots.TryGetValue(self, out var existing))
            {
                existing = new SkySlotSet { blend = CreateRcSlotsVanilla(self, room, SkyType.ORV) };
                _sceneSlots[self] = existing;
                DefaultOrvSlotsToVanillaSky(existing.blend);
            }
            _activeSlots = existing;
            _orvScene = self;

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
            if (!_staticSlots.TryGetValue(self, out var staticSetOrv) || staticSetOrv.blend == null || staticSetOrv.blend.Count < 4)
            {
                staticSetOrv = new SkySlotSet { blend = CreateStaticSlotsVanilla(self, room, SkyType.ORV) };
                _staticSlots[self] = staticSetOrv;
                int state = StateFileResolver.GetStateFromPath(room.roomSettings?.filePath, roomName);
                if (state < 1) state = 1;
                InitStaticSlotImages(staticSetOrv.blend, room, ViewType.ORV, state, "otr_sky", cam);
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

        if (isBlendManaged && snap.ViewType == ViewType.ORV && !_sceneSlots.TryGetValue(self, out _))
        {
            var slotSet = new SkySlotSet { blend = CreateRcSlotsVanilla(self, self.room, SkyType.ORV) };
            _sceneSlots[self] = slotSet;
            _activeSlots = slotSet;
            _orvScene = self;
            DefaultOrvSlotsToVanillaSky(slotSet.blend);

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

        // Sin repair path aquí: InitStaticSlotImages (OnRoomCameraUpdate)
        // aplica el bkg por-sala con el tag del estado.

        orig(self, eu);
    }

    // ============================================================
    // HELPERS OUTERRIMVIEW
    // ============================================================

    // Ocultación de otr_sky movida a ToggleVanillaSlots (OnRoomCameraUpdate):
    // ahora depende de tener <Mod> (regla 10/2026), no solo de la view.

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
    // HOOK: DistantCloud alto en PSV (solo una vez, al initiar sprites)
    // Oculta con save/restore SOLO si la regla lo pide (tener <Mod>);
    // si no, el cielo vanilla queda visible. ToggleVanillaSlots re-aplica
    // por frame después (DrawSprites reescribe isVisible por altitud).
    // ============================================================
    private static void OnDistantCloudInitiateSprites(
        On.AboveCloudsView.DistantCloud.orig_InitiateSprites orig,
        AboveCloudsView.DistantCloud self,
        RoomCamera.SpriteLeaser sLeaser,
        RoomCamera rCam)
    {
        orig(self, sLeaser, rCam);

        if (self.AboveCloudsScene != null &&
            self.AboveCloudsScene.PinkSky && self.depth >= 195f)
        {
            string roomName = rCam?.room?.abstractRoom?.name;
            if (!string.IsNullOrEmpty(roomName) &&
                BkgResolver.ShouldHideVanilla(roomName, ActiveHideState()))
            {
                foreach (var sprite in sLeaser.sprites)
                {
                    if (sprite != null)
                        HideVanillaSpriteAlpha(sprite);
                }
            }
        }
    }
}