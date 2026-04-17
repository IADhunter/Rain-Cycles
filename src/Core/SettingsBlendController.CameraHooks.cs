using UnityEngine;
using RainCycles.Settings;
using RainCycles.Snapshot;
using RainCycles.Sky;
using RainCycles.Clock;
using RainCycles.Core;

namespace RainCycles.Core;

// Hooks de cámara: OnMoveCamera, OnRoomCameraUpdate, OnRoomSettingsSave, OnDayNightDataApply
public static partial class SettingsBlendController
{

    private static void OnMoveCamera(
        On.RoomCamera.orig_MoveCamera_Room_int orig, RoomCamera self,
        Room newRoom, int camPos)
    {
        // MoveCamera → _moveCameraThisFrame=true bloquea TryApplyIdle
        _moveCameraThisFrame = true;

        string prevRoomForReset = self.room?.abstractRoom?.name;
        bool prevWasManaged = prevRoomForReset != null
            && BlendSettingsLoader.Active != null
            && BlendSettingsLoader.Active.IncludesRoom(prevRoomForReset);
        string nextRoomForReset = newRoom?.abstractRoom?.name;
        bool nextIsManaged = nextRoomForReset != null
            && BlendSettingsLoader.Active != null
            && BlendSettingsLoader.Active.IncludesRoom(nextRoomForReset);

        if (prevWasManaged && !nextIsManaged)
        {
            // Si el clock está en Idle y los slots no corresponden a StateA,
            if (BlendClock.IsRunning && BlendClock.CurrentPhase == BlendClock.Phase.Idle
                && self.room != null)
            {
                var prevSkyType = BlendSettingsLoader.Active?.GetSkyType(self.room.abstractRoom?.name ?? "") ?? SkyType.None;
                if (prevSkyType != SkyType.None && GetSlotDay(prevSkyType) != BlendClock.StateA)
                    ApplySkyForState(BlendClock.StateA, self.room);
            }
        }

        BlendClockUpdater.ClearLastIdleRoom();

        orig(self, newRoom, camPos);

        // Detach y limpieza DESPUÉS del orig:
        if (_active && _room != null && newRoom != _room)
            DetachAndRestore();

        if (prevWasManaged && !nextIsManaged)
        {
            Shader.SetGlobalVector(RainWorld.ShadPropMultiplyColor,
                new UnityEngine.Vector4(1f, 1f, 1f, 1f));
            Shader.SetGlobalVector(RainWorld.ShadPropAboveCloudsAtmosphereColor,
                new UnityEngine.Vector4(0.16078432f, 0.23137255f, 0.31764707f, 1f));
            _activeSnapshot = null;
            // Resetear para que la próxima re-entrada fuerce ApplySkyForState
            _lastIdleSkyState = -1;
        }

        if (newRoom == null) return;

        var blendSettings = BlendSettingsLoader.Active;
        if (blendSettings == null) return;

        string newRoomName = newRoom.abstractRoom?.name;
        if (newRoomName == null || !blendSettings.IncludesRoom(newRoomName)) return;

        // Si el clock no está corriendo aún, aplicar el estado correcto del ciclo inmediatamente.
        // Evita que vanilla pinte el estado base de la sala por unos frames antes de que
        // TryApplyIdle corra en el updater.
        if (!BlendClock.IsRunning)
        {
            var game = newRoom.game;
            int cycle = game?.GetStorySession?.saveState?.cycleNumber ?? 0;
            bool isArena = game?.IsArenaSession == true;
            string earlyPath = isArena
                ? ArenaStateResolver.GetSelectedSettingsPath(newRoomName, 0)
                : StateFileResolver.GetRainStateFilePath(newRoomName, cycle);
            RSPlugin.log.LogDebug($"[MoveCamera] clock=stopped room={newRoomName} earlyPath={earlyPath ?? "NULL"} self.room=={(self.room == newRoom ? "newRoom" : "other")}");
            if (earlyPath != null)
            {
                if (self.room == newRoom)
                {
                    RSPlugin.log.LogDebug($"[MoveCamera] ApplyIdleState immediate room={newRoomName}");
                    ApplyIdleState(newRoom, earlyPath, allowCameraOps: true);
                    BlendClockUpdater.SetLastIdleRoom(newRoomName);
                }
                else
                {
                    RSPlugin.log.LogDebug($"[MoveCamera] pending idle room={newRoomName}");
                    _pendingIdleRoom = newRoom;
                    _pendingIdlePath = earlyPath;
                }
            }
            return;
        }

        // consumir pending idle cuando cam llega a la sala
        if (_pendingIdleRoom != null &&
            _pendingIdleRoom.abstractRoom?.name == newRoomName &&
            _pendingIdlePath != null)
        {
            // Aplicar si cam.room ya actualizó, de lo contrario diferir al OnUpdateDayNightPalette
            if (self.room == newRoom)
            {
                ApplyIdleState(_pendingIdleRoom, _pendingIdlePath, allowCameraOps: true);
                BlendClockUpdater.SetLastIdleRoom(newRoomName);
            }
            _pendingIdleRoom = null;
            _pendingIdlePath = null;
        }

        if (BlendClock.CurrentPhase == BlendClock.Phase.Blending)
        {
            string pathA = StateFileResolver.GetRainStateSettingsFile(newRoomName, BlendClock.StateA);
            string pathB = StateFileResolver.GetRainStateSettingsFile(newRoomName, BlendClock.StateB);

            if (pathA != null && pathB != null && BlendClock.StateA != BlendClock.StateB)
            {
                var newSkyType = blendSettings.GetSkyType(newRoomName);
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
                    // same=False → NotifyCameras hará attach
                    _entryFrameT = BlendClock.SubPhaseLocalT;
                    // Pre-calcular _lastAtmosphereColor con los snapshots de la nueva sala.
                    var snapA2 = SettingsSnapshot.FromFileWithTemplate(pathA, newRoomName);
                    var snapB2 = SettingsSnapshot.FromFileWithTemplate(pathB, newRoomName);
                    if (snapA2 != null && snapB2 != null)
                    {
                        var lerped2 = SettingsSnapshot.Lerp(snapA2, snapB2, _entryFrameT);
                        if (lerped2.TintCloudAtmosphere.HasValue)
                            _lastAtmosphereColor = lerped2.TintCloudAtmosphere.Value;
                    }
                }
            }
        }
        else
        {
            int stateToShow = BlendClock.CurrentPhase == BlendClock.Phase.Done
                ? BlendClock.StateB
                : BlendClock.StateA;

            string path = StateFileResolver.GetRainStateSettingsFile(newRoomName, stateToShow);
            if (path != null)
            {
                if (self.room == newRoom)
                {
                    ApplyIdleState(newRoom, path, allowCameraOps: true);
                    BlendClockUpdater.SetLastIdleRoom(newRoomName);
                }
                else
                {
                    _pendingIdleRoom = newRoom;
                    _pendingIdlePath = path;
                }
            }
        }
    }

    // ── Bloqueo de DayNightData.Apply ────────────────────────────────────

    private static void OnRoomCameraUpdate(On.RoomCamera.orig_Update orig, RoomCamera self)
    {
        orig(self);

        if (self.room == null) return;
        var settings = BlendSettingsLoader.Active;
        if (settings == null) return;
        string roomName = self.room.abstractRoom?.name;
        if (roomName == null) return;

        if (settings.IncludesRoom(roomName))
            self.effect_dayNight = 0f;

        // Consumir pending idle en el primer frame donde cam.room ya apunta
        if (_pendingIdleRoom != null &&
            _pendingIdleRoom.abstractRoom?.name == roomName &&
            _pendingIdlePath != null &&
            !_moveCameraThisFrame)
        {
            ApplyIdleState(_pendingIdleRoom, _pendingIdlePath, allowCameraOps: true);
            BlendClockUpdater.SetLastIdleRoom(roomName);
            _pendingIdleRoom = null;
            _pendingIdlePath = null;
        }
    }

    private static void OnRoomSettingsSave(On.RoomSettings.orig_Save orig, RoomSettings self)
    {
        // Leer RC_TINT antes de que Save() lo borre, luego reinyectarlo.
        string filePath = self.filePath;
        bool isTracked = filePath != null && BlendSettingsLoader.Active != null;

        string rcTintLine = null;
        if (isTracked)
            rcTintLine = ExtractRcTintLine(filePath);

        orig(self);

        if (isTracked && rcTintLine != null)
            ReappendRcTint(filePath, rcTintLine);
    }

    private static void OnDayNightDataApply(
        On.PlacedObject.DayNightData.orig_Apply orig,
        PlacedObject.DayNightData self, Room room)
    {
        // Bloquear si la sala está bajo blend activo
        if (_active && _room != null && room == _room)
            return;

        // Bloquear si la sala está en [ROOMS] — el mod gestiona esta sala
        if (BlendSettingsLoader.Active != null && room != null)
        {
            string roomName = room.abstractRoom?.name;
            if (roomName != null && BlendSettingsLoader.Active.IncludesRoom(roomName))
                return;
        }

        orig(self, room);
    }

}