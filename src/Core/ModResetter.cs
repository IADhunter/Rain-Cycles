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

/// <summary>
/// Sistema centralizado de reset completo del mod.
/// Único punto de entrada para toda la lógica de limpieza.
/// 
/// Se ejecuta automáticamente en:
/// - ShutDownProcess (cuando se cierra una partida)
/// - RainWorldGame.ctor (cuando se inicia una nueva partida)
/// </summary>
public static class ModResetter
{
    private static bool _isInitialized = false;
    
    // Lista de tipos que contienen variables estáticas a limpiar
    private static readonly Type[] _typesToReset = new Type[]
    {
        typeof(SettingsBlendController),
        typeof(BlendClock),
        typeof(StateFileResolver),
        typeof(StaticTintManager),
        typeof(BlendSettingsLoader),
        typeof(RoomCameraExtensions),
        typeof(BlendSkyAtlasCache),
        typeof(BlendTextureManager),
    };
    
    public static void Init()
    {
        if (_isInitialized) return;
        
        On.RainWorldGame.ShutDownProcess += OnGameShutDown;
        On.RainWorldGame.ctor += OnGameCtor;
        
        _isInitialized = true;
        RSPlugin.log.LogInfo("[ModResetter] Inicializado - Sistema centralizado de limpieza activo");
    }
    
    public static void Terminate()
    {
        if (!_isInitialized) return;
        
        On.RainWorldGame.ShutDownProcess -= OnGameShutDown;
        On.RainWorldGame.ctor -= OnGameCtor;
        
        _isInitialized = false;
    }
    
    private static void OnGameShutDown(On.RainWorldGame.orig_ShutDownProcess orig, RainWorldGame self)
    {
        RSPlugin.log.LogInfo("[ModResetter] ShutDownProcess - Limpiando estado del mod");
        ResetAllModState();
        orig(self);
    }
    
    private static void OnGameCtor(On.RainWorldGame.orig_ctor orig, RainWorldGame self, ProcessManager manager)
    {
        RSPlugin.log.LogInfo("[ModResetter] RainWorldGame.ctor - Preparando estado limpio para nueva partida");
        ResetAllModState();
        orig(self, manager);
    }
    
    /// <summary>
    /// Reset completo de TODAS las variables estáticas del mod.
    /// Único método público de limpieza.
    /// </summary>
    public static void ResetAllModState()
    {
        RSPlugin.log.LogInfo("[ModResetter] ResetAllModState - Iniciando limpieza completa");
        
        // 1. Detener sistemas activos
        StopActiveSystems();
        
        // 2. Limpiar cada tipo mediante reflexión
        foreach (var type in _typesToReset)
        {
            ResetTypeStaticFields(type);
        }
        
        // 3. Limpiezas específicas que la reflexión no puede hacer
        PerformSpecificCleanup();
        
        RSPlugin.log.LogInfo("[ModResetter] ResetAllModState - Limpieza completada");
    }
    
