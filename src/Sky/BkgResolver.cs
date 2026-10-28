using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RainCycles.Sky;

// BKG RESOLVER — fuente de verdad de la imagen de fondo por sala×estado.
public static class BkgResolver
{
    private static RoomSettings _live;

    public static void SetLive(RoomSettings settings) => _live = settings;

    public static BkgTag.Data Get(string roomName, int state)
    {
        if (string.IsNullOrEmpty(roomName) || state < 1 || state > 4)
            return default;

        string path = StateFileResolver.ResolveSettingsPath(roomName, state);
        if (string.IsNullOrEmpty(path))
            return default;

        if (_live != null && _live.IsRcStateLoaded() &&
            !string.IsNullOrEmpty(_live.filePath) &&
            string.Equals(_live.filePath, path, StringComparison.OrdinalIgnoreCase))
            return _live.GetBkg();

        var snap = SettingsSnapshot.GetCached(path, roomName);
        if (snap == null || !snap.HasBkg)
            return default;

        return snap.Bkg;
    }

    // OCULTACIÓN DE SLOTS VANILLA (regla aprobada 10/2026)
    public static bool ShouldHideVanilla(string roomName, int activeState)
    {
        if (string.IsNullOrEmpty(roomName))
            return false;

        for (int state = 1; state <= 4; state++)
            if (IsBlendWithBkg(roomName, state))
                return true;

        if (activeState < 1 || activeState > 4)
            return false;

        return HasBkgAt(roomName, activeState);
    }

    private static bool IsBlendWithBkg(string roomName, int state)
    {
        string path = Paths4(roomName)[state];
        if (string.IsNullOrEmpty(path))
            return false;

        if (LiveMatches(path))
            return _live.GetRcType() == RcType.Blend && _live.GetBkg().HasMod;

        var snap = SettingsSnapshot.GetCached(path, roomName);
        return snap != null && snap.HasRcType && snap.RcType == RcType.Blend && snap.HasBkg;
    }

    private static bool HasBkgAt(string roomName, int state)
    {
        string path = Paths4(roomName)[state];
        if (string.IsNullOrEmpty(path))
            return false;

        if (LiveMatches(path))
            return _live.GetBkg().HasMod;

        var snap = SettingsSnapshot.GetCached(path, roomName);
        return snap != null && snap.HasBkg;
    }

    private static Dictionary<string, string[]> _statePaths =
        new(StringComparer.OrdinalIgnoreCase);

    private static string[] Paths4(string roomName)
    {
        if (!_statePaths.TryGetValue(roomName, out var paths))
        {
            paths = new string[5];
            for (int state = 1; state <= 4; state++)
                paths[state] = StateFileResolver.ResolveSettingsPath(roomName, state);
            _statePaths[roomName] = paths;
        }
        return paths;
    }

    public static void InvalidatePaths(string roomName)
    {
        if (!string.IsNullOrEmpty(roomName))
            _statePaths.Remove(roomName);
    }

    private static bool LiveMatches(string path)
        => _live != null && _live.IsRcStateLoaded() &&
           !string.IsNullOrEmpty(_live.filePath) &&
           string.Equals(_live.filePath, path, StringComparison.OrdinalIgnoreCase);

    public static string AtlasName(string mod, string file)
    {
        if (string.IsNullOrEmpty(mod) || string.IsNullOrEmpty(file))
            return null;

        string cleanFile = Path.GetFileNameWithoutExtension(file);
        if (string.IsNullOrEmpty(cleanFile))
            return null;

        return "rc_" + Sanitize(mod) + "_" + Sanitize(cleanFile);
    }

    private static string Sanitize(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (char c in s.ToLowerInvariant())
            sb.Append((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') ? c : '_');
        return sb.ToString();
    }
}
