using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace RainCycles.Core;

// Resuelve rutas de settings_N.txt y rota el archivo cargado según el ciclo.
public static class StateFileResolver
{
    private static int  _frozenCycle    = 0;
    private static bool _hasFrozenCycle = false;
    
    // Estado actual del ciclo (1..4)
    private static int _currentCycleState = 1;

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
        RSPlugin.log.LogDebug($"[StateFileResolver] Estado manual establecido: {state}");
    }

    public static string GetRainStateFilePath(string roomName, int cycle)
    {
        // Usar el estado actual (ya calculado por LoadRegion)
        int stateNumber = _currentCycleState;
        
        RSPlugin.log.LogDebug($"[StateFileResolver] Usando estado {stateNumber} para sala {roomName} (ciclo {cycle})");

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