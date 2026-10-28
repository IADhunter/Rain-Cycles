using System;
using System.IO;
using System.Text;
using System.Globalization;
using System.Collections.Generic;
using UnityEngine;

namespace RainCycles.Core;

// ANCESTOR REGIONAL — Fallback final para salas Template: NONE (gates: carpeta world/gates propia)

public static class AncestorResolver
{
    private const int MAX_STATES = 4;
    private static readonly NumberStyles NF = NumberStyles.Float;
    private static readonly CultureInfo INV = CultureInfo.InvariantCulture;

    private static readonly Dictionary<string, RoomSettings[]> _cache
        = new Dictionary<string, RoomSettings[]>(StringComparer.OrdinalIgnoreCase);

    // API PÚBLICA

    public static void ApplyAncestor(RoomSettings self, string roomName, Region region, int state)
    {
        if (self == null) return;
        if (state < 1 || state > MAX_STATES) return;

        if (self.isTemplate || self.isAncestor) return;

        if (self.parent != DefaultRoomSettings.ancestor) return;

        RoomSettings ancestor = GetAncestor(roomName, region?.name, state);
        if (ancestor == null) return;

        self.parent = ancestor;
        self.InheritEffects();
        self.InheritAmbientSounds();
    }

    public static void EnsureAncestorFilesExist(string key)
    {
        if (string.IsNullOrEmpty(key)) return;

        string dir = GetAncestorWriteDirectory(key);
        if (dir == null) return;

        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        for (int i = 1; i <= MAX_STATES; i++)
        {
            string path = Path.Combine(dir, $"ancestor_{i}.txt");
            if (!File.Exists(path))
            {
                File.WriteAllText(path, BuildVanillaAncestorContent(), Encoding.UTF8);
                RSPlugin.log.LogDebug($"[AncestorResolver] Creado {path}");
            }
        }
    }

    public static void Reset()
    {
        _cache.Clear();
    }

    // CLAVE DEL ANCESTOR — las gates no dependen de la región activa (tienen carpeta propia)

    public static string GetAncestorKey(string roomName, string regionName)
    {
        if (BlendSettingsLoader.IsGateRoom(roomName)) return "gates";
        return string.IsNullOrEmpty(regionName) ? null : regionName;
    }

    // OBTENER ANCESTOR (con cache)

    private static RoomSettings GetAncestor(string roomName, string regionName, int state)
    {
        string key = GetAncestorKey(roomName, regionName);
        if (key == null) return null;
        if (state < 1 || state > MAX_STATES) return null;

        // En gates la caché es por gate (cada una puede tener su propio override)
        string cacheKey = (BlendSettingsLoader.IsGateRoom(roomName) ? roomName : key).ToUpperInvariant();

        if (!_cache.TryGetValue(cacheKey, out RoomSettings[] array))
        {
            array = new RoomSettings[MAX_STATES];
            _cache[cacheKey] = array;
        }

        int idx = state - 1;
        if (array[idx] != null) return array[idx];

        string path = GetAncestorPath(roomName, regionName, state);
        if (path == null) return null;

        try
        {
            RoomSettings ancestor = ParseAncestorFile(path);
            if (ancestor != null)
            {
                array[idx] = ancestor;
                RSPlugin.log.LogDebug($"[AncestorResolver] {cacheKey} estado {state} -> {path}");
            }
            return ancestor;
        }
        catch (Exception ex)
        {
            RSPlugin.log.LogWarning($"[AncestorResolver] Error cargando ancestor_{state} de {cacheKey}: {ex.Message}");
            return null;
        }
    }

    // PARSEO DIRECTO — evita el constructor de RoomSettings

