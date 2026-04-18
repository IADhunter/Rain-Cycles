using UnityEngine;
using FilesSetting;
using RainCycles.Settings;
using RainCycles.Core;
using RainCycles.Sky;


namespace RainCycles.Clock;

public static class BlendClockUpdater
{
    // Delta lógico del juego: 1 / framesPerSecond.
    // framesPerSecond es calculado por RainWorldGame.RawUpdate antes de llamar Update,
    // e incorpora todos los factores de slow-motion: hongo (Adrenaline), VoidMelt,
    // redsIllness, ghostMode, MMF.cfgSlowTimeFactor, y DevTools (teclas A/S).
    // Así el blend se ralentiza proporcionalmente al slow-motion del juego.
    private static float _lastUnscaledTime = 0f;
    private static float GameDelta(RainWorldGame game)
    {
        float now  = UnityEngine.Time.unscaledTime;
        float dt   = Mathf.Clamp(now - _lastUnscaledTime, 0f, 0.2f);
        _lastUnscaledTime = now;

        // Escalar por slow-motion del juego: framesPerSecond/40 da la fracción de velocidad.
        // Con hongo fps=15 → factor=0.375 → timer avanza al 37.5% de la velocidad real.
        float slowFactor = Mathf.Clamp01(game.framesPerSecond / 40f);
        return dt * slowFactor;
    }

    private static string _lastRegion          = null;
    private static string _lastIdleRoom        = null;
    private static int    _lastIdleState       = -1;  // estado aplicado en último idle
    private static bool   _lastDeathRainHasHit = false;
    private static bool   _startFailed         = false;

    // Progreso heredado al cruzar región
    private static float     _savedTimerProgress = -1f;
    private static bool      _savedWasBlending   = false;
    private static BlendMode _savedMode          = BlendMode.Loop;

    public static void ClearLastIdleRoom() { _lastIdleRoom = null; _lastIdleState = -1; }
    public static void SetLastIdleRoom(string room) => _lastIdleRoom = room;

    public static void Init()
    {
        On.RainWorldGame.Update          += OnGameUpdate;
        On.RainWorldGame.ShutDownProcess += OnShutDown;
        On.RainWorldGame.Win             += OnWin;
        On.RainCycle.Update              += OnRainCycleUpdate;
    }

