using System;
using System.Collections.Generic;
using System.IO;
using RainCycles.Settings;
using RainCycles.Blend;

namespace RainCycles.Core;

// Compatibilidad Arena — sistema único de administración (sustituye a la carpeta Arena/ legacy).
//
// Diferencias con historia (confirmadas con el usuario):
//   - Blend settings: UN archivo por level: {level}_blend_settings.txt
//   - Modos: solo Loop y Cycle. EndCycle -> forzado a Loop. Triggers de Loop -> forzados a none.
//   - Setting (redirección de estado vanilla) -> ignorado (0).
//   - Estado (settings_N.txt): randomCycles de Remix ON -> aleatorio REAL por ronda;
//     OFF -> secuencial por contador de rondas en memoria (persiste entre partidas del proceso).
//   - El contador NO se resetea entre partidas (ModResetter no incluye esta clase).
//
// Ubicación de archivos (búsqueda por cada mod activo en orden inverso + StreamingAssets):
//   {mod}/levels/raincycles/  y  StreamingAssets/levels/raincycles/
//   Recursivo en subcarpetas, igual que el legacy.
//
// Flujo: ArenaGameSession.ctor (la sesión se crea en RainWorldGame.ctor ANTES del
// OverWorld/World, así que el flag y el estado ya están listos cuando se crean las
// salas -> StateFileResolver.OnRoomSettingsCtor redirige el filePath al settings_N.txt)
//   -> SetArenaMode(true) -> resolver estado -> LoadBlendSettings.
// El clock lo arranca BlendClockUpdater.OnGameUpdate (mismo gate s.Clock y auto-restart
// al salir de EditMode que el modo historia).
// En ShutDownProcess ModResetter para el clock, desengancha el blend y limpia estáticos
// (incluido el flag de arena de StateFileResolver).

public static class ArenaBlendController
{
    // Contador de rondas en memoria. No se resetea entre partidas (persiste por proceso).
    private static int _roundCount = 0;

    // ── Init ──────────────────────────────────────────────────────────────

    public static void Init()
    {
        On.ArenaGameSession.ctor += OnArenaSessionCtor;
    }

    public static void Terminate()
    {
        On.ArenaGameSession.ctor -= OnArenaSessionCtor;
    }

    // ── Hook principal ────────────────────────────────────────────────────

    private static void OnArenaSessionCtor(On.ArenaGameSession.orig_ctor orig, ArenaGameSession self, RainWorldGame game)
    {
        orig(self, game);

        if (self == null || self.arenaSitting == null) return;

        // El resolver de rutas pasa a modo arena (ModResetter ya lo puso en false al resetear).
        StateFileResolver.SetArenaMode(true);
        StateFileResolver.InvalidatePathCache();
        RoomCameraExtensions.InvalidateAllRoomCaches();

        string roomName = self.arenaSitting.GetCurrentLevel;
        if (string.IsNullOrEmpty(roomName))
        {
            RSPlugin.log.LogWarning("[ArenaBlend] No se pudo obtener el level actual.");
            return;
        }

        _roundCount++;
        int state = ResolveState(roomName);
        StateFileResolver.SetCurrentCycleState(state);

        // Solo carga el blend per-level como Active; el clock lo arranca
        // BlendClockUpdater (mismo gate s.Clock que historia, y se re-arranca al salir de EditMode).
        bool loaded = LoadBlendSettings(roomName);
        RSPlugin.log.LogInfo(
            $"[ArenaBlend] Ronda {_roundCount} level='{roomName}' state={state} blend={loaded}");
    }

    // ── Estado (settings_N.txt) ───────────────────────────────────────────

