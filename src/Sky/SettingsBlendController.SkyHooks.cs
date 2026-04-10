using UnityEngine;
using RainCycles.Settings;
using RainCycles.Snapshot;
using RainCycles.Sky;
using RainCycles.Clock;
using RainCycles.Core;

namespace RainCycles.Core;

// Sky hooks: OnRoofTopViewCtor/Update, OnAboveCloudsViewCtor/Update
public static partial class SettingsBlendController
{
    // ────────────────────

    private static void OnRoofTopViewCtor(
        On.RoofTopView.orig_ctor orig, RoofTopView self,
        Room room, RoomSettings.RoomEffect effect)
    {
        orig(self, room, effect);

        if (room == null) return;
        var settings = BlendSettingsLoader.Active;
        if (settings == null) return;
        string roomName = room.abstractRoom?.name;
        if (roomName == null || !settings.IncludesRoom(roomName)) return;
        if (settings.GetSkyType(roomName) != SkyType.RTV) return;

        // Actualizar siempre a la instancia más reciente.
        _rtvScene = self;
        _rtvSlotDay = _rtvSlotDusk = _rtvSlotNight = -1;
        AssignSkySlots(self, room, settings, SkyType.RTV);

        // Igual que ACV: forzar alphas correctos antes del primer Update.
        PreOrigForceSkyAlpha(self.daySky, self.duskSky, self.nightSky, SkyType.RTV);
    }

