using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;
using MonoMod.RuntimeDetour;
using Watcher;
using RainCycles.Blend;

namespace RainCycles.Core;

public struct TintLerpResult
{
    public Color? TintMultiply;
    public Color? TintAtmosphere;
}

public class ViewOriginalState
{
    public Color atmosphereColor;
    public Color multiplyColor;
    public bool hasAtmosphere;
    public bool hasMultiply;
    
    public ViewOriginalState(Color atmo, Color mult)
    {
        atmosphereColor = atmo;
        multiplyColor = mult;
        hasAtmosphere = true;
        hasMultiply = true;
    }
    
    public ViewOriginalState()
    {
        hasAtmosphere = false;
        hasMultiply = false;
    }
}

public static class TintManager
{
    private static bool _initialized = false;

#pragma warning disable CS0414
    private static Hook _setGlobalVectorHook;
    private static Hook _aboveCloudsViewUpdateHook;
    private static Hook _aboveCloudsViewAtmosphereColorSetterHook;
    private static Hook _roomCameraUpdateHook;
    private static Hook _aboveCloudsViewCtorHook;
    private static Hook _roofTopViewCtorHook;
#pragma warning restore CS0414
    
    private static int _atmosphereColorID;
    private static int _multiplyColorID;
    
    private static bool _inStaticRoom = false;
    private static string _currentStaticRoom = null;
    private static Vector4 _lockedAtmosphere;
    private static bool _hasLockedAtmosphere = false;
    
    private static ConditionalWeakTable<BackgroundScene, ViewOriginalState> _originalViewStates 
        = new ConditionalWeakTable<BackgroundScene, ViewOriginalState>();
    
    // ============================================================
    // INICIALIZACIÓN
    // ============================================================
    public static void Init()
    {
        if (_initialized) return;
        
        _atmosphereColorID = Shader.PropertyToID("_AboveCloudsAtmosphereColor");
        _multiplyColorID = Shader.PropertyToID("_MultiplyColor");
        
        var setGlobalVectorMethod = typeof(Shader).GetMethod("SetGlobalVector", new Type[] { typeof(int), typeof(Vector4) });
        if (setGlobalVectorMethod != null)
        {
            _setGlobalVectorHook = new Hook(setGlobalVectorMethod, 
                new Action<Action<int, Vector4>, int, Vector4>(OnSetGlobalVector));
        }
        
        var acvType = typeof(AboveCloudsView);
        var atmosphereColorProperty = acvType.GetProperty("atmosphereColor");
        if (atmosphereColorProperty != null)
        {
            var setMethod = atmosphereColorProperty.GetSetMethod();
            if (setMethod != null)
            {
                _aboveCloudsViewAtmosphereColorSetterHook = new Hook(setMethod, 
                    new Action<Action<AboveCloudsView, Color>, AboveCloudsView, Color>(OnSetAtmosphereColor));
            }
        }
        
        var acvUpdateMethod = acvType.GetMethod("Update", new Type[] { typeof(bool) });
        if (acvUpdateMethod != null)
        {
            _aboveCloudsViewUpdateHook = new Hook(acvUpdateMethod, 
                new Action<Action<AboveCloudsView, bool>, AboveCloudsView, bool>(OnAboveCloudsViewUpdate));
        }
        
        var acvCtor = acvType.GetConstructor(new Type[] { typeof(Room), typeof(RoomSettings.RoomEffect) });
        if (acvCtor != null)
        {
            _aboveCloudsViewCtorHook = new Hook(acvCtor, 
                new Action<Action<AboveCloudsView, Room, RoomSettings.RoomEffect>, AboveCloudsView, Room, RoomSettings.RoomEffect>(OnAboveCloudsViewCtor));
        }
        
        var rtvType = typeof(RoofTopView);
        var rtvCtor = rtvType.GetConstructor(new Type[] { typeof(Room), typeof(RoomSettings.RoomEffect) });
        if (rtvCtor != null)
        {
            _roofTopViewCtorHook = new Hook(rtvCtor, 
                new Action<Action<RoofTopView, Room, RoomSettings.RoomEffect>, RoofTopView, Room, RoomSettings.RoomEffect>(OnRoofTopViewCtor));
        }
        
        
        var roomCameraUpdateMethod = typeof(RoomCamera).GetMethod("Update");
        if (roomCameraUpdateMethod != null)
        {
            _roomCameraUpdateHook = new Hook(roomCameraUpdateMethod, 
                new Action<Action<RoomCamera>, RoomCamera>(OnRoomCameraUpdate));
        }
        
        On.OverWorld.Update += OnOverWorldUpdate;

        On.Watcher.OuterRimView.ctor += OnOuterRimViewCtor;

        On.Watcher.AncientUrbanView.ctor += OnAncientUrbanViewCtor;
        
        _initialized = true;
    }
    
