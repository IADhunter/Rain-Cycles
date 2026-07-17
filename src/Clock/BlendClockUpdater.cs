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
            SettingsSnapshot.PreloadRegionTemplates(regionAfter);
            BlendSettingsLoader.LoadRegion(regionAfter);

            SettingsBlendController.ResetFull();
            _startFailed = false;
            _lastRegion = regionAfter;
            RoomCameraExtensions.ClearAllCaches();
            StateFileResolver.InvalidatePathCache(); // ⭐ NUEVO
        }
    }

    private static void OnGameUpdate(On.RainWorldGame.orig_Update orig, RainWorldGame self)
    {
        SettingsBlendController.ClearFrameFlag();
        orig(self);

        if (self.GamePaused)
        {
            if (!BlendClock.EditMode)
                UpdateCameras(self);
            return;
        }

        bool isArena = self.GetStorySession == null;
        if (isArena && BlendSettingsLoader.Active == null) return;
        if (!isArena && self.GetStorySession == null) return;

        // ============================================================
        // BLEND CLOCK STARTUP
        // ============================================================
        if (!isArena && !_winHandledThisSession && !BlendClock.IsRunning && !BlendClock.EditMode && !_startFailed)
        {
            var s = BlendSettingsLoader.Active;
            
            if (s != null && s.Clock)
            {
                int initialState = ResolveInitial(s);
                BlendClock.Start(_lastRegion, initialState);

                if (!BlendClock.IsRunning)
                {
                    _startFailed = true;
                }
                else
                {
                    if (_savedState.IsRunning && _savedState.Mode == s.Mode)
                        BlendClock.RestoreState(_savedState);
                    _savedState = default;
                }
            }
        }

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

        if (BlendClock.EditMode)
        {
            UpdateSlidersOnly(self);
            return;
        }

        SettingsBlendController.ProcessPendingSkyRefresh();

        // ============================================================
        // ACTUALIZAR CÁMARAS - CACHES EVITAN TRABAJO PESADO
        // ============================================================
        UpdateCameras(self);
    }

    private static void UpdateSlidersOnly(RainWorldGame game)
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
            
            if (!RoomCameraExtensions.IsBlendRoomCached(cam.room)) continue;

            bool hasFullStates = RoomCameraExtensions.HasFullStatesCached(room);

            if (BlendClock.IsRunning && BlendClock.CurrentPhase == BlendClock.Phase.Blending && hasFullStates)
            {
                // ⭐ Ahora usa StateFileResolver.ResolveSettingsPath()
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
                    
                    SettingsBlendController.ApplyPsvAlphas(BlendClock.SubPhaseLocalT, isBlending: true);
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
                    
                    SettingsBlendController.SyncSkySlots(cam.room, idleState, idleState);
                }
            }
            else if (!BlendClock.IsRunning)
            {
                if (SettingsBlendController.IsActive && SettingsBlendController.IsExternalT && !SettingsBlendController.IsAutoBlend)
                {
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
                    
                    SettingsBlendController.SyncSkySlots(cam.room, finalState, finalState);
                }
            }
            
            AfterIdleCheck:

            if (BlendClock.IsRunning && hasFullStates)
            {
                cam.UpdateBlendPalette();
            }
            else if (!hasFullStates)
            {
                var blendData = cam.GetBlendData();
                if (blendData != null && blendData.isBlendActive)
                {
                    blendData.isBlendActive = false;
                }
            }
        }

        UpdateSliders(game);
    }

    // ============================================================
    // ⭐ CAMBIO PRINCIPAL: Ahora usa StateFileResolver.ResolveSettingsPath()
    // ============================================================
    private static string GetSettingsFile(RainWorldGame game, string room, int state)
    {
        if (game?.IsArenaSession == true)
            return ArenaStateResolver.GetSettingsPath(room, state);
        return StateFileResolver.ResolveSettingsPath(room, state);
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

        BlendClock.Start(_lastRegion, ResolveInitial(s));
    }

    private static void OnShutDown(On.RainWorldGame.orig_ShutDownProcess orig, RainWorldGame self)
    {
        BlendClock.Stop();
        orig(self);
        StateFileResolver.SetBlockLoad(false);
        RoomCameraExtensions.InvalidateAllRoomCaches();
        StateFileResolver.InvalidatePathCache(); // ⭐ NUEVO
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