    private static void OnRoofTopViewUpdate(
        On.RoofTopView.orig_Update orig, RoofTopView self, bool eu)
    {
        var cam = self.room?.game?.cameras?[0];
        bool camIsHere = cam != null && cam.room == self.room;

        if (!camIsHere)
        {
            // Sincronizar slots aunque la cámara no esté en esta sala.
            if (BlendClock.IsRunning && _rtvScene == self)
            {
                var rtSettings = BlendSettingsLoader.Active;
                string rtRoom = self.room?.abstractRoom?.name;
                if (rtSettings != null && rtRoom != null && rtSettings.IncludesRoom(rtRoom)
                    && rtSettings.GetSkyType(rtRoom) == SkyType.RTV)
                {
                    var phase = BlendClock.CurrentPhase;
                    if (phase == BlendClock.Phase.Blending)
                    {
                        int sA = BlendClock.StateA, sB = BlendClock.StateB;
                        if (GetSlotDay(SkyType.RTV) != sA || GetSlotDusk(SkyType.RTV) != sB)
                            SyncSkySlots(self.room, sA, sB);
                        if (GetSlotDay(SkyType.RTV) == sA && self.daySky != null)
                        {
                            float tNow = BlendClock.SubPhaseLocalT;
                            self.daySky.alpha  = 1f - tNow;
                            self.duskSky.alpha = 1f;
                            if (self.nightSky != null) self.nightSky.alpha = 0f;
                            _savedDayAlpha = self.daySky.alpha; _savedDuskAlpha = 1f; _savedNightAlpha = 0f;
                            _hasSavedAlphas = true;
                        }
                    }
                    else if (phase == BlendClock.Phase.Idle)
                    {
                        int sA = BlendClock.StateA, sB = NextStateIn(rtSettings, sA);
                        if (GetSlotDay(SkyType.RTV) != sA || GetSlotDusk(SkyType.RTV) != sB)
                            SyncSkySlots(self.room, sA, sB);
                        if (self.daySky != null)
                        {
                            self.daySky.alpha  = 1f;
                            self.duskSky.alpha = 1f;
                            if (self.nightSky != null) self.nightSky.alpha = 0f;
                            _savedDayAlpha = 1f; _savedDuskAlpha = 1f; _savedNightAlpha = 0f;
                            _hasSavedAlphas = true;
                        }
                    }
                    else if (phase == BlendClock.Phase.Done)
                    {
                        int sB = BlendClock.StateB, sC = NextStateIn(rtSettings, sB);
                        if (GetSlotDay(SkyType.RTV) != sB)
                            SyncSkySlots(self.room, sB, sC);
                        if (self.daySky != null)
                        {
                            self.daySky.alpha  = 1f;
                            self.duskSky.alpha = 1f;
                            if (self.nightSky != null) self.nightSky.alpha = 0f;
                            _savedDayAlpha = 1f; _savedDuskAlpha = 1f; _savedNightAlpha = 0f;
                            _hasSavedAlphas = true;
                        }
                    }
                }
            }
            orig(self, eu);
            return;
        }

        // ── PRE-ORIG: Forzar alpha correcto ANTES del rendering ───────────
        if (_pendingSkySync && _pendingSkyStateA > 0)
        {
            SyncSkySlots(self.room, _pendingSkyStateA, _pendingSkyStateB);
            _pendingSkySync   = false;
            _pendingSkyStateA = -1;
            _pendingSkyStateB = -1;
        }
        // El audit confirmó que logic==physical siempre, pero alpha=1.00
        PreOrigForceSkyAlpha(self.daySky, self.duskSky, self.nightSky, SkyType.RTV);

        orig(self, eu);

        if (self.room == null) return;
        var settings = BlendSettingsLoader.Active;
        if (settings == null) return;
        string roomName = self.room.abstractRoom?.name;
        if (roomName == null || !settings.IncludesRoom(roomName)) return;

        // Reasignar si fue limpiada por ResetFull (e.g. cambio de modo en DevTools)
        if (_rtvScene == null)
        {
            _rtvScene = self;
            _lastIdleSkyState = -1;
        }

        // Sin sky declarado para esta sala → comportamiento frozen estable
        if (settings.GetSkyType(roomName) != SkyType.RTV)
        {
            self.daySky.alpha  = 1f;
            self.duskSky.alpha = 0f;
            self.nightSky.alpha = 0f;
            OverrideBackgroundGlobalsIfActive(self.room);
            return;
        }

        // Sincronizar sprites físicos con illustrationName asignado en el ctor.
        var cam0 = self.room.game?.cameras?[0];
        if (GetSlotDay(SkyType.RTV) > 0 && cam0 != null)
        {
            var s2 = BlendSettingsLoader.Active;
            if (s2 != null)
            {
                string fA = s2.GetBkgFileForState(GetSlotDay  (SkyType.RTV), SkyType.RTV);
                string fB = s2.GetBkgFileForState(GetSlotDusk (SkyType.RTV), SkyType.RTV);
                string fC = s2.GetBkgFileForState(GetSlotNight(SkyType.RTV), SkyType.RTV);
                if (!string.IsNullOrEmpty(fA)) RefreshSlotSprite(self.daySky,   System.IO.Path.GetFileNameWithoutExtension(fA), cam0);
                if (!string.IsNullOrEmpty(fB)) RefreshSlotSprite(self.duskSky,  System.IO.Path.GetFileNameWithoutExtension(fB), cam0);
                if (!string.IsNullOrEmpty(fC)) RefreshSlotSprite(self.nightSky, System.IO.Path.GetFileNameWithoutExtension(fC), cam0);
            }
        }

        ApplySkyAlphas(self.daySky, self.duskSky, self.nightSky);
        OverrideBackgroundGlobalsIfActive(self.room);
    }