    public static void ResetStaticState()
    {
        _inStaticRoom = false;
        _currentStaticRoom = null;
        _hasLockedAtmosphere = false;
        _lockedAtmosphere = default;
    }
    
    // ============================================================
    // GUARDAR ESTADO ORIGINAL DE UNA VISTA
    // ============================================================
    public static void SaveOriginalViewStateDirect(BackgroundScene scene, Color atmosphere, Color multiply)
    {
        if (scene == null) return;
        
        if (_originalViewStates.TryGetValue(scene, out _))
            return;
        
        var originalState = new ViewOriginalState(atmosphere, multiply);
        _originalViewStates.Add(scene, originalState);
    }
    
    // ============================================================
    // DETECCIÓN DE CAMBIO DE REGIÓN
    // ============================================================
    private static string _lastLoggedRegion = null;
    
    private static void OnOverWorldUpdate(On.OverWorld.orig_Update orig, OverWorld self)
    {
        orig(self);
        
        string currentRegion = self.activeWorld?.region?.name?.ToUpperInvariant();
        if (currentRegion != null && currentRegion != _lastLoggedRegion)
        {
            _lastLoggedRegion = currentRegion;
        }
    }
    
    // ============================================================
    // GUARDAR ESTADO ORIGINAL DE ACV
    // ============================================================
    private static void OnAboveCloudsViewCtor(Action<AboveCloudsView, Room, RoomSettings.RoomEffect> orig, AboveCloudsView self, Room room, RoomSettings.RoomEffect effect)
    {
        Color currentAtmo = Shader.GetGlobalVector(_atmosphereColorID);
        Color currentMult = Shader.GetGlobalVector(_multiplyColorID);
        
        orig(self, room, effect);
        
        Color originalAtmo = self.atmosphereColor;
        Color originalMult = Shader.GetGlobalVector(_multiplyColorID);
        
        if (!_originalViewStates.TryGetValue(self, out _))
        {
            var originalState = new ViewOriginalState(originalAtmo, originalMult);
            _originalViewStates.Add(self, originalState);
        }
        
        Shader.SetGlobalVector(_atmosphereColorID, currentAtmo);
        Shader.SetGlobalVector(_multiplyColorID, currentMult);
    }
    
    // ============================================================
    // GUARDAR ESTADO ORIGINAL DE RTV
    // ============================================================
    private static void OnRoofTopViewCtor(Action<RoofTopView, Room, RoomSettings.RoomEffect> orig, RoofTopView self, Room room, RoomSettings.RoomEffect effect)
    {
        Color currentAtmo = Shader.GetGlobalVector(_atmosphereColorID);
        Color currentMult = Shader.GetGlobalVector(_multiplyColorID);
        
        orig(self, room, effect);
        
        Color originalAtmo = Shader.GetGlobalVector(_atmosphereColorID);
        Color originalMult = Shader.GetGlobalVector(_multiplyColorID);
        
        if (!_originalViewStates.TryGetValue(self, out _))
        {
            var originalState = new ViewOriginalState(originalAtmo, originalMult);
            _originalViewStates.Add(self, originalState);
        }
        
        Shader.SetGlobalVector(_atmosphereColorID, currentAtmo);
        Shader.SetGlobalVector(_multiplyColorID, currentMult);
    }
    
