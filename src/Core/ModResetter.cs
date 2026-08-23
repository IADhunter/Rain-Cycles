using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using RainCycles.Settings;
using RainCycles.Clock;
using RainCycles.Blend;
using RainCycles.Sky;
using RainCycles.Snapshot;

namespace RainCycles.Core;

// ============================================================
// SISTEMA CENTRALIZADO DE RESET COMPLETO
// ============================================================

public static class ModResetter
{
    private static bool _isInitialized = false;
    
    private static readonly Type[] _typesToReset = new Type[]
    {
        typeof(SettingsBlendController),
        typeof(BlendClock),
        typeof(StateFileResolver),
        typeof(BlendSettingsLoader),
        typeof(RoomCameraExtensions),
        typeof(BlendSkyAtlasCache),
        typeof(RainCyclesEventDispatcher),
        typeof(CycleStateResolver),
    };
    
    public static void Init()
    {
        if (_isInitialized) return;
        
        On.RainWorldGame.ShutDownProcess += OnGameShutDown;
        On.RainWorldGame.ctor += OnGameCtor;
        
        _isInitialized = true;
    }
    
    private static void OnGameShutDown(On.RainWorldGame.orig_ShutDownProcess orig, RainWorldGame self)
    {
        StateFileResolver.ExecutePendingDeletes();

        ResetAllModState();
        orig(self);
    }
    
    private static void OnGameCtor(On.RainWorldGame.orig_ctor orig, RainWorldGame self, ProcessManager manager)
    {
        ResetAllModState();
        orig(self, manager);
        
        // La carga de región/blend y el estado por ciclo son de historia.
        // En arena (GetStorySession null) lo gestiona ArenaBlendController (blend per-level).
        if (self.GetStorySession != null && self.world?.region?.name != null)
        {
            string regionCode = self.world.region.name.ToUpperInvariant();
            
            BlendSettingsLoader.LoadRegion(regionCode);
            
            int cycle = self.GetStorySession?.saveState?.cycleNumber ?? 0;
            int state = CycleStateResolver.ResolveState(cycle);

            StateFileResolver.SetCurrentCycleState(state);
            
            SettingsSnapshot.InvalidateAllCache();
            SettingsSnapshot.PreloadRegionTemplates(regionCode);
            RoomCameraExtensions.ClearAllCaches();

            BlendClockUpdater.ResetRainCycleLogFlag();
        }
        else if (!self.IsArenaSession)
        {
            RSPlugin.log.LogWarning("[ModResetter] No se pudo determinar la región después del reset");
        }
    }
    
    public static void ResetAllModState()
    {
        StopActiveSystems();
        
        foreach (var type in _typesToReset)
        {
            ResetTypeStaticFields(type);
        }
        
        PerformSpecificCleanup();
        StateFileResolver.ClearAllPendingDeletes();
    }
    
    private static void StopActiveSystems()
    {
        try
        {
            BlendClock.Stop();
            BlendClock.SetEditMode(false);
            
            if (SettingsBlendController.IsActive)
                SettingsBlendController.Detach();
        }
        catch (Exception ex)
        {
            RSPlugin.log.LogWarning($"[ModResetter] Error al detener sistemas: {ex.Message}");
        }
    }
    
    private static void ResetTypeStaticFields(Type type)
    {
        try
        {
            var fields = type.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
            
            foreach (var field in fields)
            {
                if (field.IsInitOnly || field.IsLiteral) continue;
                
                try
                {
                    object defaultValue = GetDefaultValue(field.FieldType);
                    field.SetValue(null, defaultValue);
                }
                catch (Exception)
                {
                }
            }
        }
        catch (Exception ex)
        {
            RSPlugin.log.LogWarning($"[ModResetter] Error al resetear {type.Name}: {ex.Message}");
        }
    }
    
    private static object GetDefaultValue(Type type)
    {
        if (type == typeof(string))
            return null;
        
        if (type == typeof(bool))
            return false;
        
        if (type == typeof(int))
            return 0;
        
        if (type == typeof(float))
            return 0f;
        
        if (type == typeof(double))
            return 0.0;
        