    // Aleatorio real por ronda o secuencial por contador. n > 0 garantizado por el llamador.
    // El modo secuencial usa el contador de rondas; cualquier modo aleatorio (2/3/4) usa
    // aleatorio real por ronda.
    public static int ResolveState(string roomName)
    {
        int n = CountSettingsFiles(roomName);
        if (n == 0) return 1;

        string mode = RSPlugin.cycleMode?.Value ?? RCOptions.ModeCycle;
        bool isRandomMode = mode != RCOptions.ModeCycle;

        if (isRandomMode)
            return UnityEngine.Random.Range(1, n + 1);

        // _roundCount ya fue incrementado: primera ronda (1) -> estado 1.
        return ((_roundCount - 1) % n) + 1;
    }

    public static bool HasSettings(string roomName) => CountSettingsFiles(roomName) > 0;

    // Cuenta settings_N.txt consecutivos desde 1.
    public static int CountSettingsFiles(string roomName)
    {
        int count = 0;
        while (ResolveSettingsPath(roomName, count + 1) != null)
            count++;
        return count;
    }

    // ── Resolución de rutas ───────────────────────────────────────────────

    // Ruta de {room}_settings_{state}.txt. Null si no existe.
    public static string ResolveSettingsPath(string roomName, int state)
    {
        if (string.IsNullOrEmpty(roomName) || state < 1) return null;

        string fileName = $"{roomName.ToLowerInvariant()}_settings_{state}.txt";
        return FindFirst(fileName);
    }

    // Ruta de {room}_blend_settings.txt. Null si no existe.
    public static string ResolveBlendSettingsPath(string roomName)
    {
        if (string.IsNullOrEmpty(roomName)) return null;

        string fileName = $"{roomName.ToLowerInvariant()}_blend_settings.txt";
        return FindFirst(fileName);
    }

    public static bool HasBlendSettings(string roomName) => ResolveBlendSettingsPath(roomName) != null;

    // Búsqueda directa en raíz y recursiva en subcarpetas, por mod activo (descendente) y StreamingAssets.
    private static string FindFirst(string fileName)
    {
        foreach (string root in EnumerateRoots())
        {
            string direct = Path.Combine(root, fileName);
            if (File.Exists(direct)) return direct;

            foreach (string found in Directory.GetFiles(root, fileName, SearchOption.AllDirectories))
                return found;
        }
        return null;
    }

    private static IEnumerable<string> EnumerateRoots()
    {
        for (int i = ModManager.ActiveMods.Count - 1; i >= 0; i--)
        {
            string modRoot = Path.Combine(ModManager.ActiveMods[i].path, "levels", "raincycles");
            if (Directory.Exists(modRoot)) yield return modRoot;
        }

        string baseRoot = Path.Combine(Application.streamingAssetsPath, "levels", "raincycles");
        if (Directory.Exists(baseRoot)) yield return baseRoot;
    }

    // ── Carga del blend settings per-level ────────────────────────────────

    // Crea {room}_blend_settings.txt si no existe. Devuelve la ruta o null.
    public static string EnsureBlendSettingsFile(string roomName)
    {
        if (string.IsNullOrEmpty(roomName)) return null;

        string existing = ResolveBlendSettingsPath(roomName);
        if (existing != null) return existing;

        string targetRoot = GetTargetRoot(roomName);
        try
        {
            Directory.CreateDirectory(targetRoot);
        }
        catch (Exception ex)
        {
            RSPlugin.log.LogWarning($"[ArenaBlend] No se pudo crear '{targetRoot}': {ex.Message}");
            return null;
        }

        string path = Path.Combine(targetRoot, $"{roomName.ToLowerInvariant()}_blend_settings.txt");
        try
        {
            File.WriteAllText(path, DefaultTemplate, System.Text.Encoding.UTF8);
            RSPlugin.log.LogInfo($"[ArenaBlend] Creado blend settings para level '{roomName}': {path}");
            return path;
        }
        catch (Exception ex)
        {
            RSPlugin.log.LogWarning($"[ArenaBlend] No se pudo escribir '{path}': {ex.Message}");
            return null;
        }
    }

