using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using RainCycles.Blend;

namespace RainCycles.Core;

public static class StateFileResolver
{
    private static int  _frozenCycle    = 0;
    private static bool _hasFrozenCycle = false;
    
    private static int _currentCycleState = 1;

    private static bool _arenaMode = false;

    public static bool IsArenaMode => _arenaMode;
    public static void SetArenaMode(bool value) => _arenaMode = value;
    
    private static readonly Dictionary<(string roomName, int state, string slugcat, string dlcs), string> _resolutionCache
        = new Dictionary<(string, int, string, string), string>();

    private static readonly Dictionary<string, (int count, int baseState)> _countStateCache
        = new Dictionary<string, (int, int)>();

    private static readonly Dictionary<string, (string path, int state)> _baseCache
        = new Dictionary<string, (string, int)>(StringComparer.OrdinalIgnoreCase);

    // El base depende del slugcat activo: clave = sala + sufijo del timeline
    private static string BaseCacheKey(string roomName) => roomName + "|" + _cachedBaseSuffix;

    private static void ClearCaches()
    {
        _resolutionCache.Clear();
        _countStateCache.Clear();
        _baseCache.Clear();
    }

    // SISTEMA DE PENDING DELETE
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
            return ArenaBlendController.GetStates(roomName);

        var result = new List<int>();
        int maxState = CountRainStateFiles(roomName);
        for (int i = 1; i <= maxState; i++)
        {
            if (IsPendingDelete(roomName, i)) continue;
            if (ResolveSettingsPath(roomName, i) == null) continue;
            result.Add(i);
        }

        int baseState = GetBaseState(roomName);
        if (baseState >= 1 && baseState <= 4 && !result.Contains(baseState) && !IsPendingDelete(roomName, baseState))
            result.Add(baseState);

