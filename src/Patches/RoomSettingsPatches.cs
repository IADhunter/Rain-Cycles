using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;
using RainCycles.Snapshot;
using RainCycles.Blend;

namespace RainCycles.Patches;

public static class RoomSettingsPatches
{
    private static bool _isSaving = false;

    public static void Init()
    {
        On.RoomSettings.Load_Timeline += OnLoad;
        On.RoomSettings.Save += OnSave;
        On.RoomSettings.Save_Timeline += OnSaveTimeline;
    }

    // FINDPARENT HELPER — recalcular template padre tras cambiar filePath
    private static readonly MethodInfo _findParentMI = typeof(RoomSettings)
        .GetMethod("FindParent", BindingFlags.NonPublic | BindingFlags.Instance);

    public static void RefreshParent(RoomSettings self, Region region)
    {
        _findParentMI?.Invoke(self, new object[] { region });
    }

    private static bool OnLoad(On.RoomSettings.orig_Load_Timeline orig, RoomSettings self, SlugcatStats.Timeline timelinePoint)
    {
        self.ClearExtendedData();

        string filePath = self.filePath;
        if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
        {
            ParseExtendedData(self, filePath);
        }

        BkgResolver.SetLive(self);

        SaveGuard.ClearScalars(self);
        SaveGuard.ClearBleedables(self);

        bool loaded = orig(self, timelinePoint);

        if (!RCGate.Suspended)
            SaveGuard.Capture(self);

        return loaded;
    }

    private static void OnSave(On.RoomSettings.orig_Save orig, RoomSettings self)
    {
        if (_isSaving)
        {
            orig(self);
            return;
        }

        // El State se captura ANTES de orig: vanilla reescribe el archivo entero y borra la línea
        int baseState = CaptureBaseState(self.filePath);

        if (RCGate.Suspended)
        {
            _isSaving = true;
            try
            {
                orig(self);
                PreserveExtendedData(self, baseState);
            }
            finally
            {
                _isSaving = false;
            }
            InvalidateSavedFileCaches(self);
            return;
        }

        _isSaving = true;
        SaveGuard.Restore restore = SaveGuard.Begin(self);
        try
        {
            orig(self);
            PreserveExtendedData(self, baseState);
        }
        finally
        {
            SaveGuard.End(self, restore);
            _isSaving = false;
        }

        AfterSave(self);
    }

    private static void OnSaveTimeline(On.RoomSettings.orig_Save_Timeline orig, RoomSettings self, SlugcatStats.Timeline time)
    {
        if (_isSaving)
        {
            orig(self, time);
            return;
        }

        // El State se captura ANTES de orig: vanilla reescribe el archivo entero y borra la línea
        int baseState = CaptureBaseState(self.filePath);

        if (RCGate.Suspended)
        {
            _isSaving = true;
            try
            {
                orig(self, time);
                PreserveExtendedData(self, baseState);
            }
            finally
            {
                _isSaving = false;
            }
            InvalidateSavedFileCaches(self);
            return;
        }

        _isSaving = true;
        try
        {
            orig(self, time);
            PreserveExtendedData(self, baseState);
        }
        finally
        {
            _isSaving = false;
        }

        AfterSave(self);
    }

    private static void InvalidateSavedFileCaches(RoomSettings self)
    {
        string filePath = self.filePath;
        if (string.IsNullOrEmpty(filePath)) return;

        SettingsSnapshot.InvalidateCache(filePath);

        string derivedRoom = DeriveRoomNameFromPath(filePath);
        if (!string.IsNullOrEmpty(derivedRoom))
            BkgResolver.InvalidatePaths(derivedRoom);
    }

