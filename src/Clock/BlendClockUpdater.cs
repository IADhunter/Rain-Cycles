using UnityEngine;
using FilesSetting;
using RainCycles.Settings;
using RainCycles.Core;
using RainCycles.Snapshot;
using RainCycles.Blend;

namespace RainCycles.Clock;

public static class BlendClockUpdater
{
    private static float _lastUnscaledTime = 0f;

    private static float GameDelta(RainWorldGame game)
    {
        float now = UnityEngine.Time.unscaledTime;
        float dt = Mathf.Clamp(now - _lastUnscaledTime, 0f, 0.2f);
        _lastUnscaledTime = now;
        float slowFactor = Mathf.Clamp01(game.framesPerSecond / 40f);
        return dt * slowFactor;
    }

    private static string _lastRegion = null;
    private static bool _startFailed = false;
    private static bool _lastDeathRainHasHit = false;
    private static bool _winHandledThisSession = false;
    private static BlendClock.ClockState _savedState;

    public static void Init()
    {
        On.RainWorldGame.Update += OnGameUpdate;
        On.RainWorldGame.ShutDownProcess += OnShutDown;
        On.RoomCamera.DrawUpdate += OnRoomCameraDrawUpdate;
        On.RainWorldGame.Win += OnWin;
        On.RainCycle.Update += OnRainCycleUpdate;
        On.OverWorld.Update += OnOverWorldUpdate;
        On.RoomCamera.ApplyFade += SettingsBlendController.OnApplyFade;
    }

    private static void OnRoomCameraDrawUpdate(
        On.RoomCamera.orig_DrawUpdate orig, RoomCamera self,
        float timeStacker, float timeSpeed)
    {
        if (self.room?.roomSettings != null)
            Shader.SetGlobalFloat(RainWorld.ShadPropGrime, self.room.roomSettings.Grime);
        orig(self, timeStacker, timeSpeed);
    }

    private static void OnOverWorldUpdate(On.OverWorld.orig_Update orig, OverWorld self)
    {
        string regionBefore = self.activeWorld?.region?.name?.ToUpperInvariant();
        orig(self);
        string regionAfter = self.activeWorld?.region?.name?.ToUpperInvariant();

        if (string.IsNullOrEmpty(regionAfter)) return;
        bool regionChanged = regionAfter != _lastRegion;

        if (regionChanged)
        {
            RSPlugin.log.LogInfo($"[RegionChange] {_lastRegion ?? "null"} → {regionAfter}");

            if (BlendClock.IsRunning)
            {
                _savedState = BlendClock.SaveState();
                BlendClock.Stop();
            }
            else
            {
                _savedState = default;
            }

            if (!string.IsNullOrEmpty(_lastRegion))
            {
                BlendSkyAtlasCache.UnloadRegion(_lastRegion);
            }

            SettingsBlendController.ClearAllSlots();
            BlendSkyAtlasCache.PreloadRegion(regionAfter);
            StaticTintManager.PreloadRegionTemplates(regionAfter);
            BlendSettingsLoader.LoadRegion(regionAfter);

            SettingsBlendController.ResetFull();
            _startFailed = false;
            _lastRegion = regionAfter;
            RoomCameraExtensions.ClearAllCaches();
        }
    }