    private static void OnAboveCloudsViewCtor(
        On.AboveCloudsView.orig_ctor orig, AboveCloudsView self,
        Room room, RoomSettings.RoomEffect effect)
    {
        orig(self, room, effect);

        if (room == null) return;
        var settings = BlendSettingsLoader.Active;
        if (settings == null) return;
        string roomName = room.abstractRoom?.name;
        if (roomName == null || !settings.IncludesRoom(roomName)) return;

        _aboveCloudsView = self;

        var cam = room.game?.cameras?[0];
        if (cam != null)
        {
            Color multiply, atmosphere;
            RoomEffectsApplier.CalcBackgroundColors(cam, out multiply, out atmosphere);
            Shader.SetGlobalVector(RainWorld.ShadPropMultiplyColor, multiply);
            Shader.SetGlobalVector(RainWorld.ShadPropAboveCloudsAtmosphereColor, atmosphere);
        }

        if (settings.GetSkyType(roomName) != SkyType.ACV) return;

        // Slots separados por tipo: el ctor de ACV precargado no pisa los slots RTV.
        _acvScene = self;
        _acvSlotDay = _acvSlotDusk = _acvSlotNight = -1;
        AssignSkySlots(self, room, settings, SkyType.ACV);

        // Forzar alphas correctos inmediatamente en el ctor.
        PreOrigForceSkyAlpha(self.daySky, self.duskSky, self.nightSky, SkyType.ACV);
    }

