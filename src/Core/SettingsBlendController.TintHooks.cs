using UnityEngine;
using RainCycles.Settings;
using RainCycles.Snapshot;
using RainCycles.Sky;
using RainCycles.Clock;
using RainCycles.Core;

namespace RainCycles.Core;

// Tint hooks: OnDrawUpdate, OverrideBackground, OnUpdateDayNightPalette, OnApplyFade, OnChangeBothPalettes
public static partial class SettingsBlendController
{
    private static void OnDrawUpdate(
        On.RoomCamera.orig_DrawUpdate orig, RoomCamera self, float timeStacker, float timeSpeed)
    {
        // DrawSprites lee atmosphereColor de ACV, no shader global
        if (_acvScene != null && BlendClock.IsRunning)
        {
            var cam0 = _acvScene.room?.game?.cameras?[0];
            bool acvCamIsHere = cam0 != null && cam0.room == _acvScene.room;
            var phase = BlendClock.CurrentPhase;

            // Blending + cam presente → pre-escribir atmosphereColor
            if (acvCamIsHere && phase == BlendClock.Phase.Blending)
                _acvScene.atmosphereColor = _lastAtmosphereColor;

            // exited=True → DrawSprites corre sin cam → forzar atmosphereColor
            if (_exitedManagedRoomLastFrame && phase == BlendClock.Phase.Blending)
            {
                UnityEngine.Color targetAtm = _lastAtmosphereColor;
                if (_snapA != null && _snapB != null)
                {
                    var lerped = SettingsSnapshot.Lerp(_snapA, _snapB, BlendClock.SubPhaseLocalT);
                    if (lerped.TintCloudAtmosphere.HasValue)
                        targetAtm = lerped.TintCloudAtmosphere.Value;
                }
                _acvScene.atmosphereColor = targetAtm;
            }
        }

        orig(self, timeStacker, timeSpeed);

        // MoveCamera frame → vanilla pisa globals → restaurar
        if (_moveCameraThisFrame && _hasLastGoodGlobals)
        {
            Shader.SetGlobalVector(RainWorld.ShadPropMultiplyColor, _lastGoodMultiply);
            Shader.SetGlobalVector(RainWorld.ShadPropAboveCloudsAtmosphereColor, _lastGoodAtmosphere);
            return;
        }

        if (self.room == null || !self.backgroundGraphic.isVisible) return;

        var settings = BlendSettingsLoader.Active;
        if (settings == null) return;

        string roomName = self.room.abstractRoom?.name;
        if (roomName == null || !settings.IncludesRoom(roomName)) return;

        // recalcular backgroundGraphic.color desde paleta del mod
        self.backgroundGraphic.color = UnityEngine.Color.Lerp(
            self.currentPalette.blackColor,
            self.currentPalette.fogColor,
            self.currentPalette.fogAmount);
    }

    // skyColor → multiply (tinte Background sprites)
    private static void OverrideBackgroundGlobalsIfActive(Room room)
    {
        if (!_active || _room == null || room != _room) return;

        // ShadPropMultiplyColor solo en salas RTV/ACV
        var settings = BlendSettingsLoader.Active;
        if (settings == null) return;
        string roomName = room.abstractRoom?.name;
        if (roomName == null || settings.GetSkyType(roomName) == SkyType.None) return;

        var cam = room.game?.cameras?[0];
        if (cam == null) return;

        Color multiply, atmosphere;
        RoomEffectsApplier.CalcBackgroundColors(cam, out multiply, out atmosphere);

        Shader.SetGlobalVector(RainWorld.ShadPropMultiplyColor, multiply);
        Shader.SetGlobalVector(RainWorld.ShadPropAboveCloudsAtmosphereColor, atmosphere);

        _lastGoodMultiply   = multiply;
        _lastGoodAtmosphere = atmosphere;
        _hasLastGoodGlobals = true;
    }

