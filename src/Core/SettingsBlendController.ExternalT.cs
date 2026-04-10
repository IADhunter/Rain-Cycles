using UnityEngine;
using RainCycles.Settings;
using RainCycles.Snapshot;
using RainCycles.Sky;
using RainCycles.Clock;
using RainCycles.Core;

namespace RainCycles.Core;

// Modo BlendClock (T externa): AttachWithExternalT, SetExternalT, helpers de AtmosphereColor
public static partial class SettingsBlendController
{
    // ── Fix: color suave de LightSources con colorFromEnvironment
    public static void OverrideLightColorsPostOrig()
    {
        if (!_active || !_externalT || _room == null) return;
        if (!BlendTextureManager.Ready) return;

        var cam = _room.game?.cameras?[0];
        if (cam == null) return;

        // IMPORTANTE: NO comprobar cam.room == _room aquí.
        if (_snapA == null || _snapB == null) return;

        var lerped = SettingsSnapshot.Lerp(_snapA, _snapB, _forcedT);
        RoomEffectsApplier.ApplyLightSources(_room, lerped);
    }

    // ── ─

    // Attach controlado por BlendClock. El T no viene del slider sino de <see cref="SetExternalT"/>. Usado por BlendClockUpdater.
    public static void AttachWithExternalT(Room room, string pathA, string pathB)
    {
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
            string roomName = room.abstractRoom?.name;
            int stateA = StateFileResolver.GetStateFromPath(pathA, roomName);
            int stateB = StateFileResolver.GetStateFromPath(pathB, roomName);
            if (stateA > 0 && stateB > 0)
                SyncSkySlots(room, stateA, stateB);

            // Aplicar el blend inmediatamente al T actual del clock para que
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

    // Actualiza _snapOriginal al snapshot B actual. Llamar cuando una fase de blend completa (T=1) para que el próximo Attach use el estado correcto como base, no el estado inicial del ciclo.
    private static SettingsSnapshot _pendingOrigin = null;

    public static void AdvanceOriginToB()
    {
        if (_snapB != null)
            _pendingOrigin = _snapB;
        // (e.g. settings_2=#ffffff en la sub-fase 1→2→3), no el estado idle.
    }

    // Pre-actualiza _lastAtmosphereColor con T=0 del blend que va a empezar. Llamar justo despues de que BlendClock.Tick() dispare Idle→Blending, para que DrawUpdate del siguiente frame ya use el color correcto. Sin esto DrawUpdate corre antes de TryAttach/ApplyBlend y pinta el color del idle (T=0 del state anterior) durante un frame.
    public static void PrefetchBlendAtmosphereColor(RainWorldGame game)
    {
        var s   = BlendSettingsLoader.Active;
        var cam = game.cameras?[0];
        if (s == null || cam?.room == null) return;
        string room = cam.room.abstractRoom?.name;
        if (room == null || !s.IncludesRoom(room)) return;
        string pA = StateFileResolver.GetRainStateSettingsFile(room, BlendClock.StateA);
        string pB = StateFileResolver.GetRainStateSettingsFile(room, BlendClock.StateB);
        if (pA == null || pB == null) return;
        var snapA = SettingsSnapshot.FromFileWithTemplate(pA, room);
        var snapB = SettingsSnapshot.FromFileWithTemplate(pB, room);
        if (snapA == null || snapB == null) return;
        // (e.g. settings_2=#ffffff en la sub-fase 2→3), que contamina el valor
    }

    // Fuerza atmosphereColor en la instancia ACV activa con _lastAtmosphereColor, sin esperar al próximo orig(). Llamar desde BlendClockUpdater post-tick cuando phaseChanged o subChanged.
    public static void FlushAtmosphereColorToACV()
    {
        var acv = _acvScene;
        if (acv == null) return;
        acv.atmosphereColor = _lastAtmosphereColor;
    }

    // Actualiza _lastAtmosphereColor con el TintCloudAtmosphere del estado B actual. Llamar cuando el clock salta una sub-fase completa sin attach (ej. x50), para que el fallback de OnAboveCloudsViewUpdate use el color correcto del estado donde terminó la sub-fase, no el del idle anterior.
    public static void SnapAtmosphereToCurrentStateB()
    {
        var s = BlendSettingsLoader.Active;
        if (s == null) return;

        // Buscar la sala activa con ACV o RTV
        var acv = _acvScene;
        string roomName = acv?.room?.abstractRoom?.name;
        if (roomName == null || !s.IncludesRoom(roomName)) return;

        // StateB es el estado donde terminó la sub-fase que se saltó
        string path = StateFileResolver.GetRainStateSettingsFile(roomName, BlendClock.StateB);
        if (path == null) return;

        var snap = SettingsSnapshot.FromFileWithTemplate(path, roomName);
        if (snap == null) return;

        if (snap.TintCloudAtmosphere.HasValue)
        {
            _lastAtmosphereColor = snap.TintCloudAtmosphere.Value;
            if (acv != null) acv.atmosphereColor = _lastAtmosphereColor;
        }
    }

    // Descarta el origen pendiente sin consumirlo. Llamar desde RCPanel antes de cada ActivatePhase manual para que ConsumePendingOrigin use el pathA de la fase actual como origen, no un snapB residual de una fase forward anterior. Sin esto, retroceder el slider contamina las texturas con el estado que quedó del attach previo.
    public static void ClearPendingOrigin()
    {
        _pendingOrigin = null;
    }

    // consumir _pendingOrigin en AttachWithExternalT <para> <paramref name="originPath"/> es el path del estado A de la fase actual (proporcionado por el caller). Se usa cuando el clock no corre (Edit Mode o slider manual) para que el origen visual sea exactamente el settings_N desde donde arranca la fase, evitando que BlendTextureManager hornee con el settings del ciclo actual del disco, que puede ser distinto. </para>
    private static void ConsumePendingOrigin(Room room, string originPath = null)
    {
        if (_pendingOrigin != null)
        {
            _snapOriginal  = _pendingOrigin;
            _pendingOrigin = null;
        }
        else if (BlendClock.IsRunning && BlendClock.CurrentPhase == BlendClock.Phase.Blending)
        {
            // clock corriendo → origen = snapA sub-fase actual, no disco
            var settings = BlendSettingsLoader.Active;
            string roomName = room.abstractRoom?.name;
            if (settings != null && roomName != null)
            {
                string pathA = StateFileResolver.GetRainStateSettingsFile(roomName, BlendClock.StateA);
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
            // EditMode/slider → origen = estado A de la fase
            string roomName = room.abstractRoom?.name ?? "";
            _snapOriginal = SettingsSnapshot.FromFileWithTemplate(originPath, roomName);
        }
        else if (_snapOriginal == null)
        {
            _snapOriginal = SettingsSnapshot.FromFile(room.roomSettings.filePath ?? "");
        }
    }
    public static void SetExternalT(float t)
    {
        if (!_active || !_externalT) return;
        _forcedT = t;

        // Capturar el T del primer SetExternalT del frame de entrada.
        if (_moveCameraThisFrame && _entryFrameT < 0f)
            _entryFrameT = t;

        // LightSources: intensidades (alpha) se actualizan cada tick sin throttle.
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

}
