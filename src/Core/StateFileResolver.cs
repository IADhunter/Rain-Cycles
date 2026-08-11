using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using RainCycles.Blend;

namespace RainCycles.Core;

public static class StateFileResolver
{
    private static int  _frozenCycle    = 0;
    private static bool _hasFrozenCycle = false;
    
    private static int _currentCycleState = 1;

    // Modo arena: la resolución de rutas delega en ArenaBlendController (levels/raincycles).
    // ModResetter lo resetea a false en cada partida; ArenaBlendController lo activa en su ctor hook.
    private static bool _arenaMode = false;

    public static bool IsArenaMode => _arenaMode;
    public static void SetArenaMode(bool value) => _arenaMode = value;
    
    private static readonly Dictionary<(string roomName, int state, string slugcat, string dlcs), string> _resolutionCache
        = new Dictionary<(string, int, string, string), string>();
    
    // ============================================================
    // SISTEMA DE PENDING DELETE
    // ============================================================
    private static readonly HashSet<string> _pendingDeletes = new HashSet<string>();
    
    private static string GetPendingKey(string roomName, int state)
        => $"{roomName}_{state}";
    
    public static void MarkPendingDelete(string roomName, int state)
    {
        string key = GetPendingKey(roomName, state);
        _pendingDeletes.Add(key);
        RoomCameraExtensions.InvalidateRoomCache(roomName);
    }
    
    public static void UnmarkPendingDelete(string roomName, int state)
    {
        string key = GetPendingKey(roomName, state);
        _pendingDeletes.Remove(key);
        RoomCameraExtensions.InvalidateRoomCache(roomName);
    }
    
    public static bool IsPendingDelete(string roomName, int state)
    {
        string key = GetPendingKey(roomName, state);
        return _pendingDeletes.Contains(key);
    }
    
    public static List<int> GetActiveStates(string roomName)
    {
        if (_arenaMode)
        {
            // El sistema de estados es 1-4; arena puede tener menos archivos.
            var arenaStates = new List<int>();
            int arenaCount = ArenaBlendController.CountSettingsFiles(roomName);
            for (int i = 1; i <= Math.Min(arenaCount, 4); i++)
                arenaStates.Add(i);
            return arenaStates;
        }

        var result = new List<int>();
        int maxState = CountRainStateFiles(roomName);
        for (int i = 1; i <= maxState; i++)
        {
            if (!IsPendingDelete(roomName, i))
                result.Add(i);
        }
        return result;
    }
    
    public static bool HasFullStates(string roomName)
    {
        return GetActiveStates(roomName).Count == 4;
    }
    
    public static void ExecutePendingDeletes()
    {
        if (_pendingDeletes.Count == 0) return;

        var affectedRooms = new HashSet<string>();
        
        foreach (string key in _pendingDeletes.ToList())
        {
            int lastUnderscore = key.LastIndexOf('_');
            if (lastUnderscore < 0) continue;
            
            string roomName = key.Substring(0, lastUnderscore);
            if (!int.TryParse(key.Substring(lastUnderscore + 1), out int state)) continue;
            
            affectedRooms.Add(roomName);
            
            string path = ResolveSettingsPath(roomName, state);
            if (path != null && File.Exists(path))
            {
                try
                {
                    File.Delete(path);
                }
                catch (Exception ex)
                {
                    RSPlugin.log.LogWarning($"[StateFileResolver] No se pudo eliminar {path}: {ex.Message}");
                }
            }
        }
        
        foreach (string roomName in affectedRooms)
        {
            RoomCameraExtensions.InvalidateRoomCache(roomName);
        }
        
        _pendingDeletes.Clear();
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

    // ============================================================
    // CACHE DE CONTEXTO (se recalcula solo en OnGameCtor)
    // ============================================================
    private static string _cachedSlugcatSuffix = "";
    private static List<string> _cachedActiveDLCs = new List<string>();

    private static void RebuildContextCache(RainWorldGame game)
    {
        _cachedSlugcatSuffix = ComputeSlugcatSuffix(game);
        _cachedActiveDLCs = ComputeActiveDLCSuffixes();
    }

    private static string ComputeSlugcatSuffix(RainWorldGame game)
    {
        try
        {
            if (game?.GetStorySession?.saveState != null)
            {
                string slugcat = game.GetStorySession.saveState.saveStateNumber?.value;
                if (!string.IsNullOrEmpty(slugcat))
                {
                    if (!string.Equals(slugcat, "white", StringComparison.OrdinalIgnoreCase) && 
                        !string.Equals(slugcat, "yellow", StringComparison.OrdinalIgnoreCase))
                    {
                        return "-" + slugcat.ToLowerInvariant();
                    }
                }
            }
        }
        catch (Exception)
        {
        }
        return "";
    }

    private static List<string> ComputeActiveDLCSuffixes()
    {
        var suffixes = new List<string>();
        if (ModManager.Watcher) suffixes.Add("-wtc");
        if (ModManager.MSC) suffixes.Add("-dwp");
        return suffixes;
    }

    public static string GetCurrentSlugcatSuffix() => _cachedSlugcatSuffix;
    public static List<string> GetActiveDLCSuffixes() => _cachedActiveDLCs;
    
    // ============================================================
    // RESOLUCIÓN DE RUTAS - SISTEMA PRINCIPAL
    // ============================================================
    
    public static string ResolveSettingsPath(string roomName, int state)
    {
        if (string.IsNullOrEmpty(roomName) || state < 1 || state > 4)
            return null;

        if (_arenaMode)
            return ArenaBlendController.ResolveSettingsPath(roomName, state);
        
        string slugcatSuffix = GetCurrentSlugcatSuffix();
        var activeDLCs = GetActiveDLCSuffixes();
        string dlcKey = string.Join(",", activeDLCs);
        
        var cacheKey = (roomName, state, slugcatSuffix, dlcKey);
        if (_resolutionCache.TryGetValue(cacheKey, out string cached) && cached != null)
        {
            if (File.Exists(cached)) return cached;
            _resolutionCache.Remove(cacheKey);
        }
        
        string dir = BuildDirectoryPath(roomName);
        if (!Directory.Exists(dir))
        {
            return null;
        }
        
        var candidates = BuildCandidatePaths(roomName, state, slugcatSuffix, activeDLCs);
        
        foreach (string candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                _resolutionCache[cacheKey] = candidate;
                return candidate;
            }
        }
        
        return null;
    }
    