        result.Sort();
        return result;
    }
    
    public static bool HasFullStates(string roomName)
    {
        if (RCGate.Suspended) return false;
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
        _baseCache.Clear();
        _countStateCache.Clear();
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

    // CACHE DE CONTEXTO (se recalcula solo en OnGameCtor)
    private static string _cachedSlugcatSuffix = "";
    private static string _cachedBaseSuffix = "";
    private static List<string> _cachedActiveDLCs = new List<string>();

    private static void RebuildContextCache(RainWorldGame game)
    {
        _cachedSlugcatSuffix = ComputeSlugcatSuffix(game);
        _cachedActiveDLCs = ComputeActiveDLCSuffixes();
        _cachedBaseSuffix = ComputeBaseSuffix(game);
    }

    public static string GetCurrentSlugcatName()
    {
        string suffix = GetCurrentSlugcatSuffix();
        return !string.IsNullOrEmpty(suffix) && suffix[0] == '-'
            ? suffix.Substring(1)
            : "";
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

    // BASE DEL SLUGCAT — vanilla usa "_settings-{timeline}.txt" (RoomSettings.cs:648), con
    // White/Yellow incluidos. No se reutiliza _cachedSlugcatSuffix porque ese los colapsa a ""
    // para que settings_N.txt sea compartido entre Survivor y Monk.
    private static string ComputeBaseSuffix(RainWorldGame game)
    {
        try
        {
            SlugcatStats.Name slugcat = game?.GetStorySession?.saveState?.saveStateNumber;
            if (slugcat == null) return "";
            return TimelineSuffix(SlugcatStats.SlugcatToTimeline(slugcat));
        }
        catch (Exception)
        {
            return "";
        }
    }

    private static string TimelineSuffix(SlugcatStats.Timeline timeline)
    {
        if (timeline == null || string.IsNullOrEmpty(timeline.value)) return "";
        return "-" + timeline.value.ToLowerInvariant();
    }
    
    // RESOLUCIÓN DE RUTAS - SISTEMA PRINCIPAL
    
    public static string ResolveSettingsPath(string roomName, int state)
    {
        if (string.IsNullOrEmpty(roomName) || state < 1 || state > 4)
            return null;

        if (_arenaMode)
            return ArenaBlendController.ResolveSettingsPath(roomName, state);
        
        string slugcatSuffix = GetCurrentSlugcatSuffix();
        var activeDLCs = GetActiveDLCSuffixes();
        if (activeDLCs == null) activeDLCs = new List<string>();
        string dlcKey = string.Join(",", activeDLCs);
        
        var cacheKey = (roomName, state, slugcatSuffix, dlcKey);
        if (_resolutionCache.TryGetValue(cacheKey, out string cached) && cached != null)
        {
            if (File.Exists(cached)) return cached;
            _resolutionCache.Remove(cacheKey);
        }
        
        string dir = BuildDirectoryPath(roomName);
        if (Directory.Exists(dir))
        {
            var candidates = BuildCandidatePaths(roomName, state, slugcatSuffix, activeDLCs);

            foreach (string candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    _resolutionCache[cacheKey] = candidate;
                    return candidate;
                }
            }

            string foundRecursive = SearchRecursiveForState(dir, roomName, state);
            if (foundRecursive != null)
            {
                _resolutionCache[cacheKey] = foundRecursive;
                return foundRecursive;
            }
        }

        if (GetBaseState(roomName) == state)
        {
            string basePath = GetBaseSettingsPath(roomName);
            if (basePath != null && File.Exists(basePath))
            {
                _resolutionCache[cacheKey] = basePath;
                return basePath;
            }
        }

        return null;
    }
    
    private static string SearchRecursiveForState(string baseDir, string roomName, int state)
    {
        bool isDashTwo = roomName.EndsWith("-2");
        string baseName = isDashTwo ? roomName.Substring(0, roomName.Length - 2) : roomName;
        string dashTwoName = isDashTwo ? baseName + "-2" : null;
        
        string pattern = $"*_settings*_{state}.txt";
        
        try
        {
            var files = Directory.GetFiles(baseDir, pattern, SearchOption.AllDirectories);
            
            foreach (string file in files)
            {
                string fileName = Path.GetFileNameWithoutExtension(file);
                if (isDashTwo)
                {
                    if (fileName.StartsWith(dashTwoName, StringComparison.OrdinalIgnoreCase))
                        return file;
                }
                else
                {
                    if (fileName.StartsWith(baseName, StringComparison.OrdinalIgnoreCase))
                        return file;
                }
            }
        }
        catch (Exception) { }
        
        return null;
    }
    
    private static List<string> BuildCandidatePaths(string roomName, int state, string slugcatSuffix, List<string> activeDLCs)
    {
        var candidates = new List<string>();
        string dir = BuildDirectoryPath(roomName);
        
        bool isDashTwo = roomName.EndsWith("-2");
        string baseName = isDashTwo ? roomName.Substring(0, roomName.Length - 2) : roomName;
        string dashTwoName = isDashTwo ? baseName + "-2" : null;
        
        // ORDEN DE PRIORIDAD: slugcat tiene prioridad sobre DLC
        
        foreach (string dlcSuffix in activeDLCs)
        {
            if (!string.IsNullOrEmpty(slugcatSuffix))
            {
                if (isDashTwo)
                    candidates.Add(Path.Combine(dir, $"{dashTwoName}_settings{dlcSuffix}{slugcatSuffix}_{state}.txt"));
                candidates.Add(Path.Combine(dir, $"{baseName}_settings{dlcSuffix}{slugcatSuffix}_{state}.txt"));
            }
        }
        
        if (!string.IsNullOrEmpty(slugcatSuffix))
        {
            if (isDashTwo)
                candidates.Add(Path.Combine(dir, $"{dashTwoName}_settings{slugcatSuffix}_{state}.txt"));
            candidates.Add(Path.Combine(dir, $"{baseName}_settings{slugcatSuffix}_{state}.txt"));
        }
        
        foreach (string dlcSuffix in activeDLCs)
        {
            if (isDashTwo)
                candidates.Add(Path.Combine(dir, $"{dashTwoName}_settings{dlcSuffix}_{state}.txt"));
            candidates.Add(Path.Combine(dir, $"{baseName}_settings{dlcSuffix}_{state}.txt"));
        }
        
        if (isDashTwo)
            candidates.Add(Path.Combine(dir, $"{dashTwoName}_settings_{state}.txt"));
        candidates.Add(Path.Combine(dir, $"{baseName}_settings_{state}.txt"));
        
        return candidates;
    }
    
    public static string GetRainStateSettingsFile(string roomName, int number)
        => ResolveSettingsPath(roomName, number);
    
    // MÉTODOS DE UTILIDAD
    
    public static int CountRainStateFiles(string roomName)
    {
        if (_arenaMode)
            return ArenaBlendController.CountSettingsFiles(roomName);

        int baseState = GetBaseState(roomName);

        if (_countStateCache.TryGetValue(roomName, out var cached) && cached.baseState == baseState)
            return cached.count;

        string dir = BuildDirectoryPath(roomName);
        if (!Directory.Exists(dir))
        {
            int onlyBase = baseState >= 1 ? 1 : 0;
            _countStateCache[roomName] = (onlyBase, baseState);
            return onlyBase;
        }
        
        string pattern = $"{roomName}_settings*_*.txt";
        string baseName = roomName.EndsWith("-2") ? roomName.Substring(0, roomName.Length - 2) : roomName;
        string patternDashTwo = $"{baseName}-2_settings*_*.txt";
        
        var files = Directory.GetFiles(dir, pattern, SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(dir, patternDashTwo, SearchOption.AllDirectories))
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

        if (baseState >= 1 && baseState <= 4 && !states.Contains(baseState))
        {
            string basePath = GetBaseSettingsPath(roomName);
            if (basePath != null && File.Exists(basePath))
                states.Add(baseState);
        }
        
        _countStateCache[roomName] = (states.Count, baseState);
        return states.Count;
    }
    
    public static int GetNumberedStateFromPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return -1;
        string fileName = Path.GetFileNameWithoutExtension(path);
        int lastUnderscore = fileName.LastIndexOf('_');
        if (lastUnderscore < 0) return -1;
        return int.TryParse(fileName.Substring(lastUnderscore + 1), out int n) ? n : -1;
    }

    public static int GetStateFromPath(string path, string roomName = null)
    {
        int numbered = GetNumberedStateFromPath(path);
        if (numbered >= 1 && numbered <= 4) return numbered;

        if (string.IsNullOrEmpty(path) || !IsBaseSettingsFile(path)) return -1;

        string room = string.IsNullOrEmpty(roomName) ? DeriveRoomNameFromPath(path) : roomName;
        if (string.IsNullOrEmpty(room)) return -1;

        int baseState = GetBaseState(room);
        return baseState >= 1 ? baseState : -1;
    }
    
    internal static string CreateNewRainStateFile(string name, int buttonCount, Room room)
    {
        if (_arenaMode)
            return ArenaBlendController.CreateSettingsFile(name, buttonCount, room);

        string dir = SaveModResolver.DirectoryForRoom(name) ?? BuildDirectoryPath(name);
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        string fileName = name.ToLowerInvariant();

        string filePath = Path.Combine(dir, $"{fileName}_settings_{buttonCount}.txt");
        room.roomSettings.filePath = filePath;
        room.roomSettings.Save();
        
        RoomCameraExtensions.InvalidateRoomCache(name);
        ClearCaches();
        
        return filePath;
    }

    // HELPERS
    
    private static string BuildDirectoryPath(string roomName)
    {
        string regionCode = roomName.Split('_')[0].ToLowerInvariant();
        string regionFolder = BlendSettingsLoader.IsGateRoom(roomName)
            ? BlendSettingsLoader.GateRelativeDir
            : Path.Combine("world", regionCode + "-rooms", "raincycles");

        for (int i = ModManager.ActiveMods.Count - 1; i >= 0; i--)
        {
            string candidate = Path.Combine(ModManager.ActiveMods[i].path, regionFolder);
            if (Directory.Exists(candidate))
                return candidate;
        }

        return Path.Combine(Application.streamingAssetsPath, regionFolder);
    }

    // BASE DE LA SALA — archivo no numérico (vanilla / DLC / mod de región)

    public static bool IsBaseSettingsFile(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        string name = Path.GetFileNameWithoutExtension(path);
        if (name.IndexOf("_settingstemplate", StringComparison.OrdinalIgnoreCase) >= 0) return false;

        int idx = name.IndexOf("_settings", StringComparison.OrdinalIgnoreCase);
        if (idx <= 0) return false;

        string tail = name.Substring(idx + "_settings".Length);
        int lastUnderscore = tail.LastIndexOf('_');
        if (lastUnderscore < 0) return true;
        return !int.TryParse(tail.Substring(lastUnderscore + 1), out _);
    }

    public static int ParseStateTag(string content)
    {
        if (string.IsNullOrEmpty(content)) return 0;

        int pos = 0;
        while (pos < content.Length)
        {
            int start = content.IndexOf('<', pos);
            if (start < 0) break;
            int end = content.IndexOf('>', start);
            if (end < 0) break;

            string segment = content.Substring(start + 1, end - start - 1);
            int sep = segment.IndexOf(':');
            if (sep > 0 && segment.Substring(0, sep).Trim().Equals("State", StringComparison.OrdinalIgnoreCase))
            {
                string value = segment.Substring(sep + 1).Trim();
                return int.TryParse(value, out int n) && n >= 1 && n <= 4 ? n : 0;
            }
            pos = end + 1;
        }

        return 0;
    }

    public static string GetBaseSettingsPath(string roomName)
    {
        if (string.IsNullOrEmpty(roomName)) return null;

        string key = BaseCacheKey(roomName);
        if (_baseCache.TryGetValue(key, out var cached))
        {
            if (cached.path != null && File.Exists(cached.path)) return cached.path;
            _baseCache.Remove(key);
        }

        string chosen = PickBase(roomName);
        _baseCache[key] = (chosen, chosen != null ? ReadStateTag(chosen) : 0);
        return chosen;
    }

    public static int GetBaseState(string roomName)
    {
        if (string.IsNullOrEmpty(roomName)) return 0;
        GetBaseSettingsPath(roomName);
        return _baseCache.TryGetValue(BaseCacheKey(roomName), out var cached) ? cached.state : 0;
    }

    public static bool WriteBaseState(string roomName, int state)
    {
        if (string.IsNullOrEmpty(roomName)) return false;
        if (state < 0 || state > 4) return false;

        string path = GetBaseSettingsPath(roomName);
        if (path == null || !File.Exists(path)) return false;

        try
        {
            var lines = new List<string>(File.ReadAllText(path, Encoding.UTF8).Split('\n'));
            int idx = lines.FindIndex(l => l.Trim().StartsWith("RainCycles:", StringComparison.OrdinalIgnoreCase));

            if (state >= 1)
            {
                if (idx >= 0 && ParseStateTag(lines[idx]) >= 1)
                {
                    // Ya asignado: se cambia solo el número, se conservan los demás tags
                    lines[idx] = BuildLineWithState(lines[idx], state);
                }
                else
                {
                    // Sin State (legacy / error del usuario): se descarta la línea entera
                    if (idx >= 0) lines.RemoveAt(idx);
                    while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[lines.Count - 1]))
                        lines.RemoveAt(lines.Count - 1);
                    lines.Add($"RainCycles: <State:{state}>");
                }
            }
            else
            {
                if (idx < 0) return true;
                string rebuilt = RemoveStateTag(lines[idx]);
                if (rebuilt == null) lines.RemoveAt(idx);
                else lines[idx] = rebuilt;
            }

            File.WriteAllText(path, string.Join("\n", lines), Encoding.UTF8);

            SettingsSnapshot.InvalidateCache(path);
            RoomCameraExtensions.InvalidateRoomCache(roomName);
            BkgResolver.InvalidatePaths(roomName);
            ClearCaches();
            return true;
        }
        catch (Exception ex)
        {
            RSPlugin.log.LogWarning($"[StateFileResolver] No se pudo escribir State en {path}: {ex.Message}");
            return false;
        }
    }

    public static bool ClearBaseRainCycles(string roomName)
    {
        if (string.IsNullOrEmpty(roomName)) return false;

        string path = GetBaseSettingsPath(roomName);
        if (path == null || !File.Exists(path)) return false;

        try
        {
            var lines = new List<string>(File.ReadAllText(path, Encoding.UTF8).Split('\n'));
            int removed = lines.RemoveAll(l => l.Trim().StartsWith("RainCycles:", StringComparison.OrdinalIgnoreCase));
            if (removed == 0) return true;

            File.WriteAllText(path, string.Join("\n", lines), Encoding.UTF8);

            SettingsSnapshot.InvalidateCache(path);
            RoomCameraExtensions.InvalidateRoomCache(roomName);
            BkgResolver.InvalidatePaths(roomName);
            ClearCaches();
            return true;
        }
        catch (Exception ex)
        {
            RSPlugin.log.LogWarning($"[StateFileResolver] No se pudo limpiar RainCycles en {path}: {ex.Message}");
            return false;
        }
    }

    private static string RemoveStateTag(string line)
    {
        bool cr = line.EndsWith("\r", StringComparison.Ordinal);
        if (cr) line = line.Substring(0, line.Length - 1);

        int idx = line.IndexOf("RainCycles:", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return cr ? line + "\r" : line;

        string head = line.Substring(0, idx) + "RainCycles:";
        string content = line.Substring(idx + "RainCycles:".Length);

        var kept = new List<string>();
        int pos = 0;
        while (pos < content.Length)
        {
            int start = content.IndexOf('<', pos);
            if (start < 0) break;
            int end = content.IndexOf('>', start);
            if (end < 0) break;

            string segment = content.Substring(start + 1, end - start - 1);
            int sep = segment.IndexOf(':');
            bool isState = sep > 0 && segment.Substring(0, sep).Trim().Equals("State", StringComparison.OrdinalIgnoreCase);
            if (!isState && segment.Trim().Length > 0) kept.Add(segment.Trim());
            pos = end + 1;
        }

        string result = kept.Count == 0 ? null : head + " <" + string.Join("><", kept) + ">";
        return result == null ? null : (cr ? result + "\r" : result);
    }

    private static string BuildLineWithState(string line, int state)
    {
        bool cr = line.EndsWith("\r", StringComparison.Ordinal);
        if (cr) line = line.Substring(0, line.Length - 1);

        int idx = line.IndexOf("RainCycles:", StringComparison.OrdinalIgnoreCase);
        string head = idx >= 0 ? line.Substring(0, idx) : "";
        string content = idx >= 0 ? line.Substring(idx + "RainCycles:".Length) : "";

        var kept = new List<string>();
        int pos = 0;
        while (pos < content.Length)
        {
            int start = content.IndexOf('<', pos);
            if (start < 0) break;
            int end = content.IndexOf('>', start);
            if (end < 0) break;

            string segment = content.Substring(start + 1, end - start - 1).Trim();
            int sep = segment.IndexOf(':');
            bool isState = sep > 0 && segment.Substring(0, sep).Trim().Equals("State", StringComparison.OrdinalIgnoreCase);
            if (!isState && segment.Length > 0) kept.Add(segment);
            pos = end + 1;
        }

        string built = head + $"RainCycles: <State:{state}>";
        if (kept.Count > 0) built += "<" + string.Join("><", kept) + ">";
        return cr ? built + "\r" : built;
    }

    // BASE DEL SLUGCAT — nombres en el orden en que el juego los carga (RoomSettings.cs:646-661):
    // {room}_settings-{timeline}.txt → {room}_settings.txt
    // Salas -2 (MSC): {room}-2_settings-{timeline}.txt → {room}-2_settings.txt → {base}_settings.txt

    private static List<string> GetBaseFileNames(string roomName)
    {
        var names = new List<string>();
        string suffix = _cachedBaseSuffix ?? "";

        if (_arenaMode)
        {
            string level = roomName.ToLowerInvariant();
            if (suffix.Length > 0) names.Add(level + "_settings" + suffix + ".txt");
            names.Add(level + "_settings.txt");
            return names;
        }

        string room = roomName.ToLowerInvariant();
        bool isDashTwo = !BlendSettingsLoader.IsGateRoom(roomName) && roomName.EndsWith("-2");

        if (suffix.Length > 0) names.Add(room + "_settings" + suffix + ".txt");
        names.Add(room + "_settings.txt");

        if (isDashTwo)
            names.Add(room.Substring(0, room.Length - 2) + "_settings.txt");

        return names;
    }

    // Del nombre elegido, todos los archivos existentes: mods (orden inverso) → vanilla
    private static List<string> CollectBasePaths(string roomName, string fileName)
    {
        var result = new List<string>();

        if (_arenaMode)
        {
            for (int i = ModManager.ActiveMods.Count - 1; i >= 0; i--)
            {
                string candidate = Path.Combine(ModManager.ActiveMods[i].path, "Levels", fileName);
                if (File.Exists(candidate) && !result.Contains(candidate)) result.Add(candidate);
            }

            string vanillaArena = Path.Combine(Application.streamingAssetsPath, "Levels", fileName);
            if (File.Exists(vanillaArena)) result.Add(vanillaArena);
            return result;
        }

        string regionCode = roomName.Split('_')[0].ToLowerInvariant();
        string vanillaDir = BlendSettingsLoader.IsGateRoom(roomName)
            ? BlendSettingsLoader.GateVanillaDir
            : Path.Combine("world", regionCode + "-rooms");

        for (int i = ModManager.ActiveMods.Count - 1; i >= 0; i--)
        {
            string candidate = Path.Combine(ModManager.ActiveMods[i].path, vanillaDir, fileName);
            if (File.Exists(candidate) && !result.Contains(candidate)) result.Add(candidate);
        }

        string basePath = Path.Combine(Application.streamingAssetsPath, vanillaDir, fileName);
        if (File.Exists(basePath)) result.Add(basePath);

        return result;
    }

    // Primero el archivo que cargaría el juego (variante del slugcat, luego el plano);
    // después el desempate entre mods que aporten ese mismo nombre.
    private static string PickBase(string roomName)
    {
        foreach (string fileName in GetBaseFileNames(roomName))
        {
            List<string> group = CollectBasePaths(roomName, fileName);
            if (group.Count == 0) continue;
            return PickWithinGroup(group);
        }

        return null;
    }

    private static string PickWithinGroup(List<string> candidates)
    {
        string withRainCycles = null;
        foreach (string path in candidates)
        {
            string content = ReadRainCyclesLine(path);
            if (content == null) continue;

            int state = ParseStateTag(content);
            if (state >= 1 && state <= 4) return path;

            if (withRainCycles == null) withRainCycles = path;
        }

        return withRainCycles ?? candidates[0];
    }

    private static string ReadRainCyclesLine(string path)
    {
        try
        {
            foreach (string rawLine in File.ReadAllLines(path, Encoding.UTF8))
            {
                string line = rawLine.TrimEnd('\r');
                if (!line.StartsWith("RainCycles:", StringComparison.OrdinalIgnoreCase)) continue;
                return line.Substring("RainCycles:".Length);
            }
        }
        catch (Exception) { }

        return null;
    }

    private static int ReadStateTag(string path)
    {
        string content = ReadRainCyclesLine(path);
        return content == null ? 0 : ParseStateTag(content);
    }

    private static string DeriveRoomNameFromPath(string path)
    {
        string name = Path.GetFileNameWithoutExtension(path);
        int idx = name.IndexOf("_settings", StringComparison.OrdinalIgnoreCase);
        return idx > 0 ? name.Substring(0, idx) : null;
    }
    
    // HOOKS
    
    private static void OnGameCtor(On.RainWorldGame.orig_ctor orig, RainWorldGame self, ProcessManager manager)
    {
        orig(self, manager);
        if (RCGate.Suspended) return;
        _frozenCycle    = self.GetStorySession?.saveState?.cycleNumber ?? 0;
        _hasFrozenCycle = true;
        ClearCaches();
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
            ClearCaches();
        }
    }

    private static void OnGameShutDown(On.RainWorldGame.orig_ShutDownProcess orig, RainWorldGame self)
    {
        _hasFrozenCycle = false;
        _cycleAdvancedThisSession = false;
        _currentCycleState = 1;
        ClearCaches();
        _cachedSlugcatSuffix = "";
        _cachedBaseSuffix = "";
        _cachedActiveDLCs?.Clear();
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

        if (RCGate.Suspended) return;
        if (room == null || room.game == null) return;
        if (_blockLoad) return;

        // El timeline del ctor es el dato exacto que usa el juego para elegir el base
        if (timelinePoint != null) _cachedBaseSuffix = TimelineSuffix(timelinePoint);

        var session = room.game.GetStorySession;
        if (session?.saveState == null)
        {
            if (!_arenaMode) return;

            int arenaState = Math.Max(1, _currentCycleState);
            string arenaPath = ResolveSettingsPath(name, arenaState);
            if (arenaPath == null) return;

            self.filePath = arenaPath;
            RoomSettingsPatches.RefreshParent(self, region);
            self.Load((SlugcatStats.Timeline)null);
            AncestorResolver.ApplyAncestor(self, name, region, arenaState);

            var snapArena = SettingsSnapshot.GetCached(arenaPath, name);
            if (!snapArena._hasTerrainFadePalette)
                self.terrainFadePalette = null;
            return;
        }

        int cycle = _hasFrozenCycle ? _frozenCycle : session.saveState.cycleNumber;
        int stateNumber = _currentCycleState;
        if (stateNumber < 1 || stateNumber > 4)
        {
            if (_cachedActiveDLCs == null || _cachedSlugcatSuffix == null)
                RebuildContextCache(game);
            stateNumber = CycleStateResolver.ResolveState(cycle);
            _currentCycleState = stateNumber;
        }
        string rainStatePath = ResolveSettingsPath(name, stateNumber);
        if (rainStatePath == null) return;

        self.filePath = rainStatePath;
        RoomSettingsPatches.RefreshParent(self, region);
        self.Load((SlugcatStats.Timeline)null);
        AncestorResolver.ApplyAncestor(self, name, region, stateNumber);

        var snap = SettingsSnapshot.GetCached(rainStatePath, name);
        if (!snap._hasTerrainFadePalette)
            self.terrainFadePalette = null;
    }

    // API PÚBLICA
    
    public static int GetCurrentCycleState() => _currentCycleState;
    
    public static void SetCurrentCycleState(int state)
    {
        _currentCycleState = state;
        ClearCaches();
    }
    
    public static void InvalidatePathCache()
    {
        ClearCaches();
    }
}
