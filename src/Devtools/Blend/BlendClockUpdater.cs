using UnityEngine;

namespace FilesSetting;

public static class BlendClockUpdater
{
    private const float RW_DELTA = 1f / 40f;

    private static string _lastRegion          = null;
    private static string _lastIdleRoom        = null;
    private static bool   _lastDeathRainHasHit = false;

    public static void ClearLastIdleRoom() => _lastIdleRoom = null;
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

        if (self.GetStorySession == null || self.GamePaused) return;

        // ── SafetyNet ─────────────────────────────────────────────────────
        if (SettingsBlendController.IsActive)
        {
            var cam = self.cameras?[0];
            if (cam != null)
            {
                string camRoom   = cam.room?.abstractRoom?.name;
                string blendRoom = SettingsBlendController.ActiveRoom?.abstractRoom?.name;
                if (blendRoom != null && camRoom != blendRoom)
                {
                    Plugin.RSPlugin.log.LogWarning(
                        $"[SafetyNet] Room change missed: '{blendRoom}'→'{camRoom}'. Recovering.");
                    SettingsBlendController.DetachAndRestore();
                }
            }
        }

        // ── Region change ─────────────────────────────────────────────────
        string currentRegion = BlendSettingsLoader.ActiveRegion;
        if (currentRegion != _lastRegion) { OnRegionChanged(currentRegion); _lastRegion = currentRegion; }