    private static List<string> BuildCandidatePaths(string roomName, int state, string slugcatSuffix, List<string> activeDLCs)
    {
        var candidates = new List<string>();
        string dir = BuildDirectoryPath(roomName);
        
        bool isDashTwo = roomName.EndsWith("-2");
        string baseName = isDashTwo ? roomName.Substring(0, roomName.Length - 2) : roomName;
        string dashTwoName = isDashTwo ? baseName + "-2" : null;
        
        // ============================================================
        // ORDEN DE PRIORIDAD: slugcat tiene prioridad sobre DLC
        // ============================================================
        
        // 1. DLC + slugcat
        foreach (string dlcSuffix in activeDLCs)
        {
            if (!string.IsNullOrEmpty(slugcatSuffix))
            {
                if (isDashTwo)
                    candidates.Add(Path.Combine(dir, $"{dashTwoName}_settings{dlcSuffix}{slugcatSuffix}_{state}.txt"));
                candidates.Add(Path.Combine(dir, $"{baseName}_settings{dlcSuffix}{slugcatSuffix}_{state}.txt"));
            }
        }
        
        // 2. Solo slugcat
        if (!string.IsNullOrEmpty(slugcatSuffix))
        {
            if (isDashTwo)
                candidates.Add(Path.Combine(dir, $"{dashTwoName}_settings{slugcatSuffix}_{state}.txt"));
            candidates.Add(Path.Combine(dir, $"{baseName}_settings{slugcatSuffix}_{state}.txt"));
        }
        
        // 3. Solo DLC
        foreach (string dlcSuffix in activeDLCs)
        {
            if (isDashTwo)
                candidates.Add(Path.Combine(dir, $"{dashTwoName}_settings{dlcSuffix}_{state}.txt"));
            candidates.Add(Path.Combine(dir, $"{baseName}_settings{dlcSuffix}_{state}.txt"));
        }
        
        // 4. Base
        if (isDashTwo)
            candidates.Add(Path.Combine(dir, $"{dashTwoName}_settings_{state}.txt"));
        candidates.Add(Path.Combine(dir, $"{baseName}_settings_{state}.txt"));
        
        return candidates;
    }
    
    public static string GetRainStateSettingsFile(string roomName, int number)
        => ResolveSettingsPath(roomName, number);
    
    // ============================================================
    // MÉTODOS DE UTILIDAD
    // ============================================================
    
    public static int CountRainStateFiles(string roomName)
    {
        if (_arenaMode)
            return ArenaBlendController.CountSettingsFiles(roomName);

        string dir = BuildDirectoryPath(roomName);
        if (!Directory.Exists(dir)) return 0;
        
        string pattern = $"{roomName}_settings*_*.txt";
        string baseName = roomName.EndsWith("-2") ? roomName.Substring(0, roomName.Length - 2) : roomName;
        string patternDashTwo = $"{baseName}-2_settings*_*.txt";
        
        var files = Directory.GetFiles(dir, pattern)
            .Concat(Directory.GetFiles(dir, patternDashTwo))
            .Distinct()
            .ToList();
        
        var states = new HashSet<int>();
        foreach (string file in files)
        {
            string name = Path.GetFileNameWithoutExtension(file);
            int lastUnderscore = name.LastIndexOf('_');
            if (lastUnderscore >= 0 && int.TryParse(name.Substring(lastUnderscore + 1), out int state))
            {
                states.Add(state);
            }
        }
        
        return states.Count;
    }
    