    private static void AfterSave(RoomSettings self)
    {
        string filePath = self.filePath;
        if (!string.IsNullOrEmpty(filePath))
        {
            SettingsSnapshot.InvalidateCache(filePath);

            var rw = UnityEngine.Object.FindObjectOfType<RainWorld>();
            var game = rw?.processManager?.currentMainLoop as RainWorldGame;

            string derivedRoom = DeriveRoomNameFromPath(filePath);
            if (!string.IsNullOrEmpty(derivedRoom))
            {
                RoomCameraExtensions.UnloadRoomCache(derivedRoom);
                RoomCameraExtensions.InvalidateRoomCache(derivedRoom);
                RoomCameraExtensions.ReloadRoomTerrainCache(derivedRoom);
                BkgResolver.InvalidatePaths(derivedRoom);

                if (SettingsBlendController.IsActive &&
                    string.Equals(SettingsBlendController.ActiveRoom?.abstractRoom?.name,
                        derivedRoom, StringComparison.OrdinalIgnoreCase))
                {
                    SettingsBlendController.RefreshActiveSnapshots();
                }
            }

            if (game?.cameras != null)
            {
                foreach (var cam in game.cameras)
                {
                    if (cam?.room?.roomSettings?.filePath == filePath)
                    {
                        string roomName = cam.room.abstractRoom?.name;

                        var freshSnap = SettingsSnapshot.GetCached(filePath, cam.room.abstractRoom?.name);

                        if (freshSnap != null)
                        {
                            var rs = cam.room.roomSettings;

                            if (rs.HasTint())
                            {
                                Color? tintMultiply = rs.GetTintMultiply();
                                Color? tintAtmosphere = rs.GetTintAtmosphere();

                                if (tintMultiply.HasValue)
                                {
                                    var c = tintMultiply.Value;
                                    Shader.SetGlobalVector(RainWorld.ShadPropMultiplyColor, new Vector4(c.r, c.g, c.b, 1f));
                                }
                                if (tintAtmosphere.HasValue)
                                {
                                    var c = tintAtmosphere.Value;
                                    Shader.SetGlobalVector(RainWorld.ShadPropAboveCloudsAtmosphereColor, new Vector4(c.r, c.g, c.b, 1f));

                                    TintManager.ApplyAtmosphereToScene(cam.room, c);
                                }
                            }

                            if (freshSnap.HasRcType && freshSnap.RcType == RcType.Blend)
                            {
                                if (SettingsBlendController.IsActive && SettingsBlendController.ActiveRoom == cam.room)
                                {
                                    SettingsBlendController.RefreshActiveSnapshots();
                                }
                                else
                                {
                                    if (freshSnap._hasPalette)
                                    {
                                        cam.ChangeMainPalette(freshSnap.Palette);
                                        if (freshSnap._hasFadePalette)
                                        {
                                            int fadePal = freshSnap.FadePaletteID;
                                            float fadeOp = cam.currentCameraPosition < freshSnap.FadePaletteOpacities.Length
                                                ? freshSnap.FadePaletteOpacities[cam.currentCameraPosition]
                                                : 0f;
                                            cam.ChangeFadePalette(fadePal, fadeOp);
                                        }
                                        cam.ApplyFade();
                                    }
                                }
                            }

                            cam.room.ApplyScalarEffects(freshSnap, freshSnap, 0f, claim: false);
                            Shader.SetGlobalFloat(RainWorld.ShadPropGrime, cam.room.roomSettings.Grime);
                        }
                    }
                }
            }

            SettingsBlendController.ForceRefreshSkySlots();
        }
    }

