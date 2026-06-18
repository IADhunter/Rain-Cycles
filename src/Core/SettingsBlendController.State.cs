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

    private static int _manualStateA = 1;
    private static int _manualStateB = 2;
    private static bool _isAutoBlend = false;

    private static bool  _detachedThisFrame         = false;
    private static bool  _moveCameraThisFrame        = false;

    private static readonly HashSet<string> _rcViewInjected = new HashSet<string>();

    private static bool _lastRoomWasManaged = false;
    private static string _lastManagedRoomName = null;

    private static RoofTopView     _rtvScene = null;
    private static AboveCloudsView _acvScene = null;
    private static AboveCloudsView _psvScene = null;

    // ============================================================
    // SLOTS DE BACKGROUND - 4 slots directos por estado (1-4)
    // Orden de creación INVERSO: slot3→slot2→slot1→slot0
    // Renderizado: slot3(detras) → slot2 → slot1 → slot0(encima)
    // slot0 = estado 1 (bkg01), slot3 = estado 4 (bkg04)
    // ============================================================
    private static List<BackgroundScene.Simple2DBackgroundIllustration> _rcSlotsACV = null;
    private static List<BackgroundScene.Simple2DBackgroundIllustration> _rcSlotsRTV = null;
    private static List<BackgroundScene.Simple2DBackgroundIllustration> _rcSlotsPSV = null;
    private static List<BackgroundScene.Simple2DBackgroundIllustration> _rcSlotsPSVFog = null;
    private static List<BackgroundScene.Simple2DBackgroundIllustration> _rcSlotsPSVSun = null;

    // ============================================================
    // SLOTS ESTÁTICOS (1 slot por vista, no 4)
    // ============================================================
    private static List<BackgroundScene.Simple2DBackgroundIllustration> _rcSlotsStaticACV = null;
    private static List<BackgroundScene.Simple2DBackgroundIllustration> _rcSlotsStaticRTV = null;
    private static List<BackgroundScene.Simple2DBackgroundIllustration> _rcSlotsStaticPSV = null;

    private static float _entryFrameT = -1f;

    private static SettingsSnapshot _activeSnapshot = null;

    private static bool _forceSkyRefresh = false;

    public static bool IsExternalT => _externalT;
    public static bool IsAutoBlend => _isAutoBlend;
    public static int ManualStateA => _manualStateA;
    public static int ManualStateB => _manualStateB;
    public static SettingsSnapshot ManualSnapA => _snapA;
    public static SettingsSnapshot ManualSnapB => _snapB;

    public static bool IsActive            => _active;
    public static bool DetachedThisFrame   => _detachedThisFrame;
    public static bool MoveCameraThisFrame => _moveCameraThisFrame;
    public static string           CurrentPathA        => _pathA;
    public static string           CurrentPathB        => _pathB;
    public static Room             ActiveRoom          => _room;
    public static float            ForcedT             => _forcedT;
    public static SettingsSnapshot ActiveSnapshot      => _activeSnapshot;
    public static void SetActiveSnapshot(SettingsSnapshot snap) => _activeSnapshot = snap;
    public static void ClearActiveSnapshot() => _activeSnapshot = null;

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
        
        // Hook para LoadPalette - necesario para forzar recarga en salas NO blend
        On.RoomCamera.LoadPalette           += OnLoadPalette;
    }

    // ============================================================
    // HOOK PARA LOADPALETTE - Solo para poder llamarlo externamente
    // ============================================================
    private static void OnLoadPalette(On.RoomCamera.orig_LoadPalette orig, RoomCamera self, int pal, ref Texture2D texture)
    {
        orig(self, pal, ref texture);
    }

    // ============================================================
    // MÉTODO AUXILIAR PARA FORZAR RECARGA DE PALETA
    // ============================================================
    public static void ForceLoadPalette(RoomCamera cam, int palId, ref Texture2D texture)
    {
        cam.LoadPalette(palId, ref texture);
    }

    public static void ApplyIdleTintsAndEffects(Room room, SettingsSnapshot snap)
    {
        if (room == null || snap == null) return;

        if (snap.TintMultiply.HasValue)
        {
            var c = snap.TintMultiply.Value;
            Shader.SetGlobalVector(RainWorld.ShadPropMultiplyColor, new Vector4(c.r, c.g, c.b, 1f));
        }
        if (snap.TintAtmosphere.HasValue)
        {
            var c = snap.TintAtmosphere.Value;
            Shader.SetGlobalVector(RainWorld.ShadPropAboveCloudsAtmosphereColor, new Vector4(c.r, c.g, c.b, 1f));
            
            if (room != null)
            {
                for (int i = 0; i < room.updateList.Count; i++)
                {
                    if (room.updateList[i] is AboveCloudsView acv)
                    {
                        acv.atmosphereColor = c;
                        break;
                    }
                }
            }
        }

        var rs = room.roomSettings;
        rs.Grime = snap.Grime;
        if (!RoomHasWeatherController(room))
            rs.Clouds = snap.Clouds;
        rs.CeilingDrips = snap.CeilingDrips;
        rs.BkgDroneVolume = snap.BkgDroneVolume;
        rs.RandomItemDensity = snap.RandomItemDensity;
        rs.RandomItemSpearChance = snap.RandomItemSpearChance;
        rs.WaterReflectionAlpha = snap.WaterReflectionAlpha;

        RoomEffectsApplier.ApplyDecalOpacities(room, snap);
        RoomEffectsApplier.ApplyScalarEffects(room, snap);
        RoomEffectsApplier.ApplyTerrainScalars(room, snap);

        var skyType = GetViewFromLoadedSettings(room);
        if (skyType != SkyType.None)
            ApplyRcSlotsAlpha(skyType, 0f, isBlending: false);
    }
    
    internal static void UpdateManualStates(int stateA, int stateB)
    {
        _manualStateA = stateA;
        _manualStateB = stateB;
    }
    
    public static void RefreshActiveSnapshots()
    {
        if (!_active || _room == null) return;
        
        string roomName = _room.abstractRoom?.name;
        if (!string.IsNullOrEmpty(roomName))
        {
            if (_pathA != null)
                _snapA = StaticTintManager.GetCachedSnapshot(_pathA, roomName);
            if (_pathB != null)
                _snapB = StaticTintManager.GetCachedSnapshot(_pathB, roomName);
        }
        
        float currentT = _externalT ? _forcedT : (BlendClock.IsRunning ? BlendClock.SubPhaseLocalT : 0f);
        
        var cam = _room.game?.cameras?[0];
        if (cam != null && _snapA != null && _snapB != null && _active && _externalT)
        {
            BlendTextureManager.Load(cam, _snapA, _snapB, _snapOriginal, applyFade: false);
        }
        
        if (_snapA != null && _snapB != null)
            ApplyBlend(currentT);
        
        if (BlendClock.IsRunning && _room != null)
        {
            SyncSkySlots(_room, BlendClock.StateA, BlendClock.StateB);
        }
    }
    
    public static void ForceRefreshSkySlots()
    {
        _forceSkyRefresh = true;
    }
    
    internal static void ProcessPendingSkyRefresh()
    {
        if (!_forceSkyRefresh) return;
        _forceSkyRefresh = false;
        
        if (_room != null && BlendClock.IsRunning)
        {
            SyncSkySlots(_room, BlendClock.StateA, BlendClock.StateB);
        }
    }

    public static void ApplyStaticTints(Room room)
    {
        if (room == null) return;
        
        string roomName = room.abstractRoom?.name;
        if (string.IsNullOrEmpty(roomName)) return;
        
        int state = StateFileResolver.GetCurrentCycleState();
        if (state < 1 || state > 4) state = 1;
        
        string path = StateFileResolver.GetRainStateSettingsFile(roomName, state);
        if (string.IsNullOrEmpty(path))
            return;
        
        var snap = StaticTintManager.GetCachedSnapshot(path, roomName);
        if (snap == null)
            return;
        
        if (snap.TintMultiply.HasValue)
        {
            var c = snap.TintMultiply.Value;
            Shader.SetGlobalVector(RainWorld.ShadPropMultiplyColor, new Vector4(c.r, c.g, c.b, 1f));
        }
        
        if (snap.TintAtmosphere.HasValue)
        {
            var c = snap.TintAtmosphere.Value;
            Shader.SetGlobalVector(RainWorld.ShadPropAboveCloudsAtmosphereColor, new Vector4(c.r, c.g, c.b, 1f));
            
            for (int i = 0; i < room.updateList.Count; i++)
            {
                if (room.updateList[i] is AboveCloudsView acv)
                {
                    acv.atmosphereColor = c;
                    break;
                }
            }
        }
    }

    // ============================================================
    // MÉTODOS PÚBLICOS PARA OTROS ARCHIVOS
    // ============================================================

    public static bool IsBlendRoom(Room room)
    {
        if (room?.roomSettings?.filePath == null) return false;
        var snap = StaticTintManager.GetCachedSnapshot(room);
        return snap != null && snap.HasRcType && snap.RcType == RcType.Blend;
    }

    private static SkyType GetViewFromLoadedSettings(Room room)
    {
        if (room?.roomSettings?.filePath == null) return SkyType.None;
        var snap = StaticTintManager.GetCachedSnapshot(room);
        if (snap == null) return SkyType.None;
        
        return snap.ViewType == ViewType.ACV ? SkyType.ACV
            : snap.ViewType == ViewType.RTV ? SkyType.RTV
            : snap.ViewType == ViewType.PSV ? SkyType.PSV
            : SkyType.None;
    }
}