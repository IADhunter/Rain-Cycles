using System.Collections.Generic;
using UnityEngine;
using RainCycles.Clock;
using RainCycles.Core;
using RainCycles.Snapshot;

namespace RainCycles.Blend;

public static partial class RoomCameraExtensions
{
    private static readonly Dictionary<LightSource, int> _lightIndex = new Dictionary<LightSource, int>();

    private static bool _lightsInitialized = false;

    public static void InitLights()
    {
        if (_lightsInitialized) return;

        On.LightBeam.Update += OnLightBeamUpdate;
        On.LightSource.Update += OnLightSourceUpdate;

        _lightsInitialized = true;
    }

    // ═════════════════════════════════════════════════════════════════════
    //  LIGHTBEAM - color de Environment durante blend
    // ═════════════════════════════════════════════════════════════════════
    private static void OnLightBeamUpdate(On.LightBeam.orig_Update orig, LightBeam self, bool eu)
    {
        orig(self, eu);

        if (self?.room == null) return;
        if (!SettingsBlendController.IsBlendRoom(self.room)) return;

        var beamData = self.placedObject?.data as LightBeam.LightBeamData;
        if (beamData == null) return;
        if (beamData.colorB <= 0f) return;

        var cam = self.room.game?.cameras?[0];
        if (cam == null) return;

        if (cam.room != self.room) return;

        string roomName = self.room.abstractRoom?.name;
        if (string.IsNullOrEmpty(roomName)) return;

        ResolveBlendStates(out int stateA, out int stateB, out float t);

        if (!TryGetPalettePixels(roomName, stateA, stateB, out Color32[] pixelsA, out Color32[] pixelsB))
            return;

        float dark = GetDarkPalette(self.room);
        self.environmentColor = GetInterpolatedPixelColor(cam, self.quad[1], pixelsA, pixelsB, t, dark);
    }

    // ═════════════════════════════════════════════════════════════════════
    //  LIGHTSOURCE - color de Environment durante blend
    // ═════════════════════════════════════════════════════════════════════
    private static void OnLightSourceUpdate(On.LightSource.orig_Update orig, LightSource self, bool eu)
    {
        orig(self, eu);

        if (!self.colorFromEnvironment) return;
        if (self.room == null) return;
        if (!SettingsBlendController.IsBlendRoom(self.room)) return;

        var cam = self.room.game?.cameras?[0];
        if (cam == null) return;

        if (cam.room != self.room) return;

        string roomName = self.room.abstractRoom?.name;
        if (string.IsNullOrEmpty(roomName)) return;

        ResolveBlendStates(out int stateA, out int stateB, out float t);

        if (!TryGetPalettePixels(roomName, stateA, stateB, out Color32[] pixelsA, out Color32[] pixelsB))
            return;

        float dark = GetDarkPalette(self.room);
        self.color = GetInterpolatedPixelColor(cam, self.Pos, pixelsA, pixelsB, t, dark);
    }

