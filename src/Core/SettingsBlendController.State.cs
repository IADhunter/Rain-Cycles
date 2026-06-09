using System.Collections.Generic;
using UnityEngine;
using RainCycles.Settings;
using RainCycles.Snapshot;
using RainCycles.Clock;
using RainCycles.Blend;

namespace RainCycles.Core;

public static partial class SettingsBlendController
{
    private static SettingsSnapshot _snapA;
    private static SettingsSnapshot _snapB;
    private static SettingsSnapshot _snapOriginal;
    private static Room             _room;
    private static float            _lastT        = -1f;
    private static float            _lastLightT   = -1f;
    private static bool             _active       = false;
    private static bool             _externalT    = false;
    private static float            _forcedT      = 0f;
    private static string           _pathA        = null;
    private static string           _pathB        = null;

    // ── Estados manuales para modo slider (cuando _externalT = true) ──
    private static int _manualStateA = 1;
    private static int _manualStateB = 2;

    // ── Nueva variable para distinguir auto vs manual ──
    private static bool _isAutoBlend = false;  // true = automático (clock), false = manual (slider)

    private static int   _pendingSyncSky   = -1;
    private static int   _pendingStateA    = -1;
    private static int   _pendingStateB    = -1;
    private static bool  _detachedThisFrame         = false;
    private static bool  _moveCameraThisFrame        = false;

    private static readonly HashSet<string> _rcViewInjected = new HashSet<string>();

    private static bool _lastRoomWasManaged = false;
    private static string _lastManagedRoomName = null;

    private static RoofTopView     _rtvScene = null;
    private static AboveCloudsView _acvScene = null;
    private static AboveCloudsView _psvScene = null;

    private static List<BackgroundScene.Simple2DBackgroundIllustration> _rcSlotsACV = null;
    private static List<BackgroundScene.Simple2DBackgroundIllustration> _rcSlotsRTV = null;
    private static List<BackgroundScene.Simple2DBackgroundIllustration> _rcSlotsPSV = null;
    private static List<BackgroundScene.Simple2DBackgroundIllustration> _rcSlotsPSVFog = null;
    private static List<BackgroundScene.Simple2DBackgroundIllustration> _rcSlotsPSVSun = null;

    private static int _rtvSlotDay   = -1;
    private static int _rtvSlotDusk  = -1;
    private static int _rtvSlotNight = -1;
    private static int _acvSlotDay   = -1;
    private static int _acvSlotDusk  = -1;
    private static int _acvSlotNight = -1;
    private static int _psvSlotDay   = -1;
    private static int _psvSlotDusk  = -1;
    private static int _psvSlotNight = -1;

    private static float _savedDayAlpha   = 1f;
    private static float _savedDuskAlpha  = 1f;
    private static float _savedNightAlpha = 0f;
    private static bool  _hasSavedAlphas  = false;

    private static bool  _pendingSkySync   = false;
    private static int   _pendingSkyStateA = -1;
    private static int   _pendingSkyStateB = -1;

    private static float _entryFrameT = -1f;

    private static SettingsSnapshot _activeSnapshot = null;
    private static Color _lastAtmosphereColor = new Color(0.16078432f, 0.23137255f, 0.31764707f);

    private static List<BackgroundScene.Simple2DBackgroundIllustration> _rcSlotsStaticACV = null;
    private static List<BackgroundScene.Simple2DBackgroundIllustration> _rcSlotsStaticRTV = null;
    private static List<BackgroundScene.Simple2DBackgroundIllustration> _rcSlotsStaticPSV = null;

    private static int _lastIdleRotatedState = -1;
    
    // ── Flags para refresco post-guardado ──
    private static bool _forceSkyRefresh = false;

    // ── Propiedades públicas ──────────────────────────────────────────
    public static bool IsExternalT => _externalT;
    public static bool IsAutoBlend => _isAutoBlend;  // NUEVA: true = automático (clock), false = manual (slider)
    public static int ManualStateA => _manualStateA;
    public static int ManualStateB => _manualStateB;
    public static SettingsSnapshot ManualSnapA => _snapA;
    public static SettingsSnapshot ManualSnapB => _snapB;

    // ── Helpers de slots ──────────────────────────────────────────────

    private static int GetSlotDay(SkyType t)
    {
        if (t == SkyType.RTV) return _rtvSlotDay;
        if (t == SkyType.ACV) return _acvSlotDay;
        if (t == SkyType.PSV) return _psvSlotDay;
        return -1;
    }

