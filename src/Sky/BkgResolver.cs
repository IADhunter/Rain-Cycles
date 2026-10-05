using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RainCycles.Sky;

// ================================================================
// BKG RESOLVER — fuente de verdad de la imagen de fondo por sala×estado.
// Lee el tag <Mod:mod,sky[,sun,fog]> del settings_N.txt del estado dado
// (vía SettingsSnapshot, que ya cachea por path). Sin fallback al
// blend settings viejo: sin tag → default (RC_Transparent en el slot).
// ================================================================
public static class BkgResolver
{
    // RoomSettings viva (instancia del estado seleccionado). Se fija en
    // cada Load (RoomSettingsPatches.OnLoad): las ediciones de bkg sin
    // guardar (SetBkg) viven en su ext data y el renderer las lee aquí.
    // Si su filePath no coincide con el path resuelto → fallback a disco.
    private static RoomSettings _live;

    public static void SetLive(RoomSettings settings) => _live = settings;

    // Tag del estado indicado de la sala. default si no hay sala/estado/tag.
    public static BkgTag.Data Get(string roomName, int state)
    {
        if (string.IsNullOrEmpty(roomName) || state < 1 || state > 4)
            return default;

        string path = StateFileResolver.ResolveSettingsPath(roomName, state);
        if (string.IsNullOrEmpty(path))
            return default;

        // Estado en memoria: ediciones sin guardar (persiste solo Save vanilla)
        if (_live != null && _live.IsRcStateLoaded() &&
            !string.IsNullOrEmpty(_live.filePath) &&
            string.Equals(_live.filePath, path, StringComparison.OrdinalIgnoreCase))
            return _live.GetBkg();

        var snap = SettingsSnapshot.GetCached(path, roomName);
        if (snap == null || !snap.HasBkg)
            return default;

        return snap.Bkg;
    }

    // ================================================================
    // OCULTACIÓN DE SLOTS VANILLA (regla aprobada 10/2026)
    //  1) Si ALGÚN estado (1-4) de la sala es Blend con <Mod> → ocultar
    //     SIEMPRE (el crossfade puede traer la imagen de cualquier estado
    //     y el hueco vacío en los demás estados se aceptó explícitamente).
    //  2) Si no → ocultar solo si el estado ACTIVO tiene <Mod>
    //     (cubre los 4 static: quitar bkg reaparece vanilla al instante).
    // Solo tiene sentido dentro del gate del call-site (sala Blend/Static
    // con <View>): sin view activa el juego dibuja el cielo vanilla y hay
    // que restaurarlo (el call-site hace Restore en esa rama).
    // ================================================================
    public static bool ShouldHideVanilla(string roomName, int activeState)
    {
        if (string.IsNullOrEmpty(roomName))
            return false;

        // Regla 1: ¿algún estado es Blend con <Mod>?
        for (int state = 1; state <= 4; state++)
            if (IsBlendWithBkg(roomName, state))
                return true;

        // Regla 2: si no, decidir por el estado activo.
        if (activeState < 1 || activeState > 4)
            return false;

        return HasBkgAt(roomName, activeState);
    }

    // El estado indicado es Blend y su settings trae <Mod:...>.
    private static bool IsBlendWithBkg(string roomName, int state)
    {
        string path = Paths4(roomName)[state];
        if (string.IsNullOrEmpty(path))
            return false;

        if (LiveMatches(path))
            return _live.GetRcType() == RcType.Blend && _live.GetBkg().IsValid;

        var snap = SettingsSnapshot.GetCached(path, roomName);
        return snap != null && snap.HasRcType && snap.RcType == RcType.Blend && snap.HasBkg;
    }

    // El estado indicado tiene <Mod:...> (cualquier Type).
    private static bool HasBkgAt(string roomName, int state)
    {
        string path = Paths4(roomName)[state];
        if (string.IsNullOrEmpty(path))
            return false;

        if (LiveMatches(path))
            return _live.GetBkg().IsValid;

        var snap = SettingsSnapshot.GetCached(path, roomName);
        return snap != null && snap.HasBkg;
    }

    // Rutas de los 4 estados, resueltas una vez por sala.
    // ResolveSettingsPath hace un Directory.GetFiles recursivo cuando un
    // estado no existe (los miss NO se cachean en _resolutionCache) →
    // jamás llamarlo por frame sin esta caché. Se invalida en AfterSave y
    // al borrar un estado (RCPanel RC_Minus); entre partidas la limpia
    // ModResetter (BkgResolver está en _typesToReset).
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

    // Invalidación de rutas por sala (creación/borrado de settings_N.txt).
    public static void InvalidatePaths(string roomName)
    {
        if (!string.IsNullOrEmpty(roomName))
            _statePaths.Remove(roomName);
    }

    // true si el settings vivo coincide con el path indicado (ediciones
    // sin guardar: SetBkg/SetRcType viven en su ext data).
    private static bool LiveMatches(string path)
        => _live != null && _live.IsRcStateLoaded() &&
           !string.IsNullOrEmpty(_live.filePath) &&
           string.Equals(_live.filePath, path, StringComparison.OrdinalIgnoreCase);

    // Nombre de atlas namespaced: rc_<mod>_<archivo>.
    // Espacio de nombres global único por mod → dos mods con un archivo
    // homónimo generan atlas distintos (evita que el guard idempotente
    // de Futile muestre la imagen del primer mod cargado).
    public static string AtlasName(string mod, string file)
    {
        if (string.IsNullOrEmpty(mod) || string.IsNullOrEmpty(file))
            return null;

        string cleanFile = Path.GetFileNameWithoutExtension(file);
        if (string.IsNullOrEmpty(cleanFile))
            return null;

        return "rc_" + Sanitize(mod) + "_" + Sanitize(cleanFile);
    }

    // minúsculas; no-alphanumerico (espacios, guiones…) → '_'
    private static string Sanitize(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (char c in s.ToLowerInvariant())
            sb.Append((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') ? c : '_');
        return sb.ToString();
    }
}