    // Crea {room}_settings_N.txt de arena (invocado por StateFileResolver.CreateNewRainStateFile).
    // Guarda el roomSettings actual como plantilla — mismo enfoque que historia.
    public static string CreateSettingsFile(string roomName, int state, Room room)
    {
        if (string.IsNullOrEmpty(roomName) || room == null) return null;

        string targetRoot = GetTargetRoot(roomName);
        try
        {
            Directory.CreateDirectory(targetRoot);
        }
        catch (Exception ex)
        {
            RSPlugin.log.LogWarning($"[ArenaBlend] No se pudo crear '{targetRoot}': {ex.Message}");
            return null;
        }

        string filePath = Path.Combine(targetRoot, $"{roomName.ToLowerInvariant()}_settings_{state}.txt");
        try
        {
            room.roomSettings.filePath = filePath;
            room.roomSettings.Save();
            RoomCameraExtensions.InvalidateRoomCache(roomName);
            StateFileResolver.InvalidatePathCache();
            RSPlugin.log.LogInfo($"[ArenaBlend] Creado settings arena para level '{roomName}' estado {state}: {filePath}");
            return filePath;
        }
        catch (Exception ex)
        {
            RSPlugin.log.LogWarning($"[ArenaBlend] No se pudo escribir '{filePath}': {ex.Message}");
            return null;
        }
    }

    // Carpeta levels/raincycles de destino para archivos propios del level.
    // Prioridad: mod que tiene el level -> primer root existente -> StreamingAssets.
    private static string GetTargetRoot(string roomName)
    {
        string ownerPath = ResolveLevelOwnerPath(roomName);
        if (ownerPath != null)
            return Path.Combine(ownerPath, "levels", "raincycles");

        foreach (string root in EnumerateRoots())
            return root;

        return Path.Combine(Application.streamingAssetsPath, "levels", "raincycles");
    }

    // Mod activo (descendente) cuya carpeta levels/ contiene el level de arena.
    private static string ResolveLevelOwnerPath(string roomName)
    {
        string levelFileName = $"{roomName.ToLowerInvariant()}.txt";
        for (int i = ModManager.ActiveMods.Count - 1; i >= 0; i--)
        {
            if (File.Exists(Path.Combine(ModManager.ActiveMods[i].path, "levels", levelFileName)))
                return ModManager.ActiveMods[i].path;
        }
        return null;
    }

    private const string DefaultTemplate =
        "Clock: false\n" +
        "Mode: loop\n" +
        "Idle_time: 5.0\n" +
        "Duration: 10.0\n" +
        "Trigger: none\n" +
        "wait_time: 0.0\n" +
        "Setting: 0\n";

    // Carga {room}_blend_settings.txt en BlendSettingsLoader.Active con coerciones de arena.
    // Devuelve false si no existe o falla el parseo (Active queda null -> sin blend).
    public static bool LoadBlendSettings(string roomName)
    {
        string path = ResolveBlendSettingsPath(roomName);
        if (path == null)
        {
            BlendSettingsLoader.SetActiveBlend(null, roomName);
            return false;
        }

        BlendSettings settings = BlendSettingsLoader.LoadFile(path);
        if (settings == null)
        {
            RSPlugin.log.LogWarning($"[ArenaBlend] No se pudo parsear '{path}'.");
            BlendSettingsLoader.SetActiveBlend(null, roomName);
            return false;
        }

        // Coerciones arena: modos y triggers dependientes del ciclo de lluvia de historia.
        if (settings.Mode == BlendMode.EndCycle)
        {
            RSPlugin.log.LogWarning($"[ArenaBlend] '{path}' usa EndCycle — no disponible en arena, forzado a Loop.");
            settings.Mode = BlendMode.Loop;
        }

        if (settings.Trigger != LoopTrigger.None)
        {
            RSPlugin.log.LogWarning($"[ArenaBlend] '{path}' usa trigger '{settings.Trigger}' — no disponible en arena, forzado a none.");
            settings.Trigger = LoopTrigger.None;
        }

        settings.Setting = 0;

        BlendSettingsLoader.SetActiveBlend(settings, roomName);
        return true;
    }
}