    // PARSEAR RAINCYCLES
    private static void ParseExtendedData(RoomSettings self, string filePath)
    {
        try
        {
            foreach (string line in File.ReadAllLines(filePath, Encoding.UTF8))
            {
                string trimmed = line.TrimEnd('\r');

                if (trimmed.StartsWith("RainCycles:"))
                {
                    string content = trimmed.Substring("RainCycles:".Length).Trim();

                    // BASE SIN STATE: invisible para el sistema (no se leen sus tags)
                    if (StateFileResolver.IsBaseSettingsFile(filePath) &&
                        StateFileResolver.ParseStateTag(content) == 0)
                        return;

                    ParseRainCyclesContent(self, content);
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            RSPlugin.log.LogWarning($"[RoomSettingsPatches] Error parsing: {ex.Message}");
        }
    }

    private static void ParseRainCyclesContent(RoomSettings self, string content)
    {
        bool hasType = false;
        bool hasView = false;
        RcType type = RcType.None;
        ViewType view = ViewType.None;
        Color? tintMultiply = null;
        Color? tintAtmosphere = null;
        BkgTag.Data bkg = default;

        int pos = 0;
        while (pos < content.Length)
        {
            int start = content.IndexOf('<', pos);
            if (start < 0) break;
            int end = content.IndexOf('>', start);
            if (end < 0) break;

            string segment = content.Substring(start + 1, end - start - 1);
            int sep = segment.IndexOf(':');
            if (sep > 0)
            {
                string field = segment.Substring(0, sep).Trim();
                string value = segment.Substring(sep + 1).Trim();

                switch (field)
                {
                    case "Type":
                        hasType = true;
                        type = value.ToUpperInvariant() switch
                        {
                            "STATIC" => RcType.Static,
                            "BLEND" => RcType.Blend,
                            _ => RcType.None
                        };
                        break;
                    case "View":
                        if (hasType)
                        {
                            hasView = true;
                            view = value.ToUpperInvariant() switch
                            {
                                "ACV" => ViewType.ACV,
                                "RTV" => ViewType.RTV,
                                "PSV" => ViewType.PSV,
                                "AUV" => ViewType.AUV,
                                "ORV" => ViewType.ORV,
                                _ => ViewType.None
                            };
                        }
                        break;
                    case "Tint":
                        if (hasView)
                        {
                            string[] hexes = value.Split(' ');
                            if (hexes.Length >= 1) tintMultiply = ParseHexColor(hexes[0]);
                            if (hexes.Length >= 2) tintAtmosphere = ParseHexColor(hexes[1]);
                        }
                        break;
                    case "Mod":
                        if (hasView)
                            bkg = BkgTag.Parse(value);
                        break;
                }
            }
            pos = end + 1;
        }

        if (hasType)
        {
            self.SetRcType(type);
            if (hasView)
                self.SetViewType(view);
            else
                self.SetViewType(ViewType.None);

            if (hasView && (tintMultiply.HasValue || tintAtmosphere.HasValue))
            {
                self.SetTintMultiply(tintMultiply);
                self.SetTintAtmosphere(tintAtmosphere);
            }
            else
            {
                self.SetTintMultiply(null);
                self.SetTintAtmosphere(null);
            }

            self.SetBkg(bkg);

            if (hasView)
            {
                int modIdx = content.IndexOf("<Mod:", StringComparison.OrdinalIgnoreCase);
                int tintIdx = content.IndexOf("<Tint:", StringComparison.OrdinalIgnoreCase);
                if (modIdx >= 0 && tintIdx >= 0)
                {
                    self.SetSegmentOrder(modIdx < tintIdx
                        ? RcSegmentOrder.ModFirst
                        : RcSegmentOrder.TintFirst);
                }
            }
        }
        else
        {
            self.ClearExtendedData();
        }
    }

    private static string DeriveRoomNameFromPath(string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return null;
        string name = Path.GetFileNameWithoutExtension(filePath);
        int idx = name.IndexOf("_settings", StringComparison.OrdinalIgnoreCase);
        if (idx <= 0) return null;
        return name.Substring(0, idx);
    }

    // State del base, leído del disco antes de que vanilla reescriba el archivo
    private static int CaptureBaseState(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return 0;
        if (!StateFileResolver.IsBaseSettingsFile(filePath)) return 0;

        try
        {
            return ExtractStateTag(new List<string>(File.ReadAllLines(filePath, Encoding.UTF8)));
        }
        catch (Exception)
        {
            return 0;
        }
    }

    private static void PreserveExtendedData(RoomSettings self, int baseState)
    {
        string filePath = self.filePath;
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;

        // BASE SIN STATE: Rain Cycles no escribe en él
        if (StateFileResolver.IsBaseSettingsFile(filePath) && baseState == 0) return;

        try
        {
            string content = File.ReadAllText(filePath, Encoding.UTF8);
            var lines = new List<string>(content.Split('\n'));

            lines.RemoveAll(l => l.Trim().StartsWith("RainCycles:"));

            string newLine = BuildRainCyclesLine(self, baseState);

            if (!string.IsNullOrEmpty(newLine))
            {
                while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[lines.Count - 1]))
                    lines.RemoveAt(lines.Count - 1);
                lines.Add(newLine);
            }

            File.WriteAllText(filePath, string.Join("\n", lines), Encoding.UTF8);
        }
        catch (Exception ex)
        {
            RSPlugin.log.LogWarning($"[RoomSettingsPatches] Error preserving: {ex.Message}");
        }
    }

    private static int ExtractStateTag(List<string> lines)
    {
        foreach (string line in lines)
        {
            string trimmed = line.Trim();
            if (!trimmed.StartsWith("RainCycles:", StringComparison.OrdinalIgnoreCase)) continue;
            return StateFileResolver.ParseStateTag(trimmed.Substring("RainCycles:".Length));
        }
        return 0;
    }

    private static string BuildRainCyclesLine(RoomSettings self, int baseState)
    {
        var parts = new List<string>();

        if (baseState >= 1 && baseState <= 4)
            parts.Add($"State:{baseState}");

        if (self.HasRcType())
        {
            parts.Add($"Type:{self.GetRcType()}");

            if (self.HasView())
            {
                parts.Add($"View:{self.GetViewType()}");

                if (self.GetSegmentOrder() == RcSegmentOrder.ModFirst)
                {
                    AppendBkgPart(parts, self);
                    AppendTintPart(parts, self);
                }
                else
                {
                    AppendTintPart(parts, self);
                    AppendBkgPart(parts, self);
                }
            }
        }

        if (parts.Count == 0) return null;
        return $"RainCycles: <{string.Join("><", parts)}>";
    }

