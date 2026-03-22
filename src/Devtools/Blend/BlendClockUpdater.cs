using UnityEngine;

namespace FilesSetting;

// ════════════════════════════════════════════════════════════════════════
// BLEND CLOCK UPDATER
//
// Hook en RainWorldGame.Update para avanzar BlendClock cada frame.
// Separado de BlendClock para mantener la lógica de estado limpia.
//
// También es responsable de:
//   - Arrancar BlendClock cuando se entra a una región con blend_settings.txt
//   - Detenerlo al salir de sesión
//   - Notificar a las salas activas cuando CurrentT cambia
// ════════════════════════════════════════════════════════════════════════

public static class BlendClockUpdater
{
    // Tiempo de juego en segundos: cada tick del juego = 1/40 s (40 fps fijo de RW)
    private const float RW_DELTA = 1f / 40f;

    // Región de la última sesión (para detectar cambio de región)
    private static string _lastRegion = null;

    // Última sala a la que se aplicó ApplyIdleState — evita repetirlo cada frame
    private static string _lastIdleRoom = null;

    public static void ClearLastIdleRoom() => _lastIdleRoom = null;

    // ── Init ──────────────────────────────────────────────────────────────

    public static void Init()
    {
        On.RainWorldGame.Update          += OnGameUpdate;
        On.RainWorldGame.ShutDownProcess += OnShutDown;
        On.RainWorldGame.Win             += OnWin;
    }

    // ── Hooks ─────────────────────────────────────────────────────────────

