using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;
using MonoMod.RuntimeDetour;

namespace RainCycles.Core;

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
    
    private static Hook _setGlobalVectorHook;
    private static Hook _aboveCloudsViewUpdateHook;
    private static Hook _aboveCloudsViewAtmosphereColorSetterHook;
    private static Hook _roomCameraUpdateHook;
    private static Hook _aboveCloudsViewCtorHook;
    private static Hook _roofTopViewCtorHook;
    private static Hook _aboveCloudsViewOriginalUpdateHook;
    
    private static int _atmosphereColorID;
    private static int _multiplyColorID;
    
    // ============================================================
    // MODO BLOQUEO (solo para salas estáticas)
    // ============================================================
    private static bool _inStaticRoom = false;
    private static string _currentStaticRoom = null;
    private static Vector4 _lockedAtmosphere;
    private static bool _hasLockedAtmosphere = false;
    
    // ============================================================
    // CONDITIONAL WEAK TABLE - Estado original de vistas
    // ============================================================
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
        
        var acvOriginalUpdateMethod = acvType.GetMethod("Update", new Type[] { typeof(bool) });
        if (acvOriginalUpdateMethod != null)
        {
            _aboveCloudsViewOriginalUpdateHook = new Hook(acvOriginalUpdateMethod, 
                new Action<Action<AboveCloudsView, bool>, AboveCloudsView, bool>(OnAboveCloudsViewOriginalUpdate));
        }
        
        var roomCameraUpdateMethod = typeof(RoomCamera).GetMethod("Update");
        if (roomCameraUpdateMethod != null)
        {
            _roomCameraUpdateHook = new Hook(roomCameraUpdateMethod, 
                new Action<Action<RoomCamera>, RoomCamera>(OnRoomCameraUpdate));
        }
        
        On.OverWorld.Update += OnOverWorldUpdate;
        
        _initialized = true;
    }
    
    public static void Terminate()
    {
        if (!_initialized) return;
        
        _setGlobalVectorHook?.Dispose();
        _aboveCloudsViewUpdateHook?.Dispose();
        _aboveCloudsViewAtmosphereColorSetterHook?.Dispose();
        _roomCameraUpdateHook?.Dispose();
        _aboveCloudsViewCtorHook?.Dispose();
        _roofTopViewCtorHook?.Dispose();
        _aboveCloudsViewOriginalUpdateHook?.Dispose();
        On.OverWorld.Update -= OnOverWorldUpdate;
        
        _initialized = false;
    }
    
    public static void ResetStaticState()
    {
        _inStaticRoom = false;
        _currentStaticRoom = null;
        _hasLockedAtmosphere = false;
        _lockedAtmosphere = default;
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
        
        var originalState = new ViewOriginalState(originalAtmo, originalMult);
        _originalViewStates.Add(self, originalState);
        
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
        
        var originalState = new ViewOriginalState(originalAtmo, originalMult);
        _originalViewStates.Add(self, originalState);
        
        Shader.SetGlobalVector(_atmosphereColorID, currentAtmo);
        Shader.SetGlobalVector(_multiplyColorID, currentMult);
    }
    
    // ============================================================
    // HOOK AL SETTER DE ATMOSPHERECOLOR
    // ============================================================
    private static void OnSetAtmosphereColor(Action<AboveCloudsView, Color> orig, AboveCloudsView self, Color value)
    {
        string roomName = self.room?.abstractRoom?.name;
        bool isStatic = roomName != null && StaticTintManager.IsStaticViewRoom(self.room);
        
        ViewOriginalState originalState = null;
        bool hasOriginal = _originalViewStates.TryGetValue(self, out originalState);
        
        bool isOriginalColor = hasOriginal && originalState.hasAtmosphere &&
                               Mathf.Approximately(value.r, originalState.atmosphereColor.r) &&
                               Mathf.Approximately(value.g, originalState.atmosphereColor.g) &&
                               Mathf.Approximately(value.b, originalState.atmosphereColor.b);
        
        if (isStatic && isOriginalColor && _hasLockedAtmosphere)
        {
            Color lockedColor = new Color(_lockedAtmosphere.x, _lockedAtmosphere.y, _lockedAtmosphere.z);
            orig(self, lockedColor);
            return;
        }
        
        if (isStatic && !isOriginalColor && hasOriginal)
        {
            _lockedAtmosphere = new Vector4(value.r, value.g, value.b, 1f);
            _hasLockedAtmosphere = true;
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
    // HOOK PARA DEBUG - ABOVECLOUDSVIEW.UPDATE ORIGINAL
    // ============================================================
    private static void OnAboveCloudsViewOriginalUpdate(Action<AboveCloudsView, bool> orig, AboveCloudsView self, bool eu)
    {
        orig(self, eu);
    }
    
    // ============================================================
    // VERIFICAR SI SALA TIENE PINKSKY
    // ============================================================
    private static bool RoomHasPinkSky(Room room)
    {
        if (room == null) return false;
        
        for (int i = 0; i < room.updateList.Count; i++)
        {
            if (room.updateList[i] is AboveCloudsView acv && acv.PinkSky)
                return true;
        }
        return false;
    }
    
    // ============================================================
    // RESTAURAR ESTADO ORIGINAL DE UNA VISTA
    // ============================================================
    private static void RestoreOriginalViewState(Room room)
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
    // ROOMCAMERA.UPDATE - DETECCIÓN DE ENTRADA/SALIDA
    // ============================================================
    private static void OnRoomCameraUpdate(Action<RoomCamera> orig, RoomCamera self)
    {
        orig(self);
        
        if (self?.room == null) return;
        
        string roomName = self.room.abstractRoom?.name;
        bool isStatic = StaticTintManager.IsStaticViewRoom(self.room);
        bool isBlend = SettingsBlendController.IsBlendRoom(self.room);
        bool hasPinkSky = RoomHasPinkSky(self.room);
        
        // ============================================================
        // ENTRADA A SALA ESTÁTICA
        // ============================================================
        if (isStatic && !_inStaticRoom)
        {
            _inStaticRoom = true;
            _currentStaticRoom = roomName;
            _hasLockedAtmosphere = false;
            
            SettingsBlendController.ApplyStaticTints(self.room);
        }
        // ============================================================
        // SALIDA DE SALA ESTÁTICA
        // ============================================================
        else if (!isStatic && _inStaticRoom)
        {
            RestoreOriginalViewState(self.room);
            
            _inStaticRoom = false;
            _hasLockedAtmosphere = false;
            _currentStaticRoom = null;
        }
        
        // ============================================================
        // SALA VANILLA
        // ============================================================
        if (!isStatic && !isBlend)
        {
            RestoreOriginalViewState(self.room);
        }
    }
}