    // ============================================================
    // GUARDAR ESTADO ORIGINAL DE OUTERRIMVIEW (Watcher)
    // ============================================================
    private static void OnOuterRimViewCtor(On.Watcher.OuterRimView.orig_ctor orig, Watcher.OuterRimView self, Room room, RoomSettings.RoomEffect effect)
    {
        Color currentAtmo = Shader.GetGlobalVector(_atmosphereColorID);
        Color currentMult = Shader.GetGlobalVector(_multiplyColorID);
        
        orig(self, room, effect);
        
        Color originalAtmo = Shader.GetGlobalVector(_atmosphereColorID);
        Color originalMult = Shader.GetGlobalVector(_multiplyColorID);
        
        if (!_originalViewStates.TryGetValue(self, out _))
        {
            var originalState = new ViewOriginalState(originalAtmo, originalMult);
            _originalViewStates.Add(self, originalState);
        }
        
        Shader.SetGlobalVector(_atmosphereColorID, currentAtmo);
        Shader.SetGlobalVector(_multiplyColorID, currentMult);
    }
    
    // ============================================================
    // GUARDAR ESTADO ORIGINAL DE ANCIENTURBANVIEW (Watcher)
    // ============================================================
    private static void OnAncientUrbanViewCtor(On.Watcher.AncientUrbanView.orig_ctor orig, Watcher.AncientUrbanView self, Room room, RoomSettings.RoomEffect effect)
    {
        Color currentAtmo = Shader.GetGlobalVector(_atmosphereColorID);
        Color currentMult = Shader.GetGlobalVector(_multiplyColorID);
        
        orig(self, room, effect);
        
        Color originalAtmo = Shader.GetGlobalVector(_atmosphereColorID);
        Color originalMult = Shader.GetGlobalVector(_multiplyColorID);
        
        if (!_originalViewStates.TryGetValue(self, out _))
        {
            var originalState = new ViewOriginalState(originalAtmo, originalMult);
            _originalViewStates.Add(self, originalState);
        }
        
        Shader.SetGlobalVector(_atmosphereColorID, currentAtmo);
        Shader.SetGlobalVector(_multiplyColorID, currentMult);
    }
    
    // ============================================================
    // HOOK AL SETTER DE ATMOSPHERECOLOR
    // ============================================================
    private static void OnSetAtmosphereColor(Action<AboveCloudsView, Color> orig, AboveCloudsView self, Color value)
    {
        string roomName = self.room?.abstractRoom?.name;
        bool isStatic = roomName != null && SettingsBlendController.IsStaticViewRoom(self.room);
        
        ViewOriginalState originalState = null;
        bool hasOriginal = _originalViewStates.TryGetValue(self, out originalState);
        
        bool isOriginalColor = hasOriginal && originalState.hasAtmosphere &&
                               Mathf.Approximately(value.r, originalState.atmosphereColor.r) &&
                               Mathf.Approximately(value.g, originalState.atmosphereColor.g) &&
                               Mathf.Approximately(value.b, originalState.atmosphereColor.b);
        
        if (isStatic && isOriginalColor && _hasLockedAtmosphere)
        {
            Color lockedColor = new Color(_lockedAtmosphere.x, _lockedAtmosphere.y, _lockedAtmosphere.z);
            RSPlugin.log.LogDebug($"[RC][TintManager] REDIRECT atmosphere: room={roomName} orig=({value.r:F2},{value.g:F2},{value.b:F2}) -> locked=({lockedColor.r:F2},{lockedColor.g:F2},{lockedColor.b:F2})");
            orig(self, lockedColor);
            return;
        }
        
        if (isStatic && !isOriginalColor && hasOriginal)
        {
            _lockedAtmosphere = new Vector4(value.r, value.g, value.b, 1f);
            _hasLockedAtmosphere = true;
            RSPlugin.log.LogDebug($"[RC][TintManager] LOCK atmosphere: room={roomName} value=({value.r:F2},{value.g:F2},{value.b:F2})");
        }
        
        orig(self, value);
    }
    
