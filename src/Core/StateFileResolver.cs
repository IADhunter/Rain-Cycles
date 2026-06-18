using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using UnityEngine;
using RainCycles.Blend;

namespace RainCycles.Core;

// Resuelve rutas de settings_N.txt y rota el archivo cargado según el ciclo.
public static class StateFileResolver
{
    private static int  _frozenCycle    = 0;
    private static bool _hasFrozenCycle = false;
    
    // Estado actual del ciclo (1..4)
    private static int _currentCycleState = 1;
    
    // ============================================================
    // SISTEMA DE PENDING DELETE - Estados marcados para eliminar al cerrar
    // ============================================================
    private static readonly HashSet<string> _pendingDeletes = new HashSet<string>();
    
    /// <summary>
    /// Clave única para identificar un estado: "ROOM_STATE" ej. "UW_F01_1"
    /// </summary>
    private static string GetPendingKey(string roomName, int state)
        => $"{roomName}_{state}";
    
    public static void MarkPendingDelete(string roomName, int state)
    {
        string key = GetPendingKey(roomName, state);
        _pendingDeletes.Add(key);
        
        // ════════════════════════════════════════════════════════════════════
        // INVALIDAR CACHE AL MARCAR PARA ELIMINAR
        // ════════════════════════════════════════════════════════════════════
        RoomCameraExtensions.InvalidateRoomCache(roomName);
        
        RSPlugin.log.LogInfo($"[StateFileResolver] Marcado para eliminar: {roomName} estado {state}");
    }
    
    public static void UnmarkPendingDelete(string roomName, int state)
    {
        string key = GetPendingKey(roomName, state);
        _pendingDeletes.Remove(key);
        
        // ════════════════════════════════════════════════════════════════════
        // INVALIDAR CACHE AL DESMARCAR
        // ════════════════════════════════════════════════════════════════════
        RoomCameraExtensions.InvalidateRoomCache(roomName);
        
        RSPlugin.log.LogInfo($"[StateFileResolver] Desmarcado: {roomName} estado {state}");
    }
    
    public static bool IsPendingDelete(string roomName, int state)
    {
        string key = GetPendingKey(roomName, state);
        return _pendingDeletes.Contains(key);
    }
    
    /// <summary>
    /// Devuelve los estados existentes EXCLUYENDO los marcados para borrar
    /// </summary>
    public static List<int> GetActiveStates(string roomName)
    {
        var result = new List<int>();
        int maxState = CountRainStateFiles(roomName);
        for (int i = 1; i <= maxState; i++)
        {
            if (!IsPendingDelete(roomName, i))
                result.Add(i);
        }
        return result;
    }
    
    /// <summary>
    /// Verifica si una sala tiene exactamente 4 estados activos
    /// </summary>
    public static bool HasFullStates(string roomName)
    {
        return GetActiveStates(roomName).Count == 4;
    }
    
    /// <summary>
    /// Ejecutar al cerrar la partida (ShutDownProcess)
    /// </summary>
    public static void ExecutePendingDeletes()
    {
        if (_pendingDeletes.Count == 0) return;
        
        RSPlugin.log.LogInfo($"[StateFileResolver] Ejecutando {_pendingDeletes.Count} eliminaciones pendientes");
        
        // Recopilar nombres de salas afectadas
        var affectedRooms = new HashSet<string>();
        
        foreach (string key in _pendingDeletes.ToList())
        {
            // Parsear clave: "UW_F01_1"
            int lastUnderscore = key.LastIndexOf('_');
            if (lastUnderscore < 0) continue;
            
            string roomName = key.Substring(0, lastUnderscore);
            if (!int.TryParse(key.Substring(lastUnderscore + 1), out int state)) continue;
            
            affectedRooms.Add(roomName);
            
            string path = GetRainStateSettingsFile(roomName, state);
            if (path != null && File.Exists(path))
            {
                try
                {
                    File.Delete(path);
                    RSPlugin.log.LogInfo($"[StateFileResolver] Eliminado: {path}");
                }
                catch (Exception ex)
                {
                    RSPlugin.log.LogWarning($"[StateFileResolver] No se pudo eliminar {path}: {ex.Message}");
                }
            }
        }
        
        // ════════════════════════════════════════════════════════════════════
        // INVALIDAR CACHE PARA SALAS AFECTADAS
        // ════════════════════════════════════════════════════════════════════
        foreach (string roomName in affectedRooms)
        {
            RoomCameraExtensions.InvalidateRoomCache(roomName);
        }
        
        _pendingDeletes.Clear();
        RSPlugin.log.LogInfo("[StateFileResolver] Eliminaciones pendientes completadas");
    }
    
    public static void ClearAllPendingDeletes()
    {
        _pendingDeletes.Clear();
    }

    public static void Init()
    {
        On.RoomSettings.ctor_Room_string_Region_bool_bool_Timeline_RainWorldGame += OnRoomSettingsCtor;
        On.RainWorldGame.ctor            += OnGameCtor;
        On.RainWorldGame.Win             += OnGameWin;
        On.RainWorldGame.ShutDownProcess += OnGameShutDown;
    }