    // ═════════════════════════════════════════════════════════════════════
    //  RESOLVE STATES + T
    // ═════════════════════════════════════════════════════════════════════
    private static void ResolveBlendStates(out int stateA, out int stateB, out float t)
    {
        if (SettingsBlendController.IsActive &&
            SettingsBlendController.IsExternalT &&
            !SettingsBlendController.IsAutoBlend)
        {
            stateA = SettingsBlendController.ManualStateA;
            stateB = SettingsBlendController.ManualStateB;
            t = SettingsBlendController.ForcedT;
        }
        else if (BlendClock.IsRunning)
        {
            stateA = BlendClock.StateA;
            stateB = BlendClock.StateB;
            t = BlendClock.SubPhaseLocalT;
        }
        else
        {
            stateA = BlendClock.StateA;
            stateB = stateA;
            t = 0f;
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    //  OBTENER PIXELS DE PALETA DESDE EL CACHE POR SALA
    // ═════════════════════════════════════════════════════════════════════
    private static bool TryGetPalettePixels(string roomName, int stateA, int stateB,
        out Color32[] pixelsA, out Color32[] pixelsB)
    {
        pixelsA = null;
        pixelsB = null;

        var keyA = (roomName, stateA);
        if (!_stateCache.TryGetValue(keyA, out var cachedA) || cachedA.mainPixels == null)
        {
            return false;
        }

        pixelsA = cachedA.mainPixels;
        pixelsB = pixelsA;

        if (stateA != stateB)
        {
            var keyB = (roomName, stateB);
            if (!_stateCache.TryGetValue(keyB, out var cachedB) || cachedB.mainPixels == null)
            {
                return false;
            }
            pixelsB = cachedB.mainPixels;
        }

        return true;
    }

    // ═════════════════════════════════════════════════════════════════════
    //  GET DARK PALETTE - versión por Room
    // ═════════════════════════════════════════════════════════════════════
    private static float GetDarkPalette(Room room)
    {
        if (room == null) return 0f;
        var rs = room.roomSettings;
        if (rs.DangerType != RoomRain.DangerType.None)
            return room.world.rainCycle.RainDarkPalette * rs.RainIntensity;
        if (rs.GetEffectAmount(RoomSettings.RoomEffect.Type.BrokenZeroG) > 0f
            && room.world.rainCycle.brokenAntiGrav != null)
            return 1f - room.world.rainCycle.brokenAntiGrav.CurrentLightsOn;
        return 0f;
    }

    // ═════════════════════════════════════════════════════════════════════
    //  GET INTERPOLATED PIXEL COLOR - con arrays directos
    // ═════════════════════════════════════════════════════════════════════
    private static Color GetInterpolatedPixelColor(RoomCamera cam, Vector2 pos,
        Color32[] mainPixelsA, Color32[] mainPixelsB, float t, float darkPalette)
    {
        if (mainPixelsA == null || mainPixelsB == null)
            return cam.PixelColorAtCoordinate(pos);

        Vector2 vector = pos - cam.CamPos(cam.currentCameraPosition);
        Color pixel = cam.levelTexture.GetPixel(Mathf.FloorToInt(vector.x), Mathf.FloorToInt(vector.y));

        if (pixel.r == 1f && pixel.g == 1f && pixel.b == 1f)
            return GetPalettePixelInterpolated(mainPixelsA, mainPixelsB, 0, 7, t, darkPalette);

        int num = Mathf.FloorToInt(pixel.r * 255f);
        float num2 = 0f;
        if (num > 90) num -= 90;
        else num2 = 1f;

        int num3 = Mathf.FloorToInt((float)num / 30f);
        int num4 = (num - 1) % 30;

        Color c0 = GetPalettePixelInterpolated(mainPixelsA, mainPixelsB, num4, num3, t, darkPalette);
        Color c1 = GetPalettePixelInterpolated(mainPixelsA, mainPixelsB, num4, num3 + 3, t, darkPalette);
        Color sky = GetPalettePixelInterpolated(mainPixelsA, mainPixelsB, 1, 7, t, darkPalette);
        float skyF = GetPalettePixelInterpolated(mainPixelsA, mainPixelsB, 9, 7, t, darkPalette).r;
        float t2 = (float)num4 * (1f - skyF) / 30f;

        return Color.Lerp(Color.Lerp(c1, c0, num2), sky, t2);
    }

    private static Color GetPalettePixelInterpolated(Color32[] pixelsA, Color32[] pixelsB,
        int x, int y, float t, float darkPalette)
    {
        const int W = 32;

        Color32 a1 = pixelsA[(y + 8) * W + x];
        Color32 a2 = pixelsB[(y + 8) * W + x];
        Color32 a1_dark = pixelsA[y * W + x];
        Color32 a2_dark = pixelsB[y * W + x];

        Color lit = new Color(
            (a1.r + (a2.r - a1.r) * t) / 255f,
            (a1.g + (a2.g - a1.g) * t) / 255f,
            (a1.b + (a2.b - a1.b) * t) / 255f);
        Color dark = new Color(
            (a1_dark.r + (a2_dark.r - a1_dark.r) * t) / 255f,
            (a1_dark.g + (a2_dark.g - a1_dark.g) * t) / 255f,
            (a1_dark.b + (a2_dark.b - a1_dark.b) * t) / 255f);

        return Color.Lerp(lit, dark, darkPalette);
    }

    // ═════════════════════════════════════════════════════════════════════
    //  LIGHT INDEX
    // ═════════════════════════════════════════════════════════════════════
    public static void BuildLightIndex(Room room)
    {
        _lightIndex.Clear();

        if (room == null) return;

        var placedObjects = room.roomSettings.placedObjects;
        const float EPSILON = 1f;

        for (int i = 0; i < room.updateList.Count; i++)
        {
            var light = room.updateList[i] as LightSource;
            if (light == null) continue;

            for (int j = 0; j < placedObjects.Count; j++)
            {
                if (placedObjects[j].type != PlacedObject.Type.LightSource) continue;
                if (Vector2.Distance(placedObjects[j].pos, light.pos) > EPSILON) continue;
                _lightIndex[light] = j;
                break;
            }
        }
    }

    public static void ClearLightIndex()
    {
        _lightIndex.Clear();
    }

    // ═════════════════════════════════════════════════════════════════════
    //  LIGHTSOURCES - INTENSIDAD
    // ═════════════════════════════════════════════════════════════════════
    public static void ApplyLightSources(this Room room, SettingsSnapshot a, SettingsSnapshot b, float t)
    {
        if (a.LightIntensities.Count == 0) return;

        foreach (var kv in _lightIndex)
        {
            var light = kv.Key;
            int snapIdx = kv.Value;
            if (light == null || light.slatedForDeletetion) continue;

            if (!a.LightIntensities.TryGetValue(snapIdx, out float intensityA)) continue;
            if (!b.LightIntensities.TryGetValue(snapIdx, out float intensityB))
                intensityB = intensityA;

            light.alpha = Mathf.Clamp01(Mathf.Lerp(intensityA, intensityB, t));
        }
    }

    public static void ApplyLightSourcesFromSnapshot(Room room, string path)
    {
        var snap = SettingsSnapshot.FromFileWithTemplate(path, room.abstractRoom.name);
        BuildLightIndex(room);
        room.ApplyLightSources(snap, snap, 0f);
    }

    // ═════════════════════════════════════════════════════════════════════
    //  LIGHTBEAMS - OPACIDAD/COLOR
    // ═════════════════════════════════════════════════════════════════════
    public static void ApplyLightBeams(this Room room, SettingsSnapshot a, SettingsSnapshot b, float t)
    {
        if (a.LightBeams.Count == 0) return;

        var beamSnapIndices = new List<int>();
        for (int i = 0; i < a.PlacedObjectLines.Count; i++)
            if (a.PlacedObjectLines[i].StartsWith("LightBeam><"))
                beamSnapIndices.Add(i);

        int beamCount = 0;
        for (int i = 0; i < room.updateList.Count; i++)
        {
            var beam = room.updateList[i] as LightBeam;
            if (beam == null || beam.placedObject == null) continue;

            if (beamCount < beamSnapIndices.Count)
            {
                int snapIdx = beamSnapIndices[beamCount];
                if (a.LightBeams.TryGetValue(snapIdx, out LightBeamData beamA))
                {
                    if (!b.LightBeams.TryGetValue(snapIdx, out LightBeamData beamB))
                        beamB = beamA;

                    float opacity = LerpBeamOpacity(beamA.Opacity, beamB.Opacity, t);
                    float colorA  = Mathf.Lerp(beamA.ColorA, beamB.ColorA, t);
                    float colorB  = Mathf.Lerp(beamA.ColorB, beamB.ColorB, t);

                    var beamData = beam.placedObject.data as LightBeam.LightBeamData;
                    if (beamData != null)
                    {
                        beamData.alpha  = Mathf.Clamp01(opacity);
                        beamData.colorA = Mathf.Clamp01(colorA);
                        beamData.colorB = Mathf.Clamp01(colorB);
                        beam.meshDirty  = true;
                    }
                }
            }
            beamCount++;
        }
    }

    public static void ApplyLightBeamsFromSnapshot(Room room, string path)
    {
        var snap = SettingsSnapshot.FromFileWithTemplate(path, room.abstractRoom.name);
        room.ApplyLightBeams(snap, snap, 0f);
    }

    private static float LerpBeamOpacity(float opA, float opB, float t)
    {
        GetBeamRange(opA, out float minA, out float maxA);
        GetBeamRange(opB, out float minB, out float maxB);

        float rangeB = maxB - minB;
        float normB  = rangeB > 0f ? Mathf.Clamp01((opB - minB) / rangeB) : 0f;
        float opBInRangeA = minA + normB * (maxA - minA);

        return Mathf.Lerp(opA, opBInRangeA, t);
    }

    private static void GetBeamRange(float op, out float rMin, out float rMax)
    {
        if (op < 0.3333f)      { rMin = 0f;      rMax = 0.3333f; }
        else if (op < 0.6667f) { rMin = 0.3333f; rMax = 0.6667f; }
        else                    { rMin = 0.6667f; rMax = 1f;      }
    }
}