    private static void OnGameUpdate(On.RainWorldGame.orig_Update orig, RainWorldGame self)
    {
        SettingsBlendController.ClearFrameFlag();
        orig(self);
        SettingsBlendController.OverrideLightColorsPostOrig();

        if (self.GamePaused) return;

        // En arena no hay StorySession — solo continuar si hay blend settings cargado
        bool isArena = self.GetStorySession == null;
        if (isArena && BlendSettingsLoader.Active == null) return;
        if (!isArena && self.GetStorySession == null) return;

        // ────────────────────
        if (SettingsBlendController.IsActive)
        {
            var cam = self.cameras?[0];
            if (cam != null)
            {
                string camRoom   = cam.room?.abstractRoom?.name;
                string blendRoom = SettingsBlendController.ActiveRoom?.abstractRoom?.name;
                if (blendRoom != null && camRoom != blendRoom &&
                    !SettingsBlendController.MoveCameraThisFrame)
                {
                    RSPlugin.log.LogWarning(
                        $"[SafetyNet] Room change missed: '{blendRoom}'→'{camRoom}'. Recovering.");
                    SettingsBlendController.DetachAndRestore();
                }
            }
        }

        // ────────────────────
        // En arena el clock lo gestiona ArenaBlendController — no disparar OnRegionChanged
        if (!isArena)
        {
            string currentRegion = BlendSettingsLoader.ActiveRegion;
            if (currentRegion != _lastRegion)
            {
                if (!_winHandledThisSession) OnRegionChanged(currentRegion);
                _lastRegion = currentRegion;
            }
        }

        // ────────────────────
        // En arena el clock lo arranca ArenaBlendController.OnArenaInitiate
        if (!isArena && !_winHandledThisSession && !BlendClock.IsRunning && !BlendClock.EditMode && !_startFailed)
        {
            var s = BlendSettingsLoader.Active;
            if (s != null)
            {
                bool should = s.Mode == BlendMode.Loop || s.Mode == BlendMode.Cycle
                    || (s.Mode == BlendMode.Custom &&
                        CustomModeState.IsActive(BlendSettingsLoader.ActiveRegion, s.CustomTriggerId));
                if (should)
                {
                    BlendClock.Start(ResolveInitial(s));

                    // Si sigue sin correr → no hay secuencia válida, no reintentar
                    if (!BlendClock.IsRunning)
                        _startFailed = true;

                    // Heredar progreso de región anterior si existe y el modo coincide
                    if (_savedTimerProgress >= 0f &&
                        BlendSettingsLoader.Active?.Mode == _savedMode)
                    {
                        BlendClock.InjectProgress(_savedTimerProgress, _savedWasBlending);
                        _savedTimerProgress = -1f;
                    }
                    else
                    {
                        _savedTimerProgress = -1f;
                    }
                }
            }
        }

        // ────────────────────
        if (BlendClock.IsRunning && !SettingsBlendController.IsActive &&
            !SettingsBlendController.DetachedThisFrame &&
            BlendClock.CurrentPhase == BlendClock.Phase.Blending)
            TryAttach(self);

        // ────────────────────
        bool idleOrDone = BlendClock.CurrentPhase == BlendClock.Phase.Idle ||
                          BlendClock.CurrentPhase == BlendClock.Phase.Done;
        if (BlendClock.IsRunning && !SettingsBlendController.IsActive &&
            !SettingsBlendController.DetachedThisFrame && idleOrDone)
            TryApplyIdle(self);

        if (!BlendClock.IsRunning) return;

        // No tickear mientras la cámara no tiene sala visible → el idle time
        // no corre durante la pantalla de carga, solo desde que el jugador ve la sala.
        if (self.cameras?[0]?.room == null) return;

        // Snapshot before tick
        float prevT       = BlendClock.CurrentT;
        int   prevSub     = BlendClock.SubPhaseIndex;
        var   prevPhase   = BlendClock.CurrentPhase;
        bool  prevFirst   = BlendClock.IsFirstHalf;

        float rainTimer = 0f; int rainLen = 1;
        if (self.world?.rainCycle != null)
        { rainTimer = self.world.rainCycle.timer; rainLen = self.world.rainCycle.cycleLength; }

        BlendClock.Tick(GameDelta(self), rainTimer, rainLen);

        bool tChanged     = !Mathf.Approximately(BlendClock.CurrentT, prevT);
        bool subChanged   = BlendClock.SubPhaseIndex != prevSub;
        bool phaseChanged = BlendClock.CurrentPhase != prevPhase;
        bool halfChanged  = BlendClock.IsFirstHalf != prevFirst;

        if (subChanged && BlendClock.CurrentPhase == BlendClock.Phase.Blending)
        {
            RSPlugin.log.LogDebug(
                $"[Updater] SubPhase→{BlendClock.SubPhaseIndex} A={BlendClock.StateA} B={BlendClock.StateB}");
            _lastIdleRoom = null; _lastIdleState = -1;
            SettingsBlendController.AdvanceOriginToB();
            SettingsBlendController.RotateSkySlots();
            SettingsBlendController.Detach();
        }

        if (prevPhase == BlendClock.Phase.Blending && BlendClock.CurrentPhase != BlendClock.Phase.Blending)
        {
            RSPlugin.log.LogDebug(
                $"[Updater] Blending→{BlendClock.CurrentPhase} A={BlendClock.StateA} B={BlendClock.StateB}");

            // Pre-aplicar terrain palette del estado B antes de Detach
            // para evitar el parpadeo blanco del frame de gap entre blend e idle.
            var camPre = self.cameras?[0];
            if (camPre?.room != null && BlendSettingsLoader.Active != null)
            {
                string roomPre = camPre.room.abstractRoom?.name;
                if (roomPre != null && BlendSettingsLoader.Active.IncludesRoom(roomPre))
                {
                    string pathB = GetSettingsFile(self, roomPre, BlendClock.StateB);
                    if (pathB != null)
                    {
                        var snapB = RainCycles.Snapshot.SettingsSnapshot.FromFile(pathB);
                        RoomEffectsApplier.ApplyTerrainPalette(camPre, snapB);
                        if (camPre.room.roomSettings?.TerrainPalette != null)
                            camPre.ReloadTerrainPalette();
                    }
                }
            }

            SettingsBlendController.AdvanceOriginToB();
            SettingsBlendController.RotateSkySlots();
            SettingsBlendController.Detach();
            _lastIdleRoom = null; _lastIdleState = -1;
        }

        if (halfChanged || (prevPhase == BlendClock.Phase.Done && BlendClock.CurrentPhase == BlendClock.Phase.Idle))
        { _lastIdleRoom = null; _lastIdleState = -1; }

        if (prevPhase == BlendClock.Phase.Idle && BlendClock.CurrentPhase == BlendClock.Phase.Blending)
            SettingsBlendController.PrefetchBlendAtmosphereColor(self);

        if ((tChanged || subChanged || phaseChanged || halfChanged) &&
            !SettingsBlendController.DetachedThisFrame)
            NotifyCameras(self);

        SettingsBlendController.OverrideLightColorsPostOrig();
    }