    private static void AppendTintPart(List<string> parts, RoomSettings self)
    {
        if (!self.HasTint()) return;
        Color? tintMultiply = self.GetTintMultiply();
        Color? tintAtmosphere = self.GetTintAtmosphere();
        string mul = tintMultiply.HasValue ? ColorToHex(tintMultiply.Value) : "FFFFFF";
        string atm = tintAtmosphere.HasValue ? ColorToHex(tintAtmosphere.Value) : "FFFFFF";
        parts.Add($"Tint:#{mul} #{atm}");
    }

    private static void AppendBkgPart(List<string> parts, RoomSettings self)
    {
        if (!self.HasBkg()) return;
        string bkgVal = BkgTag.Format(self.GetBkg());
        if (!string.IsNullOrEmpty(bkgVal))
            parts.Add($"Mod:{bkgVal}");
    }

    private static Color ParseHexColor(string hex)
    {
        hex = hex.Trim().TrimStart('#');
        if (hex.Length != 6) return Color.white;
        try
        {
            return new Color(
                Convert.ToByte(hex.Substring(0, 2), 16) / 255f,
                Convert.ToByte(hex.Substring(2, 2), 16) / 255f,
                Convert.ToByte(hex.Substring(4, 2), 16) / 255f);
        }
        catch { return Color.white; }
    }

    private static string ColorToHex(Color color)
    {
        return $"{Mathf.RoundToInt(color.r * 255f):X2}{Mathf.RoundToInt(color.g * 255f):X2}{Mathf.RoundToInt(color.b * 255f):X2}";
    }
}

// SAVEGUARD — baseline de carga + propiedad del blend
internal static class SaveGuard
{
    internal const int Grime = 0;
    internal const int Clouds = 1;
    internal const int CeilingDrips = 2;
    internal const int BkgDroneVolume = 3;
    internal const int RandomItemDensity = 4;
    internal const int RandomItemSpearChance = 5;
    internal const int WaterReflectionAlpha = 6;
    internal const int TerrainLight = 7;
    internal const int TerrainStainAmount = 8;
    internal const int TerrainStainBrightness = 9;
    internal const int TerrainStainHeight = 10;
    internal const int TerrainWaves = 11;
    internal const int TerrainGrain = 12;
    internal const int TerrainSkyFade = 13;
    internal const int ScalarCount = 14;

    private static readonly Func<RoomSettings, float?>[] _get =
    {
        rs => rs.grm,
        rs => rs.clds,
        rs => rs.cDrips,
        rs => rs.bkgDrnVl,
        rs => rs.rndItmDns,
        rs => rs.rndItmSprChnc,
        rs => rs.wtrRflctAlpha,
        rs => rs.terrainLight,
        rs => rs.terrainStainAmount,
        rs => rs.terrainStainBrightness,
        rs => rs.terrainStainHeight,
        rs => rs.terrainWaves,
        rs => rs.terrainGrain,
        rs => rs.terrainSkyFade,
    };

    private static readonly Action<RoomSettings, float?>[] _set =
    {
        (rs, v) => rs.grm = v,
        (rs, v) => rs.clds = v,
        (rs, v) => rs.cDrips = v,
        (rs, v) => rs.bkgDrnVl = v,
        (rs, v) => rs.rndItmDns = v,
        (rs, v) => rs.rndItmSprChnc = v,
        (rs, v) => rs.wtrRflctAlpha = v,
        (rs, v) => rs.terrainLight = v,
        (rs, v) => rs.terrainStainAmount = v,
        (rs, v) => rs.terrainStainBrightness = v,
        (rs, v) => rs.terrainStainHeight = v,
        (rs, v) => rs.terrainWaves = v,
        (rs, v) => rs.terrainGrain = v,
        (rs, v) => rs.terrainSkyFade = v,
    };

    internal sealed class EffectState
    {
        public RoomSettings.RoomEffect Effect;
        public float Baseline;
        public float BlendAmount;
        public bool BlendWrote;
    }

    private sealed class State
    {
        public bool Captured;
        public readonly float?[] Baseline = new float?[ScalarCount];
        public readonly float?[] Blend = new float?[ScalarCount];
        public readonly List<EffectState> Effects = new List<EffectState>();
    }

    internal sealed class Restore
    {
        public readonly float?[] Prev = new float?[ScalarCount];
        public readonly bool[] Changed = new bool[ScalarCount];
        public readonly List<EffectState> EffStates = new List<EffectState>();
        public readonly List<float> EffPrev = new List<float>();
    }