    private static void OnGameUpdate(On.RainWorldGame.orig_Update orig, RainWorldGame self)
    {
        // Limpiar flag del frame anterior ANTES de orig, para que cualquier
        // DetachAndRestore disparado dentro de orig sea detectado correctamente.
        SettingsBlendController.ClearFrameFlag();

        orig(self);

        // Sobreescribir color de luces DESPUÉS de que LightSource.Update corrió.
        // LightSource.Update asigna light.color = PixelColorAtCoordinate() cuando
        // colorFromEnvironment=true — eso lee paletteTexture física que va en bloques
        // (Guardián 2, threshold 0.008T). Sobreescribirlo post-orig con el color
        // calculado desde fadeTexA/B interpolados en memoria cada tick elimina el flickering.
        SettingsBlendController.OverrideLightColorsPostOrig();

        // Solo durante sesión de historia
        if (self.GetStorySession == null) return;

        // Pausar el sistema cuando el juego está pausado.
        if (self.GamePaused) return;

        // ── SafetyNet [ÚLTIMO RECURSO] ────────────────────────────────────────
        // OnMoveCamera es el detector primario. Este bloque solo debería disparar
        // si existe una ruta de cambio de sala que evita RoomCamera.MoveCamera_Room_int
        // (ej: warps o shortcuts no estándar). Si aparece este log en producción,
        // significa que OnMoveCamera tiene una laguna que hay que investigar.
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
                        $"[SafetyNet-LastResort] OnMoveCamera missed a room change: " +
                        $"'{blendRoom}'→'{camRoom}'. Recovering via DetachAndRestore.");
                    SettingsBlendController.DetachAndRestore();
                }
            }
        }

        // ── Detectar cambio de región — SIEMPRE ANTES del fallback de arranque ──
        // OnRegionChanged arranca el clock si la nueva región tiene modo Loop.
        // Este bloque debe ir antes del fallback para que en el primer frame
        // OnRegionChanged tenga la oportunidad de arrancar antes que el fallback.
        string currentRegion = BlendSettingsLoader.ActiveRegion;
        if (currentRegion != _lastRegion)
        {
            OnRegionChanged(currentRegion);
            _lastRegion = currentRegion;
        }

        // ── Fallback de arranque: si el clock no está corriendo pero debería ──
        // Cubre casos donde OnRegionChanged no pudo arrancar (ej: BlendSettings
        // aún no estaba en caché durante la transición de región). En condiciones
        // normales OnRegionChanged ya habrá arrancado el clock y este bloque no entra.
        if (!BlendClock.IsRunning)
        {
            var settings = BlendSettingsLoader.Active;
            if (settings != null && settings.Mode == BlendMode.Loop)
            {
                int initialA = ResolveInitialStateFromCycle(settings);
                BlendClock.Start(initialA);
                Plugin.RSPlugin.log.LogInfo(
                    $"[BlendClockUpdater] Clock started via fallback (region={BlendSettingsLoader.ActiveRegion}) A={BlendClock.StateA}");
            }
        }

        // ── Attach inmediato si el clock ya está en Blending y la cámara está en [ROOMS] ──
        if (BlendClock.IsRunning && !SettingsBlendController.IsActive &&
            !SettingsBlendController.DetachedThisFrame &&
            BlendClock.CurrentPhase == BlendClock.Phase.Blending)
        {
            var settings = BlendSettingsLoader.Active;
            var cam      = self.cameras?[0];
            if (settings != null && cam?.room != null)
            {
                string roomName = cam.room.abstractRoom?.name;
                if (roomName != null && settings.IncludesRoom(roomName))
                {
                    string pathA = ReadStateReadFiles.GetRainStateSettingsFile(roomName, BlendClock.StateA);
                    string pathB = ReadStateReadFiles.GetRainStateSettingsFile(roomName, BlendClock.StateB);
                    if (pathA != null && pathB != null && BlendClock.StateA != BlendClock.StateB)
                    {
                        Plugin.RSPlugin.log.LogInfo(
                            $"[BlendClockUpdater] Attaching to room '{roomName}' " +
                            $"sub={BlendClock.SubPhaseIndex} T={BlendClock.SubPhaseLocalT:F2}");
                        SettingsBlendController.AttachWithExternalT(cam.room, pathA, pathB);
                        SettingsBlendController.SetExternalT(BlendClock.SubPhaseLocalT);
                    }
                }
            }
        }

        // ── Idle: aplicar estado visual correcto si la cámara entra a [ROOMS] durante espera ──
        // Sin esto, la sala muestra settings_N del disco (el del ciclo actual) en vez del
        // estado donde el carril anterior terminó. StateA durante Idle es siempre el primer
        // estado del carril que está por empezar = último estado del carril anterior.
        if (BlendClock.IsRunning && !SettingsBlendController.IsActive &&
            !SettingsBlendController.DetachedThisFrame &&
            BlendClock.CurrentPhase == BlendClock.Phase.Idle)
        {
            var settings = BlendSettingsLoader.Active;
            var cam      = self.cameras?[0];
            if (settings != null && cam?.room != null)
            {
                string roomName = cam.room.abstractRoom?.name;
                if (roomName != null && settings.IncludesRoom(roomName) &&
                    roomName != _lastIdleRoom)
                {
                    string pathIdle = ReadStateReadFiles.GetRainStateSettingsFile(roomName, BlendClock.StateA);
                    if (pathIdle != null)
                    {
                        SettingsBlendController.ApplyIdleState(cam.room, pathIdle);
                        _lastIdleRoom = roomName;
                    }
                }
            }
        }

        // Tickear el reloj si está corriendo
        if (!BlendClock.IsRunning) return;

        float prevT        = BlendClock.CurrentT;
        int   prevSubPhase = BlendClock.SubPhaseIndex;
        var   prevPhase    = BlendClock.CurrentPhase;
        BlendClock.Tick(RW_DELTA);

        bool tChanged        = !Mathf.Approximately(BlendClock.CurrentT, prevT);
        bool subPhaseChanged = BlendClock.SubPhaseIndex != prevSubPhase;
        bool phaseChanged    = BlendClock.CurrentPhase != prevPhase;

        // Cuando cambia la sub-fase durante Blending, el controlador necesita
        // re-attacharse con los nuevos StateA/B de la sub-fase actual.
        if (subPhaseChanged && BlendClock.CurrentPhase == BlendClock.Phase.Blending)
        {
            _lastIdleRoom = null; // el blend toma el control, el idle ya no aplica
            SettingsBlendController.AdvanceOriginToB();
            SettingsBlendController.Detach();
        }

        // Cuando el carril completo termina (Done), limpiar para el siguiente carril.
        if (prevPhase == BlendClock.Phase.Blending && BlendClock.CurrentPhase == BlendClock.Phase.Done)
        {
            SettingsBlendController.AdvanceOriginToB();
            SettingsBlendController.Detach();
        }

        // Al entrar en Idle desde Done, resetear para que el próximo attach de sala
        // vuelva a aplicar el estado correcto si la cámara ya estaba en [ROOMS].
        if (prevPhase == BlendClock.Phase.Done && BlendClock.CurrentPhase == BlendClock.Phase.Idle)
            _lastIdleRoom = null;

        // Notificar si T cambió o si hubo cambio de sub-fase/fase
        // (pero no si acabamos de hacer Detach en este mismo frame)
        if ((tChanged || subPhaseChanged || phaseChanged) &&
            !SettingsBlendController.DetachedThisFrame)
            NotifyCameras(self);

        // Sobreescribir color de luces DESPUÉS de NotifyCameras.
        // En este punto SetExternalT ya actualizó _forcedT con el T del tick actual,
        // y si hubo cambio de sub-fase AttachWithExternalT ya cargó PalTex_s1/s2 correctas.
        // Así el color que asignamos corresponde exactamente al frame que se va a renderizar.
        SettingsBlendController.OverrideLightColorsPostOrig();
    }

    private static void OnShutDown(On.RainWorldGame.orig_ShutDownProcess orig, RainWorldGame self)
    {
        orig(self);
        BlendClock.Stop();
        BlendSettingsLoader.ClearCache();
        _lastRegion   = null;
        _lastIdleRoom = null;
    }

    private static void OnWin(On.RainWorldGame.orig_Win orig, RainWorldGame self, bool malnourished, bool fromWarpPoint)
    {
        orig(self, malnourished, fromWarpPoint);

        if (fromWarpPoint) return;

        BlendClock.Stop();
        _lastRegion   = null;
        _lastIdleRoom = null;
    }

    // ── Lógica de región ──────────────────────────────────────────────────

    private static void OnRegionChanged(string newRegion)
    {
        // Al cambiar de región, limpiar el blend activo y el origen pendiente.
        // Sin ResetFull aquí, un _pendingOrigin de la región anterior puede
        // contaminar el primer Attach de la nueva región.
        SettingsBlendController.ResetFull();
        _lastIdleRoom = null;

        if (BlendClock.IsRunning)
            BlendClock.Stop();

        if (string.IsNullOrEmpty(newRegion)) return;

        var settings = BlendSettingsLoader.Active;
        if (settings == null || settings.Mode != BlendMode.Loop) return;

        // Arrancar el clock en cuanto se conoce la región, sin esperar a que
        // la cámara entre a una sala de [ROOMS]. Así el idle empieza a correr
        // desde que el juego carga — el slugcat levantándose del refugio ya
        // cuenta como tiempo transcurrido antes de llegar a la sala del blend.
        int initialA = ResolveInitialStateFromCycle(settings);
        BlendClock.Start(initialA);
        Plugin.RSPlugin.log.LogInfo(
            $"[BlendClockUpdater] Clock started on region load '{newRegion}' A={BlendClock.StateA}");
    }

    /// <summary>
    /// Determina el estado A inicial usando el número de ciclo del savestate,
    /// igual que lo hace ReadStateReadFiles para cargar el settings file.
    /// </summary>
    private static int ResolveInitialStateFromCycle(BlendSettings settings)
    {
        // Intentar obtener el ciclo actual desde la sesión activa
        int cycle = 0;
        var game = GetActiveGame();
        if (game?.GetStorySession?.saveState != null)
            cycle = game.GetStorySession.saveState.cycleNumber;

        // Necesitamos N estados. Usamos la primera sala de [ROOMS].
        int n = 2;
        if (settings._hasRoomsSection)
        {
            foreach (string room in settings.Rooms)
            {
                int count = ReadStateReadFiles.CountRainStateFiles(room);
                if (count > 0) { n = count; break; }
            }
        }

        if (n <= 0) return 1;
        return (cycle % n) + 1;
    }

    // ── Notificación a cámaras ────────────────────────────────────────────

    /// <summary>
    /// Cuando CurrentT cambia, activa el blend en las salas de [ROOMS]
    /// que tienen la cámara activa apuntándolas, y actualiza los sliders.
    /// </summary>
    private static void NotifyCameras(RainWorldGame game)
    {
        var settings = BlendSettingsLoader.Active;
        if (settings == null) return;

        // Si se hizo un DetachAndRestore este frame, no re-attachar
        if (SettingsBlendController.DetachedThisFrame) return;

        var cameras = game.cameras;
        if (cameras == null) return;

        foreach (var cam in cameras)
        {
            if (cam?.room == null) continue;
            string roomName = cam.room.abstractRoom?.name;
            if (roomName == null) continue;
            if (!settings.IncludesRoom(roomName)) continue;

            // Durante Blending: attachar si no está activo o si cambió la sub-fase
            if (BlendClock.CurrentPhase == BlendClock.Phase.Blending)
            {
                string pathA = ReadStateReadFiles.GetRainStateSettingsFile(roomName, BlendClock.StateA);
                string pathB = ReadStateReadFiles.GetRainStateSettingsFile(roomName, BlendClock.StateB);

                if (pathA != null && pathB != null && BlendClock.StateA != BlendClock.StateB)
                {
                    if (!SettingsBlendController.IsActive ||
                        SettingsBlendController.CurrentPathA != pathA ||
                        SettingsBlendController.CurrentPathB != pathB)
                    {
                        Plugin.RSPlugin.log.LogInfo(
                            $"[BlendClockUpdater] Attaching sub-phase {BlendClock.SubPhaseIndex}: " +
                            $"{BlendClock.StateA}→{BlendClock.StateB} T={BlendClock.SubPhaseLocalT:F2}");
                        SettingsBlendController.AttachWithExternalT(cam.room, pathA, pathB);
                    }
                    SettingsBlendController.SetExternalT(BlendClock.SubPhaseLocalT);
                }
            }
        }

        UpdateSliders(game);
    }

    private static void UpdateSliders(RainWorldGame game)
    {
        // pages es string[] (nombres), activePage es el Page activo
        var page = game.devUI?.activePage;
        if (page == null) return;

        BlendSlider sliderA = null;
        BlendSlider sliderB = null;

        foreach (var node in page.subNodes)
        {
            if (!(node is RCPanel panel)) continue;
            foreach (var sub in panel.subNodes)
            {
                if (!(sub is BlendSlider bs)) continue;
                if (bs.IDstring == "RC_BlendSlider")  sliderA = bs;
                if (bs.IDstring == "RC_BlendSliderB") sliderB = bs;
            }
            break;
        }

        if (sliderA == null && sliderB == null) return;

        if (BlendClock.IsLaneA)
        {
            sliderA?.SetDisplayT(BlendClock.CurrentT);
            sliderB?.SetDisplayT(0f);
        }
        else
        {
            sliderA?.SetDisplayT(0f);
            sliderB?.SetDisplayT(BlendClock.CurrentT);
        }
    }

    // ── Helper ────────────────────────────────────────────────────────────

    private static RainWorldGame GetActiveGame()
    {
        // RainWorld.processManager.currentMainLoop es la forma estándar en RW
        // de obtener el juego activo desde un contexto estático.
        var rw = UnityEngine.Object.FindObjectOfType<RainWorld>();
        return rw?.processManager?.currentMainLoop as RainWorldGame;
    }

}