using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace RainCycles.Core;

// PREWARM DE TEXTURAS DE VIEW (05/10/2026)
public static partial class SettingsBlendController
{
    // ESTADO
    private const double PrewarmBudgetMs = 6.0;

    private static int _prewarmPhase;
    private static string _prewarmRegion;
    private static List<string> _prewarmFiles;
    private static int _prewarmFileIdx;
    private static List<(string mod, string file)> _prewarmBkgPairs;
    private static HashSet<string> _prewarmPairKeys;
    private static HashSet<ViewType> _prewarmViews;
    private static List<Action> _prewarmDecodes;
    private static int _prewarmDecodeIdx;
    private static HashSet<string> _prewarmRooms;
    private static List<Action> _prewarmWarms;
    private static int _prewarmWarmIdx;
    private static long _prewarmStartTicks;

    private readonly struct PrewarmGraphic
    {
        public readonly string Name;
        public readonly bool Crisp;
        public readonly bool Clamp;

        public PrewarmGraphic(string name, bool crisp, bool clamp)
        {
            Name = name;
            Crisp = crisp;
            Clamp = clamp;
        }
    }

    // SETS VANILLA POR VIEW TYPE

    private static readonly PrewarmGraphic[] _prewarmAcv =
    {
        new PrewarmGraphic("clouds1", false, false),
        new PrewarmGraphic("clouds2", false, false),
        new PrewarmGraphic("clouds3", false, false),
        new PrewarmGraphic("flyingClouds1", false, false),
        new PrewarmGraphic("AtC_NightSky", true, true),
        new PrewarmGraphic("AtC_DuskSky", true, true),
        new PrewarmGraphic("AtC_DuskSky-Rivulet", true, true),
        new PrewarmGraphic("AtC_Sky", true, true),
        new PrewarmGraphic("AtC_Spire1", true, false),
        new PrewarmGraphic("AtC_LocalSpire", true, false),
        new PrewarmGraphic("AtC_LocalSpire_Lights", true, false),
        new PrewarmGraphic("AtC_RetainingWall", true, false),
        new PrewarmGraphic("AtC_FivePebbles", true, false),
        new PrewarmGraphic("AtC_FivePebblesLight", true, false),
        new PrewarmGraphic("AtC_Structure1", true, false),
        new PrewarmGraphic("AtC_Structure2", true, false),
        new PrewarmGraphic("AtC_Structure3", true, false),
        new PrewarmGraphic("AtC_Structure4", true, false),
        new PrewarmGraphic("AtC_Structure5", true, false),
        new PrewarmGraphic("AtC_Structure6", true, false),
        new PrewarmGraphic("AtC_Spire2", true, false),
        new PrewarmGraphic("AtC_Spire3", true, false),
        new PrewarmGraphic("AtC_Spire4", true, false),
        new PrewarmGraphic("AtC_Spire5", true, false),
        new PrewarmGraphic("AtC_Spire6", true, false),
        new PrewarmGraphic("AtC_Spire7", true, false),
        new PrewarmGraphic("AtC_Spire8", true, false),
        new PrewarmGraphic("AtC_Spire9", true, false),
        new PrewarmGraphic("AtC_Structure1SAINT", true, false),
        new PrewarmGraphic("AtC_Structure2SAINT", true, false),
        new PrewarmGraphic("AtC_Structure3SAINT", true, false),
        new PrewarmGraphic("AtC_Structure4SAINT", true, false),
        new PrewarmGraphic("AtC_Structure5SAINT", true, false),
        new PrewarmGraphic("AtC_Structure6SAINT", true, false),
        new PrewarmGraphic("AtC_Spire1SAINT", true, false),
        new PrewarmGraphic("AtC_Spire2SAINT", true, false),
        new PrewarmGraphic("AtC_Spire3SAINT", true, false),
        new PrewarmGraphic("AtC_Spire4SAINT", true, false),
        new PrewarmGraphic("AtC_Spire5SAINT", true, false),
        new PrewarmGraphic("AtC_Spire6SAINT", true, false),
        new PrewarmGraphic("AtC_Spire7SAINT", true, false),
        new PrewarmGraphic("AtC_Spire8SAINT", true, false),
        new PrewarmGraphic("AtC_Spire9SAINT", true, false),
        new PrewarmGraphic("AtC_Light1", true, false),
        new PrewarmGraphic("AtC_Light2", true, false),
        new PrewarmGraphic("AtC_Light3", true, false),
    };