    public static int GetStateFromPath(string path, string roomName = null)
    {
        if (string.IsNullOrEmpty(path)) return -1;
        string fileName = Path.GetFileNameWithoutExtension(path);
        int lastUnderscore = fileName.LastIndexOf('_');
        if (lastUnderscore < 0) return -1;
        return int.TryParse(fileName.Substring(lastUnderscore + 1), out int n) ? n : -1;
    }
    
    internal static string CreateNewRainStateFile(string name, int buttonCount, Room room)
    {
        // En arena el archivo vive en levels/raincycles del mod dueño del level
        // (historia lo escribe en World/{REGION}-Rooms/RainCycles).
        if (_arenaMode)
            return ArenaBlendController.CreateSettingsFile(name, buttonCount, room);

        string dir = BuildDirectoryPath(name);
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        // El nombre de sala llega en mayúsculas (definición del world file),
        // pero los archivos vanilla en disco están en minúsculas: normaliza
        // el case para que el generado coincida con la convención del juego.
        string fileName = name.ToLowerInvariant();

        string filePath = Path.Combine(dir, $"{fileName}_settings_{buttonCount}.txt");
        room.roomSettings.filePath = filePath;
        room.roomSettings.Save();
        
        RoomCameraExtensions.InvalidateRoomCache(name);
        _resolutionCache.Clear();
        
        return filePath;
    }

    // ============================================================
    // HELPERS
    // ============================================================
    
    private static string BuildDirectoryPath(string roomName)
    {
        string regionCode   = roomName.Split('_')[0].ToUpperInvariant();
        string regionFolder = Path.Combine("World", regionCode + "-Rooms", "RainCycles");

        for (int i = ModManager.ActiveMods.Count - 1; i >= 0; i--)
        {
            string candidate = Path.Combine(ModManager.ActiveMods[i].path, regionFolder);
            if (Directory.Exists(candidate))
                return candidate;
        }

        return Path.Combine(Application.streamingAssetsPath, regionFolder);
    }
    
    // ============================================================
    // HOOKS
    // ============================================================
    
    private static void OnGameCtor(On.RainWorldGame.orig_ctor orig, RainWorldGame self, ProcessManager manager)
    {
        orig(self, manager);
        _frozenCycle    = self.GetStorySession?.saveState?.cycleNumber ?? 0;
        _hasFrozenCycle = true;
        _resolutionCache.Clear();
        RebuildContextCache(self);
    }

    private static bool _cycleAdvancedThisSession = false;

    private static void OnGameWin(On.RainWorldGame.orig_Win orig, RainWorldGame self, bool mal, bool warp)
    {
        orig(self, mal, warp);
        if (!warp && !_cycleAdvancedThisSession)
        {
            _cycleAdvancedThisSession = true;
            _frozenCycle = self.GetStorySession?.saveState?.cycleNumber ?? _frozenCycle;
            _resolutionCache.Clear();
        }
    }

    private static void OnGameShutDown(On.RainWorldGame.orig_ShutDownProcess orig, RainWorldGame self)
    {
        _hasFrozenCycle = false;
        _cycleAdvancedThisSession = false;
        _currentCycleState = 1;
        _resolutionCache.Clear();
        _cachedSlugcatSuffix = "";
        _cachedActiveDLCs.Clear();
        orig(self);
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
        if (_blockLoad) return;

        var session = room.game.GetStorySession;
        if (session?.saveState == null)
        {
            // Arena: sin StorySession, el estado lo resuelve ArenaBlendController.
            // En el ctor de la sala (dentro del ctor del juego) el estado aún es 0
            // (ModResetter lo resetea antes del orig) -> mínimo 1; el blend runtime
            // re-aplica el estado real de la ronda per-frame.
            if (!_arenaMode) return;

            int arenaState = Math.Max(1, _currentCycleState);
            string arenaPath = ResolveSettingsPath(name, arenaState);
            if (arenaPath == null) return;

            self.filePath = arenaPath;
            self.Load((SlugcatStats.Timeline)null);

            var snapArena = SettingsSnapshot.GetCached(arenaPath, name);
            if (!snapArena._hasTerrainFadePalette)
                self.terrainFadePalette = null;
            return;
        }

        int cycle = _hasFrozenCycle ? _frozenCycle : session.saveState.cycleNumber;
        int stateNumber = _currentCycleState;
        string rainStatePath = ResolveSettingsPath(name, stateNumber);
        if (rainStatePath == null) return;

        self.filePath = rainStatePath;
        self.Load((SlugcatStats.Timeline)null);

        var snap = SettingsSnapshot.GetCached(rainStatePath, name);
        if (!snap._hasTerrainFadePalette)
            self.terrainFadePalette = null;
    }

    // ============================================================
    // API PÚBLICA
    // ============================================================
    
    public static int GetCurrentCycleState() => _currentCycleState;
    
    public static void SetCurrentCycleState(int state)
    {
        _currentCycleState = state;
        _resolutionCache.Clear();
    }
    
    public static void InvalidatePathCache()
    {
        _resolutionCache.Clear();
    }
}