    private static string GetSettingsFile(RainWorldGame game, string room, int state)
    {
        if (game?.IsArenaSession == true)
            return ArenaStateResolver.GetSettingsPath(room, state);
        return StateFileResolver.GetRainStateSettingsFile(room, state);
    }

    private static void TryAttach(RainWorldGame game)
    {
        var s   = BlendSettingsLoader.Active;
        var cam = game.cameras?[0];
        if (s == null || cam?.room == null) return;
        string room = cam.room.abstractRoom?.name;
        if (room == null || !s.IncludesRoom(room)) return;
        string pA = GetSettingsFile(game, room, BlendClock.StateA);
        string pB = GetSettingsFile(game, room, BlendClock.StateB);
        if (pA != null && pB != null && BlendClock.StateA != BlendClock.StateB)
        {
            SettingsBlendController.AttachWithExternalT(cam.room, pA, pB);
            SettingsBlendController.SetExternalT(BlendClock.SubPhaseLocalT);
        }
    }

    private static void TryApplyIdle(RainWorldGame game)
    {
        var s   = BlendSettingsLoader.Active;
        var cam = game.cameras?[0];
        if (s == null || cam?.room == null) return;
        if (SettingsBlendController.MoveCameraThisFrame) return;

        string room = cam.room.abstractRoom?.name;
        if (room == null || !s.IncludesRoom(room)) return;

        int state = BlendClock.CurrentPhase == BlendClock.Phase.Done
            ? BlendClock.StateB : BlendClock.StateA;
        string path = GetSettingsFile(game, room, state);

        // Solo aplicar si cambió sala o estado
        if (path != null && (room != _lastIdleRoom || state != _lastIdleState))
        {
            RSPlugin.log.LogDebug($"[TryApplyIdle] room={room} state={state} lastRoom={_lastIdleRoom ?? "null"} lastState={_lastIdleState} path={System.IO.Path.GetFileName(path)}");

            // Si el roomSettings ya apunta al path correcto (ej: StateFileResolver ya lo cargó
            // al construir el RoomSettings), solo actualizar snapshot visual sin recargar.
            bool alreadyLoaded = string.Equals(
                cam.room.roomSettings?.filePath, path,
                System.StringComparison.OrdinalIgnoreCase);

            bool camSettled = !SettingsBlendController.MoveCameraThisFrame &&
                              cam.room?.abstractRoom?.name == room;

            // En ambos casos usar ApplyIdleState para cubrir todos los efectos:
            // Grime global, sky state, atmosphere globals, scalar effects, terrain, palette.
            // allowCameraOps=false en visitas no-firstVisit o cuando la cámara no está asentada
            // para evitar recargar paletas innecesariamente en frames intermedios.
            bool firstVisit = _lastIdleState == -1;
            SettingsBlendController.ApplyIdleState(
                cam.room, path,
                allowCameraOps: (firstVisit || !alreadyLoaded) && camSettled);

            _lastIdleRoom  = room;
            _lastIdleState = state;
        }
    }

