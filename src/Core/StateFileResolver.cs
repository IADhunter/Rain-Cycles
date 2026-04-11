using System.IO;
using System.Text.RegularExpressions;

namespace RainCycles.Core;

// Resuelve rutas de archivos settings_N.txt por sala y estado. También incluye el hook de RoomSettings.ctor que rota el settings cargado según el número de ciclo actual.
// Modo lineal:   stateNumber = (cycle % n) + 1
// Modo aleatorio: seed dispersa con primo → rompe correlación entre cycles consecutivos
public static class StateFileResolver
{
    // Ciclo congelado al inicio de la partida → no cambia hasta el siguiente ciclo real.
    // Evita que OnRoomSettingsCtor rote al settings_{N+1} en los frames finales de hibernación.
    private static int  _frozenCycle    = 0;
    private static bool _hasFrozenCycle = false;

    public static void Init()
    {
        On.RoomSettings.ctor_Room_string_Region_bool_bool_Timeline_RainWorldGame += OnRoomSettingsCtor;
        On.RainWorldGame.ctor            += OnGameCtor;
        On.RainWorldGame.Win             += OnGameWin;
        On.RainWorldGame.ShutDownProcess += OnGameShutDown;
    }

    // Congela el ciclo cuando arranca la partida
    private static void OnGameCtor(On.RainWorldGame.orig_ctor orig, RainWorldGame self, ProcessManager manager)
    {
        orig(self, manager);
        _frozenCycle    = self.GetStorySession?.saveState?.cycleNumber ?? 0;
        _hasFrozenCycle = true;
        RSPlugin.log.LogInfo($"[StateFileResolver] Cycle frozen at {_frozenCycle}.");
    }

    // Avanzar el ciclo congelado DESPUÉS de orig() → los frames finales usan el ciclo viejo
    // Guard: Win() puede llamarse múltiples veces por sesión; solo avanzamos una vez.
    private static bool _cycleAdvancedThisSession = false;

    private static void OnGameWin(On.RainWorldGame.orig_Win orig, RainWorldGame self, bool mal, bool warp)
    {
        orig(self, mal, warp);
        if (!warp && !_cycleAdvancedThisSession)
        {
            _cycleAdvancedThisSession = true;
            _frozenCycle = self.GetStorySession?.saveState?.cycleNumber ?? _frozenCycle;
            RSPlugin.log.LogInfo($"[StateFileResolver] Cycle advanced to {_frozenCycle} post-win.");
        }
    }

    private static void OnGameShutDown(On.RainWorldGame.orig_ShutDownProcess orig, RainWorldGame self)
    {
        orig(self);
        _hasFrozenCycle = false;
        _cycleAdvancedThisSession = false;
    }

    // ── Hook de rotación por ciclo ────────────────────────────────────────
    // Cuando el juego construye un RoomSettings, interceptamos y reemplazamos
    // self.filePath con el settings_N.txt correspondiente al ciclo actual.
    // Así cada ciclo el juego carga un settings distinto automáticamente.
    // Bloqueado durante transición de Win → evita que Load() recargue fade palette
    // mientras la sala todavía es visible.
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
        if (rainStatePath != null)
        {
            self.filePath = rainStatePath;
            self.Load((SlugcatStats.Timeline)null);
        }
    }

    // ── API pública ───────────────────────────────────────────────────────

    // Devuelve el path del settings_N.txt que corresponde al ciclo dado.
    // Modo lineal:   (cycle % n) + 1
    // Modo aleatorio: seed = cycle dispersado con primo → distribucion uniforme sin correlacion entre cycles consecutivos
    public static string GetRainStateFilePath(string roomName, int cycle)
    {
        int n = CountRainStateFiles(roomName);
        if (n == 0) return null;

        int stateNumber;
        if (RSPlugin.randomCycles != null && RSPlugin.randomCycles.Value)
        {
            // Multiplicar por un primo grande dispersa los bits de seeds consecutivas,
            // eliminando la correlacion que produce System.Random con seeds 84,85,86...
            int seed = unchecked(cycle * 1000003);
            stateNumber = new System.Random(seed).Next(1, n + 1);
        }
        else
        {
            stateNumber = (cycle % n) + 1;
        }

        RSPlugin.log.LogDebug($"[StateFileResolver] cycle={cycle} n={n} state={stateNumber} random={RSPlugin.randomCycles?.Value}");
        return FindFileInRainCycles(roomName, stateNumber);
    }

    // Devuelve el path de settings_N.txt para el número de estado dado. Busca recursivamente en subcarpetas de RainCycles. Null si no existe.
    public static string GetRainStateSettingsFile(string roomName, int number)
        => FindFileInRainCycles(roomName, number);

    // Cuenta cuántos settings_N.txt existen para una sala (consecutivos desde 1). Busca recursivamente en subcarpetas de RainCycles.
    public static int CountRainStateFiles(string roomName)
    {
        int count = 0;
        while (FindFileInRainCycles(roomName, count + 1) != null)
            count++;
        return count;
    }

    // Busca {roomName}_settings_{number}.txt en la carpeta RainCycles y cualquier subcarpeta.
    // Devuelve la primera coincidencia, o null si no existe.
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

    // Extrae el número de estado de un path. Ejemplo: "uw_h01_settings_2.txt" → 2. Devuelve -1 si falla.
    public static int GetStateFromPath(string path, string roomName)
    {
        if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(roomName)) return -1;
        string fileName = Path.GetFileNameWithoutExtension(path);
        int idx = fileName.ToLowerInvariant().LastIndexOf("_settings_");
        if (idx < 0) return -1;
        string numStr = fileName.Substring(idx + "_settings_".Length);
        return int.TryParse(numStr, out int n) ? n : -1;
    }

    // Crea un nuevo settings_N.txt copiando el roomSettings actual de la sala. Usado por RCPanel al añadir un nuevo estado desde DevTools.
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

    // ── Helpers privados ──────────────────────────────────────────────────

    // Resuelve la carpeta RainCycles para una sala recorriendo el stack de mods activos.
    private static string BuildDirectoryPath(string roomName)
    {
        string regionCode   = Regex.Split(roomName, "_")[0].ToUpperInvariant();
        string regionFolder = "World" + Path.DirectorySeparatorChar +
                              regionCode + "-Rooms" + Path.DirectorySeparatorChar +
                              "RainCycles";

        foreach (var mod in ModManager.ActiveMods)
        {
            string candidate = Path.Combine(mod.path, regionFolder);
            if (Directory.Exists(candidate))
                return candidate;
        }

        return AssetManager.ResolveFilePath(regionFolder);
    }
}