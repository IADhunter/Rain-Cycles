using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using RainCycles.Snapshot;
using RainCycles.Clock;
using RainCycles.Core;

namespace RainCycles.Blend;

/// <summary>
/// Extensión modular de RoomCamera para el sistema de blend.
/// 
/// Arquitectura simplificada (Single Source of Truth):
/// - BlendClock es la única fuente de verdad para StateA, StateB, T, Phase.
/// - CameraBlendData solo cachea texturas de paletas (caras de recargar).
/// - No almacena snapshots ni paths. Se resuelven cada frame desde el clock.
/// </summary>
public static partial class RoomCameraExtensions
{
    private static readonly ConditionalWeakTable<RoomCamera, CameraBlendData> _blendData
        = new ConditionalWeakTable<RoomCamera, CameraBlendData>();

    public class CameraBlendData
    {
        // === Estado: solo metadatos de texturas, NO snapshots ===
        public bool isBlendActive;
        public string roomName;
        public int lastUpdateFrame = -1;

        // === Paletas principales (cache de texturas) ===
        public Texture2D mainTexA;
        public Texture2D mainTexB;
        public int lastMainPaletteA = -1;
        public int lastMainPaletteB = -1;

        // === Arrays precargados para hot path ===
        public Color32[] mainPixelsA;
        public Color32[] mainPixelsB;

        // === Fade palettes (cache de texturas) ===
        public Texture2D fadeTexA;
        public Texture2D fadeTexB;
        public int lastFadePaletteA = -1;
        public int lastFadePaletteB = -1;

        // === Arrays precargados fade ===
        public Color32[] fadePixelsA;
        public Color32[] fadePixelsB;

        // === Últimos valores aplicados (para evitar trabajo redundante) ===
        public float lastBlendT = -1f;
        public int lastStateA = -1;
        public int lastStateB = -1;

        // === Terrain blend (ver RoomCameraBlend.Terrain.cs) ===
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
        data.lastBlendT = -1f;
        data.lastStateA = -1;
        data.lastStateB = -1;

        ClearPaletteData(cam);
        ClearEffectData(cam);

        cam.paletteBlend = 0f;

        // ════════════════════════════════════════════════════════════════════
        // LIMPIAR CACHE DE LA SALA ASOCIADA A ESTA CÁMARA
        // ════════════════════════════════════════════════════════════════════
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

    // ═════════════════════════════════════════════════════════════════════
    //  LIGHT SOURCE COLOR INTERPOLATION
    //  MOVIDO a RoomCameraBlend.Lights.cs (junto con los patches que lo
    //  consumen: LightBeam.Update / LightSource.Update). Ver ese archivo.
    // ═════════════════════════════════════════════════════════════════════
}