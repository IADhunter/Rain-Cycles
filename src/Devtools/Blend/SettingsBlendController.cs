using UnityEngine;

namespace FilesSetting;

// ════════════════════════════════════════════════════════════════════════
// Motor central del blend. Gestiona el estado activo y coordina los
// subsistemas:
//   BlendTextureManager  → mezcla de texturas de paleta
//   RoomEffectsApplier   → aplicación de efectos sobre PlacedObjects
//   SettingsSnapshot     → interpolación de valores
// ════════════════════════════════════════════════════════════════════════
public static class SettingsBlendController
{
    private static SettingsSnapshot _snapA;
    private static SettingsSnapshot _snapB;
    private static SettingsSnapshot _snapOriginal;
    private static Room             _room;
    private static float            _lastT        = -1f;
    private static float            _lastPaletteT = -1f;  // throttle para MixPalettes+Apply
    private static float            _lastLightT   = -1f;  // T de LightSources/Beams del tick anterior
    private static bool             _active       = false;
    private static bool             _externalT    = false;
    private static float            _forcedT      = 0f;
    private static string           _pathA        = null;
    private static string           _pathB        = null;

    // Bloquea re-attach en el mismo frame en que se hizo Detach.
    private static bool _detachedThisFrame    = false;

    // Bloquea TryApplyIdle en el frame exacto de MoveCamera.
    // Durante ese frame cam.room puede seguir apuntando a la sala anterior,
    // haciendo camIsHere=True cuando ya está en tránsito hacia otra sala.
    private static bool _moveCameraThisFrame  = false;
    // Flag que dura un frame extra tras salir de sala gestionada.
    // Permite que OnUpdateDayNightPalette preserve los globals correctos
    // en el primer frame donde cam.room ya apunta a la sala no gestionada.
    private static bool _exitedManagedRoomLastFrame = false;

    // Archivos settings a los que ya se inyectó RC_TINT en esta sesión.
    // Evita releer y reescribir el archivo cada frame.
    private static readonly System.Collections.Generic.HashSet<string> _rcTintInjected =
        new System.Collections.Generic.HashSet<string>();
    // Se setea en OnMoveCamera cuando la cámara aún no actualizó cam.room.
    private static Room   _pendingIdleRoom = null;
    private static string _pendingIdlePath = null;

    // Referencia a la instancia activa de AboveCloudsView para leer atmosphereColor.
    // Se actualiza en OnAboveCloudsViewCtor y se limpia en OnMoveCamera al salir.
    private static AboveCloudsView _aboveCloudsView = null;

    // ── Estado de cielo custom (RC_BKG) ──────────────────────────────────
    // Escena activa con slots de cielo. Se setea en los ctors de RTV/ACV.
    // Null cuando la sala activa no tiene sky declarado.
    private static RoofTopView     _rtvScene = null;
    private static AboveCloudsView _acvScene = null;

    // Índices de estado asignados a cada slot en la escena activa.
    // -1 = slot no asignado.
    private static int _skySlotDay   = -1;  // daySky   → estado A actual
    private static int _skySlotDusk  = -1;  // duskSky  → estado B (destino)
    private static int _skySlotNight = -1;  // nightSky → próximo estado C

    // Último estado aplicado en ApplyIdleState — evita llamar ApplySkyForState cada frame
    private static int _lastIdleSkyState = -1;

    // Últimos globals correctos aplicados por el mod a una sala gestionada.
    // Se actualizan cada vez que OverrideBackgroundGlobalsIfActive o el Caso 3
    // de OnUpdateDayNightPalette setean los globals. Se restauran durante MoveCamera.
    private static UnityEngine.Color _lastGoodMultiply    = UnityEngine.Color.white;
    private static UnityEngine.Color _lastGoodAtmosphere  = new UnityEngine.Color(0.16078432f, 0.23137255f, 0.31764707f);
    private static bool              _hasLastGoodGlobals  = false;

    // Último atmosphereColor aplicado por el mod — usado cuando _activeSnapshot es null
    // (entre sub-fases, al salir de sala) para evitar caer al vanilla azul.
    private static UnityEngine.Color _lastAtmosphereColor = new UnityEngine.Color(0.16078432f, 0.23137255f, 0.31764707f);

    // Snapshot activo actual — lerpeado durante blend, o del idle cuando el clock espera.
    // CalcBackgroundColors lo lee para obtener RC_TINT si está declarado.
    private static SettingsSnapshot _activeSnapshot = null;

    public static bool              IsActive              => _active;
    public static bool              DetachedThisFrame     => _detachedThisFrame;
    public static bool              MoveCameraThisFrame   => _moveCameraThisFrame;
    public static string            CurrentPathA          => _pathA;
    public static string            CurrentPathB          => _pathB;
    public static Room              ActiveRoom            => _room;
    public static float             ForcedT               => _forcedT;
    public static SettingsSnapshot  ActiveSnapshot        => _activeSnapshot;
    public static void SetActiveSnapshot(SettingsSnapshot snap) => _activeSnapshot = snap;

    /// <summary>Llamar al inicio de cada Update para limpiar el flag de frame.</summary>
    public static void ClearFrameFlag()
    {
        _detachedThisFrame   = false;
        _exitedManagedRoomLastFrame = _moveCameraThisFrame && _hasLastGoodGlobals;
        _moveCameraThisFrame = false;
    }

    // ── Ciclo de vida ─────────────────────────────────────────────────────

    public static void Init()
    {
        On.RoomCamera.UpdateDayNightPalette    += OnUpdateDayNightPalette;
        On.RoomCamera.ChangeBothPalettes       += OnChangeBothPalettes;
        On.RoomCamera.ApplyFade                += OnApplyFade;
        On.DevInterface.DevUI.Update           += OnDevUIUpdate;
        On.RoomCamera.MoveCamera_Room_int      += OnMoveCamera;
        On.RoofTopView.ctor                    += OnRoofTopViewCtor;
        On.RoofTopView.Update                  += OnRoofTopViewUpdate;
        On.AboveCloudsView.Update              += OnAboveCloudsViewUpdate;
        On.AboveCloudsView.ctor                += OnAboveCloudsViewCtor;

        On.PlacedObject.DayNightData.Apply     += OnDayNightDataApply;
        On.RoomCamera.DrawUpdate               += OnDrawUpdate;
        On.RoomSettings.Save                   += OnRoomSettingsSave;
        On.RoomCamera.Update                   += OnRoomCameraUpdate;
        On.RoomCamera.ChangeRoom               += OnChangeRoom;
    }

    public static void Attach(Room room, string pathA, string pathB)
    {
        _room         = room;
        _pathA        = pathA;
        _pathB        = pathB;
        _snapOriginal = SettingsSnapshot.FromFile(room.roomSettings.filePath ?? "");
        _snapA        = SettingsSnapshot.FromFileWithTemplate(pathA, room.abstractRoom.name);
        _snapB        = SettingsSnapshot.FromFileWithTemplate(pathB, room.abstractRoom.name);
        _active       = true;
        _lastT        = -1f;
        _lastPaletteT = -1f;

        var cam = room.game?.cameras?[0];
        if (cam != null)
        {
            BlendTextureManager.Load(cam, _snapA, _snapB, _snapOriginal, applyFade: false);
            // Forzar currentPalette al estado de snapA (t=0) antes de ApplyBlend.
            // Load restaura la paleta original al final del horneado, dejando
            // currentPalette con colores del settings del disco — no de snapA.
            // Sin esto, OverrideBackgroundGlobalsIfActive leería el color incorrecto
            // y los objetos de fondo mostrarían un flash blancuzco al iniciar el blend.
            BlendTextureManager.MixPalettes(cam, 0f);
            cam.ApplyFade();
        }
        RoomEffectsApplier.BuildLightIndex(room);
        ApplyBlend(0f);
    }