    private static RoomSettings ParseAncestorFile(string path)
    {
        var ancestor = new RoomSettings(
            null, "ancestor_root", null, false, false,
            (SlugcatStats.Timeline)null, null);

        ancestor.filePath = path;
        ancestor.isAncestor = true;
        ancestor.isTemplate = true;
        ancestor.parent = DefaultRoomSettings.ancestor;

        string[] lines = File.ReadAllLines(path, Encoding.UTF8);
        foreach (string rawLine in lines)
        {
            string line = rawLine.TrimEnd('\r');
            int sep = line.IndexOf(": ", StringComparison.Ordinal);
            if (sep < 0) continue;

            string key = line.Substring(0, sep);
            string val = line.Substring(sep + 2).Trim();

            switch (key)
            {
                case "Template": break;
                case "Palette": if (int.TryParse(val, out int pal)) ancestor.pal = pal; break;
                case "EffectColorA": if (int.TryParse(val, out int eca)) ancestor.eColA = eca; break;
                case "EffectColorB": if (int.TryParse(val, out int ecb)) ancestor.eColB = ecb; break;
                case "DangerType": ancestor.dType = ParseDangerType(val); break;
                case "CeilingDrips": if (float.TryParse(val, NF, INV, out float cd)) ancestor.cDrips = cd; break;
                case "RainIntensity": if (float.TryParse(val, NF, INV, out float ri)) ancestor.rInts = ri; break;
                case "RumbleIntensity": if (float.TryParse(val, NF, INV, out float ru)) ancestor.rumInts = ru; break;
                case "WaveAmplitude": if (float.TryParse(val, NF, INV, out float wa)) ancestor.wAmp = wa; break;
                case "WaveLength": if (float.TryParse(val, NF, INV, out float wl)) ancestor.wLength = wl; break;
                case "WaveSpeed": if (float.TryParse(val, NF, INV, out float ws)) ancestor.wSpeed = ws; break;
                case "SecondWaveAmplitude": if (float.TryParse(val, NF, INV, out float swa)) ancestor.swAmp = swa; break;
                case "SecondWaveLength": if (float.TryParse(val, NF, INV, out float swl)) ancestor.swLength = swl; break;
                case "Clouds": if (float.TryParse(val, NF, INV, out float cl)) ancestor.clds = cl; break;
                case "Grime": if (float.TryParse(val, NF, INV, out float gr)) ancestor.grm = gr; break;
                case "BkgDroneVolume": if (float.TryParse(val, NF, INV, out float bv)) ancestor.bkgDrnVl = bv; break;
                case "BkgDroneNoThreatVolume": if (float.TryParse(val, NF, INV, out float bnt)) ancestor.bkgDrnNoThreatVol = bnt; break;
                case "RandomItemDensity": if (float.TryParse(val, NF, INV, out float rd)) ancestor.rndItmDns = rd; break;
                case "RandomItemSpearChance": if (float.TryParse(val, NF, INV, out float rs)) ancestor.rndItmSprChnc = rs; break;
                case "WaterReflectionAlpha": if (float.TryParse(val, NF, INV, out float wr)) ancestor.wtrRflctAlpha = wr; break;
                case "TerrainPalette": ancestor.terrainPalette = val; break;
                case "TerrainLight": if (float.TryParse(val, NF, INV, out float tl)) ancestor.terrainLight = tl; break;
                case "TerrainStainAmount": if (float.TryParse(val, NF, INV, out float tsa)) ancestor.terrainStainAmount = tsa; break;
                case "TerrainStainBrightness": if (float.TryParse(val, NF, INV, out float tsb)) ancestor.terrainStainBrightness = tsb; break;
                case "TerrainStainHeight": if (float.TryParse(val, NF, INV, out float tsh)) ancestor.terrainStainHeight = tsh; break;
                case "TerrainWaves": if (float.TryParse(val, NF, INV, out float tw)) ancestor.terrainWaves = tw; break;
                case "TerrainEdgeRadius": if (float.TryParse(val, NF, INV, out float ter)) ancestor.terrainEdgeRadius = ter; break;
                case "TerrainGooHeight": if (float.TryParse(val, NF, INV, out float tg)) ancestor.terrainGooHeight = tg; break;
                case "TerrainGrain": if (float.TryParse(val, NF, INV, out float tgr)) ancestor.terrainGrain = tgr; break;
                case "TerrainDepth": if (float.TryParse(val, NF, INV, out float td)) ancestor.terrainDepth = td; break;
                case "TerrainSkyFade": if (float.TryParse(val, NF, INV, out float tsf)) ancestor.terrainSkyFade = tsf; break;
            }
        }

        return ancestor;
    }

    private static RoomRain.DangerType ParseDangerType(string val)
    {
        return val.ToUpperInvariant() switch
        {
            "RAIN" => RoomRain.DangerType.Rain,
            "FLOOD" => RoomRain.DangerType.Flood,
            "FLOODANDRAIN" => RoomRain.DangerType.FloodAndRain,
            "THUNDER" => RoomRain.DangerType.Thunder,
            "NONE" => RoomRain.DangerType.None,
            _ => RoomRain.DangerType.Rain
        };
    }

