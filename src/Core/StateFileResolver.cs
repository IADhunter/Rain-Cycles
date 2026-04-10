using System.IO;
using System.Text.RegularExpressions;

namespace RainCycles.Core;

// Resuelve rutas de archivos settings_N.txt por sala y estado. También incluye el hook de RoomSettings.ctor que rota el settings cargado según el número de ciclo actual (cycle % n + 1).
public static class StateFileResolver
{
    public static void Init()
    {
        On.RoomSettings.ctor_Room_string_Region_bool_bool_Timeline_RainWorldGame += OnRoomSettingsCtor;
    }

    // ── Hook de rotación por ciclo ────────────────────────────────────────
    // Cuando el juego construye un RoomSettings, interceptamos y reemplazamos
    // self.filePath con el settings_N.txt correspondiente al ciclo actual.
    // Así cada ciclo el juego carga un settings distinto automáticamente.
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

        int cycle = session.saveState.cycleNumber;
        string rainStatePath = GetRainStateFilePath(name, cycle);
        if (rainStatePath != null)
        {
            self.filePath = rainStatePath;
            self.Load((SlugcatStats.Timeline)null);
        }
    }

    // ── API pública ───────────────────────────────────────────────────────

    // Devuelve el path del settings_N.txt que corresponde al ciclo dado, rotando entre los N archivos disponibles. Null si no hay ninguno.
    public static string GetRainStateFilePath(string roomName, int cycle)
    {
        int n = CountRainStateFiles(roomName);
        if (n == 0) return null;

        int stateNumber = (cycle % n) + 1;
        string path = BuildFilePath(roomName, stateNumber);
        return File.Exists(path) ? path : null;
    }

    // Devuelve el path de settings_N.txt para el número de estado dado. Null si el archivo no existe.
    public static string GetRainStateSettingsFile(string roomName, int number)
    {
        string path = BuildFilePath(roomName, number);
        return File.Exists(path) ? path : null;
    }

    // Cuenta cuántos settings_N.txt existen para una sala (consecutivos desde 1).
    public static int CountRainStateFiles(string roomName)
    {
        int count = 0;
        while (File.Exists(BuildFilePath(roomName, count + 1)))
            count++;
        return count;
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

    private static string BuildFilePath(string roomName, int number)
    {
        return AssetManager.ResolveFilePath(
            "World" + Path.DirectorySeparatorChar +
            Regex.Split(roomName, "_")[0] + "-Rooms" + Path.DirectorySeparatorChar +
            "RainCycles" + Path.DirectorySeparatorChar +
            roomName + "_settings_" + number + ".txt");
    }

    private static string BuildDirectoryPath(string roomName)
    {
        return AssetManager.ResolveFilePath(
            "World" + Path.DirectorySeparatorChar +
            Regex.Split(roomName, "_")[0] + "-Rooms" + Path.DirectorySeparatorChar +
            "RainCycles");
    }
}