    private static readonly ConditionalWeakTable<RoomSettings, State> _table =
        new ConditionalWeakTable<RoomSettings, State>();

    internal static void ClearScalars(RoomSettings rs)
    {
        if (rs == null) return;
        for (int i = 0; i < ScalarCount; i++) _set[i](rs, null);
    }

    internal static void ClearBleedables(RoomSettings rs)
    {
        if (rs == null) return;
        rs.rInts = null;
        rs.rumInts = null;
        rs.wSpeed = null;
        rs.wLength = null;
        rs.wAmp = null;
        rs.bkgDrnNoThreatVol = null;
        rs.terrainEdgeRadius = null;
        rs.terrainGooHeight = null;
        rs.terrainDepth = null;
        rs.terrainPalette = null;
        rs.fadePalette = null;
        rs.terrainFadePalette = null;
    }

    internal static void Capture(RoomSettings rs)
    {
        if (rs == null) return;
        State st = _table.GetOrCreateValue(rs);
        for (int i = 0; i < ScalarCount; i++)
        {
            st.Baseline[i] = _get[i](rs);
            st.Blend[i] = null;
        }
        st.Effects.Clear();
        if (rs.effects != null)
        {
            for (int i = 0; i < rs.effects.Count; i++)
            {
                RoomSettings.RoomEffect e = rs.effects[i];
                if (e == null || !IsBlendManagedType(e.type)) continue;
                st.Effects.Add(new EffectState { Effect = e, Baseline = e.amount });
            }
        }
        st.Captured = true;
    }

    internal static void NoteBlendScalar(RoomSettings rs, int index, float value)
    {
        if (rs == null || index < 0 || index >= ScalarCount) return;
        _table.GetOrCreateValue(rs).Blend[index] = value;
    }

    internal static void NoteBlendEffect(RoomSettings rs, RoomSettings.RoomEffect effect, float amount)
    {
        if (rs == null || effect == null) return;
        List<EffectState> list = _table.GetOrCreateValue(rs).Effects;
        for (int i = 0; i < list.Count; i++)
        {
            if (!ReferenceEquals(list[i].Effect, effect)) continue;
            list[i].BlendAmount = amount;
            list[i].BlendWrote = true;
            return;
        }
    }

    internal static bool IsBlendOwned(RoomSettings rs, RoomSettings.RoomEffect.Type type)
    {
        if (rs == null || !_table.TryGetValue(rs, out State st) || !st.Captured) return false;
        RoomSettings.RoomEffect e = rs.GetEffect(type);
        if (e == null) return false;
        List<EffectState> list = st.Effects;
        for (int i = 0; i < list.Count; i++)
        {
            if (!ReferenceEquals(list[i].Effect, e)) continue;
            return list[i].BlendWrote && e.amount == list[i].BlendAmount;
        }
        return false;
    }

    internal static Restore Begin(RoomSettings rs)
    {
        if (rs == null) return null;
        if (!_table.TryGetValue(rs, out State st) || !st.Captured) return null;

        Restore restore = new Restore();
        for (int i = 0; i < ScalarCount; i++)
        {
            float? current = _get[i](rs);
            float? blend = st.Blend[i];
            if (!blend.HasValue) continue;
            if (current != blend) continue;
            restore.Prev[i] = current;
            restore.Changed[i] = true;
            _set[i](rs, st.Baseline[i]);
        }
        List<EffectState> effects = st.Effects;
        for (int i = 0; i < effects.Count; i++)
        {
            EffectState es = effects[i];
            if (!es.BlendWrote || es.Effect.amount != es.BlendAmount) continue;
            restore.EffStates.Add(es);
            restore.EffPrev.Add(es.Effect.amount);
            es.Effect.amount = es.Baseline;
        }

        return restore;
    }

    internal static void End(RoomSettings rs, Restore restore)
    {
        if (rs == null || restore == null) return;
        for (int i = 0; i < ScalarCount; i++)
            if (restore.Changed[i]) _set[i](rs, restore.Prev[i]);
        for (int i = 0; i < restore.EffStates.Count; i++)
            restore.EffStates[i].Effect.amount = restore.EffPrev[i];
    }

    private static bool IsBlendManagedType(RoomSettings.RoomEffect.Type type)
    {
        RoomSettings.RoomEffect.Type[] arr = RoomCameraExtensions.ScalarEffectTypes;
        for (int i = 0; i < arr.Length; i++)
            if (type == arr[i]) return true;
        return false;
    }
}
