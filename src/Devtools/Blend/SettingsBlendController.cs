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
    private static bool _detachedThisFrame = false;

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

    // Snapshot activo actual — lerpeado durante blend, o del idle cuando el clock espera.
    // CalcBackgroundColors lo lee para obtener RC_TINT si está declarado.
    private static SettingsSnapshot _activeSnapshot = null;

    public static bool              IsActive          => _active;
    public static bool              DetachedThisFrame => _detachedThisFrame;
    public static string            CurrentPathA      => _pathA;
    public static string            CurrentPathB      => _pathB;
    public static Room              ActiveRoom        => _room;
    public static float             ForcedT           => _forcedT;
    public static SettingsSnapshot  ActiveSnapshot    => _activeSnapshot;
    public static void SetActiveSnapshot(SettingsSnapshot snap) => _activeSnapshot = snap;

    /// <summary>Llamar al inicio de cada Update para limpiar el flag de frame.</summary>
    public static void ClearFrameFlag() => _detachedThisFrame = false;

    // ── Ciclo de vida ─────────────────────────────────────────────────────

    public static void Init()
    {
        On.RoomCamera.UpdateDayNightPalette    += OnUpdateDayNightPalette;
        On.RoomCamera.ChangeBothPalettes       += OnChangeBothPalettes;
        On.DevInterface.DevUI.Update           += OnDevUIUpdate;
        On.RoomCamera.MoveCamera_Room_int      += OnMoveCamera;
        On.RoofTopView.Update                  += OnRoofTopViewUpdate;
        On.AboveCloudsView.Update              += OnAboveCloudsViewUpdate;
        On.AboveCloudsView.ctor                += OnAboveCloudsViewCtor;
        On.PlacedObject.DayNightData.Apply     += OnDayNightDataApply;
        On.RoomCamera.DrawUpdate               += OnDrawUpdate;
        On.RoomSettings.Save                   += OnRoomSettingsSave;
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
        _activeSnapshot    = null;
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
            Plugin.RSPlugin.log.LogInfo(
                $"[DetachAndRestore] room={_room.abstractRoom?.name}" +
                $" snapOriginal.Palette={_snapOriginal?.Palette}" +
                $" snapA.Palette={_snapA?.Palette} snapB.Palette={_snapB?.Palette}" +
                $" lastT={_lastT:F2} active={_active}");
            if (cam != null)
            {
                var orig = _snapOriginal;
                var rs   = _room.roomSettings;
                if (orig != null && rs != null)
                {
                    // Restaurar campos escalares de roomSettings que ApplyBlend contaminó
                    rs.Grime                 = orig.Grime;
                    rs.Clouds                = orig.Clouds;
                    rs.CeilingDrips          = orig.CeilingDrips;
                    rs.BkgDroneVolume        = orig.BkgDroneVolume;
                    rs.RandomItemDensity     = orig.RandomItemDensity;
                    rs.RandomItemSpearChance = orig.RandomItemSpearChance;
                    rs.WaterReflectionAlpha  = orig.WaterReflectionAlpha;

                    // Restaurar shader global de Grime — persiste entre salas si no se limpia
                    Shader.SetGlobalFloat(RainWorld.ShadPropGrime, orig.Grime);

                    // Los globals MultiplyColor y AtmosphereColor los retoma RoofTopView/
                    // AboveCloudsView en su próximo Update. No necesitan restauración explícita
                    // porque ya no usamos valores hardcodeados ni campos del snapshot — se
                    // calculan dinámicamente desde currentPalette en cada tick.

                    // Restaurar efectos escalares al estado original
                    // ApplyScalarEffects los dejó en valores mezclados en roomSettings.effects
                    RoomEffectsApplier.ApplyScalarEffects(_room, orig);
                }

                // Restaurar fadeTexA/B directamente desde la copia original.
                // ChangeBothPalettes(0,0,0f) no es suficiente — paleta 0 es válida
                // y puede contaminar las texturas. Sobreescribir los píxeles directamente
                // garantiza que la sala destino herede texturas limpias.
                BlendTextureManager.RestoreOriginalTextures(cam);
                cam.paletteBlend = 0f;
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
        Plugin.RSPlugin.log.LogInfo("[BlendController] ResetFull: blend cleared.");
    }

    /// <summary>
    /// Aplica un estado visual directamente (sin blend) cuando el clock está en Idle.
    /// Evita que la sala muestre el settings_N del disco mientras espera el próximo
    /// carril — en cambio muestra el estado correcto donde el carril anterior terminó.
    /// No activa el sistema de blend ni toca _snapOriginal/_pendingOrigin.
    /// </summary>
    public static void ApplyIdleState(Room room, string path)
    {
        if (room == null || path == null) return;
        var cam = room.game?.cameras?[0];
        if (cam == null) return;

        var snap = SettingsSnapshot.FromFileWithTemplate(path, room.abstractRoom.name);
        if (snap == null) return;
        _activeSnapshot = snap; // expuesto para CalcBackgroundColors → RC_TINT

        // Paleta principal + effect colors horneados.
        // ChangeMainPalette carga la textura base desde disco sin effect colors.
        // Hay que hornearlos explícitamente antes de ApplyFade, igual que hace
        // BlendTextureManager.Load al preparar los snapshots para el blend.
        // Sin esto, el juego aplica los EffectColors del roomSettings (settings_4)
        // causando un flash visible al entrar a la sala durante el Idle.
        //
        // También actualizamos roomSettings.EffectColorA/B para que cualquier hook
        // nativo que llame ApplyEffectColorsToAllPaletteTextures (ej. UpdateDayNightPalette)
        // use los colors correctos y no los de settings_4.
        var rs = room.roomSettings;
        if (rs != null)
        {
            rs.EffectColorA = snap.EffectColorA;
            rs.EffectColorB = snap.EffectColorB;
        }

        cam.ChangeMainPalette(snap.Palette);
        cam.ApplyEffectColorsToAllPaletteTextures(snap.EffectColorA, snap.EffectColorB);

        // Fade palette — si está definida
        if (snap.FadePaletteID > 0 && snap.FadePaletteOpacities.Length > 0)
        {
            int camIdx  = cam.currentCameraPosition;
            float opac  = camIdx < snap.FadePaletteOpacities.Length
                          ? snap.FadePaletteOpacities[camIdx] : 0f;
            cam.ChangeFadePalette(snap.FadePaletteID, opac);
            cam.ApplyEffectColorsToAllPaletteTextures(snap.EffectColorA, snap.EffectColorB);
        }
        cam.ApplyFade();

        // Aplicar globals de fondo desde la paleta recién cargada.
        // ApplyFade → ApplyPalette actualiza cam.currentPalette, así que
        // CalcBackgroundColors ya puede leer skyColor/fogColor correctamente.
        // No hay lógica <def> aquí porque en Idle solo hay un estado activo
        // (no hay blend A→B), y si ese estado tiene <def> simplemente no
        // debería llamarse ApplyIdleState con tinte — el caller es responsable.
        // Por simplicidad aplicamos siempre el color de la paleta activa.
        {
            Color multiply, atmosphere;
            RoomEffectsApplier.CalcBackgroundColors(cam, out multiply, out atmosphere);
            Shader.SetGlobalVector(RainWorld.ShadPropMultiplyColor, multiply);
            Shader.SetGlobalVector(RainWorld.ShadPropAboveCloudsAtmosphereColor, atmosphere);
        }
        RoomEffectsApplier.ApplyShaderGlobals(snap);
        RoomEffectsApplier.ApplyScalarEffects(room, snap);
        RoomEffectsApplier.ApplyLightSources(room, snap);
        RoomEffectsApplier.ApplyLightBeams(room, snap);

        Plugin.RSPlugin.log.LogInfo(
            $"[BlendController] ApplyIdleState room={room.abstractRoom?.name} " +
            $"path={System.IO.Path.GetFileName(path)} Palette={snap.Palette}");
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
        if (cam == null || cam.room != _room) return;

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
        _room      = room;
        _pathA     = pathA;
        _pathB     = pathB;
        ConsumePendingOrigin(room);
        _snapA        = SettingsSnapshot.FromFileWithTemplate(pathA, room.abstractRoom.name);
        _snapB        = SettingsSnapshot.FromFileWithTemplate(pathB, room.abstractRoom.name);
        _active       = true;
        _externalT    = true;
        _lastT        = -1f;
        _lastPaletteT = -1f;
        _lastLightT   = -1f;

        Plugin.RSPlugin.log.LogInfo(
            $"[BlendController] AttachWithExternalT room={room.abstractRoom?.name}" +
            $" filePath={room.roomSettings.filePath}" +
            $" snapOriginal.Palette={_snapOriginal?.Palette}" +
            $" pathA={System.IO.Path.GetFileName(pathA)}" +
            $" pathB={System.IO.Path.GetFileName(pathB)}" +
            $" snapA.Palette={_snapA?.Palette} snapB.Palette={_snapB?.Palette}");

        var cam = room.game?.cameras?[0];
        if (cam != null)
        {
            BlendTextureManager.Load(cam, _snapA, _snapB, _snapOriginal, applyFade: false);
            RoomEffectsApplier.BuildLightIndex(room);

            // Aplicar el blend inmediatamente al T actual del clock para que
            // al entrar a la sala el jugador vea el estado correcto sin esperar
            // al próximo SetExternalT.
            if (_externalT && BlendClock.IsRunning &&
                BlendClock.CurrentPhase == BlendClock.Phase.Blending)
            {
                float immediateT = BlendClock.SubPhaseLocalT;
                _lastT = -1f;
                _lastPaletteT = -1f; // forzar ApplyBlend aunque T sea igual al anterior
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
            Plugin.RSPlugin.log.LogInfo(
                $"[BlendController] Origin will advance to snapB (Palette={_snapB.Palette})");
        }
    }

    /// <summary>
    /// Consume el origen pendiente — llamar en AttachWithExternalT si existe.
    /// </summary>
    private static void ConsumePendingOrigin(Room room)
    {
        if (_pendingOrigin != null)
        {
            _snapOriginal  = _pendingOrigin;
            _pendingOrigin = null;
            Plugin.RSPlugin.log.LogInfo(
                $"[BlendController] Origin consumed (Palette={_snapOriginal.Palette})");
        }
        else if (BlendClock.IsRunning && BlendClock.CurrentPhase == BlendClock.Phase.Blending)
        {
            // El clock está corriendo — el origen visual real es el snapA de la sub-fase actual,
            // no el archivo de disco (que siempre es el settings del ciclo, e.g. settings_4).
            // Cargar snapA desde el path que el clock indica.
            var settings = BlendSettingsLoader.Active;
            string roomName = room.abstractRoom?.name;
            if (settings != null && roomName != null)
            {
                string pathA = ReadStateReadFiles.GetRainStateSettingsFile(roomName, BlendClock.StateA);
                if (pathA != null)
                {
                    _snapOriginal = SettingsSnapshot.FromFileWithTemplate(pathA, roomName);
                    Plugin.RSPlugin.log.LogInfo(
                        $"[BlendController] Origin from clock StateA={BlendClock.StateA} " +
                        $"(Palette={_snapOriginal?.Palette})");
                }
                else
                    _snapOriginal = SettingsSnapshot.FromFile(room.roomSettings.filePath ?? "");
            }
            else
                _snapOriginal = SettingsSnapshot.FromFile(room.roomSettings.filePath ?? "");
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
        if (_active && _room != null && newRoom != _room)
        {
            Plugin.RSPlugin.log.LogInfo(
                $"[BlendController] Camera leaving blend room '{_room.abstractRoom?.name}' → Detach");
            // DetachAndRestore llama Detach() al final, que pone _active=false.
            // Esto ocurre ANTES de orig(), así OnChangeBothPalettes no interviene
            // cuando el juego carga la paleta de la nueva sala.
            DetachAndRestore();
        }

        // Limpiar _lastIdleRoom para que ApplyIdleState vuelva a correr
        // al reingresar a la sala — aunque sea la misma de antes.
        BlendClockUpdater.ClearLastIdleRoom();

        orig(self, newRoom, camPos);

        // Si la nueva sala está en [ROOMS], aplicar los globals de fondo.
        // Si cam.room ya es newRoom (transición directa) → inmediato.
        // Si cam.room aún apunta a la sala anterior (tubería) → diferido:
        // guardar pending para que OnUpdateDayNightPalette lo consuma en cuanto
        // cam.room coincida, evitando el flash de ChangeBothPalettes en sala incorrecta.
        if (!_active && newRoom != null)
        {
            var settings = BlendSettingsLoader.Active;
            if (settings != null)
            {
                string roomName = newRoom.abstractRoom?.name;
                if (roomName != null && settings.IncludesRoom(roomName))
                {
                    string path = newRoom.roomSettings?.filePath;
                    if (path != null)
                    {
                        if (self.room == newRoom)
                            ApplyIdleState(newRoom, path);
                        else
                        {
                            _pendingIdleRoom = newRoom;
                            _pendingIdlePath = path;
                        }
                    }
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
            System.IO.File.AppendAllText(filePath, suffix + "RC_TINT: #FFFFFF #FFFFFF\n", System.Text.Encoding.UTF8);
            Plugin.RSPlugin.log.LogInfo($"[RC_TINT] Línea inyectada en: {System.IO.Path.GetFileName(filePath)}");
        }
        catch (System.Exception e)
        {
            Plugin.RSPlugin.log.LogWarning($"[RC_TINT] No se pudo escribir en {filePath}: {e.Message}");
        }
    }

    /// <summary>
    /// Lee la línea RC_TINT del archivo ANTES de que Save() la borre.
    /// Devuelve la línea completa, o "RC_TINT: # #" si no existe.
    /// </summary>
    public static bool FileHasRcTint(string filePath)
    {
        try
        {
            if (!System.IO.File.Exists(filePath)) return false;
            foreach (var line in System.IO.File.ReadAllLines(filePath, System.Text.Encoding.UTF8))
                if (line.TrimEnd('\r').StartsWith("RC_TINT:")) return true;
        }
        catch { }
        return false;
    }

    public static string ExtractRcTintLine(string filePath)
    {
        try
        {
            if (!System.IO.File.Exists(filePath)) return "RC_TINT: #FFFFFF #FFFFFF";
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
        return "RC_TINT: #FFFFFF #FFFFFF";
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
        if (BlendSettingsLoader.Active != null && room != null)
        {
            string roomName = room.abstractRoom?.name;
            if (roomName != null && BlendSettingsLoader.Active.IncludesRoom(roomName))
                return;
        }

        orig(self, room);
    }

    // ── Bloqueo de RoofTopView / AboveCloudsView ──────────────────────────
    // orig() corre siempre para físicas, nubes, humo, etc.
    // Pero si la sala está en [ROOMS], bloqueamos los efectos visuales de
    // transición DayNight: alphas de las imágenes de cielo y shader globals.
    // Los alphas se restauran al estado "día" (daySky visible, resto ocultos).
    // Los shader globals los sobreescribe OverrideBackgroundGlobalsIfActive.

    private static void OnRoofTopViewUpdate(
        On.RoofTopView.orig_Update orig, RoofTopView self, bool eu)
    {
        orig(self, eu);

        if (self.room == null) return;
        var settings = BlendSettingsLoader.Active;
        if (settings == null) return;
        string roomName = self.room.abstractRoom?.name;
        if (roomName == null || !settings.IncludesRoom(roomName)) return;

        // Congelar imágenes de cielo en estado "día" — el mod las controlará
        // cuando implemente su propio sistema de intercalado de cielo.
        self.daySky.alpha  = 1f;
        self.duskSky.alpha = 0f;
        self.nightSky.alpha = 0f;

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

        // Guardar referencia para que OverrideBackgroundGlobalsIfActive pueda
        // leer atmosphereColor (el color azul atmosférico de los edificios lejanos)
        // en lugar de usar fogColor de la paleta para ShadPropAboveCloudsAtmosphereColor.
        _aboveCloudsView = self;

        var cam = room.game?.cameras?[0];
        if (cam == null) return;

        // Restaurar los globals que el constructor pisó con valores hardcodeados.
        Color multiply, atmosphere;
        RoomEffectsApplier.CalcBackgroundColors(cam, out multiply, out atmosphere);
        Shader.SetGlobalVector(RainWorld.ShadPropMultiplyColor, multiply);
        Shader.SetGlobalVector(RainWorld.ShadPropAboveCloudsAtmosphereColor, atmosphere);
    }

    private static void OnAboveCloudsViewUpdate(
        On.AboveCloudsView.orig_Update orig, AboveCloudsView self, bool eu)
    {
        orig(self, eu);

        if (self.room == null) return;
        var settings = BlendSettingsLoader.Active;
        if (settings == null) return;
        string roomName = self.room.abstractRoom?.name;
        if (roomName == null || !settings.IncludesRoom(roomName)) return;

        // Congelar imágenes de cielo en estado "día".
        self.daySky.alpha  = 1f;
        self.duskSky.alpha = 0f;
        self.nightSky.alpha = 0f;

        // Mantener referencia actualizada — por si la instancia se recreó
        _aboveCloudsView = self;

        OverrideBackgroundGlobalsIfActive(self.room);
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
    //
    // Si el estado A o B tiene el tag <def> en su línea de sequence, ese lado
    // del blend usa Color.white (sin tinte) en lugar del color de su paleta.
    // Esto permite que ciertos settings mantengan el aspecto vanilla (gris neutro)
    // mientras otros aplican el tinte de su paleta — la transición entre ambos
    // se interpola suavemente según T.
    private static void OverrideBackgroundGlobalsIfActive(Room room)
    {
        if (!_active || _room == null || room != _room) return;

        var cam = room.game?.cameras?[0];
        if (cam == null) return;

        // skyColor  → multiply (tinte general de sprites Background)
        // fogColor  → atmosphere (tinte atmosférico de edificios lejanos)
        // Ambos vienen de blend_texture.png del mod si existe,
        // con fallback a la paleta activa.
        float tBlend = _externalT ? _forcedT : BlendSlider.BlendFactor;
        Color multiply, atmosphere;
        RoomEffectsApplier.CalcBackgroundColors(cam, out multiply, out atmosphere);

        var settings = BlendSettingsLoader.Active;
        if (settings != null && settings.DefaultBackgroundStates.Count > 0
            && BlendClock.IsRunning && _externalT)
        {
            float t = _externalT ? _forcedT : BlendSlider.BlendFactor;

            bool defA = settings.IsDefaultBackground(BlendClock.StateA);
            bool defB = settings.IsDefaultBackground(BlendClock.StateB);

            if (defA || defB)
            {
                if (defA && defB)
                {
                    multiply   = Color.white;
                    atmosphere = Color.white;
                }
                else if (defA)
                {
                    multiply   = Color.Lerp(Color.white, multiply,   t);
                    atmosphere = Color.Lerp(Color.white, atmosphere, t);
                }
                else
                {
                    multiply   = Color.Lerp(multiply,   Color.white, t);
                    atmosphere = Color.Lerp(atmosphere, Color.white, t);
                }
            }
        }

        Shader.SetGlobalVector(RainWorld.ShadPropMultiplyColor, multiply);
        Shader.SetGlobalVector(RainWorld.ShadPropAboveCloudsAtmosphereColor, atmosphere);
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
                    ApplyIdleState(_pendingIdleRoom, _pendingIdlePath);
                    _pendingIdleRoom = null;
                    _pendingIdlePath = null;
                }

                // Sobreescribir globals de fondo con la paleta actual de la cámara.
                // Esto corre cada frame así que corrige cualquier contaminación posterior.
                Color multiply, atmosphere;
                RoomEffectsApplier.CalcBackgroundColors(self, out multiply, out atmosphere);
                Shader.SetGlobalVector(RainWorld.ShadPropMultiplyColor, multiply);
                Shader.SetGlobalVector(RainWorld.ShadPropAboveCloudsAtmosphereColor, atmosphere);
                return;
            }
        }

        // Sala no gestionada por el mod → comportamiento nativo.
        orig(self);
    }

    private static void OnChangeBothPalettes(
        On.RoomCamera.orig_ChangeBothPalettes orig, RoomCamera self,
        int palA, int palB, float blend)
    {
        orig(self, palA, palB, blend);
        // Verificación estricta: solo intervenir si el blend está activo
        // Y la cámara apunta exactamente a nuestra sala
        if (!_active || _room == null || self.room != _room) return;
        if (!BlendTextureManager.Ready) return;

        float t = _externalT ? _forcedT : BlendSlider.BlendFactor;
        MixAndApply(self, t, SettingsSnapshot.Lerp(_snapA, _snapB, t));
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