    private static void OnGameUpdate(On.RainWorldGame.orig_Update orig, RainWorldGame self)
    {
        SettingsBlendController.ClearFrameFlag();
        orig(self);
        SettingsBlendController.OverrideLightColorsPostOrig();

        // SAFETY NET: Limpieza para caso idle (fuga de paleta residual)
        var activeRoom = SettingsBlendController.ActiveRoom;
        if (activeRoom != null)
        {
            var cam = self.cameras?[0];
            if (cam != null)
            {
                string camRoom = cam.room?.abstractRoom?.name;
                string blendRoom = activeRoom.abstractRoom?.name;
                
                if (blendRoom != null && camRoom != blendRoom && 
                    !SettingsBlendController.MoveCameraThisFrame)
                {
                    RSPlugin.log.LogWarning(
                        $"Room change missed (idle): '{blendRoom}'→'{camRoom}'. Forcing cleanup.");
                    
                    SettingsBlendController.Detach();
                    
                    var blendData = cam.GetBlendData();
                    if (blendData != null)
                    {
                        blendData.isBlendActive = false;
                    }
                    
                    int correctPal = cam.room?.roomSettings?.Palette ?? 0;
                    cam.ChangeMainPalette(correctPal);
                    cam.ApplyFade();
                }
            }
        }

        if (self.GamePaused)
        {
            if (!BlendClock.EditMode)
                UpdateCameras(self);
            return;
        }

        bool isArena = self.GetStorySession == null;
        if (isArena && BlendSettingsLoader.Active == null) return;
        if (!isArena && self.GetStorySession == null) return;

        if (SettingsBlendController.IsActive)
        {
            var cam = self.cameras?[0];
            if (cam != null)
            {
                string camRoom = cam.room?.abstractRoom?.name;
                string blendRoom = SettingsBlendController.ActiveRoom?.abstractRoom?.name;
                if (blendRoom != null && camRoom != blendRoom && !SettingsBlendController.MoveCameraThisFrame)
                {
                    RSPlugin.log.LogWarning($"Room change missed: '{blendRoom}'→'{camRoom}'. Recovering.");
                    SettingsBlendController.Detach();
                    int correctPal = cam.room?.roomSettings?.Palette ?? 0;
                    cam.ChangeMainPalette(correctPal);
                    cam.ApplyFade();
                }
            }
        }

        // ============================================================
        // BLEND CLOCK STARTUP
        // ============================================================
        if (!isArena && !_winHandledThisSession && !BlendClock.IsRunning && !BlendClock.EditMode && !_startFailed)
        {
            var s = BlendSettingsLoader.Active;
            
            if (s != null && s.Clock)
            {
                RSPlugin.log.LogInfo($"[BlendClock] Starting for region {_lastRegion}");
                int initialState = ResolveInitial(s);
                BlendClock.Start(_lastRegion, initialState);

                if (!BlendClock.IsRunning)
                {
                    _startFailed = true;
                    RSPlugin.log.LogWarning($"[BlendClock] Start failed for region {_lastRegion}");
                }
                else
                {
                    if (_savedState.IsRunning && _savedState.Mode == s.Mode)
                        BlendClock.RestoreState(_savedState);
                    _savedState = default;
                }
            }
            else if (s != null && !s.Clock && self.cameras?[0]?.room != null)
            {
                var cam = self.cameras[0];
                if (cam?.room != null && SettingsBlendController.IsBlendRoom(cam.room))
                    ApplyStaticTintsForCurrentState(cam.room);
            }
        }

        // NO actualizar el clock si EditMode está activo
        if (!BlendClock.EditMode && BlendClock.IsRunning)
        {
            float rainTimer = 0f;
            int rainLen = 1;
            if (self.world?.rainCycle != null)
            {
                rainTimer = self.world.rainCycle.timer;
                rainLen = self.world.rainCycle.cycleLength;
            }
            BlendClock.Tick(GameDelta(self), rainTimer, rainLen);
        }

        // EN MODO EDICIÓN: NO actualizar cámaras con lógica de blend
        if (BlendClock.EditMode)
        {
            UpdateSlidersOnly(self);
            return;
        }

        // ============================================================
        // Procesar refresco pendiente de sky slots (post-guardado)
        // ============================================================
        SettingsBlendController.ProcessPendingSkyRefresh();

        UpdateCameras(self);
        SettingsBlendController.OverrideLightColorsPostOrig();
    }

    private static void UpdateSlidersOnly(RainWorldGame game)
    {
        var page = game.devUI?.activePage;
        if (page == null) return;

        // No logs necesarios - función silenciosa
        BlendSlider slider = null;
        foreach (var node in page.subNodes)
        {
            if (node is RCPanel panel)
            {
                foreach (var sub in panel.subNodes)
                {
                    if (sub is BlendSlider bs) { slider = bs; break; }
                }
                break;
            }
        }
    }

    private static void UpdateCameras(RainWorldGame game)
    {
        var s = BlendSettingsLoader.Active;
        if (s == null) return;

        foreach (var cam in game.cameras ?? System.Array.Empty<RoomCamera>())
        {
            if (cam?.room == null) continue;
            string room = cam.room.abstractRoom?.name;
            if (room == null) continue;
            if (!SettingsBlendController.IsBlendRoom(cam.room)) continue;

            if (BlendClock.IsRunning && BlendClock.CurrentPhase == BlendClock.Phase.Blending)
            {
                string pA = GetSettingsFile(game, room, BlendClock.StateA);
                string pB = GetSettingsFile(game, room, BlendClock.StateB);

                if (pA != null && pB != null && BlendClock.StateA != BlendClock.StateB)
                {
                    if (!SettingsBlendController.IsActive ||
                        SettingsBlendController.CurrentPathA != pA ||
                        SettingsBlendController.CurrentPathB != pB)
                    {
                        SettingsBlendController.AttachWithExternalT(cam.room, pA, pB, isAuto: true);
                    }
                    SettingsBlendController.SetExternalT(BlendClock.SubPhaseLocalT);
                }
            }
            else if (BlendClock.IsRunning && BlendClock.CurrentPhase == BlendClock.Phase.Idle)
            {
                int idleState = BlendClock.StateA;
                string path = GetSettingsFile(game, room, idleState);

                if (path != null)
                {
                    bool stateChanged = SettingsBlendController.IsActive &&
                                       (SettingsBlendController.CurrentPathA != path ||
                                        SettingsBlendController.CurrentPathB != path);

                    if (stateChanged || !SettingsBlendController.IsActive)
                    {
                        if (SettingsBlendController.IsActive)
                            SettingsBlendController.Detach();
                        SettingsBlendController.AttachWithExternalT(cam.room, path, path, isAuto: true);
                    }
                    SettingsBlendController.SetExternalT(0f);
                    SettingsBlendController.RotateSlotsOnIdle(cam.room, idleState);
                }
            }
            else if (!BlendClock.IsRunning)
            {
                if (SettingsBlendController.IsActive && SettingsBlendController.IsExternalT && !SettingsBlendController.IsAutoBlend)
                {
                    // Modo manual activo - silencioso
                    if (SettingsBlendController.ActiveRoom == cam.room)
                    {
                        cam.UpdateBlendPalette();
                    }
                    goto AfterIdleCheck;
                }
                
                int finalState = BlendClock.StateA;
                string path = GetSettingsFile(game, room, finalState);
                if (path != null)
                {
                    if (!SettingsBlendController.IsActive || SettingsBlendController.CurrentPathA != path)
                        SettingsBlendController.AttachWithExternalT(cam.room, path, path, isAuto: true);
                    SettingsBlendController.SetExternalT(0f);
                }
            }
            
            AfterIdleCheck:

            if (BlendClock.IsRunning)
                cam.UpdateBlendPalette();
        }

        UpdateSliders(game);
    }