    public static void Detach()
    {
        // Forzar paletteB = -1 en la cámara ANTES de destruir las texturas.
        // MixPalettes sobreescribe los píxeles de fadeTexB sin actualizar cam.paletteB,
        // por lo que el ID de paletteB puede no coincidir con los píxeles reales.
        // ChangeBothPalettes solo recarga fadeTexB si palB != cam.paletteB (early return).
        // Si paletteB coincide accidentalmente con la fade palette de la nueva sala,
        // LoadPalette no corre y fadeTexB conserva los píxeles contaminados del blend.
        // Resetear a -1 garantiza que el próximo ChangeBothPalettes siempre recargue.
        if (_room != null)
        {
            var cam = _room.game?.cameras?[0];
            if (cam != null) cam.paletteB = -1;
        }
        _active            = false;
        _externalT         = false;
        _detachedThisFrame = true;
        _room              = null;
        _pathA             = null;
        _pathB             = null;
        _snapOriginal      = null;
        _lastLightT        = -1f;
        _pendingIdleRoom   = null;
        _pendingIdlePath   = null;
        _aboveCloudsView   = null;
        // _activeSnapshot se preserva intencionalmente — CalcBackgroundColors lo usa
        // durante el Idle entre halvs para mantener los colores correctos.
        // Se limpia en MoveCamera al salir de sala gestionada, o en ApplyIdleState.
        // _rtvScene / _acvScene / _skySlot* NO se limpian aquí:
        // la escena de cielo sobrevive entre blends mientras la sala esté cargada.
        // Se limpian en ResetFull() cuando el jugador cambia de sala/región.
        RoomEffectsApplier.ClearLightIndex();
        BlendTextureManager.Destroy();
    }

    /// <summary>
    /// Detach completo: restaura la sala al estado original capturado en Attach,
    /// antes de destruir las texturas de blend.
    /// </summary>
    public static void DetachAndRestore()
    {
        if (_room != null)
        {
            var cam = _room.game?.cameras?[0];
            if (cam != null)
            {
                var orig = _snapOriginal;
                var rs   = _room.roomSettings;
                if (orig != null && rs != null)
                {
                    rs.Grime                 = orig.Grime;
                    rs.Clouds                = orig.Clouds;
                    rs.CeilingDrips          = orig.CeilingDrips;
                    rs.BkgDroneVolume        = orig.BkgDroneVolume;
                    rs.RandomItemDensity     = orig.RandomItemDensity;
                    rs.RandomItemSpearChance = orig.RandomItemSpearChance;
                    rs.WaterReflectionAlpha  = orig.WaterReflectionAlpha;

                    Shader.SetGlobalFloat(RainWorld.ShadPropGrime, orig.Grime);
                    RoomEffectsApplier.ApplyScalarEffects(_room, orig);
                }

                BlendTextureManager.RestoreOriginalTextures(cam);
                cam.paletteBlend = 0f;
                // Los globals ShadPropMultiplyColor y ShadPropAboveCloudsAtmosphereColor
                // se resetean en OnMoveCamera DESPUÉS del orig, para que el último frame
                // de render de la sala gestionada use los valores correctos del mod.
            }
        }
        Detach();
    }

    /// <summary>
    /// Limpieza completa del estado del blend. Usar cuando el modo cambia o el
    /// usuario reinicia el sistema desde DevTools.
    /// Equivale a DetachAndRestore() + limpiar _pendingOrigin, en una sola
    /// operación atómica para no dejar la sala contaminada ni el origen sucio.
    /// </summary>
    public static void ResetFull()
    {
        _pendingOrigin = null;
        if (_active)
            DetachAndRestore();
        _rtvScene = null; _acvScene = null;
        _skySlotDay = _skySlotDusk = _skySlotNight = -1;
        _lastIdleSkyState = -1;
        _lastAtmosphereColor = new UnityEngine.Color(0.16078432f, 0.23137255f, 0.31764707f);
        _hasLastGoodGlobals = false;
    }

    /// <summary>
    /// Aplica un estado visual directamente (sin blend) cuando el clock está en Idle.
    /// Evita que la sala muestre el settings_N del disco mientras espera el próximo
    /// carril — en cambio muestra el estado correcto donde el carril anterior terminó.
    /// No activa el sistema de blend ni toca _snapOriginal/_pendingOrigin.
    /// </summary>
    public static void ApplyIdleState(Room room, string path, bool allowCameraOps = false)
    {
        if (room == null || path == null) return;
        var cam = room.game?.cameras?[0];
        if (cam == null) return;

        bool camIsHere = cam.room == room;

        var snap = SettingsSnapshot.FromFileWithTemplate(path, room.abstractRoom.name);
        if (snap == null) return;
        _activeSnapshot = snap;

        // Siempre: estado de sala (no depende de dónde está la cámara)
        var rs = room.roomSettings;
        if (rs != null)
        {
            rs.EffectColorA = snap.EffectColorA;
            rs.EffectColorB = snap.EffectColorB;
        }
        RoomEffectsApplier.ApplyShaderGlobals(snap);
        RoomEffectsApplier.ApplyScalarEffects(room, snap);
        RoomEffectsApplier.BuildLightIndex(room);
        RoomEffectsApplier.ApplyLightSources(room, snap);
        RoomEffectsApplier.ApplyLightBeams(room, snap);

        // Sincronizar cielo al estado idle — solo cuando el estado cambia.
        int idleState = ReadStateReadFiles.GetStateFromPath(path, room.abstractRoom?.name);
        if (idleState > 0 && idleState != _lastIdleSkyState)
        {
            _lastIdleSkyState = idleState;
            ApplySkyForState(idleState, room);
        }

        // Operaciones de cámara: SOLO cuando el caller garantiza que
        // la cámara ya está en esta sala y no va a moverse este frame.
        // Desde el updater en background: allowCameraOps=false (default).
        // Desde OnMoveCamera tras verificar self.room==newRoom: allowCameraOps=true.
        if (!allowCameraOps || !camIsHere) return;

        cam.ChangeMainPalette(snap.Palette);
        cam.ApplyEffectColorsToAllPaletteTextures(snap.EffectColorA, snap.EffectColorB);

        if (snap._hasFadePalette && snap.FadePaletteOpacities.Length > 0)
        {
            int camIdx = cam.currentCameraPosition;
            float opac = camIdx < snap.FadePaletteOpacities.Length
                         ? snap.FadePaletteOpacities[camIdx] : 0f;
            cam.ChangeFadePalette(snap.FadePaletteID, opac);
            cam.ApplyEffectColorsToAllPaletteTextures(snap.EffectColorA, snap.EffectColorB);
        }
        cam.ApplyFade();

        // Solo escribir los shader globals de fondo si la cámara sigue en una
        // sala gestionada con sky declarado (RTV o ACV).
        // ShadPropMultiplyColor afecta todos los sprites con shader de fondo —
        // en salas normales sin sky debe quedar en (1,1,1,1) para no teñir objetos.
        if (cam.room != null && BlendSettingsLoader.Active != null)
        {
            string camRoomName = cam.room.abstractRoom?.name;
            if (camRoomName != null && BlendSettingsLoader.Active.IncludesRoom(camRoomName))
            {
                var skyType = BlendSettingsLoader.Active.GetSkyType(camRoomName);
                if (skyType != SkyType.None)
                {
                    Color multiply, atmosphere;
                    RoomEffectsApplier.CalcBackgroundColors(cam, out multiply, out atmosphere);
                    Shader.SetGlobalVector(RainWorld.ShadPropMultiplyColor, multiply);
                    Shader.SetGlobalVector(RainWorld.ShadPropAboveCloudsAtmosphereColor, atmosphere);
                }
            }
        }
    }

    // ── Fix: color suave de LightSources con colorFromEnvironment ────────────
    // LightSource.Update asigna light.color = PixelColorAtCoordinate() que lee
    // paletteTexture física. Esa textura solo se actualiza cuando el Guardián 2
    // dispara (~0.008T), causando saltos bruscos de color — el flickering.
    // OverrideLightColorsPostOrig sobreescribe light.color después de que
    // LightSource.Update corrió, usando interpolación suave desde fadeTexA/B
    // en memoria (actualizada cada tick), eliminando el problema completamente.
    public static void OverrideLightColorsPostOrig()
    {
        if (!_active || !_externalT || _room == null) return;
        if (!BlendTextureManager.Ready) return;

        var cam = _room.game?.cameras?[0];
        if (cam == null) return;

        // IMPORTANTE: NO comprobar cam.room == _room aquí.
        // Los light sources de la sala blend (_room) deben mantenerse actualizados
        // aunque la cámara esté en otra sala (e.g. PS1 mientras VR1 blendea en segundo plano).
        // Si se omite este override cuando cam.room != _room, las luces de VR1 quedan
        // congeladas en el último color calculado, y al entrar a VR1 hay un frame
        // de flash de color incorrecto porque el color no coincide con el T actual.
        if (_snapA == null || _snapB == null) return;

        var lerped = SettingsSnapshot.Lerp(_snapA, _snapB, _forcedT);
        RoomEffectsApplier.ApplyLightSources(_room, lerped);
    }

