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

    private static AboveCloudsView.HorizonFog _cachedVanillaFog = null;

    // ============================================================
    // SLOTS DE BACKGROUND
    // ============================================================
    private static List<BackgroundScene.Simple2DBackgroundIllustration> _rcSlotsACV = null;
    private static List<BackgroundScene.Simple2DBackgroundIllustration> _rcSlotsRTV = null;
    private static List<BackgroundScene.Simple2DBackgroundIllustration> _rcSlotsPSV = null;
    private static List<BackgroundScene.Simple2DBackgroundIllustration> _rcSlotsPSVFog = null;
    private static List<BackgroundScene.Simple2DBackgroundIllustration> _rcSlotsPSVSun = null;

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
        On.Watcher.OuterRimView.ctor        += OnOuterRimViewCtor;
        On.Watcher.AncientUrbanView.ctor    += OnAncientUrbanViewCtor;
        On.RoomCamera.ChangeRoom            += OnChangeRoom;
        On.RoomCamera.Update                += OnRoomCameraUpdate;
        On.RoomCamera.LoadPalette           += OnLoadPalette;
        On.AboveCloudsView.HorizonFog.DrawSprites += OnHorizonFogDrawSprites;
        On.AboveCloudsView.DistantCloud.InitiateSprites += OnDistantCloudInitiateSprites;
    }

    public static void Terminate()
    {
        On.RoomCamera.ChangeBothPalettes    -= OnChangeBothPalettes;
        On.RoomCamera.ApplyFade             -= OnApplyFade;
        On.RoomCamera.ApplyPalette          -= OnApplyPalette;
        On.DevInterface.DevUI.Update        -= OnDevUIUpdate;
        On.RoomCamera.MoveCamera_Room_int   -= OnMoveCamera;
        On.RoofTopView.ctor                 -= OnRoofTopViewCtor;
        On.RoofTopView.Update               -= OnRoofTopViewUpdate;
        On.AboveCloudsView.Update           -= OnAboveCloudsViewUpdate;
        On.AboveCloudsView.ctor             -= OnAboveCloudsViewCtor;
        On.Watcher.OuterRimView.ctor        -= OnOuterRimViewCtor;
        On.Watcher.AncientUrbanView.ctor    -= OnAncientUrbanViewCtor;
        On.RoomCamera.ChangeRoom            -= OnChangeRoom;
        On.RoomCamera.Update                -= OnRoomCameraUpdate;
        On.RoomCamera.LoadPalette           -= OnLoadPalette;
        On.AboveCloudsView.HorizonFog.DrawSprites -= OnHorizonFogDrawSprites;
        On.AboveCloudsView.DistantCloud.InitiateSprites -= OnDistantCloudInitiateSprites;
    }

    // ============================================================
    // IS STATIC VIEW ROOM
    // ============================================================
    public static bool IsStaticViewRoom(Room room)
    {
        if (room == null) return false;
        var snap = SettingsSnapshot.GetCached(room.roomSettings?.filePath, room.abstractRoom?.name);
        return snap != null && snap.HasRcType && snap.RcType == RcType.Static;
    }

    // ============================================================
    // HOOK LOADPALETTE
    // ============================================================
    private static void OnLoadPalette(On.RoomCamera.orig_LoadPalette orig, RoomCamera self, int pal, ref Texture2D texture)
    {
        orig(self, pal, ref texture);
    }

    // ============================================================
    // FORCE LOAD PALETTE
    // ============================================================
    public static void ForceLoadPalette(RoomCamera cam, int palId, ref Texture2D texture)
    {
        cam.LoadPalette(palId, ref texture);
    }

    // ============================================================
    // APPLY IDLE TINTS AND EFFECTS
    // ============================================================
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

        room.ApplyRoomScalars(snap, snap, 0f);

        if (IsBlendRoom(room))
        {
            room.ApplyDecalOpacities(snap);
        }

        room.ApplyScalarEffects(snap, snap, 0f);

        var skyType = GetViewFromLoadedSettings(room);
        if (skyType != SkyType.None)
            ApplyRcSlotsAlpha(skyType, 0f, isBlending: false);
    }
    
    // ============================================================
    // UPDATE MANUAL STATES
    // ============================================================
    internal static void UpdateManualStates(int stateA, int stateB)
    {
        _manualStateA = stateA;
        _manualStateB = stateB;
    }
    
    // ============================================================
    // REFRESH ACTIVE SNAPSHOTS
    // ============================================================
    public static void RefreshActiveSnapshots()
    {
        if (!_active || _room == null) return;
        
        string roomName = _room.abstractRoom?.name;
        if (!string.IsNullOrEmpty(roomName))
        {
            if (_pathA != null)
                _snapA = SettingsSnapshot.GetCached(_pathA, roomName);
            if (_pathB != null)
                _snapB = SettingsSnapshot.GetCached(_pathB, roomName);
        }
        
        float currentT = _externalT ? _forcedT : (BlendClock.IsRunning ? BlendClock.SubPhaseLocalT : 0f);
        
        if (_snapA != null && _snapB != null)
            ApplyBlend(currentT);
        
        if (BlendClock.IsRunning && _room != null)
        {
            SyncSkySlots(_room, BlendClock.StateA, BlendClock.StateB);
        }
    }
    
    // ============================================================
    // FORCE REFRESH SKY SLOTS
    // ============================================================
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

    // ============================================================
    // APPLY STATIC TINTS
    // ============================================================
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
        
        var snap = SettingsSnapshot.GetCached(path, roomName);
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
    // IS BLEND ROOM
    // ============================================================
    public static bool IsBlendRoom(Room room)
    {
        if (room?.roomSettings?.filePath == null) return false;
        var snap = SettingsSnapshot.GetCached(room.roomSettings?.filePath, room.abstractRoom?.name);
        return snap != null && snap.HasRcType && snap.RcType == RcType.Blend;
    }

    // ============================================================
    // GET VIEW FROM LOADED SETTINGS
    // ============================================================
    private static SkyType GetViewFromLoadedSettings(Room room)
    {
        if (room?.roomSettings?.filePath == null) return SkyType.None;
        var snap = SettingsSnapshot.GetCached(room.roomSettings?.filePath, room.abstractRoom?.name);
        if (snap == null) return SkyType.None;
        
        return snap.ViewType == ViewType.ACV ? SkyType.ACV
            : snap.ViewType == ViewType.RTV ? SkyType.RTV
            : snap.ViewType == ViewType.PSV ? SkyType.PSV
            : SkyType.None;
    }

    // ============================================================
    // CLEAR CACHED FOG
    // ============================================================
    public static void ClearCachedVanillaFog()
    {
        _cachedVanillaFog = null;
    }
}