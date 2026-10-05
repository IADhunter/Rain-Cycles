using UnityEngine;
using RainCycles.Snapshot;
using RainCycles.Patches;

namespace RainCycles.Blend;

public static partial class RoomCameraExtensions
{
    // ════════════════════════════════════════════════════════════════════
    //  ROOM SCALARS - PROPIEDADES ESCALARES DIRECTAS DE ROOMSETTINGS
    // ════════════════════════════════════════════════════════════════════
    // Cada escritura reclama la propiedad en SaveGuard (dueño del valor):
    // al guardar, Save escribe el baseline de carga en vez del valor
    // transitorio del blend. Ver docs/SETTINGS_SAVE_GUARD.md.
    public static void ApplyRoomScalars(this Room room, SettingsSnapshot a, SettingsSnapshot b, float t)
    {
        if (room == null || a == null || b == null) return;

        var rs = room.roomSettings;
        if (rs == null) return;

        rs.Grime = Mathf.Lerp(a.Grime, b.Grime, t);
        SaveGuard.NoteBlendScalar(rs, SaveGuard.Grime, rs.Grime);

        if (!RoomHasWeatherController(room))
        {
            rs.Clouds = LerpClouds(a.Clouds, b.Clouds, t);
            SaveGuard.NoteBlendScalar(rs, SaveGuard.Clouds, rs.Clouds);
        }

        rs.CeilingDrips = Mathf.Lerp(a.CeilingDrips, b.CeilingDrips, t);
        SaveGuard.NoteBlendScalar(rs, SaveGuard.CeilingDrips, rs.CeilingDrips);

        rs.BkgDroneVolume = Mathf.Lerp(a.BkgDroneVolume, b.BkgDroneVolume, t);
        SaveGuard.NoteBlendScalar(rs, SaveGuard.BkgDroneVolume, rs.BkgDroneVolume);

        rs.RandomItemDensity = Mathf.Lerp(a.RandomItemDensity, b.RandomItemDensity, t);
        SaveGuard.NoteBlendScalar(rs, SaveGuard.RandomItemDensity, rs.RandomItemDensity);

        rs.RandomItemSpearChance = Mathf.Lerp(a.RandomItemSpearChance, b.RandomItemSpearChance, t);
        SaveGuard.NoteBlendScalar(rs, SaveGuard.RandomItemSpearChance, rs.RandomItemSpearChance);

        rs.WaterReflectionAlpha = Mathf.Lerp(a.WaterReflectionAlpha, b.WaterReflectionAlpha, t);
        SaveGuard.NoteBlendScalar(rs, SaveGuard.WaterReflectionAlpha, rs.WaterReflectionAlpha);
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
        SaveGuard.NoteBlendScalar(rs, SaveGuard.TerrainLight, rs.TerrainLight);

        rs.TerrainStainAmount = LerpTerrainScalar(a.TerrainStainAmount, b.TerrainStainAmount, t);
        SaveGuard.NoteBlendScalar(rs, SaveGuard.TerrainStainAmount, rs.TerrainStainAmount);

        rs.TerrainStainBrightness = LerpTerrainScalar(a.TerrainStainBrightness, b.TerrainStainBrightness, t);
        SaveGuard.NoteBlendScalar(rs, SaveGuard.TerrainStainBrightness, rs.TerrainStainBrightness);

        rs.TerrainStainHeight = LerpTerrainScalar(a.TerrainStainHeight, b.TerrainStainHeight, t);
        SaveGuard.NoteBlendScalar(rs, SaveGuard.TerrainStainHeight, rs.TerrainStainHeight);

        rs.TerrainWaves = LerpTerrainScalar(a.TerrainWaves, b.TerrainWaves, t);
        SaveGuard.NoteBlendScalar(rs, SaveGuard.TerrainWaves, rs.TerrainWaves);

        rs.TerrainGrain = LerpTerrainScalar(a.TerrainGrain, b.TerrainGrain, t);
        SaveGuard.NoteBlendScalar(rs, SaveGuard.TerrainGrain, rs.TerrainGrain);

        rs.TerrainSkyFade = LerpTerrainScalar(a.TerrainSkyFade, b.TerrainSkyFade, t);
        SaveGuard.NoteBlendScalar(rs, SaveGuard.TerrainSkyFade, rs.TerrainSkyFade);
    }

    private static float LerpTerrainScalar(float? va, float? vb, float t)
    {
        float a = va.HasValue && va.Value >= 0f ? va.Value : 0f;
        float b = vb.HasValue && vb.Value >= 0f ? vb.Value : 0f;
        return Mathf.Lerp(a, b, t);
    }
}