    // ── Modo BlendClock (T externa) ───────────────────────────────────────

    /// <summary>
    /// Attach controlado por BlendClock. El T no viene del slider sino de
    /// <see cref="SetExternalT"/>. Usado por BlendClockUpdater.
    /// </summary>
    public static void AttachWithExternalT(Room room, string pathA, string pathB)
    {
        Plugin.RSPlugin.log.LogInfo(
            $"[Attach] room={room?.abstractRoom?.name} A={System.IO.Path.GetFileName(pathA)} B={System.IO.Path.GetFileName(pathB)} " +
            $"T={BlendClock.SubPhaseLocalT:F3} StateA={BlendClock.StateA} StateB={BlendClock.StateB}");
        _room      = room;
        _pathA     = pathA;
        _pathB     = pathB;
        ConsumePendingOrigin(room, pathA);
        _snapA        = SettingsSnapshot.FromFileWithTemplate(pathA, room.abstractRoom.name);
        _snapB        = SettingsSnapshot.FromFileWithTemplate(pathB, room.abstractRoom.name);
        _active       = true;
        _externalT    = true;
        _lastT        = -1f;
        _lastPaletteT = -1f;
        _lastLightT   = -1f;


        var cam = room.game?.cameras?[0];
        if (cam != null)
        {
            BlendTextureManager.Load(cam, _snapA, _snapB, _snapOriginal, applyFade: false);
            RoomEffectsApplier.BuildLightIndex(room);

            // Deducir stateA/stateB desde los paths para no depender de BlendClock.
            // Funciona tanto en modo automático como en modo DevTools (slider manual).
            string roomName = room.abstractRoom?.name;
            int stateA = ReadStateReadFiles.GetStateFromPath(pathA, roomName);
            int stateB = ReadStateReadFiles.GetStateFromPath(pathB, roomName);
            if (stateA > 0 && stateB > 0)
                SyncSkySlots(room, stateA, stateB);

            // Aplicar el blend inmediatamente al T actual del clock para que
            // al entrar a la sala el jugador vea el estado correcto sin esperar
            // al próximo SetExternalT.
            if (_externalT && BlendClock.IsRunning &&
                BlendClock.CurrentPhase == BlendClock.Phase.Blending)
            {
                float immediateT = BlendClock.SubPhaseLocalT;
                _lastT = -1f;
                _lastPaletteT = -1f;
                ApplyBlend(immediateT);
            }
        }
    }

    /// <summary>
    /// Actualiza _snapOriginal al snapshot B actual.
    /// Llamar cuando una fase de blend completa (T=1) para que el próximo
    /// Attach use el estado correcto como base, no el estado inicial del ciclo.
    /// </summary>
    // Snapshot preservado entre fases del Loop — survives Detach()
    private static SettingsSnapshot _pendingOrigin = null;

    public static void AdvanceOriginToB()
    {
        if (_snapB != null)
        {
            _pendingOrigin = _snapB;
        }
    }

    /// <summary>
    /// Descarta el origen pendiente sin consumirlo.
    /// Llamar desde RCPanel antes de cada ActivatePhase manual para que
    /// ConsumePendingOrigin use el pathA de la fase actual como origen,
    /// no un snapB residual de una fase forward anterior.
    /// Sin esto, retroceder el slider contamina las texturas con el estado
    /// que quedó del attach previo.
    /// </summary>
    public static void ClearPendingOrigin()
    {
        _pendingOrigin = null;
    }

    /// <summary>
    /// Consume el origen pendiente — llamar en AttachWithExternalT si existe.
    /// <para>
    /// <paramref name="originPath"/> es el path del estado A de la fase actual
    /// (proporcionado por el caller). Se usa cuando el clock no corre (Edit Mode
    /// o slider manual) para que el origen visual sea exactamente el settings_N
    /// desde donde arranca la fase, evitando que BlendTextureManager hornee con
    /// el settings del ciclo actual del disco, que puede ser distinto.
    /// </para>
    /// </summary>
    private static void ConsumePendingOrigin(Room room, string originPath = null)
    {
        if (_pendingOrigin != null)
        {
            _snapOriginal  = _pendingOrigin;
            _pendingOrigin = null;
        }
        else if (BlendClock.IsRunning && BlendClock.CurrentPhase == BlendClock.Phase.Blending)
        {
            // El clock está corriendo — el origen visual real es el snapA de la sub-fase actual,
            // no el archivo de disco (que siempre es el settings del ciclo, e.g. settings_4).
            var settings = BlendSettingsLoader.Active;
            string roomName = room.abstractRoom?.name;
            if (settings != null && roomName != null)
            {
                string pathA = ReadStateReadFiles.GetRainStateSettingsFile(roomName, BlendClock.StateA);
                if (pathA != null)
                    _snapOriginal = SettingsSnapshot.FromFileWithTemplate(pathA, roomName);
                else
                    _snapOriginal = SettingsSnapshot.FromFile(room.roomSettings.filePath ?? "");
            }
            else
                _snapOriginal = SettingsSnapshot.FromFile(room.roomSettings.filePath ?? "");
        }
        else if (originPath != null)
        {
            // Modo manual sin clock (Edit Mode / slider): el origen es el estado A
            // de la fase, no el settings del ciclo actual en disco.
            string roomName = room.abstractRoom?.name ?? "";
            _snapOriginal = SettingsSnapshot.FromFileWithTemplate(originPath, roomName);
        }
        else if (_snapOriginal == null)
        {
            _snapOriginal = SettingsSnapshot.FromFile(room.roomSettings.filePath ?? "");
        }
    }
    /// </summary>
    public static void SetExternalT(float t)
    {
        if (!_active || !_externalT) return;
        _forcedT = t;

        // LightSources: intensidades (alpha) se actualizan cada tick sin throttle.
        // El color ambiente lo maneja el juego nativamente via colorFromEnvironment,
        // leyendo paletteTexture que MixAndApply actualiza con el Guardián 2.
        if (_snapA != null && _snapB != null && _room != null)
        {
            bool lightTChanged = !Mathf.Approximately(t, _lastLightT);
            if (lightTChanged || _lastLightT < 0f)
            {
                _lastLightT = t;
                var lerpedForLights = SettingsSnapshot.Lerp(_snapA, _snapB, t);
                RoomEffectsApplier.ApplyLightSources(_room, lerpedForLights);
                RoomEffectsApplier.ApplyLightBeams(_room, lerpedForLights);
            }
        }

        // El resto del blend (paleta, escalares) solo si T cambió suficientemente
        if (Mathf.Abs(t - _lastT) >= 0.005f)
        {
            _lastT = t;
            ApplyBlend(t);
        }
    }

    // ── Hooks ─────────────────────────────────────────────────────────────