    private static void OnRainCycleUpdate(On.RainCycle.orig_Update orig, RainCycle self)
    {
        bool was = self.deathRainHasHit;
        orig(self);
        if (!was && self.deathRainHasHit && !_lastDeathRainHasHit)
        { _lastDeathRainHasHit = true; OnDeathRainHit(); }
        else if (!self.deathRainHasHit) _lastDeathRainHasHit = false;
    }

    private static void OnDeathRainHit()
    {
        var s = BlendSettingsLoader.Active;
        if (s == null || s.Mode != BlendMode.EndCycle || BlendClock.EditMode || BlendClock.IsRunning) return;
        RSPlugin.log.LogInfo("[BlendClockUpdater] Death rain → EndCycle trigger.");
        BlendClock.Start(ResolveInitial(s));
    }

    private static void OnShutDown(On.RainWorldGame.orig_ShutDownProcess orig, RainWorldGame self)
    {
        // ResetFull antes de orig() → restaura paletas antes de que el juego destruya la cámara
        SettingsBlendController.ResetFull();
        BlendClock.Stop();
        orig(self);
        BlendSettingsLoader.ClearCache();
        BlendSkyAtlasCache.UnloadAll();
        _lastRegion = _lastIdleRoom = null; _lastIdleState = -1;
        _lastDeathRainHasHit = false;
        _winHandledThisSession = false;
        _savedTimerProgress = -1f;
        StateFileResolver.SetBlockLoad(false);
    }

