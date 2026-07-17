using UnityEngine;
using RainCycles.Snapshot;

namespace RainCycles.Blend;

public static partial class RoomCameraExtensions
{
    // ════════════════════════════════════════════════════════════════════
    //  ROOM SCALARS - PROPIEDADES ESCALARES DIRECTAS DE ROOMSETTINGS
    // ════════════════════════════════════════════════════════════════════
    public static void ApplyRoomScalars(this Room room, SettingsSnapshot a, SettingsSnapshot b, float t)
    {
        if (room == null || a == null || b == null) return;

        var rs = room.roomSettings;
        if (rs == null) return;

        rs.Grime = Mathf.Lerp(a.Grime, b.Grime, t);

        if (!RoomHasWeatherController(room))
            rs.Clouds = LerpClouds(a.Clouds, b.Clouds, t);

        rs.CeilingDrips = Mathf.Lerp(a.CeilingDrips, b.CeilingDrips, t);
        rs.BkgDroneVolume = Mathf.Lerp(a.BkgDroneVolume, b.BkgDroneVolume, t);
        rs.RandomItemDensity = Mathf.Lerp(a.RandomItemDensity, b.RandomItemDensity, t);
        rs.RandomItemSpearChance = Mathf.Lerp(a.RandomItemSpearChance, b.RandomItemSpearChance, t);
        rs.WaterReflectionAlpha = Mathf.Lerp(a.WaterReflectionAlpha, b.WaterReflectionAlpha, t);
    }

    private static float LerpClouds(float cloudsA, float cloudsB, float t)
    {
        if (cloudsA <= 0f && cloudsB <= 0f)
            return 0f;
        if (t <= 0f)
            return cloudsA;
        if (t >= 1f)
            return cloudsB;

        float lerped = Mathf.Lerp(cloudsA, cloudsB, t);
        return Mathf.Max(lerped, 0.001f);
    }

    private static bool RoomHasWeatherController(Room room)
    {
        if (room == null) return false;
        for (int i = 0; i < room.updateList.Count; i++)
            if (room.updateList[i]?.GetType().Name == "WeatherController") return true;
        return false;
    }

    // ════════════════════════════════════════════════════════════════════
    //  TERRAIN SCALARS - INTERPOLACIÓN Y APLICACIÓN DURANTE EL BLEND
    // ════════════════════════════════════════════════════════════════════
    public static void ApplyTerrainScalars(this Room room, SettingsSnapshot a, SettingsSnapshot b, float t)
    {
        if (room == null || a == null || b == null) return;

        var rs = room.roomSettings;
        if (rs == null) return;

        rs.TerrainLight = LerpTerrainScalar(a.TerrainLight, b.TerrainLight, t);
        rs.TerrainStainAmount = LerpTerrainScalar(a.TerrainStainAmount, b.TerrainStainAmount, t);
        rs.TerrainStainBrightness = LerpTerrainScalar(a.TerrainStainBrightness, b.TerrainStainBrightness, t);
        rs.TerrainStainHeight = LerpTerrainScalar(a.TerrainStainHeight, b.TerrainStainHeight, t);
        rs.TerrainWaves = LerpTerrainScalar(a.TerrainWaves, b.TerrainWaves, t);
        rs.TerrainGrain = LerpTerrainScalar(a.TerrainGrain, b.TerrainGrain, t);
        rs.TerrainSkyFade = LerpTerrainScalar(a.TerrainSkyFade, b.TerrainSkyFade, t);
    }

    private static float LerpTerrainScalar(float? va, float? vb, float t)
    {
        float a = va.HasValue && va.Value >= 0f ? va.Value : 0f;
        float b = vb.HasValue && vb.Value >= 0f ? vb.Value : 0f;
        return Mathf.Lerp(a, b, t);
    }
}