    /// <summary>
    /// Detiene sistemas que puedan estar en ejecución
    /// </summary>
    private static void StopActiveSystems()
    {
        try
        {
            // Detener BlendClock
            BlendClock.Stop();
            BlendClock.SetEditMode(false);
            
            // Detener SettingsBlendController si está activo
            if (SettingsBlendController.IsActive)
                SettingsBlendController.Detach();
        }
        catch (Exception ex)
        {
            RSPlugin.log.LogWarning($"[ModResetter] Error al detener sistemas: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Limpia todos los campos estáticos de un tipo usando reflexión
    /// </summary>
    private static void ResetTypeStaticFields(Type type)
    {
        try
        {
            var fields = type.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
            int resetCount = 0;
            
            foreach (var field in fields)
            {
                // Saltar campos readonly y constantes
                if (field.IsInitOnly || field.IsLiteral) continue;
                
                try
                {
                    object defaultValue = GetDefaultValue(field.FieldType);
                    field.SetValue(null, defaultValue);
                    resetCount++;
                }
                catch (Exception ex)
                {
                    RSPlugin.log.LogDebug($"[ModResetter] No se pudo resetear {type.Name}.{field.Name}: {ex.Message}");
                }
            }
            
            if (resetCount > 0)
            {
                RSPlugin.log.LogDebug($"[ModResetter] {type.Name}: {resetCount} campos estáticos reseteados");
            }
        }
        catch (Exception ex)
        {
            RSPlugin.log.LogWarning($"[ModResetter] Error al resetear {type.Name}: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Devuelve el valor por defecto para un tipo
    /// </summary>
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
    
    /// <summary>
    /// Limpiezas específicas que la reflexión automática no puede manejar
    /// </summary>
    private static void PerformSpecificCleanup()
    {
        try
        {
            // Limpiar diccionarios y colecciones específicas via reflexión manual
            CleanupSettingsBlendController();
            CleanupStaticTintManager();
            CleanupRoomCameraExtensions();
            CleanupBlendSkyAtlasCache();
            CleanupBlendTextureManager();
        }
        catch (Exception ex)
        {
            RSPlugin.log.LogWarning($"[ModResetter] Error en limpieza específica: {ex.Message}");
        }
    }
    
    private static void CleanupSettingsBlendController()
    {
        var type = typeof(SettingsBlendController);
        
        // Limpiar referencias a objetos Unity
        SetFieldValue(type, "_room", null);
        SetFieldValue(type, "_rtvScene", null);
        SetFieldValue(type, "_acvScene", null);
        SetFieldValue(type, "_psvScene", null);
        
        // Limpiar listas y diccionarios
        ClearCollectionField(type, "_rcViewInjected");
        ClearCollectionField(type, "_rcSlotsACV");
        ClearCollectionField(type, "_rcSlotsRTV");
        ClearCollectionField(type, "_rcSlotsPSV");
        ClearCollectionField(type, "_rcSlotsPSVFog");
        ClearCollectionField(type, "_rcSlotsPSVSun");
        ClearCollectionField(type, "_rcSlotsStaticACV");
        ClearCollectionField(type, "_rcSlotsStaticRTV");
        ClearCollectionField(type, "_rcSlotsStaticPSV");
        
        // Limpiar snapshots
        SetFieldValue(type, "_snapA", null);
        SetFieldValue(type, "_snapB", null);
        SetFieldValue(type, "_snapOriginal", null);
        SetFieldValue(type, "_activeSnapshot", null);
        
        // Resetear valores numéricos específicos
        SetFieldValue(type, "_active", false);
        SetFieldValue(type, "_externalT", false);
        SetFieldValue(type, "_detachedThisFrame", false);
        SetFieldValue(type, "_moveCameraThisFrame", false);
        SetFieldValue(type, "_hasSavedAlphas", false);
        SetFieldValue(type, "_pendingSkySync", false);
        SetFieldValue(type, "_lastT", -1f);
        SetFieldValue(type, "_lastPaletteT", -1f);
        SetFieldValue(type, "_lastLightT", -1f);
        SetFieldValue(type, "_forcedT", 0f);
        SetFieldValue(type, "_entryFrameT", -1f);
        SetFieldValue(type, "_lastIdleRotatedState", -1);
        SetFieldValue(type, "_pendingSyncSky", -1);
        SetFieldValue(type, "_pendingStateA", -1);
        SetFieldValue(type, "_pendingStateB", -1);
        SetFieldValue(type, "_pendingSkyStateA", -1);
        SetFieldValue(type, "_pendingSkyStateB", -1);
        
        // Slots
        SetFieldValue(type, "_rtvSlotDay", -1);
        SetFieldValue(type, "_rtvSlotDusk", -1);
        SetFieldValue(type, "_rtvSlotNight", -1);
        SetFieldValue(type, "_acvSlotDay", -1);
        SetFieldValue(type, "_acvSlotDusk", -1);
        SetFieldValue(type, "_acvSlotNight", -1);
        SetFieldValue(type, "_psvSlotDay", -1);
        SetFieldValue(type, "_psvSlotDusk", -1);
        SetFieldValue(type, "_psvSlotNight", -1);
        
        RSPlugin.log.LogDebug("[ModResetter] SettingsBlendController limpiado");
    }
    
    private static void CleanupStaticTintManager()
    {
        var type = typeof(StaticTintManager);
        ClearCollectionField(type, "_snapCache");
        RSPlugin.log.LogDebug("[ModResetter] StaticTintManager limpiado");
    }
    
    private static void CleanupRoomCameraExtensions()
    {
        var type = typeof(RoomCameraExtensions);
        ClearCollectionField(type, "_stateCache");
        SetFieldValue(type, "_preloadHooksInitialized", false);
        RSPlugin.log.LogDebug("[ModResetter] RoomCameraExtensions limpiado");
    }
    
    private static void CleanupBlendSkyAtlasCache()
    {
        var type = typeof(BlendSkyAtlasCache);
        ClearCollectionField(type, "_cache");
        RSPlugin.log.LogDebug("[ModResetter] BlendSkyAtlasCache limpiado");
    }
    
    private static void CleanupBlendTextureManager()
    {
        var type = typeof(BlendTextureManager);
        
        // Destruir texturas
        var texA_s1 = GetFieldValue<UnityEngine.Object>(type, "TexA_s1");
        var texA_s2 = GetFieldValue<UnityEngine.Object>(type, "TexA_s2");
        var texB_s1 = GetFieldValue<UnityEngine.Object>(type, "TexB_s1");
        var texB_s2 = GetFieldValue<UnityEngine.Object>(type, "TexB_s2");
        
        if (texA_s1 != null) UnityEngine.Object.Destroy(texA_s1);
        if (texA_s2 != null) UnityEngine.Object.Destroy(texA_s2);
        if (texB_s1 != null) UnityEngine.Object.Destroy(texB_s1);
        if (texB_s2 != null) UnityEngine.Object.Destroy(texB_s2);
        
        // Limpiar arrays
        SetFieldValue(type, "TexA_s1", null);
        SetFieldValue(type, "TexA_s2", null);
        SetFieldValue(type, "TexB_s1", null);
        SetFieldValue(type, "TexB_s2", null);
        SetFieldValue(type, "PxA_s1", null);
        SetFieldValue(type, "PxA_s2", null);
        SetFieldValue(type, "PxB_s1", null);
        SetFieldValue(type, "PxB_s2", null);
        SetFieldValue(type, "Ready", false);
        
        // Limpiar terrain
        var terrainPalA = GetFieldValue<IDisposable>(type, "TerrainPalA");
        var terrainPalB = GetFieldValue<IDisposable>(type, "TerrainPalB");
        terrainPalA?.Dispose();
        terrainPalB?.Dispose();
        SetFieldValue(type, "TerrainPalA", null);
        SetFieldValue(type, "TerrainPalB", null);
        
        RSPlugin.log.LogDebug("[ModResetter] BlendTextureManager limpiado");
    }
    
    // --- Métodos auxiliares de reflexión ---
    
    private static void SetFieldValue(Type type, string fieldName, object value)
    {
        var field = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
        if (field != null)
        {
            field.SetValue(null, value);
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
    }
}