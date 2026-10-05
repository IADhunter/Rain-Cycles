using UnityEngine;
using System.Collections.Generic;
using RainCycles.Settings;
using RainCycles.Snapshot;
using RainCycles.Sky;
using RainCycles.Clock;
using RainCycles.Core;
using RainCycles.Blend;
using Watcher;

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

        if (prevWasManaged && !nextIsManaged)
        {
            Detach();

            var blendData = self.GetBlendData();
            if (blendData != null)
            {
                blendData.isBlendActive = false;
            }

            var cam = self.game?.cameras?[0];
            if (cam != null)
            {
                cam.paletteA = -1;
                cam.paletteBlend = 0f;
            }

            _activeSnapshot = null;
            _psvScene = null;
            _acvScene = null;
            _orvScene = null;
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
            _activeSlots = null;
            _psvScene = null;
            _acvScene = null;
            _orvScene = null;
            _lastRoomWasManaged = false;
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
                    // isAuto: true — este attach es el blend automático del
                    // ciclo (BlendClock.IsRunning && Blending). Con el default
                    // false, UpdateCameras no lo re-marea (needsAttach solo
                    // compara rutas) y el flag quedaba como "manual":
                    // UpdateSliders dejaba de seguir el reloj y SwitchTab
                    // lo trataba como blend manual de usuario.
                    AttachWithExternalT(newRoom, pathA, pathB, isAuto: true);
                    SetExternalT(t);
                }
                else
                {
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
                else
                {
                    RSPlugin.log.LogWarning($"[RC][CameraHooks] ChangeRoom: slot[{i}] has NO spriteLeaser! slot={slot.illustrationName} alpha={slot.alpha}");
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
            if (_psvScene != null || _acvScene != null || _orvScene != null)
            _activeSnapshot = null;
            _psvScene = null;
            _acvScene = null;
            _orvScene = null;
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

            // Ocultar vanilla solo si la regla lo pide: (1) algún estado
            // Blend con <Mod>, o (2) el estado activo con <Mod>. Sin <Mod>
            // → restaurar para que el cielo vanilla se vea al instante.
            bool hideVanilla = BkgResolver.ShouldHideVanilla(roomName, ActiveHideState());
            ToggleVanillaSlots(self, rcState.View, hideVanilla);
        }
        else
        {
            // Sala sin view/tipo activo → el juego dibuja vanilla: devolver
            // lo que se hubiera ocultado en una visita previa.
            RestoreVanillaAlpha();
        }

        if (_psvScene != null && _activeSlots?.fog != null && _activeSlots.fog.Count > 0)
        {
            SyncFogSlotPosition(self);
        }
        else if (isStaticRoom && TryResolveStaticFog(self.room, out var staticFogScene, out var staticFogSlots))
        {
            SyncFogSlotPosition(self, staticFogScene, staticFogSlots);
        }
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
                        {
                            Shader.SetGlobalVector(RainWorld.ShadPropMultiplyColor,
                                new Vector4(snap2.TintMultiply.Value.r, snap2.TintMultiply.Value.g, snap2.TintMultiply.Value.b, 1f));
                        }
                        if (snap2?.TintAtmosphere != null)
                        {
                            Shader.SetGlobalVector(RainWorld.ShadPropAboveCloudsAtmosphereColor,
                                new Vector4(snap2.TintAtmosphere.Value.r, snap2.TintAtmosphere.Value.g, snap2.TintAtmosphere.Value.b, 1f));
                        }
                    }
                }
            }
        }
    }

    // ============================================================
    // TOGGLE VANILLA SLOTS - OCULTAR/RESTAURAR SEGÚN <Mod> ACTIVO
    //
    // Guarda el alfa previo la primera vez que oculta: vanilla solo
    // reescribe daySky/duskSky en transiciones (AboveCloudsView.cs:373,
    // RoofTopView.cs:136) y nightSky jamás → en estado estable el campo
    // queda congelado y hay que restaurar el valor guardado, no "dejar
    // de tocar". Se recorre por frame (mismo coste que el ForceHide
    // anterior) para reaplicar ante reescrituras puntuales de vanilla.
    // ============================================================
    private static Dictionary<BackgroundScene.Simple2DBackgroundIllustration, float> _savedVanillaAlpha = new();
    private static Dictionary<FSprite, float> _savedVanillaSpriteAlpha = new();
    private static string _hiddenRoom;

    // Estado activo para decidir ocultación: en EditMode manda la
    // selección del panel (ManualStateA, sincronizada con
    // RCPanel.ButtonSelectedA); fuera, el reloj.
    private static int ActiveHideState()
        => BlendClock.EditMode ? ManualStateA : BlendClock.StateA;

    private static void ToggleVanillaSlots(RoomCamera cam, ViewType view, bool hide)
    {
        var room = cam?.room;
        if (room == null)
        {
            RestoreVanillaAlpha();
            return;
        }

        string roomName = room.abstractRoom?.name;

        // Cambio de sala: lo ocultado en la anterior ya no aplica.
        if (_hiddenRoom != null &&
            !string.Equals(_hiddenRoom, roomName, System.StringComparison.OrdinalIgnoreCase))
            RestoreVanillaAlpha();

        if (!hide)
        {
            RestoreVanillaAlpha();
            return;
        }

        for (int i = 0; i < room.updateList.Count; i++)
        {
            if (room.updateList[i] is AboveCloudsView acv)
            {
                if (view == ViewType.ACV || view == ViewType.PSV)
                {
                    HideVanillaAlpha(acv.daySky);
                    HideVanillaAlpha(acv.duskSky);
                    HideVanillaAlpha(acv.nightSky);

                    // PSV: también el abi pnk_ (HorizonFog va por leasers).
                    if (view == ViewType.PSV)
                    {
                        foreach (var el in acv.elements)
                        {
                            if (el is BackgroundScene.AdditiveBackgroundIllustration abi &&
                                abi.illustrationName?.StartsWith("pnk_") == true)
                                HideVanillaAlpha(abi);
                        }
                    }
                }
            }
            else if (room.updateList[i] is RoofTopView rtv)
            {
                if (view == ViewType.RTV)
                {
                    HideVanillaAlpha(rtv.daySky);
                    HideVanillaAlpha(rtv.duskSky);
                    HideVanillaAlpha(rtv.nightSky);
                }
            }
            else if (room.updateList[i] is OuterRimView orv)
            {
                if (view == ViewType.ORV)
                {
                    foreach (var el in orv.elements)
                    {
                        if (el is BackgroundScene.Simple2DBackgroundIllustration ill &&
                            ill.illustrationName == "otr_sky")
                        {
                            HideVanillaAlpha(ill);
                            break;
                        }
                    }
                }
            }
        }

        // Sprites: HorizonFog pnk_* (solo PSV) y DistantCloud de alto
        // depth (PinkSky). Solo alpha: DistantCloud.DrawSprites reescribe
        // isVisible por frame por altitud (AboveCloudsView.cs:682).
        if (cam.spriteLeasers != null)
        {
            foreach (var sl in cam.spriteLeasers)
            {
                if (sl?.sprites == null || sl.drawableObject == null) continue;

                if (view == ViewType.PSV &&
                    sl.drawableObject is AboveCloudsView.HorizonFog hf &&
                    hf.illustrationName?.StartsWith("pnk_") == true)
                {
                    foreach (var sp in sl.sprites)
                        if (sp != null) HideVanillaSpriteAlpha(sp);
                }
                else if (sl.drawableObject is AboveCloudsView.DistantCloud dc &&
                         dc.depth >= 195f &&
                         dc.AboveCloudsScene != null && dc.AboveCloudsScene.PinkSky)
                {
                    foreach (var sp in sl.sprites)
                        if (sp != null) HideVanillaSpriteAlpha(sp);
                }
            }
        }

        _hiddenRoom = roomName;
    }

    private static void HideVanillaAlpha(BackgroundScene.Simple2DBackgroundIllustration ill)
    {
        if (ill == null) return;
        if (!_savedVanillaAlpha.ContainsKey(ill))
            _savedVanillaAlpha[ill] = ill.alpha;
        ill.alpha = 0f;
    }

    private static void HideVanillaSpriteAlpha(FSprite sprite)
    {
        if (!_savedVanillaSpriteAlpha.ContainsKey(sprite))
            _savedVanillaSpriteAlpha[sprite] = sprite.alpha;
        sprite.alpha = 0f;
    }

    private static void RestoreVanillaAlpha()
    {
        if (_savedVanillaAlpha.Count > 0)
        {
            foreach (var kv in _savedVanillaAlpha)
                kv.Key.alpha = kv.Value;
            _savedVanillaAlpha.Clear();
        }
        if (_savedVanillaSpriteAlpha.Count > 0)
        {
            foreach (var kv in _savedVanillaSpriteAlpha)
                kv.Key.alpha = kv.Value;
            _savedVanillaSpriteAlpha.Clear();
        }
        _hiddenRoom = null;
    }

    private static List<BackgroundScene.Simple2DBackgroundIllustration> GetRcSlotsForRoom(Room room)
    {
        if (room == null) return null;
        for (int i = 0; i < room.updateList.Count; i++)
        {
            if (room.updateList[i] is AboveCloudsView acv && _sceneSlots.TryGetValue(acv, out var set))
                return set.blend;
            if (room.updateList[i] is RoofTopView rtv && _sceneSlots.TryGetValue(rtv, out var rtvSet))
                return rtvSet.blend;
        }
        return _activeSlots?.blend;
    }
}
