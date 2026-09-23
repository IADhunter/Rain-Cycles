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
    private static bool _loggedRainCycleThisSession = false;
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
            RSPlugin.log.LogInfo($"[RC][RegionChange] {regionBefore ?? "(null)"} → {regionAfter} | " +
                $"clockRunning={BlendClock.IsRunning} gateActive={BlendSettingsLoader.IsGateActive} " +
                $"active={(BlendSettingsLoader.Active != null ? BlendSettingsLoader.ActiveRegion : "null")}");

            if (BlendClock.IsRunning)
            {
                _savedState = BlendClock.SaveState();
                RSPlugin.log.LogInfo($"[RC][RegionChange] SaveState: T={_savedState.T:F3} phase={_savedState.CurrentPhase} " +
                    $"stateA={_savedState.StateA} stateB={_savedState.StateB} mode={_savedState.Mode} " +
                    $"loopAct={_savedState.LoopActivated} timer={_savedState.Timer:F2}");
                BlendClock.Stop();
            }
            else
            {
                _savedState = default;
                RSPlugin.log.LogInfo("[RC][RegionChange] Clock no corría → _savedState = default (SIN transferencia)");
            }

            if (!string.IsNullOrEmpty(_lastRegion))
            {
                BlendSkyAtlasCache.UnloadRegion(_lastRegion);
            }

            SettingsBlendController.ClearAllSlots();
            SettingsSnapshot.InvalidateAllCache();
            BlendSkyAtlasCache.PreloadRegion(regionAfter);
            SettingsSnapshot.PreloadRegionTemplates(regionAfter);
            BlendSettingsLoader.LoadRegion(regionAfter);

            SettingsBlendController.ResetFull();
            _startFailed = false;
            _lastRegion = regionAfter;
            RoomCameraExtensions.ClearAllCaches();
            StateFileResolver.InvalidatePathCache();

            var settings = BlendSettingsLoader.Active;
            bool isClockEnabled = settings != null && settings.Clock;
            int initialState = ResolveInitial(settings);

            RSPlugin.log.LogInfo($"[RC][RegionChange] LoadRegion({regionAfter}) → " +
                $"active={BlendSettingsLoader.ActiveRegion} settingsNull={settings == null} " +
                $"clock={isClockEnabled} mode={settings?.Mode} initialState={initialState}");

            RainCyclesEventDispatcher.DispatchRegionEnter(
                regionAfter,
                isClockEnabled ? settings?.Mode : null,
                isClockEnabled,
                initialState);
        }
    }

    private static void OnGameUpdate(On.RainWorldGame.orig_Update orig, RainWorldGame self)
    {
        SettingsBlendController.ClearFrameFlag();
        orig(self);

        if (self?.GetStorySession == null && self?.world == null) return;

        if (self.GamePaused)
        {
            return;
        }

        bool isArena = self.GetStorySession == null;
        if (isArena && BlendSettingsLoader.Active == null) return;
        if (!isArena && self.GetStorySession == null) return;

        if (!isArena && !_winHandledThisSession && !BlendClock.IsRunning && !BlendClock.EditMode && !_startFailed)
        {
            var s = BlendSettingsLoader.Active;
            
            if (s != null && s.Clock)
            {
                int initialState = ResolveInitial(s);

                float rainTimer = 0f;
                int rainLen = 1;
                if (self.world?.rainCycle != null)
                {
                    rainTimer = self.world.rainCycle.timer;
                    rainLen = self.world.rainCycle.cycleLength;
                }

                RSPlugin.log.LogInfo($"[RC][Start] region={_lastRegion} active={BlendSettingsLoader.ActiveRegion} " +
                    $"initialState={initialState} mode={s.Mode} savedState.IsRunning={_savedState.IsRunning} " +
                    $"savedMode={_savedState.Mode} sMode={s.Mode} " +
                    $"modeMatch={_savedState.Mode == s.Mode}");

                BlendClock.Start(_lastRegion, initialState, rainTimer, rainLen);

                if (!BlendClock.IsRunning)
                {
                    _startFailed = true;
                    RSPlugin.log.LogInfo("[RC][Start] FAILED — IsRunning=false tras Start");
                }
                else
                {
                    if (_savedState.IsRunning && _savedState.Mode == s.Mode)
                    {
                        bool rainCycleEnded = self.world?.rainCycle != null
                            && self.world.rainCycle.timer >= self.world.rainCycle.cycleLength;
                        RSPlugin.log.LogInfo($"[RC][RestoreState] → intentando restore T={_savedState.T:F3} " +
                            $"phase={_savedState.CurrentPhase} stateA={_savedState.StateA} " +
                            $"stateB={_savedState.StateB} loopAct={_savedState.LoopActivated} " +
                            $"rainCycleEnded={rainCycleEnded}");
                        BlendClock.RestoreState(_savedState, rainCycleEnded);
                        RSPlugin.log.LogInfo($"[RC][RestoreState] RESULTADO: T={BlendClock.T:F3} " +
                            $"phase={BlendClock.CurrentPhase} stateA={BlendClock.StateA} " +
                            $"stateB={BlendClock.StateB} running={BlendClock.IsRunning}");
                    }
                    else
                    {
                        RSPlugin.log.LogInfo($"[RC][RestoreState] SKIP: " +
                            $"savedRunning={_savedState.IsRunning} " +
                            $"modeMatch={_savedState.Mode == s.Mode} " +
                            $"(saved={_savedState.Mode}, s={s.Mode})");
                    }
                    _savedState = default;
                }
            }
            else
            {
                RSPlugin.log.LogInfo($"[RC][Start] SKIP start-block: " +
                    $"s={(s == null ? "null" : "ok")} clock={(s != null ? s.Clock.ToString() : "n/a")} " +
                    $"savedState.IsRunning={_savedState.IsRunning} (savedState NO consumido)");
            }
        }

        // Arena: mismo gate s.Clock y auto-restart al salir de EditMode.
        // El estado y el blend per-level los prepara ArenaBlendController en su ctor hook.
        if (isArena && !BlendClock.IsRunning && !BlendClock.EditMode && !_startFailed)
        {
            var s = BlendSettingsLoader.Active;

            if (s != null && s.Clock)
            {
                string roomName = self.GetArenaGameSession?.arenaSitting?.GetCurrentLevel;
                int initialState = ResolveInitial(s);

                float rainTimer = 0f;
                int rainLen = 1;
                if (self.world?.rainCycle != null)
                {
                    rainTimer = self.world.rainCycle.timer;
                    rainLen = self.world.rainCycle.cycleLength;
                }

                BlendClock.Start(roomName, initialState, rainTimer, rainLen);

                if (!BlendClock.IsRunning)
                    _startFailed = true;
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

                if (!_loggedRainCycleThisSession)
                {
                    _loggedRainCycleThisSession = true;
                }
            }
            BlendClock.Tick(GameDelta(self), rainTimer, rainLen);
        }

        if (BlendClock.EditMode)
        {
            UpdateSlidersOnly(self);
            return;
        }

        SettingsBlendController.ProcessPendingSkyRefresh();

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

            // Gate rooms: cargar blend settings específicos de esta gate
            if (BlendSettingsLoader.IsGateRoom(room) && !BlendSettingsLoader.IsGateActive)
            {
                RSPlugin.log.LogInfo($"[RC][Gate] ENTER {room} — cargando gate settings " +
                    $"(prevActive={BlendSettingsLoader.ActiveRegion}) " +
                    $"clock: running={BlendClock.IsRunning} T={BlendClock.T:F3} " +
                    $"phase={BlendClock.CurrentPhase} stateA={BlendClock.StateA} stateB={BlendClock.StateB}");
                BlendSettingsLoader.LoadGateSettings(room);
                s = BlendSettingsLoader.Active;
                RSPlugin.log.LogInfo($"[RC][Gate] ENTER {room} → active={BlendSettingsLoader.ActiveRegion} " +
                    $"settingsNull={s == null} " +
                    $"mode={(s != null ? s.Mode.ToString() : "n/a")} " +
                    $"idleTime={(s != null ? s.IdleTime.ToString() : "n/a")} " +
                    $"duration={(s != null ? s.Duration.ToString() : "n/a")} " +
                    $"clock={(s != null ? s.Clock.ToString() : "n/a")} " +
                    $"| clock tras load: running={BlendClock.IsRunning} T={BlendClock.T:F3}");
            }
            // Salir de gate: restaurar settings de la región
            else if (!BlendSettingsLoader.IsGateRoom(room) && BlendSettingsLoader.IsGateActive)
            {
                RSPlugin.log.LogInfo($"[RC][Gate] EXIT {room} — restaurando región {_lastRegion} " +
                    $"(prevActive={BlendSettingsLoader.ActiveRegion}) " +
                    $"clock: running={BlendClock.IsRunning} T={BlendClock.T:F3} " +
                    $"phase={BlendClock.CurrentPhase} stateA={BlendClock.StateA} stateB={BlendClock.StateB}");
                BlendSettingsLoader.LoadRegion(_lastRegion);
                s = BlendSettingsLoader.Active;
                RSPlugin.log.LogInfo($"[RC][Gate] EXIT {room} → active={BlendSettingsLoader.ActiveRegion} " +
                    $"settingsNull={s == null} " +
                    $"mode={(s != null ? s.Mode.ToString() : "n/a")} " +
                    $"| clock tras load: running={BlendClock.IsRunning} T={BlendClock.T:F3}");
            }

            var state = RoomCameraExtensions.GetRoomBlendState(cam.room);
            if (!state.IsBlend) continue;

            bool hasFullStates = state.HasFullStates;

            if (BlendClock.IsRunning && BlendClock.CurrentPhase == BlendClock.Phase.Blending && hasFullStates)
            {
                string pA = GetSettingsFile(game, room, BlendClock.StateA);
                string pB = GetSettingsFile(game, room, BlendClock.StateB);

                if (pA != null && pB != null && BlendClock.StateA != BlendClock.StateB)
                {
                    bool needsAttach = !SettingsBlendController.IsActive ||
                        SettingsBlendController.CurrentPathA != pA ||
                        SettingsBlendController.CurrentPathB != pB;

                    if (needsAttach)
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
                    {
                        SettingsBlendController.AttachWithExternalT(cam.room, path, path, isAuto: true);
                    }
                    SettingsBlendController.SetExternalT(0f);
                    
                    SettingsBlendController.SyncSkySlots(cam.room, finalState, finalState);
                }
            }
            
            AfterIdleCheck:

            if (BlendClock.IsRunning && hasFullStates)
            {
                cam.UpdateBlendPalette();

                var blendTexData = cam.GetBlendData();
                if (blendTexData != null && blendTexData.isBlendActive &&
                    blendTexData.terrainBlendedTexture != null)
                {
                    Shader.SetGlobalTexture("_terrainPalette", blendTexData.terrainBlendedTexture);
                }
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

    private static string GetSettingsFile(RainWorldGame game, string room, int state)
    {
        // StateFileResolver delega a ArenaBlendController cuando el modo arena está activo.
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
            BlendClock.OnDeathRainTriggered();
            OnDeathRainHit();
        }
        else if (!self.deathRainHasHit)
        {
            _lastDeathRainHasHit = false;
        }
    }

    public static void ResetRainCycleLogFlag()
    {
        _loggedRainCycleThisSession = false;
    }

    private static void OnDeathRainHit()
    {
        var s = BlendSettingsLoader.Active;
        if (s == null || s.Mode != BlendMode.EndCycle || BlendClock.EditMode || BlendClock.IsRunning) return;

        BlendClock.Start(_lastRegion, ResolveInitial(s));
    }

    private static void OnShutDown(On.RainWorldGame.orig_ShutDownProcess orig, RainWorldGame self)
    {
        if (self != null && self.GetStorySession != null)
            BlendClock.Stop();
        orig(self);
        StateFileResolver.SetBlockLoad(false);
        _startFailed = false; // partida nueva -> reintentos frescos (historia y arena)
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