    private static readonly PrewarmGraphic[] _prewarmPsvExtras =
    {
        new PrewarmGraphic("pnk_sky", true, true),
        new PrewarmGraphic("pnk_structure1", true, false),
        new PrewarmGraphic("pnk_structure2", true, false),
        new PrewarmGraphic("pnk_structure3", true, false),
        new PrewarmGraphic("pnk_fog", true, true),
        new PrewarmGraphic("pnk_sun", true, true),
    };

    private static readonly PrewarmGraphic[] _prewarmRtv =
    {
        new PrewarmGraphic("Rf_Sky", true, true),
        new PrewarmGraphic("Rf_DuskSky", true, true),
        new PrewarmGraphic("Rf_DuskSky-Rivulet", true, true),
        new PrewarmGraphic("Rf_NightSky", true, true),
        new PrewarmGraphic("smoke1", false, false),
        new PrewarmGraphic("floor", true, false),
        new PrewarmGraphic("city1", false, true),
        new PrewarmGraphic("city2", false, true),
        new PrewarmGraphic("city3", false, true),
        new PrewarmGraphic("Rf_Rubble", true, false),
        new PrewarmGraphic("Rf_HoleFix", true, false),
        new PrewarmGraphic("RF_CityA", true, false),
        new PrewarmGraphic("RF_CityB", true, false),
        new PrewarmGraphic("RF_CityC", true, false),
        new PrewarmGraphic("RF_CityA_DM", true, false),
        new PrewarmGraphic("RF_CityB_DM", true, false),
        new PrewarmGraphic("RF_CityC_DM", true, false),
    };

    private static readonly PrewarmGraphic[] _prewarmOrv =
    {
        new PrewarmGraphic("otr_sky", true, true),
        new PrewarmGraphic("au_dustgradient", false, true),
        new PrewarmGraphic("otr_dustdunes", false, false),
        new PrewarmGraphic("otr_fallingstar", true, true),
        new PrewarmGraphic("otr_firmamentcloud", false, false),
        new PrewarmGraphic("otr_starcatcher1", true, false),
        new PrewarmGraphic("otr_starcatcher2", true, false),
    };

    private static readonly PrewarmGraphic[] _prewarmAuv =
    {
        new PrewarmGraphic("au_sky", true, true),
        new PrewarmGraphic("smoke1", false, false),
        new PrewarmGraphic("au_dustgradient", false, false),
    };

    // HOOK
    public static void InitPrewarm()
    {
        On.RainWorldGame.Update += OnPrewarmGameUpdate;
    }

    private static void OnPrewarmGameUpdate(On.RainWorldGame.orig_Update orig, RainWorldGame self)
    {
        orig(self);

        if (RCGate.Suspended) return;

        try
        {
            if (_prewarmPhase == 4) return;
            if (self.IsArenaSession) return;

            World world = self.world;
            if (world == null || string.IsNullOrEmpty(world.name)) return;

            if (!string.Equals(world.name, _prewarmRegion, StringComparison.OrdinalIgnoreCase))
                PrewarmStartRegion(world.name);

            if (_prewarmPhase >= 1 && _prewarmPhase <= 3)
                PrewarmRun();
        }
        catch (Exception ex)
        {
            _prewarmPhase = 4;
            RSPlugin.log.LogWarning($"[RC][Prewarm] abortado: {ex}");
        }
    }