    // ============================================================
    // HOOK PRINCIPAL - SHADER.SETGLOBALVECTOR
    // ============================================================
    private static void OnSetGlobalVector(Action<int, Vector4> orig, int nameID, Vector4 value)
    {
        if (_inStaticRoom && nameID == _atmosphereColorID && _hasLockedAtmosphere)
        {
            RSPlugin.log.LogDebug($"[RC][TintManager] REDIRECT global atmosphere: locked=({_lockedAtmosphere.x:F2},{_lockedAtmosphere.y:F2},{_lockedAtmosphere.z:F2}) was=({value.x:F2},{value.y:F2},{value.z:F2})");
            orig(nameID, _lockedAtmosphere);
            return;
        }
        
        orig(nameID, value);
    }
    
    // ============================================================
    // HOOK - ABOVECLOUDSVIEW.UPDATE (REFORZAR COLORES)
    // ============================================================
    private static void OnAboveCloudsViewUpdate(Action<AboveCloudsView, bool> orig, AboveCloudsView self, bool eu)
    {
        orig(self, eu);
        
        if (_inStaticRoom && _hasLockedAtmosphere && self.atmosphereColor != new Color(_lockedAtmosphere.x, _lockedAtmosphere.y, _lockedAtmosphere.z))
        {
            self.atmosphereColor = new Color(_lockedAtmosphere.x, _lockedAtmosphere.y, _lockedAtmosphere.z);
        }
    }
    
    // ============================================================
    // RESTAURAR ESTADO ORIGINAL DE UNA VISTA - PÚBLICO
    // ============================================================
    public static void RestoreOriginalViewState(Room room)
    {
        if (room == null) return;
        
        for (int i = 0; i < room.updateList.Count; i++)
        {
            var scene = room.updateList[i] as BackgroundScene;
            if (scene == null) continue;
            
            if (_originalViewStates.TryGetValue(scene, out ViewOriginalState originalState))
            {
                if (originalState.hasMultiply)
                {
                    Vector4 multVec = new Vector4(originalState.multiplyColor.r, originalState.multiplyColor.g, originalState.multiplyColor.b, 1f);
                    Shader.SetGlobalVector(_multiplyColorID, multVec);
                }
                
                if (originalState.hasAtmosphere)
                {
                    Vector4 atmoVec = new Vector4(originalState.atmosphereColor.r, originalState.atmosphereColor.g, originalState.atmosphereColor.b, 1f);
                    Shader.SetGlobalVector(_atmosphereColorID, atmoVec);
                    
                    if (scene is AboveCloudsView acv)
                    {
                        acv.atmosphereColor = originalState.atmosphereColor;
                    }
                }
                return;
            }
        }
    }
    
    // ============================================================
    // OBTENER COLORES ORIGINALES VANILLA DE UNA SALA
    // ============================================================
    public static bool TryGetOriginalColors(Room room, out Color multiply, out Color atmosphere)
    {
        multiply = Color.white;
        atmosphere = Color.white;
        
        if (room == null) return false;
        
        for (int i = 0; i < room.updateList.Count; i++)
        {
            var scene = room.updateList[i] as BackgroundScene;
            if (scene == null) continue;
            
            if (_originalViewStates.TryGetValue(scene, out ViewOriginalState state))
            {
                multiply = state.multiplyColor;
                atmosphere = state.atmosphereColor;
                return true;
            }
        }
        return false;
    }
    
    // ============================================================
    // ROOMCAMERA.UPDATE - MANEJA ENTRADA/SALIDA DE SALAS
    // ============================================================
    private static string _lastRoomName = null;
    private static bool _wasStaticRoom = false;
    
