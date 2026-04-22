using UnityEngine;
using RainCycles.Settings;
using RainCycles.Snapshot;
using RainCycles.Sky;
using RainCycles.Clock;
using RainCycles.Core;

namespace RainCycles.Core;

// Motor de blend: ApplyBlend, MixAndApply
public static partial class SettingsBlendController
{
    // ── Blend por frame

    private static void ApplyBlend(float t)
    {
        if (_snapA == null || _snapB == null || _room == null) return;

        var cam = _room.game?.cameras?[0];
        if (cam == null) return;

        if (!BlendTextureManager.Ready)
            BlendTextureManager.Load(cam, _snapA, _snapB, _snapOriginal);

        var lerped = SettingsSnapshot.Lerp(_snapA, _snapB, t);
        _activeSnapshot = lerped;

        // Mantener _lastAtmosphereColor siempre actualizado al valor lerpeado actual.
        if (lerped.TintCloudAtmosphere.HasValue)
            _lastAtmosphereColor = lerped.TintCloudAtmosphere.Value;
        var rs = _room.roomSettings;
        rs.Grime                 = lerped.Grime;
        // Clouds: ceder a Forecast/mods de clima si tienen WeatherController en esta sala
        if (!RoomHasWeatherController(_room))
            rs.Clouds            = lerped.Clouds;
        rs.CeilingDrips          = lerped.CeilingDrips;
        rs.BkgDroneVolume        = lerped.BkgDroneVolume;
        rs.RandomItemDensity     = lerped.RandomItemDensity;
        rs.RandomItemSpearChance = lerped.RandomItemSpearChance;
        rs.WaterReflectionAlpha  = lerped.WaterReflectionAlpha;

        RoomEffectsApplier.ApplyShaderGlobals(lerped);
        MixAndApply(cam, t, lerped);

        // Aplicar globals de fondo aquí cubre salas sin RoofTopView/AboveCloudsView
        OverrideBackgroundGlobalsIfActive(_room);

        // En modo no-externo (slider manual) los lights se aplican aquí junto con
        if (!_externalT)
        {
            RoomEffectsApplier.ApplyLightSources(_room, lerped);
            RoomEffectsApplier.ApplyLightBeams(_room, lerped);
        }
    }

    // ELIMINADO: bakear TintMultiply/TintAtmosphere en fadeTexA[1,7] y [2,7] causaba que paletteTexture.skyColor/fogColor quedaran contaminados con el color del tinte. Esos píxeles los lee PixelColorAtCoordinate para colorear tiles y objetos físicos — exactamente los objetos que NO deben ser teñidos.  El mecanismo correcto es doble: 1. ShadPropMultiplyColor / ShadPropAboveCloudsAtmosphereColor se setean via Shader.SetGlobalVector en OverrideBackgroundGlobalsIfActive y OnUpdateDayNightPalette — afectan solo los shaders Background/DistantBkgObject. 2. OnApplyFade sobreescribe currentPalette.skyColor/fogColor directamente sobre la struct después de cada ApplyFade — el vanilla los lee para backgroundGraphic.color y RoofTopView.Update sin tocar fadeTexA.
    private static void BakeTintIntoFadeTex(RoomCamera cam, SettingsSnapshot snap)
    {
// no-op intencional
    }

    private static void MixAndApply(RoomCamera cam, float t, SettingsSnapshot lerped)
    {
        if (!BlendTextureManager.Ready) return;

        // Interpolar opacidad de FadePalette según posición de cámara
        int   camIdx     = cam.currentCameraPosition;
        float opacA      = camIdx < _snapA.FadePaletteOpacities.Length ? _snapA.FadePaletteOpacities[camIdx] : 0f;
        float opacB      = camIdx < _snapB.FadePaletteOpacities.Length ? _snapB.FadePaletteOpacities[camIdx] : 0f;
        cam.paletteBlend = Mathf.Lerp(opacA, opacB, t);

        bool camChanged = camIdx != _lastCamIdx;
        _lastCamIdx = camIdx;

        // Throttle: MixPalettes + Texture2D.Apply son costosos (upload CPU→GPU).
        // camChanged fuerza refresco inmediato al moverse a otra screen durante blend.
        const float PALETTE_THRESHOLD = 0.008f;
        if (Mathf.Abs(t - _lastPaletteT) >= PALETTE_THRESHOLD || _lastPaletteT < 0f || camChanged)
        {
            _lastPaletteT = t;
            BlendTextureManager.MixPalettes(cam, t);
            cam.ApplyFade();
            RoomEffectsApplier.ApplyLightSources(_room, lerped);
            RoomEffectsApplier.ApplyLightBeams(_room, lerped);

            // El fade de terrain está horneado dentro de TerrainPxA/B con el camIdx del Load.
            // Al moverse a otra screen hay que rebakear con el nuevo camIdx para que
            // MixTerrainPalette lerp con la opacidad correcta para esa posición.
            if (camChanged && BlendTextureManager.TerrainReady)
                BlendTextureManager.LoadTerrain(cam, _snapA, _snapB);
        }

        RoomEffectsApplier.ApplyDecalOpacities(_room, lerped);
        RoomEffectsApplier.ApplyScalarEffects(_room, lerped);
        RoomEffectsApplier.ApplyTerrainScalars(_room, lerped);

        // Terrain palette blend — pixel-a-pixel igual que paletas normales
        if (BlendTextureManager.TerrainReady)
            BlendTextureManager.MixTerrainPalette(cam, t);
    }
    // Detecta si un mod de clima (Forecast u otro) gestiona Clouds en esta sala.
    // Usa reflexión por nombre para evitar dependencia directa en el assembly de Forecast.
    private static bool RoomHasWeatherController(Room room)
    {
        if (room == null) return false;
        for (int i = 0; i < room.updateList.Count; i++)
            if (room.updateList[i]?.GetType().Name == "WeatherController") return true;
        return false;
    }

}