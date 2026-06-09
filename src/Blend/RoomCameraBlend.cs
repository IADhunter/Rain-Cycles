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

        // === Terrain ===
        public Texture2D terrainTexA;
        public Texture2D terrainTexB;
        public string lastTerrainPalA;
        public string lastTerrainPalB;
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
        ClearTerrainData(cam);
        ClearEffectData(cam);

        cam.paletteBlend = 0f;

        RSPlugin.log.LogDebug($"[RoomCameraExt] ClearBlendSnapshots: cam={cam.cameraNumber}");
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
    //  LIGHT SOURCE COLOR INTERPOLATION - Para luces Environment durante blend
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Obtiene el color interpolado de un píxel usando los datos de blend actuales.
    /// Útil para luces Environment durante el blend.
    /// </summary>
    public static Color GetInterpolatedPixelColor(this RoomCamera cam, Vector2 pos, float t)
    {
        var data = GetBlendData(cam);
        if (data == null || data.mainPixelsA == null || data.mainPixelsB == null)
            return cam.PixelColorAtCoordinate(pos);
        
        Vector2 vector = pos - cam.CamPos(cam.currentCameraPosition);
        Color pixel = cam.levelTexture.GetPixel(Mathf.FloorToInt(vector.x), Mathf.FloorToInt(vector.y));
        
        float dark = GetDarkPalette(cam);
        
        if (pixel.r == 1f && pixel.g == 1f && pixel.b == 1f)
            return GetPalettePixelInterpolated(data, 0, 7, t, dark);
        
        int num = Mathf.FloorToInt(pixel.r * 255f);
        float num2 = 0f;
        if (num > 90) num -= 90;
        else num2 = 1f;
        
        int num3 = Mathf.FloorToInt((float)num / 30f);
        int num4 = (num - 1) % 30;
        
        Color c0 = GetPalettePixelInterpolated(data, num4, num3, t, dark);
        Color c1 = GetPalettePixelInterpolated(data, num4, num3 + 3, t, dark);
        Color sky = GetPalettePixelInterpolated(data, 1, 7, t, dark);
        float skyF = GetPalettePixelInterpolated(data, 9, 7, t, dark).r;
        float t2 = (float)num4 * (1f - skyF) / 30f;
        
        return Color.Lerp(Color.Lerp(c1, c0, num2), sky, t2);
    }

    private static Color GetPalettePixelInterpolated(CameraBlendData data, int x, int y, float t, float darkPalette)
    {
        const int W = 32;
        
        Color32 a1 = data.mainPixelsA[(y + 8) * W + x];
        Color32 a2 = data.mainPixelsB[(y + 8) * W + x];
        Color32 a1_dark = data.mainPixelsA[y * W + x];
        Color32 a2_dark = data.mainPixelsB[y * W + x];
        
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

    private static float GetDarkPalette(RoomCamera cam)
    {
        var room = cam.room;
        if (room == null) return 0f;
        var rs = room.roomSettings;
        if (rs.DangerType != RoomRain.DangerType.None)
            return room.world.rainCycle.RainDarkPalette * rs.RainIntensity;
        if (rs.GetEffectAmount(RoomSettings.RoomEffect.Type.BrokenZeroG) > 0f
            && room.world.rainCycle.brokenAntiGrav != null)
            return 1f - room.world.rainCycle.brokenAntiGrav.CurrentLightsOn;
        return 0f;
    }
}