    private static void OnMoveCamera(
        On.RoomCamera.orig_MoveCamera_Room_int orig, RoomCamera self,
        Room newRoom, int camPos)
    {
        string prevRoom = self.room?.abstractRoom?.name ?? "null";
        string nextRoom = newRoom?.abstractRoom?.name ?? "null";
        Plugin.RSPlugin.log.LogInfo(
            $"[MoveCamera] {prevRoom}→{nextRoom} _active={_active} _room={_room?.abstractRoom?.name ?? "null"} " +
            $"Phase={BlendClock.CurrentPhase} IsRunning={BlendClock.IsRunning}");

        // Bloquear TryApplyIdle durante este frame: cam.room puede quedar
        // apuntando a la sala anterior hasta el frame siguiente, causando
        // camIsHere=True cuando ya está en tránsito hacia otra sala.
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
            Plugin.RSPlugin.log.LogInfo(
                $"[MoveCamera] Exiting managed room '{prevRoomForReset}' → unmanaged '{nextRoomForReset}'. paletteA={self.paletteA} paletteB={self.paletteB} paletteBlend={self.paletteBlend:F3}");
            if (self.fadeTexA != null)
            {
                var pxA = self.fadeTexA.GetPixel(1, 15);
                var pxB = self.fadeTexB != null ? self.fadeTexB.GetPixel(1, 15) : UnityEngine.Color.black;
                Plugin.RSPlugin.log.LogInfo($"[MoveCamera] fadeTexA(1,15)={pxA} fadeTexB(1,15)={pxB}");
            }
        }

        BlendClockUpdater.ClearLastIdleRoom();

        orig(self, newRoom, camPos);

        // Detach y limpieza DESPUÉS del orig:
        // - El último frame de render de la sala gestionada usa los valores correctos.
        // - OverrideBackgroundGlobalsIfActive puede correr con _active=true ese frame.
        if (_active && _room != null && newRoom != _room)
            DetachAndRestore();

        if (prevWasManaged && !nextIsManaged)
        {
            Shader.SetGlobalVector(RainWorld.ShadPropMultiplyColor,
                new UnityEngine.Vector4(1f, 1f, 1f, 1f));
            Shader.SetGlobalVector(RainWorld.ShadPropAboveCloudsAtmosphereColor,
                new UnityEngine.Vector4(0.16078432f, 0.23137255f, 0.31764707f, 1f));
            _activeSnapshot = null;
        }

        // Log post-orig para diagnóstico
        if (prevWasManaged && !nextIsManaged && self.room != null)
        {
            var pxA2 = self.fadeTexA?.GetPixel(1, 15) ?? UnityEngine.Color.black;
            var pxB2 = self.fadeTexB?.GetPixel(1, 15) ?? UnityEngine.Color.black;
            Plugin.RSPlugin.log.LogInfo(
                $"[MoveCamera-postOrig-palette] paletteA={self.paletteA} paletteB={self.paletteB} paletteBlend={self.paletteBlend:F3} fadeTexA={pxA2} fadeTexB={pxB2}");
        }

        Plugin.RSPlugin.log.LogInfo(
            $"[MoveCamera-postOrig] camRoom={self.room?.abstractRoom?.name ?? "null"} newRoom={nextRoom} same={(self.room == newRoom)}");

        if (newRoom == null || !BlendClock.IsRunning) return;

        var blendSettings = BlendSettingsLoader.Active;
        if (blendSettings == null) return;

        string newRoomName = newRoom.abstractRoom?.name;
        if (newRoomName == null || !blendSettings.IncludesRoom(newRoomName)) return;

