using UnityEngine;
using RainCycles.Settings;
using RainCycles.Snapshot;
using RainCycles.Clock;
using RainCycles.Core;
using RainCycles.Blend;

namespace RainCycles.Core;

public static partial class SettingsBlendController
{
    private static void ApplyBlend(float t)
    {
        // ================================================================
        // Si la sala actual es estática, NO hacer nada
        // Las salas estáticas son completamente independientes del blend
        // ================================================================
        if (_room != null && StaticTintManager.IsStaticViewRoom(_room))
        {
            return;
        }

        if (_snapA == null || _snapB == null || _room == null)
            return;

        var cam = _room.game?.cameras?[0];
        if (cam == null)
            return;

        // ================================================================
        // 1. PALETAS — COMPLETAMENTE AUTÓNOMAS
        // ================================================================
        var blendData = cam.GetBlendData();
        if (blendData == null || !blendData.isBlendActive)
            cam.SetBlendActive(_room.abstractRoom.name);
        
        cam.UpdateBlendPalette();

        // ================================================================
        // 2. TINTES — Shaders globales + ACV.atmosphereColor
        // ================================================================
        var lerped = SettingsSnapshot.Lerp(_snapA, _snapB, t);
        _activeSnapshot = lerped;

        if (lerped.TintMultiply.HasValue)
        {
            var c = lerped.TintMultiply.Value;
            Shader.SetGlobalVector(RainWorld.ShadPropMultiplyColor, new Vector4(c.r, c.g, c.b, 1f));
        }
        if (lerped.TintAtmosphere.HasValue)
        {
            var c = lerped.TintAtmosphere.Value;
            Shader.SetGlobalVector(RainWorld.ShadPropAboveCloudsAtmosphereColor, new Vector4(c.r, c.g, c.b, 1f));
            
            // Aplicar también a ACV si existe en la sala
            if (_room != null)
            {
                for (int i = 0; i < _room.updateList.Count; i++)
                {
                    if (_room.updateList[i] is AboveCloudsView acv)
                    {
                        acv.atmosphereColor = c;
                        break;
                    }
                }
            }
        }

        // ================================================================
        // 3. ROOMSETTINGS — Propiedades escalares
        // ================================================================
        var rs = _room.roomSettings;
        rs.Grime = lerped.Grime;
        if (!RoomHasWeatherController(_room))
            rs.Clouds = lerped.Clouds;
        rs.CeilingDrips = lerped.CeilingDrips;
        rs.BkgDroneVolume = lerped.BkgDroneVolume;
        rs.RandomItemDensity = lerped.RandomItemDensity;
        rs.RandomItemSpearChance = lerped.RandomItemSpearChance;
        rs.WaterReflectionAlpha = lerped.WaterReflectionAlpha;

        // ================================================================
        // 4. MIXANDAPPLY — Decals, luces, efectos, terrain
        // ================================================================
        MixAndApply(cam, t, lerped);

        if (!_externalT)
        {
            RoomEffectsApplier.ApplyLightSources(_room, lerped);
            RoomEffectsApplier.ApplyLightBeams(_room, lerped);
        }

        // Crossfade alpha en slots de fondo
        if (_room != null)
        {
            var skyType = GetViewFromLoadedSettings(_room);
            if (skyType != SkyType.None)
                ApplyRcSlotsAlpha(skyType, t, isBlending: true);
        }
    }

    private static void MixAndApply(RoomCamera cam, float t, SettingsSnapshot lerped)
    {
        RoomEffectsApplier.ApplyDecalOpacities(_room, lerped);
        RoomEffectsApplier.ApplyScalarEffects(_room, lerped);
        RoomEffectsApplier.ApplyTerrainScalars(_room, lerped);

        if (BlendTextureManager.TerrainReady)
            BlendTextureManager.MixTerrainPalette(cam, t, _snapA, _snapB);
    }

    private static bool RoomHasWeatherController(Room room)
    {
        if (room == null) return false;
        for (int i = 0; i < room.updateList.Count; i++)
            if (room.updateList[i]?.GetType().Name == "WeatherController") return true;
        return false;
    }

    private static void OnChangeBothPalettes(
        On.RoomCamera.orig_ChangeBothPalettes orig, RoomCamera self,
        int palA, int palB, float blend)
    {
        var blendData = self.GetBlendData();
        bool isBlendActive = blendData != null && blendData.isBlendActive;

        if (self.loadingRoom != null && !IsBlendRoom(self.loadingRoom))
        {
            orig(self, palA, palB, blend);
            return;
        }

        if (self.room != null && !IsBlendRoom(self.room) && isBlendActive)
        {
            if (blendData != null) blendData.isBlendActive = false;
            orig(self, palA, palB, blend);
            return;
        }

        if (_active && _room != null && self.room == _room && isBlendActive
            && !_moveCameraThisFrame)
        {
            return;
        }
        orig(self, palA, palB, blend);
    }

    private static void OnApplyPalette(On.RoomCamera.orig_ApplyPalette orig, RoomCamera self)
    {
        orig(self);

        if (_active && BlendTextureManager.TerrainReady && self.terrainPalette != null)
        {
            float t = _externalT ? _forcedT : BlendClock.SubPhaseLocalT;
            BlendTextureManager.MixTerrainPalette(self, t, _snapA, _snapB);
        }
    }

    public static void OnApplyFade(On.RoomCamera.orig_ApplyFade orig, RoomCamera self)
    {
        orig(self);
    }
}