    private static int GetSlotDusk(SkyType t)
    {
        if (t == SkyType.RTV) return _rtvSlotDusk;
        if (t == SkyType.ACV) return _acvSlotDusk;
        if (t == SkyType.PSV) return _psvSlotDusk;
        return -1;
    }

    private static int GetSlotNight(SkyType t)
    {
        if (t == SkyType.RTV) return _rtvSlotNight;
        if (t == SkyType.ACV) return _acvSlotNight;
        if (t == SkyType.PSV) return _psvSlotNight;
        return -1;
    }

    private static void SetSlotDay(SkyType t, int v)
    {
        if (t == SkyType.RTV) _rtvSlotDay = v;
        else if (t == SkyType.ACV) _acvSlotDay = v;
        else if (t == SkyType.PSV) _psvSlotDay = v;
    }

    private static void SetSlotDusk(SkyType t, int v)
    {
        if (t == SkyType.RTV) _rtvSlotDusk = v;
        else if (t == SkyType.ACV) _acvSlotDusk = v;
        else if (t == SkyType.PSV) _psvSlotDusk = v;
    }

    private static void SetSlotNight(SkyType t, int v)
    {
        if (t == SkyType.RTV) _rtvSlotNight = v;
        else if (t == SkyType.ACV) _acvSlotNight = v;
        else if (t == SkyType.PSV) _psvSlotNight = v;
    }

    private static int ActiveSlotDay(RoomCamera cam)
    {
        if (cam?.room == null) return -1;
        var s = BlendSettingsLoader.Active;
        if (s == null) return -1;
        string n = cam.room.abstractRoom?.name;
        if (n == null) return -1;
        var t = GetViewFromLoadedSettings(cam.room);
        return t == SkyType.RTV ? _rtvSlotDay : t == SkyType.ACV ? _acvSlotDay : t == SkyType.PSV ? _psvSlotDay : -1;
    }

    // ── Helper: determinar si una sala es blend (RC_TYPE: Blend) ───────

    public static bool IsBlendRoom(Room room)
    {
        if (room?.roomSettings?.filePath == null) return false;
        var snap = StaticTintManager.GetCachedSnapshot(room);
        return snap != null && snap.HasRcType && snap.RcType == RcType.Blend;
    }

    // ── API pública ───────────────────────────────────────────────────

    public static bool IsRoomManagedByMod(string roomName)
    {
        if (string.IsNullOrEmpty(roomName)) return false;
        return false;
    }
    
    public static bool IsRoomManagedByMod(Room room)
    {
        if (room == null) return false;
        return IsBlendRoom(room);
    }

    public static bool             IsActive            => _active;
    public static bool             DetachedThisFrame   => _detachedThisFrame;
    public static bool             MoveCameraThisFrame => _moveCameraThisFrame;
    public static string           CurrentPathA        => _pathA;
    public static string           CurrentPathB        => _pathB;
    public static Room             ActiveRoom          => _room;
    public static float            ForcedT             => _forcedT;
    public static SettingsSnapshot ActiveSnapshot      => _activeSnapshot;
    public static void SetActiveSnapshot(SettingsSnapshot snap) => _activeSnapshot = snap;
    public static void ClearActiveSnapshot() => _activeSnapshot = null;
    public static void SetLastAtmosphereColor(Color c) => _lastAtmosphereColor = c;

    public static void ClearFrameFlag()
    {
        _detachedThisFrame   = false;
        _moveCameraThisFrame = false;
        _entryFrameT         = -1f;
    }

    public static void Init()
    {
        On.RoomCamera.ChangeBothPalettes    += OnChangeBothPalettes;
        On.RoomCamera.ApplyFade             += OnApplyFade;
        On.RoomCamera.ApplyPalette          += OnApplyPalette;
        On.DevInterface.DevUI.Update        += OnDevUIUpdate;
        On.RoomCamera.MoveCamera_Room_int   += OnMoveCamera;
        On.RoofTopView.ctor                 += OnRoofTopViewCtor;
        On.RoofTopView.Update               += OnRoofTopViewUpdate;
        On.AboveCloudsView.Update           += OnAboveCloudsViewUpdate;
        On.AboveCloudsView.ctor             += OnAboveCloudsViewCtor;
        On.RoomCamera.ChangeRoom            += OnChangeRoom;
        On.RoomSettings.Save                += OnRoomSettingsSave;
        On.RoomCamera.Update                += OnRoomCameraUpdate;
    }