    private static void OnUpdateDayNightPalette(
        On.RoomCamera.orig_UpdateDayNightPalette orig, RoomCamera self)
    {
        // bloquear DayNight nativo solo en salas [ROOMS]
        if ((_moveCameraThisFrame || _exitedManagedRoomLastFrame) && _hasLastGoodGlobals)
        {
            Shader.SetGlobalVector(RainWorld.ShadPropMultiplyColor, _lastGoodMultiply);
            Shader.SetGlobalVector(RainWorld.ShadPropAboveCloudsAtmosphereColor, _lastGoodAtmosphere);
            return;
        }
        if (_moveCameraThisFrame && BlendClock.IsRunning)
            return;

        // Caso 1: blend activo → aplicar blend, no orig
        if (_active && _room != null)
        {
            float t = _externalT ? _forcedT : BlendSlider.BlendFactor;
            if (Mathf.Abs(t - _lastT) >= 0.01f)
            {
                _lastT = t;
                ApplyBlend(t);
            }
            return;
        }

        // Caso 2: clock corre, sin attach → bloquear DayNight
        if (BlendClock.IsRunning && BlendSettingsLoader.Active != null && self.room != null)
        {
            string roomName = self.room.abstractRoom?.name;
            if (roomName != null && BlendSettingsLoader.Active.IncludesRoom(roomName))
                return;
        }

        // Caso 3: sala en [ROOMS] sin clock → bloquear + reaplica globals
        if (BlendSettingsLoader.Active != null && self.room != null)
        {
            string roomName = self.room.abstractRoom?.name;
            if (roomName != null && BlendSettingsLoader.Active.IncludesRoom(roomName))
            {
                // consumir idle diferido si cam.room ya actualizó
                if (_pendingIdleRoom != null && _pendingIdleRoom == self.room && _pendingIdlePath != null)
                {
                    ApplyIdleState(_pendingIdleRoom, _pendingIdlePath, allowCameraOps: true);
                    BlendClockUpdater.SetLastIdleRoom(self.room.abstractRoom?.name);
                    _pendingIdleRoom = null;
                    _pendingIdlePath = null;
                }

                // sobreescribir globals → corrige contaminación posterior
                Color multiply, atmosphere;
                RoomEffectsApplier.CalcBackgroundColors(self, out multiply, out atmosphere);
                Shader.SetGlobalVector(RainWorld.ShadPropMultiplyColor, multiply);
                Shader.SetGlobalVector(RainWorld.ShadPropAboveCloudsAtmosphereColor, atmosphere);
                _lastGoodMultiply   = multiply;
                _lastGoodAtmosphere = atmosphere;
                _hasLastGoodGlobals = true;
                return;
            }
        }

        // sala no gestionada → orig
        orig(self);

        // post-orig → recalcular globals desde fadeTexA (ya tiene paleta nueva)
        if (BlendClock.IsRunning && self.room != null && self.fadeTexA != null)
        {
            Color multiply   = self.fadeTexA.GetPixel(1, 15);
            Color atmosphere = self.fadeTexA.GetPixel(2, 15);
            Shader.SetGlobalVector(RainWorld.ShadPropMultiplyColor, multiply);
            Shader.SetGlobalVector(RainWorld.ShadPropAboveCloudsAtmosphereColor, atmosphere);
        }
    }

