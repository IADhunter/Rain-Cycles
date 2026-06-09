using UnityEngine;
using System.Collections.Generic;
using RainCycles.Settings;
using RainCycles.Snapshot;

namespace RainCycles.Core;

// Sistema de tintes estáticos para salas con RC_TYPE: Static
public static class StaticTintManager
{
    private static readonly Dictionary<string, SettingsSnapshot> _snapCache =
        new Dictionary<string, SettingsSnapshot>(System.StringComparer.OrdinalIgnoreCase);

    // ── Caché ────────────────────────────────────────────────────────────

    public static void PreloadRegionTemplates(string regionCode)
    {
        if (string.IsNullOrEmpty(regionCode)) return;
        string upper = regionCode.ToUpperInvariant();
        string searchPattern = $"{upper}_settingstemplate_*.txt";
        string regionFolder = System.IO.Path.Combine("World", upper);

        for (int i = ModManager.ActiveMods.Count - 1; i >= 0; i--)
        {
            string dir = System.IO.Path.Combine(ModManager.ActiveMods[i].path, regionFolder);
            if (System.IO.Directory.Exists(dir))
            {
                foreach (string file in System.IO.Directory.GetFiles(dir, searchPattern))
                {
                    if (!_snapCache.ContainsKey(file))
                        _snapCache[file] = SettingsSnapshot.FromFile(file);
                }
            }
        }

        string vanillaDir = System.IO.Path.Combine(Application.streamingAssetsPath, regionFolder);
        if (System.IO.Directory.Exists(vanillaDir))
        {
            foreach (string file in System.IO.Directory.GetFiles(vanillaDir, searchPattern))
            {
                if (!_snapCache.ContainsKey(file))
                    _snapCache[file] = SettingsSnapshot.FromFile(file);
            }
        }
    }

    public static bool IsStaticViewRoom(Room room)
    {
        if (room == null) return false;
        var snap = GetCachedSnapshot(room);
        return snap != null && snap.HasRcType && snap.RcType == RcType.Static;
    }

    public static SettingsSnapshot GetCachedSnapshot(Room room)
    {
        string path = room?.roomSettings?.filePath;
        if (string.IsNullOrEmpty(path)) return null;

        if (!_snapCache.TryGetValue(path, out var snap))
        {
            string roomName = room.abstractRoom?.name ?? "";
            snap = SettingsSnapshot.FromFileWithTemplate(path, roomName);
            _snapCache[path] = snap;
        }
        return snap;
    }

    public static SettingsSnapshot GetCachedSnapshot(string path, string roomName)
    {
        if (string.IsNullOrEmpty(path)) return null;
        if (!_snapCache.TryGetValue(path, out var snap))
        {
            snap = SettingsSnapshot.FromFileWithTemplate(path, roomName);
            _snapCache[path] = snap;
        }
        return snap;
    }

    public static bool TryGetCachedPath(string path, out SettingsSnapshot snap)
    {
        return _snapCache.TryGetValue(path, out snap);
    }

    /// <summary>
    /// Invalida la caché para un archivo específico.
    /// Útil después de guardar cambios en disco.
    /// </summary>
    public static void InvalidateCache(string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return;
        
        if (_snapCache.Remove(filePath))
        {
            RSPlugin.log.LogDebug($"[StaticTintManager] Caché invalidada: {System.IO.Path.GetFileName(filePath)}");
        }
    }

    /// <summary>
    /// Invalida toda la caché (útil para reset completo).
    /// </summary>
    public static void InvalidateAllCache()
    {
        int count = _snapCache.Count;
        _snapCache.Clear();
        RSPlugin.log.LogDebug($"[StaticTintManager] Caché completa invalidada: {count} entradas eliminadas");
    }

    // ── Aplicación ──────────────────────────────────────────────────────

    public static void ApplyForRoom(Room room)
    {
        if (room == null) return;
        string roomName = room.abstractRoom?.name;
        if (string.IsNullOrEmpty(roomName)) return;

        string path = room.roomSettings?.filePath;
        if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
            return;

        var snap = GetCachedSnapshot(room);

        // Solo aplicar tintes si la sala es estática (RC_TYPE: Static)
        if (snap.HasRcType && snap.RcType == RcType.Static)
        {
            if (snap.TintMultiply.HasValue)
            {
                var c = snap.TintMultiply.Value;
                Shader.SetGlobalVector(RainWorld.ShadPropMultiplyColor, new Vector4(c.r, c.g, c.b, 1f));
            }
            if (snap.TintAtmosphere.HasValue)
            {
                var c = snap.TintAtmosphere.Value;
                Shader.SetGlobalVector(RainWorld.ShadPropAboveCloudsAtmosphereColor, new Vector4(c.r, c.g, c.b, 1f));
            }
            if (snap.TintCloudAtmosphere.HasValue)
            {
                for (int i = 0; i < room.updateList.Count; i++)
                {
                    if (room.updateList[i] is AboveCloudsView acv)
                    {
                        acv.atmosphereColor = snap.TintCloudAtmosphere.Value;
                        break;
                    }
                }
            }
        }
    }

    public static void ApplyCloudAtmosphereToInstance(AboveCloudsView acv, Room room)
    {
        if (acv == null || room == null) return;
        string roomName = room.abstractRoom?.name;
        if (string.IsNullOrEmpty(roomName)) return;

        string path = room.roomSettings?.filePath;
        if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
            return;

        var snap = GetCachedSnapshot(room);

        // Solo aplicar si la sala es estática
        if (snap.HasRcType && snap.RcType == RcType.Static && snap.TintCloudAtmosphere.HasValue)
            acv.atmosphereColor = snap.TintCloudAtmosphere.Value;
    }

    public static void Init()
    {
        // On.RoomCamera.UpdateDayNightPalette += OnUpdateDayNightPalette;  // ELIMINADO - El mod no debe interferir con DayNight
    }

    // ── PSV defaults ────────────────────────────────────────────────────

    private static readonly Color PSV_MULTIPLY = new Color(1f, 1f, 1f);
    private static readonly Color PSV_ATMOSPHERE = new Color(0.682f, 0.286f, 0.529f);

    public static void ApplyPSVDefaults(Room room)
    {
        if (room?.roomSettings?.filePath == null) return;
        var snap = GetCachedSnapshot(room);
        if (snap == null) return;
        // Solo aplicar defaults si no es estática ni blend
        if (snap.HasRcType && (snap.RcType == RcType.Static || snap.RcType == RcType.Blend)) return;
        if (!snap.Effects.Contains("PinkSky")) return;

        Shader.SetGlobalVector(RainWorld.ShadPropMultiplyColor,
            new Vector4(PSV_MULTIPLY.r, PSV_MULTIPLY.g, PSV_MULTIPLY.b, 1f));
        Shader.SetGlobalVector(RainWorld.ShadPropAboveCloudsAtmosphereColor,
            new Vector4(PSV_ATMOSPHERE.r, PSV_ATMOSPHERE.g, PSV_ATMOSPHERE.b, 1f));
    }
}