    // ── Aplicar tintes y efectos estáticos en idle (ligero, sin texturas) ──
    public static void ApplyIdleTintsAndEffects(Room room, SettingsSnapshot snap)
    {
        if (room == null || snap == null) return;

        // Tintes globales
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
            _lastAtmosphereColor = snap.TintCloudAtmosphere.Value;
            for (int i = 0; i < room.updateList.Count; i++)
            {
                if (room.updateList[i] is AboveCloudsView acv)
                {
                    acv.atmosphereColor = snap.TintCloudAtmosphere.Value;
                    break;
                }
            }
        }

        // RoomSettings escalares
        var rs = room.roomSettings;
        rs.Grime = snap.Grime;
        if (!RoomHasWeatherController(room))
            rs.Clouds = snap.Clouds;
        rs.CeilingDrips = snap.CeilingDrips;
        rs.BkgDroneVolume = snap.BkgDroneVolume;
        rs.RandomItemDensity = snap.RandomItemDensity;
        rs.RandomItemSpearChance = snap.RandomItemSpearChance;
        rs.WaterReflectionAlpha = snap.WaterReflectionAlpha;

        // Decals y efectos escalares (sin luces dinámicas)
        RoomEffectsApplier.ApplyDecalOpacities(room, snap);
        RoomEffectsApplier.ApplyScalarEffects(room, snap);
        RoomEffectsApplier.ApplyTerrainScalars(room, snap);

        // Sky slots alpha (idle = estado único, t=0)
        var skyType = GetViewFromLoadedSettings(room);
        if (skyType != SkyType.None)
            ApplyRcSlotsAlpha(skyType, 0f, isBlending: false);
    }
    
    // ── Método interno para actualizar estados manuales ─────────────────
    internal static void UpdateManualStates(int stateA, int stateB)
    {
        _manualStateA = stateA;
        _manualStateB = stateB;
        RSPlugin.log.LogDebug($"[SettingsBlendController] Manual states updated: A={stateA}, B={stateB}");
    }
    
    // ── Nuevos métodos para refresco post-guardado ──────────────────────
    
    /// <summary>
    /// Refresca los snapshots activos del blend después de guardar cambios.
    /// </summary>
    public static void RefreshActiveSnapshots()
    {
        if (!_active || _room == null) return;
        
        RSPlugin.log.LogDebug($"[SettingsBlendController] RefreshActiveSnapshots: room={_room.abstractRoom?.name}");
        
        // Recargar snapshots desde disco (caché invalidada previamente)
        string roomName = _room.abstractRoom?.name;
        if (!string.IsNullOrEmpty(roomName))
        {
            if (_pathA != null)
                _snapA = StaticTintManager.GetCachedSnapshot(_pathA, roomName);
            if (_pathB != null)
                _snapB = StaticTintManager.GetCachedSnapshot(_pathB, roomName);
        }
        
        // Reaplicar blend con el T actual
        float currentT = _externalT ? _forcedT : (BlendClock.IsRunning ? BlendClock.SubPhaseLocalT : 0f);
        
        // Forzar recarga de texturas de blend si estamos en modo blend activo
        var cam = _room.game?.cameras?[0];
        if (cam != null && _snapA != null && _snapB != null && _active && _externalT)
        {
            // Recargar texturas de blend
            BlendTextureManager.Load(cam, _snapA, _snapB, _snapOriginal, applyFade: false);
        }
        
        // Reaplicar blend
        if (_snapA != null && _snapB != null)
            ApplyBlend(currentT);
        
        // Sincronizar sky slots si es necesario
        if (BlendClock.IsRunning && _room != null)
        {
            SyncSkySlots(_room, BlendClock.StateA, BlendClock.StateB);
        }
    }
    
    /// <summary>
    /// Fuerza el refresco de los sky slots en la próxima actualización.
    /// </summary>
    public static void ForceRefreshSkySlots()
    {
        _forceSkyRefresh = true;
        RSPlugin.log.LogDebug("[SettingsBlendController] ForceRefreshSkySlots: marcado para próximo ciclo");
    }
    
    /// <summary>
    /// Procesa el refresco pendiente de sky slots.
    /// Debe llamarse desde el ciclo de actualización del juego.
    /// </summary>
    internal static void ProcessPendingSkyRefresh()
    {
        if (!_forceSkyRefresh) return;
        _forceSkyRefresh = false;
        
        if (_room != null && BlendClock.IsRunning)
        {
            RSPlugin.log.LogDebug("[SettingsBlendController] Procesando refresco de sky slots");
            SyncSkySlots(_room, BlendClock.StateA, BlendClock.StateB);
        }
    }
}