        // ── Fallback start ────────────────────────────────────────────────
        if (!BlendClock.IsRunning && !BlendClock.EditMode)
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
                    Plugin.RSPlugin.log.LogInfo(
                        $"[BlendClockUpdater] Fallback start mode={s.Mode} A={BlendClock.StateA}");
                }
            }
        }

        // ── Attach to camera if blending ──────────────────────────────────
        if (BlendClock.IsRunning && !SettingsBlendController.IsActive &&
            !SettingsBlendController.DetachedThisFrame &&
            BlendClock.CurrentPhase == BlendClock.Phase.Blending)
            TryAttach(self);

        // ── Apply idle/done visual ────────────────────────────────────────
        bool idleOrDone = BlendClock.CurrentPhase == BlendClock.Phase.Idle ||
                          BlendClock.CurrentPhase == BlendClock.Phase.Done;
        if (BlendClock.IsRunning && !SettingsBlendController.IsActive &&
            !SettingsBlendController.DetachedThisFrame && idleOrDone)
            TryApplyIdle(self);

        if (!BlendClock.IsRunning) return;

        // Snapshot before tick
        float prevT       = BlendClock.CurrentT;
        int   prevSub     = BlendClock.SubPhaseIndex;
        var   prevPhase   = BlendClock.CurrentPhase;
        bool  prevFirst   = BlendClock.IsFirstHalf;

        float rainTimer = 0f; int rainLen = 1;
        if (self.world?.rainCycle != null)
        { rainTimer = self.world.rainCycle.timer; rainLen = self.world.rainCycle.cycleLength; }

        BlendClock.Tick(RW_DELTA, rainTimer, rainLen);

        bool tChanged     = !Mathf.Approximately(BlendClock.CurrentT, prevT);
        bool subChanged   = BlendClock.SubPhaseIndex != prevSub;
        bool phaseChanged = BlendClock.CurrentPhase != prevPhase;
        bool halfChanged  = BlendClock.IsFirstHalf != prevFirst;

        // Sub-phase change → re-attach with new A/B
        if (subChanged && BlendClock.CurrentPhase == BlendClock.Phase.Blending)
        {
            Plugin.RSPlugin.log.LogInfo(
                $"[Updater] SubPhase→{BlendClock.SubPhaseIndex} A={BlendClock.StateA} B={BlendClock.StateB}");
            _lastIdleRoom = null;
            SettingsBlendController.AdvanceOriginToB();
            SettingsBlendController.RotateSkySlots();
            SettingsBlendController.Detach();
        }

        // Left Blending → detach
        if (prevPhase == BlendClock.Phase.Blending && BlendClock.CurrentPhase != BlendClock.Phase.Blending)
        {
            Plugin.RSPlugin.log.LogInfo(
                $"[Updater] Blending→{BlendClock.CurrentPhase} A={BlendClock.StateA} B={BlendClock.StateB}");
            SettingsBlendController.AdvanceOriginToB();
            SettingsBlendController.RotateSkySlots();
            SettingsBlendController.Detach();
            _lastIdleRoom = null;
        }

        if (halfChanged || (prevPhase == BlendClock.Phase.Done && BlendClock.CurrentPhase == BlendClock.Phase.Idle))
            _lastIdleRoom = null;

        if ((tChanged || subChanged || phaseChanged || halfChanged) &&
            !SettingsBlendController.DetachedThisFrame)
            NotifyCameras(self);

        SettingsBlendController.OverrideLightColorsPostOrig();
    }

    private static void TryAttach(RainWorldGame game)
    {
        var s   = BlendSettingsLoader.Active;
        var cam = game.cameras?[0];
        if (s == null || cam?.room == null) return;
        string room = cam.room.abstractRoom?.name;
        if (room == null || !s.IncludesRoom(room)) return;
        string pA = ReadStateReadFiles.GetRainStateSettingsFile(room, BlendClock.StateA);
        string pB = ReadStateReadFiles.GetRainStateSettingsFile(room, BlendClock.StateB);
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

        // Bloquear durante el frame exacto de MoveCamera.
        if (SettingsBlendController.MoveCameraThisFrame) return;

        string room = cam.room.abstractRoom?.name;
        if (room == null || !s.IncludesRoom(room)) return;

        // Solo actualizar estado interno (luces, roomSettings, _activeSnapshot).
        // Las operaciones de cámara (ChangeMainPalette, ApplyFade, shader globals)
        // las maneja exclusivamente OnMoveCamera con allowCameraOps=true.
        // No seteamos _lastIdleRoom aquí — eso bloquearía el pending consumer
        // que sí aplica la paleta correctamente cuando la cámara llega a la sala.
        int state = BlendClock.CurrentPhase == BlendClock.Phase.Done
            ? BlendClock.StateB : BlendClock.StateA;
        string path = ReadStateReadFiles.GetRainStateSettingsFile(room, state);
        if (path != null && room != _lastIdleRoom)
        {
            // allowCameraOps=true cuando la cámara ya está asentada en la sala gestionada.
            // Necesario para actualizar currentPalette via ApplyFade durante el Idle
            // entre halvs — sin esto CalcBackgroundColors lee skyColor incorrecto.
            bool camSettled = !SettingsBlendController.MoveCameraThisFrame && cam.room?.abstractRoom?.name == room;
            SettingsBlendController.ApplyIdleState(cam.room, path, allowCameraOps: camSettled);
            // NO setear _lastIdleRoom aquí — el pending consumer necesita correr
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
        Plugin.RSPlugin.log.LogInfo("[BlendClockUpdater] Death rain → EndCycle trigger.");
        BlendClock.Start(ResolveInitial(s));
    }

    private static void OnShutDown(On.RainWorldGame.orig_ShutDownProcess orig, RainWorldGame self)
    {
        orig(self); BlendClock.Stop(); BlendSettingsLoader.ClearCache();
        BlendSkyAtlasCache.UnloadAll();
        _lastRegion = _lastIdleRoom = null; _lastDeathRainHasHit = false;
    }

    private static void OnWin(On.RainWorldGame.orig_Win orig, RainWorldGame self, bool mal, bool warp)
    {
        orig(self, mal, warp);
        if (warp) return;
        BlendClock.Stop(); _lastRegion = _lastIdleRoom = null;
    }

    private static void OnRegionChanged(string region)
    {
        // Descargar atlas de cielo de todas las regiones excepto la nueva
        BlendSkyAtlasCache.UnloadAllExcept(region);

        SettingsBlendController.ResetFull(); _lastIdleRoom = null;
        if (BlendClock.IsRunning) BlendClock.Stop();
        if (string.IsNullOrEmpty(region)) return;

        // Precargar todos los atlas de sky de la región activa ahora mismo.
        // Así cuando el jugador llegue a una sala con sky, los atlas ya están
        // en Futile y LoadGraphic retorna inmediatamente sin freeze.
        BlendSkyAtlasCache.PreloadRegion(region);

        var s = BlendSettingsLoader.Active;
        if (s == null || BlendClock.EditMode) return;
        if (s.Mode == BlendMode.Custom && !CustomModeState.IsActive(region, s.CustomTriggerId)) return;
        if (s.Mode == BlendMode.EndCycle) return;
        BlendClock.Start(ResolveInitial(s));
        Plugin.RSPlugin.log.LogInfo(
            $"[BlendClockUpdater] Clock started '{region}' mode={s.Mode} A={BlendClock.StateA}");
    }

    private static int ResolveInitial(BlendSettings s)
    {
        int cycle = 0;
        var game  = GetGame();
        if (game?.GetStorySession?.saveState != null) cycle = game.GetStorySession.saveState.cycleNumber;
        int n = 2;
        if (s._hasRoomsSection)
            foreach (string room in s.Rooms.Keys)
            { int c = ReadStateReadFiles.CountRainStateFiles(room); if (c > 0) { n = c; break; } }
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
            string pA = ReadStateReadFiles.GetRainStateSettingsFile(room, BlendClock.StateA);
            string pB = ReadStateReadFiles.GetRainStateSettingsFile(room, BlendClock.StateB);
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
            // Slider A locked; slider B shows the full 0→1 global progress
            sliderA?.SetDisplayT(0f);
            sliderB?.SetDisplayT(BlendClock.GlobalT);
        }
        else
        {
            // Slider B locked; slider A shows progress
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