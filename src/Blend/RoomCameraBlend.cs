using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using RainCycles.Snapshot;
using RainCycles.Clock;
using RainCycles.Core;

namespace RainCycles.Blend;

public static partial class RoomCameraExtensions
{
    private static readonly ConditionalWeakTable<RoomCamera, CameraBlendData> _blendData
        = new ConditionalWeakTable<RoomCamera, CameraBlendData>();

    public class CameraBlendData
    {
        // === Estado ===
        public bool isBlendActive;
        public string roomName;

        // === Paletas principales ===
        public Texture2D mainTexA;
        public Texture2D mainTexB;
        public int lastMainPaletteA = -1;
        public int lastMainPaletteB = -1;

        // === Arrays precargados para hot path ===
        public Color32[] mainPixelsA;
        public Color32[] mainPixelsB;

        // === Fade palettes ===
        public Texture2D fadeTexA;
        public Texture2D fadeTexB;
        public int lastFadePaletteA = -1;
        public int lastFadePaletteB = -1;

        // === Arrays precargados fade ===
        public Color32[] fadePixelsA;
        public Color32[] fadePixelsB;

        // === Terrain blend ===
        public Texture2D terrainBlendedTexture;
        public int lastTerrainStateA = -1;
        public int lastTerrainStateB = -1;
        public float lastTerrainT = -1f;
        public Color[] terrainScratchA;
        public Color[] terrainScratchB;
        public Color[] terrainResultScratch;
        public int terrainScratchTotal = -1;
    }

    public static CameraBlendData GetBlendData(this RoomCamera cam)
    {
        if (cam == null) return null;
        _blendData.TryGetValue(cam, out var data);
        return data;
    }

    public static void ClearBlendSnapshots(this RoomCamera cam)
    {
        if (cam == null) return;
        var data = GetBlendData(cam);
        if (data == null) return;

        data.isBlendActive = false;

        ClearPaletteData(cam);
        ClearEffectData(cam);

        cam.paletteBlend = 0f;

        if (!string.IsNullOrEmpty(data.roomName))
        {
            InvalidateRoomCache(data.roomName);
        }
    }

    private static bool ShouldHaveBlendActive(RoomCamera cam)
    {
        if (cam?.room == null) return false;
        if (!SettingsBlendController.IsBlendRoom(cam.room)) return false;

        var settings = BlendSettingsLoader.Active;
        bool clockActive = settings != null && settings.Clock && BlendClock.IsRunning;
        bool blendInProgress = SettingsBlendController.IsActive;

        return clockActive || blendInProgress;
    }

    private static CameraBlendData GetOrCreateData(RoomCamera cam)
    {
        if (cam == null) throw new ArgumentNullException(nameof(cam));

        if (!_blendData.TryGetValue(cam, out var data))
        {
            data = new CameraBlendData();
            _blendData.Add(cam, data);
        }
        return data;
    }

    }