    // MÁQUINA DE ESTADOS
    private static void PrewarmStartRegion(string region)
    {
        _prewarmRegion = region;
        _prewarmFiles = null;
        _prewarmFileIdx = 0;
        _prewarmBkgPairs = new List<(string, string)>();
        _prewarmPairKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _prewarmViews = new HashSet<ViewType>();
        _prewarmDecodes = null;
        _prewarmDecodeIdx = 0;
        _prewarmRooms = new HashSet<string>(StringComparer.Ordinal);
        _prewarmWarms = null;
        _prewarmWarmIdx = 0;
        _prewarmStartTicks = Stopwatch.GetTimestamp();
        _prewarmPhase = 1;
    }

    private static void PrewarmRun()
    {
        long budgetTicks = (long)(PrewarmBudgetMs * Stopwatch.Frequency / 1000.0);
        long deadline = Stopwatch.GetTimestamp() + budgetTicks;

        while (Stopwatch.GetTimestamp() < deadline)
        {
            if (_prewarmPhase == 1)
            {
                if (_prewarmFiles == null)
                {
                    PrewarmScanFiles();
                    if (_prewarmFiles.Count == 0)
                    {
                        PrewarmFinish("sin archivos settings");
                        return;
                    }
                }
                else if (_prewarmFileIdx >= _prewarmFiles.Count)
                {
                    PrewarmBuildWarms();
                    _prewarmPhase = 2;
                }
                else
                {
                    PrewarmParseOne(_prewarmFiles[_prewarmFileIdx++]);
                }
            }
            else if (_prewarmPhase == 2)
            {
                if (_prewarmWarmIdx >= _prewarmWarms.Count)
                {
                    PrewarmBuildDecodes();
                    _prewarmPhase = 3;
                }
                else
                {
                    PrewarmRunWarm(_prewarmWarms[_prewarmWarmIdx++]);
                }
            }
            else if (_prewarmPhase == 3)
            {
                if (_prewarmDecodeIdx >= _prewarmDecodes.Count)
                {
                    PrewarmFinish(null);
                    return;
                }
                PrewarmRunDecode(_prewarmDecodes[_prewarmDecodeIdx++]);
            }
            else
            {
                return;
            }
        }
    }

    private static void PrewarmFinish(string reason)
    {
        _prewarmPhase = 4;
    }

    // FASE DE PARSE (calienta SettingsSnapshot para el runtime)
    private static void PrewarmScanFiles()
    {
        _prewarmFiles = new List<string>();
        string lower = _prewarmRegion.ToLowerInvariant();
        var seenDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var roots = new List<string>();

        for (int i = 0; i < ModManager.ActiveMods.Count; i++)
        {
            string dir = Path.Combine(ModManager.ActiveMods[i].path, "world", lower + "-rooms");
            if (seenDirs.Add(dir) && Directory.Exists(dir))
                roots.Add(dir);
        }

        string vanilla = Path.Combine(Application.streamingAssetsPath, "world", lower + "-rooms");
        if (seenDirs.Add(vanilla) && Directory.Exists(vanilla))
            roots.Add(vanilla);

        for (int i = 0; i < roots.Count; i++)
            _prewarmFiles.AddRange(Directory.GetFiles(roots[i], "*settings*.txt", SearchOption.AllDirectories));
    }

    private static void PrewarmParseOne(string file)
    {
        string fname = Path.GetFileNameWithoutExtension(file);
        int idx = fname.IndexOf("_settings", StringComparison.OrdinalIgnoreCase);
        if (idx <= 0) return;

        if (idx + "_settings".Length < fname.Length)
        {
            char sep = fname[idx + "_settings".Length];
            if (sep != '.' && sep != '_' && sep != '-') return;
        }

        string roomName = fname.Substring(0, idx);

        int lastUnderscore = fname.LastIndexOf('_');
        if (lastUnderscore > idx &&
            int.TryParse(fname.Substring(lastUnderscore + 1), out int warmState) &&
            warmState >= 1 && warmState <= 4)
        {
            _prewarmRooms.Add(roomName.ToUpperInvariant());
        }

        SettingsSnapshot snap = SettingsSnapshot.GetCached(file, roomName);
        if (snap == null) return;

        if (snap.HasBkg)
        {
            PrewarmAddPair(snap.Bkg.Mod, snap.Bkg.Sky);
            PrewarmAddPair(snap.Bkg.Mod, snap.Bkg.Sun);
            PrewarmAddPair(snap.Bkg.Mod, snap.Bkg.Fog);
        }

        if (snap.ViewType != ViewType.None)
            _prewarmViews.Add(snap.ViewType);
    }