    private static bool _winHandledThisSession = false;

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
        {
            // Solo limpiar estado lógico sin restaurar texturas → evita parpadeo vanilla
            // en los frames finales antes de la pantalla de resultados.
            // ShutDownProcess hará la limpieza completa cuando la cámara ya no sea visible.
            SettingsBlendController.ResetFullSoft();
            _lastRegion = _lastIdleRoom = null; _lastIdleState = -1;
        }
    }

    private static void OnRegionChanged(string region)
    {
        BlendSkyAtlasCache.UnloadAllExcept(region);

        // Capturar progreso antes de detener el clock
        if (BlendClock.IsRunning)
        {
            _savedMode          = BlendSettingsLoader.Active?.Mode ?? BlendMode.Loop;
            _savedWasBlending   = BlendClock.CurrentPhase == BlendClock.Phase.Blending;
            _savedTimerProgress = _savedWasBlending
                ? BlendClock.CurrentT
                : BlendClock.IdleProgress;
        }
        else
        {
            _savedTimerProgress = -1f;
        }

        SettingsBlendController.ResetFull(); _lastIdleRoom = null; _lastIdleState = -1; _startFailed = false;
        if (BlendClock.IsRunning) BlendClock.Stop();
        if (string.IsNullOrEmpty(region)) return;

        BlendSkyAtlasCache.PreloadRegion(region);

        // Pre-aplicar idle state inmediatamente si la cámara ya está en una sala gestionada.
        // Evita el frame de gap donde la cámara muestra un settings incorrecto
        // antes de que OnGameUpdate arranque el clock y llame TryApplyIdle.
        var game2 = GetGame();
        var cam2  = game2?.cameras?[0];
        if (cam2?.room != null)
        {
            var s2 = BlendSettingsLoader.Active;
            if (s2 != null)
            {
                string room2 = cam2.room.abstractRoom?.name;
                if (room2 != null && s2.IncludesRoom(room2))
                {
                    bool isArena2 = game2?.IsArenaSession == true;
                    int cycle2    = game2?.GetStorySession?.saveState?.cycleNumber ?? 0;
                    string path2  = isArena2
                        ? ArenaStateResolver.GetSelectedSettingsPath(room2, 0)
                        : StateFileResolver.GetRainStateFilePath(room2, cycle2);
                    if (path2 != null)
                        SettingsBlendController.ApplyIdleState(cam2.room, path2, allowCameraOps: true);
                }
            }
        }
        // No arrancamos el clock aquí — puede ocurrir durante pantalla de carga,
        // antes de que el jugador tenga control. El bloque fallback de OnGameUpdate
        // lo arrancará cuando el jugador esté realizado y la sala sea visible.
    }

    private static int ResolveInitial(BlendSettings s)
    {
        int cycle = 0;
        var game  = GetGame();
        if (game?.GetStorySession?.saveState != null) cycle = game.GetStorySession.saveState.cycleNumber;

        int n = 2;
        bool isArena = game?.IsArenaSession == true;
        if (s._hasRoomsSection)
            foreach (string room in s.Rooms.Keys)
            {
                int c = isArena
                    ? ArenaStateResolver.CountSettingsFiles(room)
                    : StateFileResolver.CountRainStateFiles(room);
                if (c > 0) { n = c; break; }
            }
        return n > 0 ? (cycle % n) + 1 : 1;
    }

    private static void NotifyCameras(RainWorldGame game)
    {
        var s = BlendSettingsLoader.Active;
        if (s == null || SettingsBlendController.DetachedThisFrame) return;
        foreach (var cam in game.cameras ?? System.Array.Empty<RoomCamera>())
        {
            if (cam?.room == null) continue;
            string room = cam.room.abstractRoom?.name;
            if (room == null || !s.IncludesRoom(room)) continue;
            if (BlendClock.CurrentPhase != BlendClock.Phase.Blending) continue;
            string pA = GetSettingsFile(game, room, BlendClock.StateA);
            string pB = GetSettingsFile(game, room, BlendClock.StateB);
            if (pA != null && pB != null && BlendClock.StateA != BlendClock.StateB)
            {
                if (!SettingsBlendController.IsActive ||
                    SettingsBlendController.CurrentPathA != pA ||
                    SettingsBlendController.CurrentPathB != pB)
                    SettingsBlendController.AttachWithExternalT(cam.room, pA, pB);
                SettingsBlendController.SetExternalT(BlendClock.SubPhaseLocalT);
            }
        }
        UpdateSliders(game);
    }

    private static void UpdateSliders(RainWorldGame game)
    {
        var page = game.devUI?.activePage;
        if (page == null) return;

        BlendSlider sliderA = null, sliderB = null;
        foreach (var node in page.subNodes)
        {
            if (!(node is RCPanel panel)) continue;
            foreach (var sub in panel.subNodes)
            {
                if (sub is BlendSlider bs)
                {
                    if (bs.IDstring == "RC_BlendSlider")  sliderA = bs;
                    if (bs.IDstring == "RC_BlendSliderB") sliderB = bs;
                }
            }
            break;
        }

        var s = BlendSettingsLoader.Active;
        bool isLoop = s == null || s.Mode == BlendMode.Loop || s.Mode == BlendMode.Custom;

        if (isLoop)
        {
            sliderA?.SetDisplayT(0f);
            sliderB?.SetDisplayT(BlendClock.GlobalT);
        }
        else
        {
            sliderA?.SetDisplayT(BlendClock.CurrentT);
            sliderB?.SetDisplayT(0f);
        }
    }

    private static RainWorldGame GetGame()
    {
        var rw = UnityEngine.Object.FindObjectOfType<RainWorld>();
        return rw?.processManager?.currentMainLoop as RainWorldGame;
    }
}