    private static void OnRoomCameraUpdate(Action<RoomCamera> orig, RoomCamera self)
    {
        orig(self);
        
        if (self?.room == null) return;
        
        string roomName = self.room.abstractRoom?.name;
        var state = RoomCameraExtensions.GetRoomBlendState(self.room);
        bool isStatic = state.IsStatic;
        bool isBlend = state.IsBlend;
        bool hasTint = state.HasTint;
        bool roomChanged = (roomName != _lastRoomName);
        _lastRoomName = roomName;

        if (roomChanged)
        {
            RSPlugin.log.LogDebug($"[RC][TintManager] RoomCameraUpdate: room={roomName} isStatic={isStatic} isBlend={isBlend} hasTint={hasTint} prevRoom={_lastRoomName}");
        }

        if (isStatic && !_inStaticRoom && roomChanged)
        {
            _inStaticRoom = true;
            _currentStaticRoom = roomName;
            _hasLockedAtmosphere = false;
            
            RSPlugin.log.LogDebug($"[RC][TintManager] ENTERING STATIC ROOM: {roomName} hasTint={hasTint}");
            
            if (hasTint)
            {
                SettingsBlendController.ApplyStaticTints(self.room);
            }
        }
        else if (!isStatic && _inStaticRoom && roomChanged)
        {
            RSPlugin.log.LogDebug($"[RC][TintManager] LEAVING STATIC ROOM: {roomName}");
            _inStaticRoom = false;
            _hasLockedAtmosphere = false;
            _currentStaticRoom = null;
        }
        
        if (roomChanged)
        {
            if (!isStatic && !isBlend)
            {
                RestoreOriginalViewState(self.room);
            }
            else if (!isStatic && isBlend && !hasTint)
            {
                RestoreOriginalViewState(self.room);
            }
        }
        
        if (!isStatic && _wasStaticRoom && roomChanged)
        {
            _wasStaticRoom = false;
        }
        _wasStaticRoom = isStatic;

        // Última defensa: si attach/reloj/flags no escribieron el tinte,
        // re-aplicarlo aquí cada frame (patrón Region Kit / PausedUpdate).
        EnforceManagedTints(self.room);
    }

    // ============================================================
    // ENFORCE — ÚLTIMA DEFENSA DE TINTES MANAGED
    // ============================================================
    // Independiente de: _inStaticRoom, _lastRoomWasManaged, fase del
    // reloj, ni attach. Solo se retira si el blend engine ya gestiona
    // ESTA sala (ApplyBlend es el dueño legítimo en ese caso).
    // ============================================================
    private static string _lastEnforceLogKey = null;