        if (type.IsEnum)
            return Enum.ToObject(type, 0);
        
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
        {
            var dictType = typeof(Dictionary<,>).MakeGenericType(type.GetGenericArguments());
            return Activator.CreateInstance(dictType);
        }
        
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(HashSet<>))
        {
            var setType = typeof(HashSet<>).MakeGenericType(type.GetGenericArguments());
            return Activator.CreateInstance(setType);
        }
        
        if (type.IsClass)
            return null;
        
        if (type.IsValueType)
            return Activator.CreateInstance(type);
        
        return null;
    }
    
    private static void PerformSpecificCleanup()
    {
        try
        {
            CleanupSettingsBlendController();
            CleanupRoomCameraExtensions();
            CleanupBlendSkyAtlasCache();
        }
        catch (Exception ex)
        {
            RSPlugin.log.LogWarning($"[ModResetter] Error en limpieza específica: {ex.Message}");
        }
    }
    
    private static void CleanupSettingsBlendController()
    {
        var type = typeof(SettingsBlendController);
        
        SetFieldValue(type, "_room", null);
        SetFieldValue(type, "_rtvScene", null);
        SetFieldValue(type, "_acvScene", null);
        SetFieldValue(type, "_psvScene", null);
        SetFieldValue(type, "_orvScene", null);
        
        ClearCollectionField(type, "_rcViewInjected");
        ClearCollectionField(type, "_rcSlotsACV");
        ClearCollectionField(type, "_rcSlotsRTV");
        ClearCollectionField(type, "_rcSlotsPSV");
        ClearCollectionField(type, "_rcSlotsPSVFog");
        ClearCollectionField(type, "_rcSlotsPSVSun");
        ClearCollectionField(type, "_rcSlotsORV");
        ClearCollectionField(type, "_rcSlotsStaticACV");
        ClearCollectionField(type, "_rcSlotsStaticRTV");
        ClearCollectionField(type, "_rcSlotsStaticPSV");
        ClearCollectionField(type, "_rcSlotsStaticORV");
        
        SetFieldValue(type, "_snapA", null);
        SetFieldValue(type, "_snapB", null);
        SetFieldValue(type, "_snapOriginal", null);
        SetFieldValue(type, "_activeSnapshot", null);
        
        SetFieldValue(type, "_active", false);
        SetFieldValue(type, "_externalT", false);
        SetFieldValue(type, "_detachedThisFrame", false);
        SetFieldValue(type, "_moveCameraThisFrame", false);
        SetFieldValue(type, "_lastT", -1f);
        SetFieldValue(type, "_lastLightT", -1f);
        SetFieldValue(type, "_forcedT", 0f);
    }
    
    private static void CleanupRoomCameraExtensions()
    {
        var type = typeof(RoomCameraExtensions);
        ClearCollectionField(type, "_stateCache");
        SetFieldValue(type, "_preloadHooksInitialized", false);
        SetFieldValue(type, "_lightsInitialized", false);
    }
    
    private static void CleanupBlendSkyAtlasCache()
    {
        var type = typeof(BlendSkyAtlasCache);
        ClearCollectionField(type, "_cache");
    }
    
    // ============================================================
    // MÉTODOS AUXILIARES DE REFLEXIÓN
    // ============================================================
    
    private static void SetFieldValue(Type type, string fieldName, object value)
    {
        var field = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
        if (field != null)
        {
            field.SetValue(null, value);
        }
        else
        {
            RSPlugin.log.LogWarning($"[ModResetter] Campo '{fieldName}' no encontrado en {type.Name} — el cleanup específico para este campo ya no tiene efecto.");
        }
    }
    
    private static T GetFieldValue<T>(Type type, string fieldName)
    {
        var field = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
        if (field != null)
        {
            return (T)field.GetValue(null);
        }
        return default(T);
    }
    
    private static void ClearCollectionField(Type type, string fieldName)
    {
        var field = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
        if (field != null)
        {
            var collection = field.GetValue(null) as IEnumerable;
            var clearMethod = collection?.GetType().GetMethod("Clear");
            if (clearMethod != null)
            {
                clearMethod.Invoke(collection, null);
            }
            else if (collection is IDictionary dict)
            {
                dict.Clear();
            }
            else if (collection is IList list)
            {
                list.Clear();
            }
        }
        else
        {
            RSPlugin.log.LogWarning($"[ModResetter] Campo de colección '{fieldName}' no encontrado en {type.Name} — el cleanup específico para este campo ya no tiene efecto.");
        }
    }
}