    private static void OnAboveCloudsViewUpdate(
        On.AboveCloudsView.orig_Update orig, AboveCloudsView self, bool eu)
    {
        var cam = self.room?.game?.cameras?[0];
        bool camIsHere = cam != null && cam.room == self.room;

        if (!camIsHere)
        {
            // Mismo tratamiento que RTV: sincronizar slots y alphas de fondo.
            if (BlendClock.IsRunning && _acvScene == self)
            {
                var acvSettings = BlendSettingsLoader.Active;
                string acvRoom = self.room?.abstractRoom?.name;
                if (acvSettings != null && acvRoom != null && acvSettings.IncludesRoom(acvRoom)
                    && acvSettings.GetSkyType(acvRoom) == SkyType.ACV)
                {
                    var phase = BlendClock.CurrentPhase;
                    if (phase == BlendClock.Phase.Blending)
                    {
                        int sA = BlendClock.StateA, sB = BlendClock.StateB;
                        if (GetSlotDay(SkyType.ACV) != sA || GetSlotDusk(SkyType.ACV) != sB)
                            SyncSkySlots(self.room, sA, sB);
                        if (GetSlotDay(SkyType.ACV) == sA && self.daySky != null)
                        {
                            float tNow = BlendClock.SubPhaseLocalT;
                            self.daySky.alpha  = 1f - tNow;
                            self.duskSky.alpha = 1f;
                            if (self.nightSky != null) self.nightSky.alpha = 0f;
                            _savedDayAlpha = self.daySky.alpha; _savedDuskAlpha = 1f; _savedNightAlpha = 0f;
                            _hasSavedAlphas = true;
                        }
                        self.atmosphereColor = _lastAtmosphereColor;
                    }
                    else if (phase == BlendClock.Phase.Idle)
                    {
                        int sA = BlendClock.StateA, sB = NextStateIn(acvSettings, sA);
                        if (GetSlotDay(SkyType.ACV) != sA || GetSlotDusk(SkyType.ACV) != sB)
                            SyncSkySlots(self.room, sA, sB);
                        if (self.daySky != null)
                        {
                            self.daySky.alpha  = 1f;
                            self.duskSky.alpha = 1f;
                            if (self.nightSky != null) self.nightSky.alpha = 0f;
                            _savedDayAlpha = 1f; _savedDuskAlpha = 1f; _savedNightAlpha = 0f;
                            _hasSavedAlphas = true;
                        }
                        self.atmosphereColor = _lastAtmosphereColor;
                    }
                    else if (phase == BlendClock.Phase.Done)
                    {
                        int sB = BlendClock.StateB, sC = NextStateIn(acvSettings, sB);
                        if (GetSlotDay(SkyType.ACV) != sB)
                            SyncSkySlots(self.room, sB, sC);
                        if (self.daySky != null)
                        {
                            self.daySky.alpha  = 1f;
                            self.duskSky.alpha = 1f;
                            if (self.nightSky != null) self.nightSky.alpha = 0f;
                            _savedDayAlpha = 1f; _savedDuskAlpha = 1f; _savedNightAlpha = 0f;
                            _hasSavedAlphas = true;
                        }
                        self.atmosphereColor = _lastAtmosphereColor;
                    }
                }
            }
            orig(self, eu);
            return;
        }

        PreOrigForceSkyAlpha(self.daySky, self.duskSky, self.nightSky, SkyType.ACV);

        orig(self, eu);

        if (self.room == null) return;
        var settings = BlendSettingsLoader.Active;
        if (settings == null) return;
        string roomName = self.room.abstractRoom?.name;
        if (roomName == null || !settings.IncludesRoom(roomName)) return;

        // Reasignar si fue limpiada por ResetFull (e.g. cambio de modo en DevTools)
        if (_acvScene == null)
        {
            _acvScene = self;
            _lastIdleSkyState = -1;
        }

        // Sincronizar sprites físicos — igual que RTV
        var cam0acv = self.room.game?.cameras?[0];
        if (GetSlotDay(SkyType.ACV) > 0 && cam0acv != null)
        {
            var s2 = BlendSettingsLoader.Active;
            if (s2 != null)
            {
                string fA = s2.GetBkgFileForState(GetSlotDay  (SkyType.ACV), SkyType.ACV);
                string fB = s2.GetBkgFileForState(GetSlotDusk (SkyType.ACV), SkyType.ACV);
                string fC = s2.GetBkgFileForState(GetSlotNight(SkyType.ACV), SkyType.ACV);
                if (!string.IsNullOrEmpty(fA)) RefreshSlotSprite(self.daySky,   System.IO.Path.GetFileNameWithoutExtension(fA), cam0acv);
                if (!string.IsNullOrEmpty(fB)) RefreshSlotSprite(self.duskSky,  System.IO.Path.GetFileNameWithoutExtension(fB), cam0acv);
                if (!string.IsNullOrEmpty(fC)) RefreshSlotSprite(self.nightSky, System.IO.Path.GetFileNameWithoutExtension(fC), cam0acv);
            }
        }

        // Sin sky declarado → frozen estable
        if (settings.GetSkyType(roomName) != SkyType.ACV)
        {
            self.daySky.alpha  = 1f;
            self.duskSky.alpha = 0f;
            self.nightSky.alpha = 0f;
            _aboveCloudsView = self;
            OverrideBackgroundGlobalsIfActive(self.room);
            return;
        }

        ApplySkyAlphas(self.daySky, self.duskSky, self.nightSky);

        // Forzar alpha=1 en todos los DistantBuilding
        foreach (var el in self.elements)
            if (el is AboveCloudsView.DistantBuilding db) db.alpha = 1f;

        // Cuando los slots están desincronizados (_skySlotDay != BlendClock.StateA),
        bool slotsInSync = !BlendClock.IsRunning ||
                           BlendClock.CurrentPhase != BlendClock.Phase.Blending ||
                           (GetSlotDay(SkyType.ACV) == BlendClock.StateA && !_subChangedThisFrame);

        if (slotsInSync)
        {
            var cloudSnap = _activeSnapshot;
            var newAtm = (cloudSnap != null && cloudSnap.TintCloudAtmosphere.HasValue)
                ? cloudSnap.TintCloudAtmosphere.Value
                : _lastAtmosphereColor;

            // Solo actualizar _lastAtmosphereColor durante blend activo.
            if (cloudSnap != null && cloudSnap.TintCloudAtmosphere.HasValue
                && _active && _externalT)
                _lastAtmosphereColor = newAtm;

            self.atmosphereColor = newAtm;
        }
        else
        {
            // Slots desincronizados — mantener el último color conocido.
            self.atmosphereColor = _lastAtmosphereColor;
        }

        _aboveCloudsView = self;
        OverrideBackgroundGlobalsIfActive(self.room);

    }

}
