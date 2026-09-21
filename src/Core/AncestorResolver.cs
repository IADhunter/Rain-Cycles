using System;
using System.IO;
using System.Text;
using System.Globalization;
using System.Collections.Generic;
using UnityEngine;

namespace RainCycles.Core;

// ============================================================
// ANCESTOR REGIONAL — Fallback final para salas Template: NONE
// ============================================================
// Cadena de herencia cuando una sala no declara un campo:
//   Sala → ancestor regional (nuestro) → DefaultRoomSettings.ancestor (vanilla)
//
// Los archivos ancestor_X.txt se guardan en World/<REGION>/raincycles/
// y se crean automáticamente con valores vanilla si no existen.
//
// Se aplica SOLO a salas cuyo parent es DefaultRoomSettings.ancestor
// (Template: NONE o sin template), igual que el ancestor vanilla.

public static class AncestorResolver
{
    private const int MAX_STATES = 4;
    private static readonly NumberStyles NF = NumberStyles.Float;
    private static readonly CultureInfo INV = CultureInfo.InvariantCulture;

    // Cache: regionName → array de 4 RoomSettings (índice 0 = estado 1)
    private static readonly Dictionary<string, RoomSettings[]> _cache
        = new Dictionary<string, RoomSettings[]>(StringComparer.OrdinalIgnoreCase);

    // ============================================================
    // API PÚBLICA
    // ============================================================

    /// <summary>
    /// Aplica el ancestor regional a un RoomSettings si su parent es
    /// DefaultRoomSettings.ancestor (Template: NONE o sin template).
    /// Cambia parent al ancestor regional y re-hereda effects/sounds.
    /// </summary>
    public static void ApplyAncestor(RoomSettings self, Region region, int state)
    {
        if (self == null || region == null) return;
        if (state < 1 || state > MAX_STATES) return;

        // No aplicar a templates ni ancestors
        if (self.isTemplate || self.isAncestor) return;

        // Solo aplicar a salas cuyo parent es el ancestor vanilla
        // (FindParent ya puso parent = DefaultRoomSettings.ancestor para Template: NONE)
        if (self.parent != DefaultRoomSettings.ancestor) return;

        // Obtener el ancestor para este estado
        RoomSettings ancestor = GetAncestor(region.name, state);
        if (ancestor == null) return;

        // Cambiar parent y re-heredar
        self.parent = ancestor;
        self.InheritEffects();
        self.InheritAmbientSounds();
    }

    /// <summary>
    /// Asegura que existan los archivos ancestor_X.txt para una región.
    /// Los crea con valores vanilla si no existen.
    /// </summary>
    public static void EnsureAncestorFilesExist(string regionName)
    {
        if (string.IsNullOrEmpty(regionName)) return;

        string dir = GetAncestorDirectory(regionName);
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        for (int i = 1; i <= MAX_STATES; i++)
        {
            string path = GetAncestorPath(regionName, i);
            if (!File.Exists(path))
            {
                File.WriteAllText(path, BuildVanillaAncestorContent(), Encoding.UTF8);
                RSPlugin.log.LogDebug($"[AncestorResolver] Creado {Path.GetFileName(path)} en {regionName}");
            }
        }
    }

    /// <summary>
    /// Limpia la cache. Llamado por ModResetter al reiniciar la partida.
    /// </summary>
    public static void Reset()
    {
        _cache.Clear();
    }

    // ============================================================
    // OBTENER ANCESTOR (con cache)
    // ============================================================

    private static RoomSettings GetAncestor(string regionName, int state)
    {
        if (string.IsNullOrEmpty(regionName)) return null;
        if (state < 1 || state > MAX_STATES) return null;

        string key = regionName.ToUpperInvariant();

        if (!_cache.TryGetValue(key, out RoomSettings[] array))
        {
            array = new RoomSettings[MAX_STATES];
            _cache[key] = array;
        }

        int idx = state - 1;
        if (array[idx] != null) return array[idx];

        string path = GetAncestorPath(regionName, state);
        if (!File.Exists(path)) return null;

        try
        {
            RoomSettings ancestor = ParseAncestorFile(path);
            if (ancestor != null)
                array[idx] = ancestor;
            return ancestor;
        }
        catch (Exception ex)
        {
            RSPlugin.log.LogWarning($"[AncestorResolver] Error cargando ancestor_{state} de {regionName}: {ex.Message}");
            return null;
        }
    }

    // ============================================================
    // PARSEO DIRECTO — evita el constructor de RoomSettings
    // ============================================================

    /// <summary>
    /// Parsea un archivo ancestor_X.txt y construye un RoomSettings
    /// con isAncestor=true, parent=DefaultRoomSettings.ancestor,
    /// y los campos parseados del archivo.
    /// </summary>
    private static RoomSettings ParseAncestorFile(string path)
    {
        // Crear RoomSettings con el constructor que se cortocircuita
        // "roottemplate" → filePath="" y return (sin FindParent ni Load)
        var ancestor = new RoomSettings(
            null, "ancestor_root", null, false, false,
            (SlugcatStats.Timeline)null, null);

        // Re-asignar campos que el constructor "roottemplate" setea
        ancestor.filePath = path;
        ancestor.isAncestor = true;
        ancestor.isTemplate = true;
        ancestor.parent = DefaultRoomSettings.ancestor;

        // Parsear el archivo línea por línea (mismo formato que _settings.txt)
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
                case "Template": break; // Ignorado — siempre usamos DefaultRoomSettings.ancestor
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

    // ============================================================
    // RUTAS
    // ============================================================

    private static string GetAncestorDirectory(string regionName)
    {
        string regionCode = regionName.ToUpperInvariant();
        string relativePath = Path.Combine("world", regionCode, "raincycles");

        for (int i = ModManager.ActiveMods.Count - 1; i >= 0; i--)
        {
            string candidate = Path.Combine(ModManager.ActiveMods[i].path, relativePath);
            if (Directory.Exists(candidate))
                return candidate;
        }

        return Path.Combine(Application.streamingAssetsPath, relativePath);
    }

    private static string GetAncestorPath(string regionName, int state)
    {
        string dir = GetAncestorDirectory(regionName);
        return Path.Combine(dir, $"ancestor_{state}.txt");
    }

    // ============================================================
    // CONTENIDO VANILLA POR DEFECTO
    // ============================================================

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