    private static void OnGameCtor(On.RainWorldGame.orig_ctor orig, RainWorldGame self, ProcessManager manager)
    {
        orig(self, manager);
        _frozenCycle    = self.GetStorySession?.saveState?.cycleNumber ?? 0;
        _hasFrozenCycle = true;
    }

    private static bool _cycleAdvancedThisSession = false;

    private static void OnGameWin(On.RainWorldGame.orig_Win orig, RainWorldGame self, bool mal, bool warp)
    {
        orig(self, mal, warp);
        if (!warp && !_cycleAdvancedThisSession)
        {
            _cycleAdvancedThisSession = true;
            _frozenCycle = self.GetStorySession?.saveState?.cycleNumber ?? _frozenCycle;
        }
    }

    private static void OnGameShutDown(On.RainWorldGame.orig_ShutDownProcess orig, RainWorldGame self)
    {
        orig(self);
        _hasFrozenCycle = false;
        _cycleAdvancedThisSession = false;
        _currentCycleState = 1;
    }

    private static bool _blockLoad = false;
    public static void SetBlockLoad(bool value) => _blockLoad = value;

    private static void OnRoomSettingsCtor(
        On.RoomSettings.orig_ctor_Room_string_Region_bool_bool_Timeline_RainWorldGame orig,
        RoomSettings self, Room room, string name, Region region,
        bool template, bool firstTemplate,
        SlugcatStats.Timeline timelinePoint, RainWorldGame game)
    {
        orig(self, room, name, region, template, firstTemplate, timelinePoint, game);

        if (room == null || room.game == null) return;
        var session = room.game.GetStorySession;
        if (session?.saveState == null) return;
        if (_blockLoad) return;

        int cycle = _hasFrozenCycle ? _frozenCycle : session.saveState.cycleNumber;
        string rainStatePath = GetRainStateFilePath(name, cycle);
        if (rainStatePath == null) return;

        self.filePath = rainStatePath;
        self.Load((SlugcatStats.Timeline)null);

        var snap = SettingsSnapshot.FromFile(rainStatePath);
        if (!snap._hasTerrainFadePalette)
            self.terrainFadePalette = null;
    }

    // ── API pública ───────────────────────────────────────────────────────

    /// <summary>
    /// Devuelve el estado actual del ciclo (1..4).
    /// </summary>
    public static int GetCurrentCycleState() => _currentCycleState;

    /// <summary>
    /// Establece manualmente el estado actual del ciclo.
    /// Usado por BlendSettingsLoader al cargar una región.
    /// </summary>
    public static void SetCurrentCycleState(int state)
    {
        _currentCycleState = state;
    }

    public static string GetRainStateFilePath(string roomName, int cycle)
    {
        int stateNumber = _currentCycleState;
        return FindFileInRainCycles(roomName, stateNumber);
    }

    public static string GetRainStateSettingsFile(string roomName, int number)
        => FindFileInRainCycles(roomName, number);

    public static int CountRainStateFiles(string roomName)
    {
        int count = 0;
        while (FindFileInRainCycles(roomName, count + 1) != null)
            count++;
        return count;
    }

    public static int GetStateFromPath(string path, string roomName = null)
    {
        if (string.IsNullOrEmpty(path)) return -1;
        string fileName = Path.GetFileNameWithoutExtension(path);
        int idx = fileName.ToLowerInvariant().LastIndexOf("_settings_");
        if (idx < 0) return -1;
        string numStr = fileName.Substring(idx + "_settings_".Length);
        return int.TryParse(numStr, out int n) ? n : -1;
    }

    internal static string CreateNewRainStateFile(string name, int buttonCount, Room room)
    {
        string dir = BuildDirectoryPath(name);
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        string filePath = Path.Combine(dir, $"{name}_settings_{buttonCount}.txt");
        room.roomSettings.filePath = filePath;
        room.roomSettings.Save();
        
        // ════════════════════════════════════════════════════════════════════
        // INVALIDAR CACHE AL CREAR NUEVO ESTADO
        // ════════════════════════════════════════════════════════════════════
        RoomCameraExtensions.InvalidateRoomCache(name);
        
        return filePath;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static string FindFileInRainCycles(string roomName, int number)
    {
        string fileName = $"{roomName}_settings_{number}.txt";
        string baseDir  = BuildDirectoryPath(roomName);

        if (!Directory.Exists(baseDir)) return null;

        string direct = Path.Combine(baseDir, fileName);
        if (File.Exists(direct)) return direct;

        foreach (string found in Directory.GetFiles(baseDir, fileName, SearchOption.AllDirectories))
            return found;

        return null;
    }

    private static string BuildDirectoryPath(string roomName)
    {
        string regionCode   = Regex.Split(roomName, "_")[0].ToUpperInvariant();
        string regionFolder = Path.Combine("World", regionCode + "-Rooms", "RainCycles");

        for (int i = ModManager.ActiveMods.Count - 1; i >= 0; i--)
        {
            string candidate = Path.Combine(ModManager.ActiveMods[i].path, regionFolder);
            if (Directory.Exists(candidate))
                return candidate;
        }

        return Path.Combine(Application.streamingAssetsPath, regionFolder);
    }
}