    private static string GetSettingsFile(RainWorldGame game, string room, int state)
    {
        if (game?.IsArenaSession == true)
            return ArenaStateResolver.GetSettingsPath(room, state);
        return StateFileResolver.GetRainStateSettingsFile(room, state);
    }

    private static void ApplyStaticTintsForCurrentState(Room room)
    {
        if (room == null) return;
        string settingsPath = room.roomSettings?.filePath;
        if (string.IsNullOrEmpty(settingsPath)) return;

        var snap = SettingsSnapshot.FromFile(settingsPath);
        if (snap == null) return;

        if (snap.TintMultiply.HasValue)
        {
            var c = snap.TintMultiply.Value;
            Shader.SetGlobalVector(RainWorld.ShadPropMultiplyColor, new Vector4(c.r, c.g, c.b, 1f));
        }
        if (snap.TintAtmosphere.HasValue)
        {
            var c = snap.TintAtmosphere.Value;
            Shader.SetGlobalVector(RainWorld.ShadPropAboveCloudsAtmosphereColor, new Vector4(c.r, c.g, c.b, 1f));
        }
        if (snap.TintCloudAtmosphere.HasValue)
        {
            for (int i = 0; i < room.updateList.Count; i++)
            {
                if (room.updateList[i] is AboveCloudsView acv)
                {
                    acv.atmosphereColor = snap.TintCloudAtmosphere.Value;
                    break;
                }
            }
        }
    }

    private static void UpdateSliders(RainWorldGame game)
    {
        var page = game.devUI?.activePage;
        if (page == null) return;

        BlendSlider slider = null;
        foreach (var node in page.subNodes)
        {
            if (node is RCPanel panel)
            {
                foreach (var sub in panel.subNodes)
                {
                    if (sub is BlendSlider bs) { slider = bs; break; }
                }
                break;
            }
        }

        if (slider == null) return;
        
        if (SettingsBlendController.IsActive && SettingsBlendController.IsExternalT && !SettingsBlendController.IsAutoBlend)
        {
            return;
        }
        
        if (!BlendClock.EditMode)
        {
            if (BlendClock.IsRunning)
            {
                slider.SetDisplayT(BlendClock.T);
            }
            else
            {
                slider.SetDisplayT(0f);
            }
        }
    }

    private static int ResolveInitial(BlendSettings s)
    {
        int state = StateFileResolver.GetCurrentCycleState();
        if (state > 0) return state;
        RSPlugin.log.LogWarning("[ResolveInitial] Fallback a estado 1");
        return 1;
    }

    private static void OnRainCycleUpdate(On.RainCycle.orig_Update orig, RainCycle self)
    {
        bool was = self.deathRainHasHit;
        orig(self);
        if (!was && self.deathRainHasHit && !_lastDeathRainHasHit)
        {
            _lastDeathRainHasHit = true;
            OnDeathRainHit();
        }
        else if (!self.deathRainHasHit)
        {
            _lastDeathRainHasHit = false;
        }
    }

    private static void OnDeathRainHit()
    {
        var s = BlendSettingsLoader.Active;
        if (s == null || s.Mode != BlendMode.EndCycle || BlendClock.EditMode || BlendClock.IsRunning) return;

        RSPlugin.log.LogInfo("[BlendClock] Death rain triggered EndCycle");
        BlendClock.Start(_lastRegion, ResolveInitial(s));
    }

    private static void OnShutDown(On.RainWorldGame.orig_ShutDownProcess orig, RainWorldGame self)
    {
        BlendClock.Stop();
        orig(self);
        StateFileResolver.SetBlockLoad(false);
    }

    private static void OnWin(On.RainWorldGame.orig_Win orig, RainWorldGame self, bool mal, bool warp)
    {
        if (!_winHandledThisSession)
        {
            _winHandledThisSession = true;
            StateFileResolver.SetBlockLoad(true);
            BlendClock.Stop();
        }
        orig(self, mal, warp);
        if (!warp)
            _lastRegion = null;
    }
}