    private static void OnChangeRoom(
        On.RoomCamera.orig_ChangeRoom orig, RoomCamera self,
        Room newRoom, int cameraPosition)
    {
        // sala destino no gestionada → paletteB=-1 antes de orig
        if (BlendClock.IsRunning && newRoom != null && BlendSettingsLoader.Active != null)
        {
            string newRoomName = newRoom.abstractRoom?.name;
            if (newRoomName != null && !BlendSettingsLoader.Active.IncludesRoom(newRoomName))
            {
                self.paletteB = -1;
            }
        }

        // ChangeRoom → BackgroundScene.Update → InitiateSprites (un frame después)
        if (BlendClock.IsRunning && newRoom != null && BlendSettingsLoader.Active != null)
        {
            string newRoomName = newRoom.abstractRoom?.name;
            if (newRoomName != null && BlendSettingsLoader.Active.IncludesRoom(newRoomName))
            {
                var skyType = BlendSettingsLoader.Active.GetSkyType(newRoomName);
                var acv = _acvScene;
                var rtv = _rtvScene;
                bool isAcv = skyType == SkyType.ACV && acv != null && acv.room?.abstractRoom?.name == newRoomName;
                bool isRtv = skyType == SkyType.RTV && rtv != null && rtv.room?.abstractRoom?.name == newRoomName;

                if (isAcv || isRtv)
                {
                    var day  = isAcv ? acv.daySky   : rtv.daySky;
                    var dusk = isAcv ? acv.duskSky  : rtv.duskSky;
                    var ngt  = isAcv ? acv.nightSky : rtv.nightSky;
                    var thisSky = isAcv ? SkyType.ACV : SkyType.RTV;

                    // sincronizar illustrationName pre-orig
                    var phase = BlendClock.CurrentPhase;
                    if (phase == BlendClock.Phase.Blending && GetSlotDay(thisSky) != BlendClock.StateA)
                        SyncSkySlots(newRoom, BlendClock.StateA, BlendClock.StateB);
                    else if (phase == BlendClock.Phase.Done && GetSlotDay(thisSky) != BlendClock.StateB)
                        SyncSkySlots(newRoom, BlendClock.StateB,
                            NextStateIn(BlendSettingsLoader.Active, BlendClock.StateB));
                    else if (phase == BlendClock.Phase.Idle && GetSlotDay(thisSky) != BlendClock.StateA)
                        SyncSkySlots(newRoom, BlendClock.StateA,
                            NextStateIn(BlendSettingsLoader.Active, BlendClock.StateA));

                    // forzar alpha pre-orig
                    if (day != null) PreOrigForceSkyAlpha(day, dusk, ngt, thisSky);

                    // forzar atmosphereColor pre-orig
                    if (isAcv && acv != null)
                        acv.atmosphereColor = _lastAtmosphereColor;
                }
            }
        }

        orig(self, newRoom, cameraPosition);

        // clock puede avanzar entre orig y BackgroundScene.Update → _pendingSkySync
        if (BlendClock.IsRunning && newRoom != null && BlendSettingsLoader.Active != null)
        {
            string newRoomName = newRoom.abstractRoom?.name;
            if (newRoomName != null && BlendSettingsLoader.Active.IncludesRoom(newRoomName))
            {
                var skyType = BlendSettingsLoader.Active.GetSkyType(newRoomName);
                bool hasSky = skyType == SkyType.ACV || skyType == SkyType.RTV;
                if (hasSky)
                {
                    // estados al momento actual del clock
                    var phase = BlendClock.CurrentPhase;
                    int syncA, syncB;
                    if (phase == BlendClock.Phase.Blending)
                    {
                        syncA = BlendClock.StateA;
                        syncB = BlendClock.StateB;
                    }
                    else if (phase == BlendClock.Phase.Done)
                    {
                        syncA = BlendClock.StateB;
                        syncB = NextStateIn(BlendSettingsLoader.Active, BlendClock.StateB);
                    }
                    else // Idle
                    {
                        syncA = BlendClock.StateA;
                        syncB = NextStateIn(BlendSettingsLoader.Active, BlendClock.StateA);
                    }
                    // _pendingSkySync → consumido en primer Update camIsHere=true
                    _pendingSkySync   = true;
                    _pendingSkyStateA = syncA;
                    _pendingSkyStateB = syncB;

                    // forzar illustrationName+alpha post-orig directamente
                    var acvPost = _acvScene;
                    var rtvPost = _rtvScene;
                    bool isAcvPost = skyType == SkyType.ACV && acvPost != null
                        && acvPost.room?.abstractRoom?.name == newRoomName;
                    bool isRtvPost = skyType == SkyType.RTV && rtvPost != null
                        && rtvPost.room?.abstractRoom?.name == newRoomName;

                    if (isAcvPost || isRtvPost)
                    {
                        // SyncSkySlots actualiza illustrationName en los tres slots.
                        SyncSkySlots(newRoom, syncA, syncB);
                        // PreOrigForceSkyAlpha escribe alpha correcto según phase actual.
                        var dayPost  = isAcvPost ? acvPost.daySky   : rtvPost.daySky;
                        var duskPost = isAcvPost ? acvPost.duskSky  : rtvPost.duskSky;
                        var ngtPost  = isAcvPost ? acvPost.nightSky : rtvPost.nightSky;
                        if (dayPost != null) PreOrigForceSkyAlpha(dayPost, duskPost, ngtPost, skyType);
                    }

                    RSPlugin.log.LogDebug(
                        $"[OnChangeRoom] PendingSkySync post-orig: phase={phase} A={syncA} B={syncB}");
                }
            }
        }
    }

    private static void OnApplyFade(On.RoomCamera.orig_ApplyFade orig, RoomCamera self)
    {
        orig(self);

        if (self.room == null) return;
        var settings = BlendSettingsLoader.Active;
        if (settings == null) return;
        string roomName = self.room.abstractRoom?.name;
        if (roomName == null || !settings.IncludesRoom(roomName)) return;

        // Sobreescribir skyColor/fogColor en currentPalette con los tints del mod.
        var snap = _activeSnapshot;
        if (snap == null) return;

        if (snap.TintMultiply.HasValue)
            self.currentPalette.skyColor = snap.TintMultiply.Value;
        if (snap.TintAtmosphere.HasValue)
            self.currentPalette.fogColor = snap.TintAtmosphere.Value;
    }

    private static void OnChangeBothPalettes(
        On.RoomCamera.orig_ChangeBothPalettes orig, RoomCamera self,
        int palA, int palB, float blend)
    {
        // Si el blend está activo en esta sala exacta, el mod tiene control total
        if (_active && _room != null && self.room == _room && BlendTextureManager.Ready
            && !_moveCameraThisFrame)
        {
            float t = _externalT ? _forcedT : BlendSlider.BlendFactor;
            MixAndApply(self, t, SettingsSnapshot.Lerp(_snapA, _snapB, t));
            return;
        }

        orig(self, palA, palB, blend);
    }

}