    // RUTAS — lectura: primer archivo existente por prioridad (mismo criterio que el snapshot)
    // Gates: {gate}_ancestor_N.txt (override) → ancestor_N.txt (compartido) → ancestor vanilla

    public static string GetAncestorPath(string roomName, string regionName, int state)
    {
        string key = GetAncestorKey(roomName, regionName);
        if (key == null) return null;

        string dir = Path.Combine("world", key.ToLowerInvariant(), "raincycles");

        foreach (string fileName in GetAncestorFileNames(roomName, state))
        {
            string found = ResolveExisting(Path.Combine(dir, fileName));
            if (found != null) return found;
        }

        return null;
    }

    private static List<string> GetAncestorFileNames(string roomName, int state)
    {
        var names = new List<string>();
        if (BlendSettingsLoader.IsGateRoom(roomName))
            names.Add(roomName.ToLowerInvariant() + $"_ancestor_{state}.txt");
        names.Add($"ancestor_{state}.txt");
        return names;
    }

    private static string ResolveExisting(string relativePath)
    {
        ModManager.Mod target = SaveModResolver.GetTargetMod();
        if (target != null)
        {
            string targetPath = Path.Combine(target.path, relativePath);
            if (File.Exists(targetPath)) return targetPath;
        }

        for (int i = ModManager.ActiveMods.Count - 1; i >= 0; i--)
        {
            ModManager.Mod mod = ModManager.ActiveMods[i];
            if (mod == null) continue;
            string candidate = Path.Combine(mod.path, relativePath);
            if (File.Exists(candidate)) return candidate;
        }

        string vanillaPath = Path.Combine(Application.streamingAssetsPath, relativePath);
        return File.Exists(vanillaPath) ? vanillaPath : null;
    }

    // RUTAS — escritura (botón Ancestor): no debe caer en un mod que solo tenga la carpeta de región

    private static string GetAncestorWriteDirectory(string key)
    {
        string keyCode = key.ToLowerInvariant();
        string ancestorDir = Path.Combine("world", keyCode, "raincycles");

        ModManager.Mod target = SaveModResolver.GetTargetMod();
        if (target != null)
            return Path.Combine(target.path, ancestorDir);

        for (int i = ModManager.ActiveMods.Count - 1; i >= 0; i--)
        {
            ModManager.Mod mod = ModManager.ActiveMods[i];
            if (mod == null) continue;
            if (Directory.Exists(Path.Combine(mod.path, ancestorDir)))
                return Path.Combine(mod.path, ancestorDir);
        }

        string keyRoot = Path.Combine("world", keyCode);
        for (int i = ModManager.ActiveMods.Count - 1; i >= 0; i--)
        {
            ModManager.Mod mod = ModManager.ActiveMods[i];
            if (mod == null) continue;
            if (Directory.Exists(Path.Combine(mod.path, keyRoot)))
                return Path.Combine(mod.path, ancestorDir);
        }

        return Path.Combine(Application.streamingAssetsPath, ancestorDir);
    }

    // CONTENIDO VANILLA POR DEFECTO

    private static string BuildVanillaAncestorContent()
    {
        return
@"Template: NONE
DangerType: Rain
CeilingDrips: 0.5
RainIntensity: 1
RumbleIntensity: 1
WaveAmplitude: 0
WaveLength: 0.5
WaveSpeed: 0.75
SecondWaveAmplitude: 0
SecondWaveLength: 0.16666667
Palette: 0
EffectColorA: 0
EffectColorB: 0
Clouds: 0
Grime: 0.5
BkgDroneVolume: 0.3
BkgDroneNoThreatVolume: 1
RandomItemDensity: 0.5
RandomItemSpearChance: 0.2
WaterReflectionAlpha: 1
TerrainPalette: NO PALETTE
TerrainLight: 0.5
TerrainStainAmount: 0
TerrainStainBrightness: 0.5
TerrainStainHeight: 0
TerrainWaves: 0
TerrainEdgeRadius: 60
TerrainGooHeight: 1
TerrainGrain: 0.35
TerrainDepth: -5
TerrainSkyFade: 0";
    }
}