    public static void EnforceManagedTints(Room room)
    {
        if (room == null) return;

        string roomName = room.abstractRoom?.name;
        if (string.IsNullOrEmpty(roomName)) return;

        var state = RoomCameraExtensions.GetRoomBlendState(room);
        if (!state.IsBlend && !state.IsStatic) return;
        if (!state.HasTint) return;

        // Anti-pelea: attach OK para esta sala → ApplyIdleTints/ApplyBlend ya escribe.
        if (SettingsBlendController.IsActive && SettingsBlendController.ActiveRoom == room)
            return;

        // EditMode: el RCPanel es dueño de los tintes (botón/slider).
        // Enforce aquí pisaría el estado seleccionado con el del ciclo.
        if (BlendClock.EditMode) return;

        int cycleState = StateFileResolver.GetCurrentCycleState();
        if (cycleState < 1 || cycleState > 4) cycleState = 1;

        int resolveState;
        if (state.IsStatic)
        {
            resolveState = cycleState;
        }
        else
        {
            resolveState = (BlendClock.IsRunning && BlendClock.StateA >= 1 && BlendClock.StateA <= 4)
                ? BlendClock.StateA
                : cycleState;
        }

        string path = StateFileResolver.ResolveSettingsPath(roomName, resolveState);
        if (string.IsNullOrEmpty(path)) return;

        var snap = SettingsSnapshot.GetCached(path, roomName);
        if (snap == null) return;
        if (!snap.TintMultiply.HasValue && !snap.TintAtmosphere.HasValue) return;

        // Static: refrescar el lock ANTES de escribir el global, o
        // OnSetGlobalVector redirigiría al color bloqueado viejo.
        if (state.IsStatic)
        {
            _inStaticRoom = true;
            _currentStaticRoom = roomName;
        }

        if (snap.TintMultiply.HasValue)
        {
            var c = snap.TintMultiply.Value;
            var desired = new Vector4(c.r, c.g, c.b, 1f);
            if (!Vector4Approx(Shader.GetGlobalVector(RainWorld.ShadPropMultiplyColor), desired))
            {
                Shader.SetGlobalVector(RainWorld.ShadPropMultiplyColor, desired);
                LogEnforce(roomName, "mult", c, resolveState, state.IsStatic);
            }
        }

        if (snap.TintAtmosphere.HasValue)
        {
            var c = snap.TintAtmosphere.Value;
            var desired = new Vector4(c.r, c.g, c.b, 1f);

            if (state.IsStatic)
            {
                _lockedAtmosphere = desired;
                _hasLockedAtmosphere = true;
            }

            bool changed = false;

            // Campo de la vista PRIMERO (setter pasa por OnSetAtmosphereColor
            // y puede actualizar el lock), luego el global.
            for (int i = 0; i < room.updateList.Count; i++)
            {
                if (room.updateList[i] is AboveCloudsView acv)
                {
                    if (!Vector4Approx((Vector4)acv.atmosphereColor, desired))
                    {
                        acv.atmosphereColor = c;
                        changed = true;
                    }
                    break;
                }
            }

            if (!Vector4Approx(Shader.GetGlobalVector(RainWorld.ShadPropAboveCloudsAtmosphereColor), desired))
            {
                Shader.SetGlobalVector(RainWorld.ShadPropAboveCloudsAtmosphereColor, desired);
                changed = true;
            }

            if (changed)
                LogEnforce(roomName, "atmo", c, resolveState, state.IsStatic);
        }
    }

    private static bool Vector4Approx(Vector4 a, Vector4 b)
    {
        return (a - b).sqrMagnitude < 1e-6f;
    }

    private static void LogEnforce(string roomName, string which, Color c, int state, bool isStatic)
    {
        // Solo loguea cuando (sala, canal, color, estado) cambia — sin spam por frame.
        string key = $"{roomName}|{which}|{c.r:F3}|{c.g:F3}|{c.b:F3}|{state}";
        if (key == _lastEnforceLogKey) return;
        _lastEnforceLogKey = key;
        RSPlugin.log.LogDebug(
            $"[RC][TintManager] ENFORCE {which}: room={roomName} state={state} " +
            $"static={isStatic} color=({c.r:F2},{c.g:F2},{c.b:F2})");
    }

    // ================================================================
    // INTERPOLACIÓN DE TINTES (migrado desde SettingsSnapshotLerp)
    // ================================================================
    public static TintLerpResult InterpolateTints(SettingsSnapshot a, SettingsSnapshot b, float t, Color? vanillaMultiply = null, Color? vanillaAtmosphere = null)
    {
        t = Mathf.Clamp01(t);

        Color aMultiply = a.TintMultiply ?? vanillaMultiply ?? Color.white;
        Color bMultiply = b.TintMultiply ?? vanillaMultiply ?? Color.white;
        Color aAtmosphere = a.TintAtmosphere ?? vanillaAtmosphere ?? Color.white;
        Color bAtmosphere = b.TintAtmosphere ?? vanillaAtmosphere ?? Color.white;

        var result = new TintLerpResult();
        result.TintMultiply = Color.Lerp(aMultiply, bMultiply, t);
        result.TintAtmosphere = Color.Lerp(aAtmosphere, bAtmosphere, t);

        return result;
    }
}