    private static void PrewarmAddPair(string mod, string file)
    {
        if (string.IsNullOrEmpty(mod) || string.IsNullOrEmpty(file)) return;
        if (!_prewarmPairKeys.Add(mod + "|" + file)) return;
        _prewarmBkgPairs.Add((mod, file));
    }

    // FASE DE CALENTADO DE CACHES (StateFileResolver)
    private static void PrewarmBuildWarms()
    {
        _prewarmWarms = new List<Action>();
        foreach (string room in _prewarmRooms)
        {
            string r = room;
            for (int state = 1; state <= 4; state++)
            {
                int s = state;
                _prewarmWarms.Add(() => StateFileResolver.ResolveSettingsPath(r, s));
            }
            _prewarmWarms.Add(() => StateFileResolver.CountRainStateFiles(r));
        }
    }

    private static void PrewarmRunWarm(Action warm)
    {
        try
        {
            warm();
        }
        catch (Exception ex)
        {
            RSPlugin.log.LogWarning($"[RC][Prewarm] warm: {ex.Message}");
        }
    }

    // FASE DE DECODE
    private static void PrewarmBuildDecodes()
    {
        _prewarmDecodes = new List<Action>();

        for (int i = 0; i < _prewarmBkgPairs.Count; i++)
        {
            (string mod, string file) pair = _prewarmBkgPairs[i];
            string atlas = BkgResolver.AtlasName(pair.mod, pair.file);
            _prewarmDecodes.Add(() => EnsureBkgAtlas(atlas, pair.mod, pair.file));
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (ViewType vt in _prewarmViews)
        {
            if (vt == ViewType.ACV || vt == ViewType.PSV)
                PrewarmQueueSet(_prewarmAcv, seen);
            if (vt == ViewType.PSV)
                PrewarmQueueSet(_prewarmPsvExtras, seen);
            if (vt == ViewType.RTV)
                PrewarmQueueSet(_prewarmRtv, seen);
            if (vt == ViewType.ORV)
                PrewarmQueueSet(_prewarmOrv, seen);
            if (vt == ViewType.AUV)
                PrewarmQueueSet(_prewarmAuv, seen);
        }
    }

    private static void PrewarmQueueSet(PrewarmGraphic[] set, HashSet<string> seen)
    {
        for (int i = 0; i < set.Length; i++)
        {
            PrewarmGraphic g = set[i];
            if (!seen.Add(g.Name)) continue;
            _prewarmDecodes.Add(() => PrewarmVanillaGraphic(g.Name, g.Crisp, g.Clamp));
        }
    }

    private static void PrewarmRunDecode(Action decode)
    {
        try
        {
            decode();
        }
        catch (Exception ex)
        {
            RSPlugin.log.LogWarning($"[RC][Prewarm] decode: {ex.Message}");
        }
    }

    private static void PrewarmVanillaGraphic(string name, bool crisp, bool clamp)
    {
        if (Futile.atlasManager.GetAtlasWithName(name) != null) return;

        string path = AssetManager.ResolveFilePath(
            "Illustrations" + Path.DirectorySeparatorChar + name + ".png");
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

        Texture2D tex = new Texture2D(1, 1, TextureFormat.ARGB32, false);
        AssetManager.SafeWWWLoadTexture(ref tex, "file:///" + path, clamp, crisp);
        HeavyTexturesCache.LoadAndCacheAtlasFromTexture(name, tex, false);
    }
}
