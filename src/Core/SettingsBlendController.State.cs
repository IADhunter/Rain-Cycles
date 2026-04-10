using UnityEngine;
using RainCycles.Settings;
using RainCycles.Snapshot;
using RainCycles.Sky;
using RainCycles.Clock;

namespace RainCycles.Core;

public static partial class SettingsBlendController
{
    private static SettingsSnapshot _snapA;
    private static SettingsSnapshot _snapB;
    private static SettingsSnapshot _snapOriginal;
    private static Room             _room;
    private static float            _lastT        = -1f;
    private static float            _lastPaletteT = -1f;  // throttle → MixPalettes+Apply
    private static float            _lastLightT   = -1f;  // throttle → LightSources/Beams
    private static bool             _active       = false;
    private static bool             _externalT    = false;
    private static float            _forcedT      = 0f;
    private static string           _pathA        = null;
    private static string           _pathB        = null;

    private static bool _detachedThisFrame         = false; // bloquea re-attach mismo frame
    private static bool _moveCameraThisFrame        = false; // MoveCamera → bloquea TryApplyIdle
    private static bool _subChangedThisFrame        = false; // clock avanzó sub-fase este frame
    private static bool _exitedManagedRoomLastFrame = false; // frame extra al salir de sala

    // RC_TINT → inyectado una vez por archivo por sesión
    private static readonly System.Collections.Generic.HashSet<string> _rcTintInjected =
        new System.Collections.Generic.HashSet<string>();

    private static Room   _pendingIdleRoom = null; // OnMoveCamera → cam.room aún no actualizó
    private static string _pendingIdlePath = null;

    private static AboveCloudsView _aboveCloudsView = null; // instancia activa ACV

    // ── RC_BKG ───────────────────────────────────────────────────────────
    private static RoofTopView     _rtvScene = null;
    private static AboveCloudsView _acvScene = null;

    // slots separados por tipo → ctor adyacente no pisa slots activos
    private static int _rtvSlotDay   = -1;  // RTV: daySky   → A
    private static int _rtvSlotDusk  = -1;  //      duskSky  → B
    private static int _rtvSlotNight = -1;  //      nightSky → C
    private static int _acvSlotDay   = -1;  // ACV: daySky   → A
    private static int _acvSlotDusk  = -1;  //      duskSky  → B
    private static int _acvSlotNight = -1;  //      nightSky → C

    private static int  GetSlotDay  (SkyType t) => t == SkyType.RTV ? _rtvSlotDay   : _acvSlotDay;
    private static int  GetSlotDusk (SkyType t) => t == SkyType.RTV ? _rtvSlotDusk  : _acvSlotDusk;
    private static int  GetSlotNight(SkyType t) => t == SkyType.RTV ? _rtvSlotNight : _acvSlotNight;
    private static void SetSlotDay  (SkyType t, int v) { if (t == SkyType.RTV) _rtvSlotDay   = v; else _acvSlotDay   = v; }
    private static void SetSlotDusk (SkyType t, int v) { if (t == SkyType.RTV) _rtvSlotDusk  = v; else _acvSlotDusk  = v; }
    private static void SetSlotNight(SkyType t, int v) { if (t == SkyType.RTV) _rtvSlotNight = v; else _acvSlotNight = v; }

    // slot activo en cam.room sin conocer SkyType
    private static int ActiveSlotDay(RoomCamera cam)
    {
        if (cam?.room == null) return -1;
        var s = BlendSettingsLoader.Active;
        if (s == null) return -1;
        string n = cam.room.abstractRoom?.name;
        if (n == null) return -1;
        var t = s.GetSkyType(n);
        return t == SkyType.RTV ? _rtvSlotDay : t == SkyType.ACV ? _acvSlotDay : -1;
    }

    private static int _lastIdleSkyState = -1; // evita ApplySkyForState cada frame

    // últimos globals correctos → restaurados en MoveCamera
    private static Color _lastGoodMultiply   = Color.white;
    private static Color _lastGoodAtmosphere = new Color(0.16078432f, 0.23137255f, 0.31764707f);
    private static bool  _hasLastGoodGlobals = false;

    // fallback atmosphereColor → evita vanilla azul entre sub-fases
    private static Color _lastAtmosphereColor = new Color(0.16078432f, 0.23137255f, 0.31764707f);

    // alphas capturados en Detach → frames de transición sin saltos
    private static float _savedDayAlpha   = 1f;
    private static float _savedDuskAlpha  = 1f;
    private static float _savedNightAlpha = 0f;
    private static bool  _hasSavedAlphas  = false;

    // SyncSkySlots corrió, cámara ausente → RefreshSlotSprite pendiente
    private static bool  _pendingSkySync   = false;
    private static int   _pendingSkyStateA = -1;
    private static int   _pendingSkyStateB = -1;
    private static float _entryFrameT      = -1f;

    // lerpeado blend/idle → CalcBackgroundColors lee RC_TINT
    private static SettingsSnapshot _activeSnapshot = null;

    public static bool             IsActive            => _active;
    public static bool             DetachedThisFrame   => _detachedThisFrame;
    public static bool             MoveCameraThisFrame => _moveCameraThisFrame;
    public static void             NotifySubChanged()  => _subChangedThisFrame = true;
    public static string           CurrentPathA        => _pathA;
    public static string           CurrentPathB        => _pathB;
    public static Room             ActiveRoom          => _room;
    public static float            ForcedT             => _forcedT;
    public static SettingsSnapshot ActiveSnapshot      => _activeSnapshot;
    public static void SetActiveSnapshot(SettingsSnapshot snap) => _activeSnapshot = snap;
    // fuerza ACV.Update → _lastAtmosphereColor el próximo frame
    public static void ClearActiveSnapshot() => _activeSnapshot = null;

    public static void ClearFrameFlag()
    {
        _detachedThisFrame          = false;
        _subChangedThisFrame        = false;
        _exitedManagedRoomLastFrame = _moveCameraThisFrame && _hasLastGoodGlobals;
        _moveCameraThisFrame        = false;
        _entryFrameT                = -1f;
    }

    public static void Init()
    {
        On.RoomCamera.UpdateDayNightPalette += OnUpdateDayNightPalette;
        On.RoomCamera.ChangeBothPalettes    += OnChangeBothPalettes;
        On.RoomCamera.ApplyFade             += OnApplyFade;
        On.DevInterface.DevUI.Update        += OnDevUIUpdate;
        On.RoomCamera.MoveCamera_Room_int   += OnMoveCamera;
        On.RoofTopView.ctor                 += OnRoofTopViewCtor;
        On.RoofTopView.Update               += OnRoofTopViewUpdate;
        On.AboveCloudsView.Update           += OnAboveCloudsViewUpdate;
        On.AboveCloudsView.ctor             += OnAboveCloudsViewCtor;
        On.PlacedObject.DayNightData.Apply  += OnDayNightDataApply;
        On.RoomCamera.DrawUpdate            += OnDrawUpdate;
        On.RoomSettings.Save                += OnRoomSettingsSave;
        On.RoomCamera.Update                += OnRoomCameraUpdate;
        On.RoomCamera.ChangeRoom            += OnChangeRoom;
    }
}
