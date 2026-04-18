using RainCycles.Settings;

namespace RainCycles.Core;

// Compatibilidad Arena — carga settings_N.txt al inicio de cada partida
// hookeando RoomSettings.ctor igual que StateFileResolver hace para Story.
//
// Flujo:
//   ArenaGameSession.ctor  → selecciona path del state, lo guarda en _selectedPath
//   RoomSettings.ctor      → si es sala arena activa, reemplaza filePath y recarga
//   ArenaGameSession.Initiate → activa blend si hay blend_settings.txt
//   ArenaGameSession.EndSession / ShutDownProcess → avanza contador, limpia

public static class ArenaBlendController
{
    // Contador de partidas en memoria. Avanza al terminar o salir de cada partida.
    private static int    _sessionCount         = 0;
    private static string _currentRoom          = null;
    private static string _selectedSettingsPath = null;
    private static bool   _sessionAdvanced      = false; // guard: solo avanzar una vez por partida

    // ── Init ──────────────────────────────────────────────────────────────

    public static void Init()
    {
        On.ArenaGameSession.ctor       += OnArenaCtor;
        On.ArenaGameSession.Initiate   += OnArenaInitiate;
        On.ArenaGameSession.EndSession += OnArenaEndSession;
        On.RainWorldGame.ShutDownProcess += OnShutDown;
        On.RoomSettings.ctor_Room_string_Region_bool_bool_Timeline_RainWorldGame
                                       += OnRoomSettingsCtor;
    }

    public static void Terminate()
    {
        On.ArenaGameSession.ctor       -= OnArenaCtor;
        On.ArenaGameSession.Initiate   -= OnArenaInitiate;
        On.ArenaGameSession.EndSession -= OnArenaEndSession;
        On.RainWorldGame.ShutDownProcess -= OnShutDown;
        On.RoomSettings.ctor_Room_string_Region_bool_bool_Timeline_RainWorldGame
                                       -= OnRoomSettingsCtor;
    }

    // ── Hooks ─────────────────────────────────────────────────────────────

    private static void OnArenaCtor(
        On.ArenaGameSession.orig_ctor orig,
        ArenaGameSession self,
        RainWorldGame game)
    {
        orig(self, game);

        string roomName = self.arenaSitting?.GetCurrentLevel;
        if (string.IsNullOrEmpty(roomName)) return;
        if (!ArenaStateResolver.HasSettings(roomName)) return;

        _currentRoom     = roomName;
        _sessionAdvanced = false;

        // Resolver una sola vez — SelectState con DateTime.Ticks no es idempotente
        int state = ArenaStateResolver.SelectState(roomName, _sessionCount);
        _selectedSettingsPath = ArenaStateResolver.GetSettingsPath(roomName, state);

        RSPlugin.log.LogInfo(
            $"[ArenaBlend] Room='{roomName}' sessionCount={_sessionCount} " +
            $"state={state} path={_selectedSettingsPath}");
    }

    private static void OnRoomSettingsCtor(
        On.RoomSettings.orig_ctor_Room_string_Region_bool_bool_Timeline_RainWorldGame orig,
        RoomSettings self,
        Room room, string name, Region region,
        bool template, bool firstTemplate,
        SlugcatStats.Timeline timelinePoint,
        RainWorldGame game)
    {
        orig(self, room, name, region, template, firstTemplate, timelinePoint, game);

        if (_selectedSettingsPath == null) return;
        if (_currentRoom == null) return;
        if (template || firstTemplate) return;
        if (!string.Equals(name, _currentRoom, System.StringComparison.OrdinalIgnoreCase)) return;

        self.filePath = _selectedSettingsPath;
        self.Load((SlugcatStats.Timeline)null);

        RSPlugin.log.LogInfo(
            $"[ArenaBlend] RoomSettings loaded for '{name}' → {_selectedSettingsPath}");
    }

    private static void OnArenaInitiate(
        On.ArenaGameSession.orig_Initiate orig,
        ArenaGameSession self)
    {
        orig(self);

        if (_currentRoom == null) return;

        if (ArenaStateResolver.HasBlendSettings(_currentRoom))
        {
            string blendPath = ArenaStateResolver.GetBlendSettingsPath(_currentRoom);
            BlendSettingsLoader.LoadFromPath(blendPath, _currentRoom);

            // Arrancar el clock — en arena el updater no lo hace automáticamente
            var s = BlendSettingsLoader.Active;
            if (s != null && !BlendClock.IsRunning && !BlendClock.EditMode)
            {
                int n = ArenaStateResolver.CountSettingsFiles(_currentRoom);
                int initial = n > 0 ? (_sessionCount % n) + 1 : 1;
                BlendClock.Start(initial);
            }

            RSPlugin.log.LogInfo(
                $"[ArenaBlend] Blend active for '{_currentRoom}' — {blendPath}");
        }
    }

    private static void OnArenaEndSession(
        On.ArenaGameSession.orig_EndSession orig,
        ArenaGameSession self)
    {
        orig(self);
        AdvanceSession("EndSession");
    }

    // Captura salida por menú sin EndSession
    private static void OnShutDown(
        On.RainWorldGame.orig_ShutDownProcess orig,
        RainWorldGame self)
    {
        orig(self);

        if (_currentRoom != null)
            AdvanceSession("ShutDown");
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static void AdvanceSession(string source)
    {
        if (_sessionAdvanced) return; // ya avanzamos esta partida
        _sessionAdvanced = true;

        if (SettingsBlendController.IsActive)
            SettingsBlendController.Detach();

        BlendClock.Stop();
        BlendSettingsLoader.ClearArenaSession();

        _sessionCount++;
        _currentRoom          = null;
        _selectedSettingsPath = null;

        RSPlugin.log.LogDebug(
            $"[ArenaBlend] Session ended ({source}). Next sessionCount={_sessionCount}");
    }
}