        // Consumir pending idle si la cámara llega a la sala que lo espera.
        // Esto cubre el caso de tuberías donde same=False en el primer MoveCamera
        // y el pending no puede aplicarse hasta que la cámara llega en el siguiente viaje.
        // Se aplica ANTES del bloque Blending/Idle para que el estado visual quede
        // correcto aunque en el mismo frame el clock avance a Blending.
        if (_pendingIdleRoom != null &&
            _pendingIdleRoom.abstractRoom?.name == newRoomName &&
            _pendingIdlePath != null)
        {
            Plugin.RSPlugin.log.LogInfo(
                $"[MoveCamera] Consuming pending idle for {newRoomName} (same={self.room == newRoom})");
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
            string pathA = ReadStateReadFiles.GetRainStateSettingsFile(newRoomName, BlendClock.StateA);
            string pathB = ReadStateReadFiles.GetRainStateSettingsFile(newRoomName, BlendClock.StateB);

            if (pathA != null && pathB != null && BlendClock.StateA != BlendClock.StateB)
            {
                if (self.room == newRoom)
                {
                    Plugin.RSPlugin.log.LogInfo(
                        $"[MoveCamera] Attach inmediato room={newRoomName} T={BlendClock.SubPhaseLocalT:F3} {BlendClock.StateA}→{BlendClock.StateB}");
                    AttachWithExternalT(newRoom, pathA, pathB);
                    SetExternalT(BlendClock.SubPhaseLocalT);
                }
            }
        }
        else
        {
            int stateToShow = BlendClock.CurrentPhase == BlendClock.Phase.Done
                ? BlendClock.StateB
                : BlendClock.StateA;

            string path = ReadStateReadFiles.GetRainStateSettingsFile(newRoomName, stateToShow);
            if (path != null)
            {
                if (self.room == newRoom)
                {
                    Plugin.RSPlugin.log.LogInfo(
                        $"[MoveCamera] ApplyIdleState inmediato room={newRoomName} state={stateToShow}");
                    ApplyIdleState(newRoom, path, allowCameraOps: true);
                    BlendClockUpdater.SetLastIdleRoom(newRoomName);
                }
                else
                {
                    Plugin.RSPlugin.log.LogInfo(
                        $"[MoveCamera] Pending idle room={newRoomName} state={stateToShow}");
                    _pendingIdleRoom = newRoom;
                    _pendingIdlePath = path;
                }
            }
        }
    }

    private static void OnDevUIUpdate(On.DevInterface.DevUI.orig_Update orig, DevInterface.DevUI self)
    {
        orig(self);

        // Inyectar RC_TINT en el settings file activo si aún no tiene la línea.
        // Ocurre una vez por archivo por sesión, en cuanto DevTools está abierto
        // en una sala declarada en [ROOMS].
        var cam = self.game?.cameras?[0];
        if (cam?.room != null)
        {
            var settings = BlendSettingsLoader.Active;
            if (settings != null && settings.IncludesRoom(cam.room.abstractRoom?.name ?? ""))
            {
                string filePath = cam.room.roomSettings?.filePath;
                if (filePath != null && !_rcTintInjected.Contains(filePath))
                {
                    _rcTintInjected.Add(filePath);
                    InjectRcTintIfMissing(filePath);
                }
            }
        }

        if (!_active || _room == null) return;

        // Solo aplica blend en modo manual (slider). La detección de cambio de sala
        // es responsabilidad exclusiva de OnMoveCamera (primario) y del SafetyNet
        // de BlendClockUpdater (último recurso). No duplicar aquí.
        if (_externalT) return;

        float t = BlendSlider.BlendFactor;
        if (Mathf.Abs(t - _lastT) >= 0.005f)
        {
            _lastT = t;
            ApplyBlend(t);
        }
    }

    // Agrega RC_TINT al final del settings file si no existe.
    // El formato vacío deja los valores listos para que el modder los complete.
    private static void InjectRcTintIfMissing(string filePath)
    {
        try
        {
            if (!System.IO.File.Exists(filePath)) return;

            string content = System.IO.File.ReadAllText(filePath, System.Text.Encoding.UTF8);
            if (content.Contains("RC_TINT:")) return;

            string suffix = content.EndsWith("\n") ? "" : "\n";
            System.IO.File.AppendAllText(filePath, suffix + "RC_TINT: #ffffff #ffffff #ffffff\n", System.Text.Encoding.UTF8);
        }
        catch (System.Exception e)
        {
            Plugin.RSPlugin.log.LogWarning($"[RC_TINT] No se pudo escribir en {filePath}: {e.Message}");
        }
    }

    /// <summary>
    /// Lee la línea RC_TINT del archivo ANTES de que Save() la borre.
    /// Devuelve la línea completa, o "RC_TINT: #ffffff #ffffff #ffffff" si no existe.
    /// </summary>
    public static string ExtractRcTintLine(string filePath)
    {
        try
        {
            if (!System.IO.File.Exists(filePath)) return "RC_TINT: #ffffff #ffffff #ffffff";
            foreach (var line in System.IO.File.ReadAllLines(filePath, System.Text.Encoding.UTF8))
            {
                if (line.TrimEnd('\r').StartsWith("RC_TINT:"))
                    return line.TrimEnd('\r');
            }
        }
        catch (System.Exception e)
        {
            Plugin.RSPlugin.log.LogWarning($"[RC_TINT] ExtractRcTintLine excepción: {e.Message}");
        }
        return "RC_TINT: #ffffff #ffffff #ffffff";
    }

    public static void ReappendRcTint(string filePath, string rcTintLine)
    {
        try
        {
            if (!System.IO.File.Exists(filePath)) return;
            string content = System.IO.File.ReadAllText(filePath, System.Text.Encoding.UTF8);

            if (content.Contains("RC_TINT:")) return;

            string suffix = content.EndsWith("\n") ? "" : "\n";
            System.IO.File.AppendAllText(filePath, suffix + rcTintLine + "\n", System.Text.Encoding.UTF8);
            _rcTintInjected.Add(filePath);
        }
        catch (System.Exception e)
        {
            Plugin.RSPlugin.log.LogWarning($"[RC_TINT] ReappendRcTint falló en {filePath}: {e.Message}");
        }
    }

    // ── Bloqueo de DayNightData.Apply ────────────────────────────────────
    // DayNightData.Apply() se llama desde Room.NowViewed() cuando la sala
    // tiene el efecto DayNight + un PlacedObject DayNightSettings.
    // Setea rainCycle.duskPalette y rainCycle.nightPalette globalmente.
    // Si la sala está bajo blend, bloqueamos esto para que el mod controle
    // cuándo y cómo ocurre la transición de paleta.

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
        // a la sala pendiente. Cubre el caso de tuberías (same=False en OnMoveCamera)
        // donde la paleta no pudo aplicarse hasta que la cámara actualizó.
        if (_pendingIdleRoom != null &&
            _pendingIdleRoom.abstractRoom?.name == roomName &&
            _pendingIdlePath != null &&
            !_moveCameraThisFrame)
        {
            Plugin.RSPlugin.log.LogInfo(
                $"[OnRoomCameraUpdate] Consuming pending idle room={roomName}");
            ApplyIdleState(_pendingIdleRoom, _pendingIdlePath, allowCameraOps: true);
            BlendClockUpdater.SetLastIdleRoom(roomName);
            _pendingIdleRoom = null;
            _pendingIdlePath = null;
        }
    }

    private static void OnRoomSettingsSave(On.RoomSettings.orig_Save orig, RoomSettings self)
    {
        // Leer RC_TINT antes de que Save() lo borre, luego reinyectarlo.
        // Cubre tanto nuestro Save() como cualquier Save() de RegionKit u otros mods.
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
        // independientemente del estado del clock o del blend.
        // DayNightData.Apply() solo setea duskPalette/nightPalette en rainCycle
        // (valores de índice de paleta visual, no el timer) — seguro bloquearlo.
        if (BlendSettingsLoader.Active != null && room != null)
        {
            string roomName = room.abstractRoom?.name;
            if (roomName != null && BlendSettingsLoader.Active.IncludesRoom(roomName))
                return;
        }

        orig(self, room);
    }

    // ── RoofTopView / AboveCloudsView — ctors, updates y helpers de sky ──

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
        // Rain World precarga salas adyacentes — puede haber múltiples instancias.
        // La que importa es la última creada para una sala en [ROOMS].
        _rtvScene = self;
        _skySlotDay = _skySlotDusk = _skySlotNight = -1;  // resetear slots para la nueva instancia
        AssignSkySlots(self, room, settings, SkyType.RTV);
    }

    private static void OnRoofTopViewUpdate(
        On.RoofTopView.orig_Update orig, RoofTopView self, bool eu)
    {
        var cam = self.room?.game?.cameras?[0];
        bool camIsHere = cam != null && cam.room == self.room;

        if (!camIsHere)
        {
            orig(self, eu);
            return;
        }

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
        // El ctor solo cambia illustrationName pero no recrea el FSprite —
        // RefreshSlotSprite lo hace aquí en el primer Update cuando ya existe el sLeaser.
        // Usamos illustrationName como indicador: si el sprite físico ya tiene el nombre
        // correcto, RefreshSlotSprite no hace nada (guard interno).
        var cam0 = self.room.game?.cameras?[0];
        if (_skySlotDay > 0 && cam0 != null)
        {
            var s2 = BlendSettingsLoader.Active;
            if (s2 != null)
            {
                string fA = s2.GetBkgFileForState(_skySlotDay,   SkyType.RTV);
                string fB = s2.GetBkgFileForState(_skySlotDusk,  SkyType.RTV);
                string fC = s2.GetBkgFileForState(_skySlotNight, SkyType.RTV);
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

        _acvScene = self;
        _skySlotDay = _skySlotDusk = _skySlotNight = -1;
        AssignSkySlots(self, room, settings, SkyType.ACV);
    }

    private static void OnAboveCloudsViewUpdate(
        On.AboveCloudsView.orig_Update orig, AboveCloudsView self, bool eu)
    {
        // Verificar si la cámara está actualmente en esta sala ANTES del orig.
        // Si no está, el orig puede correr (física de nubes) pero no aplicamos
        // nada visual — así evitamos que el vanilla pise los shaders globals
        // cuando el jugador ya salió de la sala.
        var cam = self.room?.game?.cameras?[0];
        bool camIsHere = cam != null && cam.room == self.room;

        if (!camIsHere)
        {
            orig(self, eu);
            return;
        }

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
        if (_skySlotDay > 0 && cam0acv != null)
        {
            var s2 = BlendSettingsLoader.Active;
            if (s2 != null)
            {
                string fA = s2.GetBkgFileForState(_skySlotDay,   SkyType.ACV);
                string fB = s2.GetBkgFileForState(_skySlotDusk,  SkyType.ACV);
                string fC = s2.GetBkgFileForState(_skySlotNight, SkyType.ACV);
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
        // El vanilla Update los puede resetear a 0 basándose en el rain cycle timer.
        foreach (var el in self.elements)
            if (el is AboveCloudsView.DistantBuilding db) db.alpha = 1f;

        var cloudSnap = _activeSnapshot;
        var newAtm = (cloudSnap != null && cloudSnap.TintCloudAtmosphere.HasValue)
            ? cloudSnap.TintCloudAtmosphere.Value
            : _lastAtmosphereColor; // usar último color del mod, no el vanilla

        // Actualizar el último color conocido cuando tenemos un snapshot válido
        if (cloudSnap != null && cloudSnap.TintCloudAtmosphere.HasValue)
            _lastAtmosphereColor = newAtm;

        self.atmosphereColor = newAtm;

        _aboveCloudsView = self;
        OverrideBackgroundGlobalsIfActive(self.room);
    }

    // ── Sky helpers ───────────────────────────────────────────────────────




    /// <summary>
    /// Asigna los nombres de archivo del mod a los slots daySky/duskSky/nightSky
    /// según la secuencia declarada en blend_settings. Llama LoadGraphic por cada
    /// imagen y registra el atlas en BlendSkyAtlasCache.
    /// Corre en el ctor — antes de elementsAddedToRoom, sin crear objetos nuevos.
    /// </summary>
    private static void AssignSkySlots(BackgroundScene scene, Room room,
        BlendSettings settings, SkyType sky)
    {
        // Resolver la secuencia de estados: A=activo, B=destino, C=siguiente
        int stateA = BlendClock.IsRunning ? BlendClock.StateA : 1;
        int stateB = BlendClock.IsRunning ? BlendClock.StateB : NextStateIn(settings, stateA);
        int stateC = NextStateIn(settings, stateB);

        _skySlotDay   = stateA;
        _skySlotDusk  = stateB;
        _skySlotNight = stateC;

        string region = BlendSettingsLoader.ActiveRegion;

        LoadAndAssignSlot(scene, sky, stateA, region, settings,
            slot => {
                if (scene is RoofTopView rtv) rtv.daySky.illustrationName = slot;
                else if (scene is AboveCloudsView acv) acv.daySky.illustrationName = slot;
            });

        LoadAndAssignSlot(scene, sky, stateB, region, settings,
            slot => {
                if (scene is RoofTopView rtv) rtv.duskSky.illustrationName = slot;
                else if (scene is AboveCloudsView acv) acv.duskSky.illustrationName = slot;
            });

        LoadAndAssignSlot(scene, sky, stateC, region, settings,
            slot => {
                if (scene is RoofTopView rtv) rtv.nightSky.illustrationName = slot;
                else if (scene is AboveCloudsView acv) acv.nightSky.illustrationName = slot;
            });

        Plugin.RSPlugin.log.LogInfo(
            $"[SkyBkg] Assigned slots: day={stateA} dusk={stateB} night={stateC} sky={sky}");
    }

    private static void LoadAndAssignSlot(BackgroundScene scene, SkyType sky,
        int state, string region, BlendSettings settings,
        System.Action<string> assign)
    {
        string file = settings.GetBkgFileForState(state, sky);
        if (string.IsNullOrEmpty(file)) return;
        string name = System.IO.Path.GetFileNameWithoutExtension(file);
        // El atlas ya fue precargado en OnRegionChanged via BlendSkyAtlasCache.PreloadRegion.
        // Solo registrar por si acaso y asignar el nombre al slot.
        BlendSkyAtlasCache.Register(region, name);
        assign(name);
    }

    /// <summary>
    /// Aplica alphas a los tres slots según el T actual del blend.
    /// El crossfade es unidireccional como en vanilla:
    ///   duskSky (B) siempre alpha=1 — está debajo, siempre visible
    ///   daySky  (A) baja de 1→0 revelando duskSky
    ///   nightSky (C) siempre alpha=0 — esperando para el próximo ciclo
    /// Esto evita el flash blanco que ocurre cuando ambos alphas suman <1.
    /// </summary>
    private static void ApplySkyAlphas(
        BackgroundScene.Simple2DBackgroundIllustration day,
        BackgroundScene.Simple2DBackgroundIllustration dusk,
        BackgroundScene.Simple2DBackgroundIllustration night)
    {
        float t = (_active && _externalT) ? _forcedT : 0f;
        day.alpha   = 1f - t;   // A se desvanece
        dusk.alpha  = 1f;       // B siempre visible debajo
        night.alpha = 0f;       // C oculto, listo
    }

    /// <summary>
    /// Rota los slots al avanzar la sub-fase: B pasa a ser A, C pasa a ser B,
    /// el nuevo C se calcula de la secuencia. Cambia illustrationName en el slot
    /// correspondiente y recrea su FSprite via el patrón de SaintsJourneyIllustration.
    /// </summary>
    public static void RotateSkySlots()
    {
        // La sincronización real ocurre en AttachWithExternalT via SyncSkySlots.
        // RotateSkySlots se mantiene para compatibilidad con BlendClockUpdater
        // pero ya no necesita hacer nada — SyncSkySlots lo cubre en el re-attach.
    }

    /// <summary>
    /// Cambia illustrationName del slot y recrea su FSprite si la cámara está disponible.
    /// Patrón de SaintsJourneyIllustration: RemoveAllSpritesFromContainer + InitiateSprites.
    /// </summary>
    // Cache de reflexión para FSprite._element — evita buscar el campo cada frame


    private static void RefreshSlotSprite(
        BackgroundScene.Simple2DBackgroundIllustration slot,
        string newName, RoomCamera cam, float initialAlpha = 0f)
    {
        if (slot.illustrationName == newName) return;
        slot.illustrationName = newName;
        if (cam == null) return;

        foreach (var sLeaser in cam.spriteLeasers)
        {
            if (sLeaser.drawableObject == slot && sLeaser.sprites != null && sLeaser.sprites.Length > 0)
            {
                var oldSprite = sLeaser.sprites[0];
                var container = oldSprite.container;
                if (container == null) break;

                int childIndex = -1;
                for (int i = 0; i < container.GetChildCount(); i++)
                    if (container.GetChildAt(i) == oldSprite) { childIndex = i; break; }

                var newSprite = new FSprite(newName, true);
                newSprite.x      = oldSprite.x;
                newSprite.y      = oldSprite.y;
                newSprite.shader = oldSprite.shader;
                newSprite.alpha  = initialAlpha;

                oldSprite.RemoveFromContainer();
                sLeaser.sprites[0] = newSprite;

                if (childIndex >= 0 && childIndex <= container.GetChildCount())
                    container.AddChildAtIndex(newSprite, childIndex);
                else
                    container.AddChild(newSprite);

                break;
            }
        }
    }


    /// <summary>
    /// Sincroniza los tres slots de cielo (daySky/duskSky/nightSky) con el par
    /// stateA/stateB del blend actual. Corre en cada AttachWithExternalT.
    /// A diferencia de RotateSkySlots (que rota incrementalmente), este método
    /// calcula directamente desde los estados actuales — siempre correcto.
    /// </summary>
    private static void SyncSkySlots(Room room, int stateA, int stateB)
    {
        var settings = BlendSettingsLoader.Active;
        if (settings == null || room == null) return;
        string roomName = room.abstractRoom?.name;
        if (roomName == null) return;

        SkyType sky = settings.GetSkyType(roomName);
        if (sky == SkyType.None) return;

        // Verificar que tenemos la escena correcta para esta sala
        if (sky == SkyType.RTV && (_rtvScene == null || _rtvScene.room?.abstractRoom?.name != roomName)) return;
        if (sky == SkyType.ACV && (_acvScene == null || _acvScene.room?.abstractRoom?.name != roomName)) return;

        int stateC = NextStateIn(settings, stateB);

        // Sin cambio — no hacer nada
        if (_skySlotDay == stateA && _skySlotDusk == stateB && _skySlotNight == stateC) return;

        Plugin.RSPlugin.log.LogInfo($"[SkySync] stateA={stateA} stateB={stateB} stateC={stateC} prevDay={_skySlotDay} prevDusk={_skySlotDusk}");

        string fileA = settings.GetBkgFileForState(stateA, sky);
        string fileB = settings.GetBkgFileForState(stateB, sky);
        string fileC = settings.GetBkgFileForState(stateC, sky);

        var cam = room.game?.cameras?[0];

        // Actualizar solo los slots que cambiaron — evita recrear sprites innecesariamente
        RoofTopView    rtv = sky == SkyType.RTV ? _rtvScene : null;
        AboveCloudsView acv = sky == SkyType.ACV ? _acvScene : null;

        if (_skySlotDay != stateA)
        {
            if (!string.IsNullOrEmpty(fileA))
            {
                if (rtv != null) RefreshSlotSprite(rtv.daySky, System.IO.Path.GetFileNameWithoutExtension(fileA), cam);
                if (acv != null) RefreshSlotSprite(acv.daySky, System.IO.Path.GetFileNameWithoutExtension(fileA), cam);
            }
            if (rtv != null) rtv.daySky.alpha = 1f;
            if (acv != null) acv.daySky.alpha = 1f;
            _skySlotDay = stateA;
        }
        if (_skySlotDusk != stateB)
        {
            // Actualizar duskSky en el mismo frame que daySky arranca en alpha=1.
            // daySky tapa a duskSky completamente, así que el cambio no es visible.
            if (!string.IsNullOrEmpty(fileB))
            {
                if (rtv != null) RefreshSlotSprite(rtv.duskSky, System.IO.Path.GetFileNameWithoutExtension(fileB), cam);
                if (acv != null) RefreshSlotSprite(acv.duskSky, System.IO.Path.GetFileNameWithoutExtension(fileB), cam);
            }
            if (rtv != null) rtv.duskSky.alpha = 1f;
            if (acv != null) acv.duskSky.alpha = 1f;
            _skySlotDusk = stateB;
        }
        if (_skySlotNight != stateC)
        {
            if (!string.IsNullOrEmpty(fileC))
            {
                if (rtv != null) RefreshSlotSprite(rtv.nightSky, System.IO.Path.GetFileNameWithoutExtension(fileC), cam);
                if (acv != null) RefreshSlotSprite(acv.nightSky, System.IO.Path.GetFileNameWithoutExtension(fileC), cam);
            }
            _skySlotNight = stateC;
        }
    }

    /// <summary>
    /// Aplica instantáneamente la imagen de cielo correspondiente a un estado.
    /// Llamar desde RCPanel al hacer swap (cambio instantáneo de estado).
    /// </summary>
    public static void ApplySkyForState(int state, Room room)
    {
        if (room == null) return;
        var settings = BlendSettingsLoader.Active;
        if (settings == null) return;
        string roomName = room.abstractRoom?.name;
        if (roomName == null) return;

        SkyType sky = settings.GetSkyType(roomName);
        if (sky == SkyType.None) return;

        string fileDay  = settings.GetBkgFileForState(state, sky);
        if (string.IsNullOrEmpty(fileDay)) return;
        string nameDay  = System.IO.Path.GetFileNameWithoutExtension(fileDay);

        int stateB = NextStateIn(settings, state);
        int stateC = NextStateIn(settings, stateB);
        string fileDusk = settings.GetBkgFileForState(stateB, sky);
        string nameDusk = string.IsNullOrEmpty(fileDusk) ? null
            : System.IO.Path.GetFileNameWithoutExtension(fileDusk);

        var cam = room.game?.cameras?[0];

        if (sky == SkyType.RTV && _rtvScene != null)
        {
            RefreshSlotSprite(_rtvScene.daySky,  nameDay,  cam, 1f);
            if (nameDusk != null) RefreshSlotSprite(_rtvScene.duskSky, nameDusk, cam, 1f);
            _rtvScene.daySky.alpha   = 1f;
            _rtvScene.duskSky.alpha  = 1f;
            _rtvScene.nightSky.alpha = 0f;
        }
        else if (sky == SkyType.ACV && _acvScene != null)
        {
            RefreshSlotSprite(_acvScene.daySky,  nameDay,  cam, 1f);
            if (nameDusk != null) RefreshSlotSprite(_acvScene.duskSky, nameDusk, cam, 1f);
            _acvScene.daySky.alpha   = 1f;
            _acvScene.duskSky.alpha  = 1f;
            _acvScene.nightSky.alpha = 0f;
        }

        _skySlotDay   = state;
        _skySlotDusk  = stateB;
        _skySlotNight = stateC;
    }

    /// <summary>
    /// Devuelve el estado siguiente a 'state' en la secuencia del blend settings.
    /// Si no hay secuencia declarada, devuelve el mismo estado.
    /// </summary>
    private static int NextStateIn(BlendSettings settings, int state)
    {
        if (settings == null || state < 1) return state;
        // Buscar en sequences el estado que sigue a 'state'
        foreach (var kv in settings.Sequences)
        {
            var seq = kv.Value;
            int idx = seq.IndexOf(state);
            if (idx >= 0)
                return seq[(idx + 1) % seq.Count];
        }
        // Fallback: state+1 cíclico dentro del rango de bkg declarados
        int max = 0;
        foreach (var alias in settings.StateBkgAlias.Keys)
            if (alias > max) max = alias;
        if (max < 1) return state;
        return (state % max) + 1;
    }

    // RoomCamera.DrawUpdate setea backgroundGraphic.color cada frame usando
    // currentPalette.blackColor/fogColor — completamente independiente de los
    // shader globals. Esto hace que el _bkg.png tome el color vanilla (nocturno
    // al acabar el ciclo) ignorando todo lo que el mod haya seteado.
    // Solución: después de orig(), sobreescribir el color con el calculado
    // desde la paleta activa del mod si la sala está en [ROOMS].
    private static void OnDrawUpdate(
        On.RoomCamera.orig_DrawUpdate orig, RoomCamera self, float timeStacker, float timeSpeed)
    {
        orig(self, timeStacker, timeSpeed);

        // Durante el frame de MoveCamera, el vanilla puede haber pisado los globals
        // desde dentro de RoofTopView.Update / AboveCloudsView.Update (orig).
        // Restaurar los últimos valores correctos que el mod aplicó.
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

        // Recalcular con la paleta que el mod controla.
        self.backgroundGraphic.color = UnityEngine.Color.Lerp(
            self.currentPalette.blackColor,
            self.currentPalette.fogColor,
            self.currentPalette.fogAmount);
    }

    // Sobreescribe MultiplyColor y AtmosphereColor usando los colores extraídos
    // directamente de la paleta activa de la cámara (cam.currentPalette).
    // skyColor  → multiply (tinte Background sprites)
    // fogColor  → atmosphere (niebla AboveClouds)
    private static void OverrideBackgroundGlobalsIfActive(Room room)
    {
        if (!_active || _room == null || room != _room) return;

        // ShadPropMultiplyColor solo debe setearse en salas con RTV o ACV.
        // En salas normales sin sky afectaría objetos físicos incorrectamente.
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
        // Bloquear la transición nativa DayNight únicamente si la sala actual
        // está declarada en [ROOMS] del blend_settings.txt (es decir, el blend
        // está attacheado o el clock corre y la sala es candidata).
        //
        // Salas con DayNight que NO están en [ROOMS] no son gestionadas por
        // el mod — su transición nativa sigue corriendo sin interferencia.
        // Cuando el modder quiera reemplazar esa transición, simplemente
        // registra la sala en [ROOMS] y el bloqueo entra automáticamente.
        //
        // Caso 0: frame de MoveCamera o frame inmediatamente posterior al salir de
        // sala gestionada. En ambos casos, self.room puede ser la sala nueva (no
        // gestionada), pero los globals deben preservarse con los últimos valores
        // correctos del mod para evitar el flash vanilla de un frame.
        if ((_moveCameraThisFrame || _exitedManagedRoomLastFrame) && _hasLastGoodGlobals)
        {
            Shader.SetGlobalVector(RainWorld.ShadPropMultiplyColor, _lastGoodMultiply);
            Shader.SetGlobalVector(RainWorld.ShadPropAboveCloudsAtmosphereColor, _lastGoodAtmosphere);
            return;
        }
        if (_moveCameraThisFrame && BlendClock.IsRunning)
            return;

        // Caso 1: blend attacheado en esta sala → aplicar blend propio, no orig.
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

        // Caso 2: clock corriendo y esta sala está en [ROOMS] pero el blend
        // aún no está attacheado (Idle o entre sub-fases) → bloquear DayNight
        // igualmente para que no interfiera con el attach inminente.
        if (BlendClock.IsRunning && BlendSettingsLoader.Active != null && self.room != null)
        {
            string roomName = self.room.abstractRoom?.name;
            if (roomName != null && BlendSettingsLoader.Active.IncludesRoom(roomName))
                return;
        }

        // Caso 3: sala declarada en [ROOMS] sin clock activo (ej. modo Cycle con
        // swap estático, o cualquier modo mientras el clock está parado).
        // Si el mod gestiona la sala, el DayNight vanilla nunca debe correr en ella
        // independientemente del estado del clock o del blend.
        // Además, reaplicamos los globals de fondo cada vez que este hook corre,
        // para que cualquier hook de RegionKit u otro mod que haya pisado
        // currentPalette después de ApplyIdleState sea corregido inmediatamente.
        if (BlendSettingsLoader.Active != null && self.room != null)
        {
            string roomName = self.room.abstractRoom?.name;
            if (roomName != null && BlendSettingsLoader.Active.IncludesRoom(roomName))
            {
                // Consumir ApplyIdleState diferido: si OnMoveCamera no pudo aplicarlo
                // porque cam.room aún apuntaba a la sala anterior, lo aplicamos aquí
                // en el primer frame donde cam.room ya es la sala correcta.
                if (_pendingIdleRoom != null && _pendingIdleRoom == self.room && _pendingIdlePath != null)
                {
                    Plugin.RSPlugin.log.LogInfo(
                        $"[PendingIdle consumed] room={self.room.abstractRoom?.name} path={_pendingIdlePath}");
                    ApplyIdleState(_pendingIdleRoom, _pendingIdlePath, allowCameraOps: true);
                    BlendClockUpdater.SetLastIdleRoom(self.room.abstractRoom?.name);
                    _pendingIdleRoom = null;
                    _pendingIdlePath = null;
                }

                // Sobreescribir globals de fondo con la paleta actual de la cámara.
                // Esto corre cada frame así que corrige cualquier contaminación posterior.
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

        // Sala no gestionada por el mod → comportamiento nativo.
        orig(self);

        // Después del orig: los shader globals pueden tener valores de la sala
        // gestionada anterior (VR1). Como orig() para salas sin DayNight no los
        // actualiza, los recalculamos leyendo fadeTexA.
        //
        // POR QUÉ fadeTexA y no paletteTexture:
        // ChangeRoom (línea 1821 del engine) llama ChangeMainPalette(PS1) que carga
        // la paleta de PS1 en fadeTexA. paletteTexture solo se actualiza en ApplyFade(),
        // que corre en línea 1751 de ApplyPositionChange — DESPUÉS de ChangeRoom (1736).
        // Por tanto, cuando UpdateDayNightPalette corre desde dentro de ChangeRoom (1877),
        // fadeTexA ya tiene PS1 pero paletteTexture todavía tiene VR1.
        //
        // fadeTexA tiene 16 filas. ApplyFade mezcla filas [8..15] con [0..7].
        // skyColor está en (1, 15), fogColor en (2, 15) — la fila "superior" de la paleta.
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
        // Si la sala destino no es gestionada por el mod (ej. PS1), resetear
        // paletteB a -1 ANTES del orig para garantizar que ChangeBothPalettes
        // dentro de ChangeRoom siempre recargue fadeTexB desde disco.
        // Sin esto, si paletteB coincide con la fade palette de newRoom,
        // ChangeBothPalettes hace early-return y fadeTexB conserva los píxeles
        // mezclados del blend de VR1 — contaminando la paleta de PS1.
        if (BlendClock.IsRunning && newRoom != null && BlendSettingsLoader.Active != null)
        {
            string newRoomName = newRoom.abstractRoom?.name;
            if (newRoomName != null && !BlendSettingsLoader.Active.IncludesRoom(newRoomName))
            {
                self.paletteB = -1;
            }
        }

        orig(self, newRoom, cameraPosition);
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
        // currentPalette es una struct — necesitamos escribir los campos directamente.
        // Así el vanilla siempre lee los colores del mod sin importar qué paleta cargó.
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
        // de las paletas — orig() sobreescribiría las texturas que BlendTextureManager
        // acaba de hornear. Bloqueamos orig() y reaplicamos el blend directamente.
        //
        // EXCEPCIÓN: si estamos en el frame de transición de sala (_moveCameraThisFrame),
        // esta llamada viene de ChangeRoom para la nueva sala (ej. PS1). Aunque self.room
        // todavía apunta a VR1 (_room), la paleta solicitada es la de PS1. Dejar pasar
        // el orig para que PS1 cargue correctamente su propia fade palette en fadeTexB.
        // Sin esto, fadeTexB queda con la paleta del setting activo de VR1 y PS1
        // hereda ese color — exactamente la contaminación múltiple confirmada.
        if (_active && _room != null && self.room == _room && BlendTextureManager.Ready
            && !_moveCameraThisFrame)
        {
            float t = _externalT ? _forcedT : BlendSlider.BlendFactor;
            MixAndApply(self, t, SettingsSnapshot.Lerp(_snapA, _snapB, t));
            return;
        }

        orig(self, palA, palB, blend);
    }

    // ── Blend por frame ───────────────────────────────────────────────────

    private static void ApplyBlend(float t)
    {
        if (_snapA == null || _snapB == null || _room == null) return;

        var cam = _room.game?.cameras?[0];
        if (cam == null) return;

        if (!BlendTextureManager.Ready)
            BlendTextureManager.Load(cam, _snapA, _snapB, _snapOriginal);

        var lerped = SettingsSnapshot.Lerp(_snapA, _snapB, t);
        _activeSnapshot = lerped; // expuesto para CalcBackgroundColors → RC_TINT
        var rs = _room.roomSettings;
        rs.Grime                 = lerped.Grime;
        rs.Clouds                = lerped.Clouds;
        rs.CeilingDrips          = lerped.CeilingDrips;
        rs.BkgDroneVolume        = lerped.BkgDroneVolume;
        rs.RandomItemDensity     = lerped.RandomItemDensity;
        rs.RandomItemSpearChance = lerped.RandomItemSpearChance;
        rs.WaterReflectionAlpha  = lerped.WaterReflectionAlpha;

        RoomEffectsApplier.ApplyShaderGlobals(lerped);
        MixAndApply(cam, t, lerped);

        // Aplicar globals de fondo aquí cubre salas sin RoofTopView/AboveCloudsView
        // (en esas salas los hooks OnRoofTopViewUpdate/OnAboveCloudsViewUpdate nunca
        // disparan). Para salas con esos efectos, OverrideBackgroundGlobalsIfActive
        // sobreescribirá estos valores justo después — sin conflicto.
        OverrideBackgroundGlobalsIfActive(_room);

        // En modo no-externo (slider manual) los lights se aplican aquí junto con
        // el blend completo. En modo externo (Loop), ya fueron aplicados en
        // SetExternalT cada tick — no duplicar.
        if (!_externalT)
        {
            RoomEffectsApplier.ApplyLightSources(_room, lerped);
            RoomEffectsApplier.ApplyLightBeams(_room, lerped);
        }
    }

    /// <summary>
    /// ELIMINADO: bakear TintMultiply/TintAtmosphere en fadeTexA[1,7] y [2,7]
    /// causaba que paletteTexture.skyColor/fogColor quedaran contaminados con el
    /// color del tinte. Esos píxeles los lee PixelColorAtCoordinate para colorear
    /// tiles y objetos físicos — exactamente los objetos que NO deben ser teñidos.
    ///
    /// El mecanismo correcto es doble:
    ///   1. ShadPropMultiplyColor / ShadPropAboveCloudsAtmosphereColor se setean
    ///      via Shader.SetGlobalVector en OverrideBackgroundGlobalsIfActive y
    ///      OnUpdateDayNightPalette — afectan solo los shaders Background/DistantBkgObject.
    ///   2. OnApplyFade sobreescribe currentPalette.skyColor/fogColor directamente
    ///      sobre la struct después de cada ApplyFade — el vanilla los lee para
    ///      backgroundGraphic.color y RoofTopView.Update sin tocar fadeTexA.
    /// </summary>
    private static void BakeTintIntoFadeTex(RoomCamera cam, SettingsSnapshot snap)
    {
        // No-op intencional — ver comentario del método.
    }

    private static void MixAndApply(RoomCamera cam, float t, SettingsSnapshot lerped)
    {
        if (!BlendTextureManager.Ready) return;

        // Interpolar opacidad de FadePalette según posición de cámara
        int   camIdx     = cam.currentCameraPosition;
        float opacA      = camIdx < _snapA.FadePaletteOpacities.Length ? _snapA.FadePaletteOpacities[camIdx] : 0f;
        float opacB      = camIdx < _snapB.FadePaletteOpacities.Length ? _snapB.FadePaletteOpacities[camIdx] : 0f;
        cam.paletteBlend = Mathf.Lerp(opacA, opacB, t);

        // Throttle: MixPalettes + Texture2D.Apply son costosos (upload CPU→GPU).
        // Solo ejecutar cuando T cambió lo suficiente para ser visualmente perceptible.
        // Con duration=60s a 40fps, T avanza ~0.00042/frame → threshold de 0.008
        // reduce llamadas a Apply de 40/s a ~2/s sin diferencia visual detectable.
        // El color de LightSources se gestiona en OverrideLightColorsPostOrig,
        // que usa interpolación en memoria independiente de este throttle.
        const float PALETTE_THRESHOLD = 0.008f;
        if (Mathf.Abs(t - _lastPaletteT) >= PALETTE_THRESHOLD || _lastPaletteT < 0f)
        {
            _lastPaletteT = t;
            BlendTextureManager.MixPalettes(cam, t);
            cam.ApplyFade();
            RoomEffectsApplier.ApplyLightSources(_room, lerped);
            RoomEffectsApplier.ApplyLightBeams(_room, lerped);
        }

        RoomEffectsApplier.ApplyDecalOpacities(_room, lerped);
        // LightSources y LightBeams se aplican aquí solo cuando el blend NO es
        // controlado por T externa (modo manual/slider). En modo externo (Loop),
        // SetExternalT los aplica cada tick antes de este bloque para mayor suavidad.
        RoomEffectsApplier.ApplyScalarEffects(_room, lerped);
    }
}