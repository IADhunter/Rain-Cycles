using System;
using System.Collections.Generic;
using System.IO;
using RainCycles.Settings;
using RainCycles.Blend;

namespace RainCycles.Core;


public static class ArenaBlendController
{
    private static int _roundCount = 0;


    public static void Init()
    {
        On.ArenaGameSession.ctor += OnArenaSessionCtor;
    }

    public static void Terminate()
    {
        On.ArenaGameSession.ctor -= OnArenaSessionCtor;
    }


    private static void OnArenaSessionCtor(On.ArenaGameSession.orig_ctor orig, ArenaGameSession self, RainWorldGame game)
    {
        orig(self, game);

        if (self == null || self.arenaSitting == null) return;

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

        bool loaded = LoadBlendSettings(roomName);
        RSPlugin.log.LogInfo(
            $"[ArenaBlend] Ronda {_roundCount} level='{roomName}' state={state} blend={loaded}");
    }


    public static int ResolveState(string roomName)
    {
        var states = GetStates(roomName);
        if (states.Count == 0) return 1;

        string mode = RSPlugin.cycleMode?.Value ?? RCOptions.ModeCycle;
        bool isRandomMode = mode != RCOptions.ModeCycle;

        if (isRandomMode)
            return states[UnityEngine.Random.Range(0, states.Count)];

        return states[(_roundCount - 1) % states.Count];
    }

    public static bool HasSettings(string roomName) => GetStates(roomName).Count > 0;

    public static List<int> GetStates(string roomName)
    {
        var states = new List<int>();
        for (int i = 1; i <= 4; i++)
            if (ResolveSettingsPath(roomName, i) != null) states.Add(i);
        return states;
    }

    public static int CountSettingsFiles(string roomName) => GetStates(roomName).Count;


    public static string ResolveSettingsPath(string roomName, int state)
    {
        if (string.IsNullOrEmpty(roomName) || state < 1 || state > 4) return null;

        string fileName = $"{roomName.ToLowerInvariant()}_settings_{state}.txt";
        string numbered = FindFirst(fileName);
        if (numbered != null) return numbered;

        // El base del level (Levels/{level}_settings.txt) es el estado N si lo declara
        if (StateFileResolver.GetBaseState(roomName) == state)
            return StateFileResolver.GetBaseSettingsPath(roomName);

        return null;
    }

    public static string ResolveBlendSettingsPath(string roomName)
    {
        if (string.IsNullOrEmpty(roomName)) return null;

        string fileName = $"{roomName.ToLowerInvariant()}_blend_settings.txt";
        return FindFirst(fileName);
    }

    public static bool HasBlendSettings(string roomName) => ResolveBlendSettingsPath(roomName) != null;

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

    private static string GetTargetRoot(string roomName)
    {
        string ownerPath = ResolveLevelOwnerPath(roomName);
        if (ownerPath != null)
            return Path.Combine(ownerPath, "levels", "raincycles");

        foreach (string root in EnumerateRoots())
            return root;

        return Path.Combine(Application.streamingAssetsPath, "levels", "raincycles");
    }

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
        "wait_time: 0.0\n";

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

        if (settings.Mode == BlendMode.Rain)
        {
            RSPlugin.log.LogWarning($"[ArenaBlend] '{path}' usa Rain — no disponible en arena, forzado a Loop.");
            settings.Mode = BlendMode.Loop;
        }

        if (settings.Trigger != LoopTrigger.None)
        {
            RSPlugin.log.LogWarning($"[ArenaBlend] '{path}' usa trigger '{settings.Trigger}' — no disponible en arena, forzado a none.");
            settings.Trigger = LoopTrigger.None;
        }

        BlendSettingsLoader.SetActiveBlend(settings, roomName);
        return true;
    }
}
