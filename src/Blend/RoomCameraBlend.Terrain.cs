using UnityEngine;
using RainCycles.Snapshot;

namespace RainCycles.Blend;

public static partial class RoomCameraExtensions
{
    // ═════════════════════════════════════════════════════════════════════
    //  TERRAIN PALETTE
    // ═════════════════════════════════════════════════════════════════════
    // TODO: Implementar cuando el sistema de terrain esté listo.
    //       Por ahora, stubs mínimos para mantener la arquitectura.

    public static void SetBlendTerrain(this RoomCamera cam, CameraBlendData data,
        SettingsSnapshot snapA, SettingsSnapshot snapB)
    {
        if (cam == null || data == null) return;

        // TODO: Cargar terrain textures de snapA.TerrainPaletteName / snapB.TerrainPaletteName
        // TODO: Almacenar en data.terrainTexA / data.terrainTexB
        // TODO: Actualizar data.lastTerrainPalA / data.lastTerrainPalB

        RSPlugin.log.LogDebug($"[RoomCameraExt.Terrain] SetBlendTerrain STUB: cam={cam.cameraNumber}");
    }

    public static void UpdateBlendTerrain(this RoomCamera cam, float t)
    {
        if (cam == null) return;

        var data = GetBlendData(cam);
        if (data == null || !data.isBlendActive) return;

        // TODO: Interpolar terrainTexA ↔ terrainTexB según t
        // TODO: Aplicar resultado a cam.room.roomSettings.TerrainPalette o equivalente

        // Stub: no-op por ahora
    }

    // ── Private: limpieza (llamado desde base) ─────────────────────────

    private static void ClearTerrainData(RoomCamera cam)
    {
        var data = GetBlendData(cam);
        if (data == null) return;

        if (data.terrainTexA != null) { UnityEngine.Object.Destroy(data.terrainTexA); data.terrainTexA = null; }
        if (data.terrainTexB != null) { UnityEngine.Object.Destroy(data.terrainTexB); data.terrainTexB = null; }
        data.lastTerrainPalA = null;
        data.lastTerrainPalB = null;
    }
}