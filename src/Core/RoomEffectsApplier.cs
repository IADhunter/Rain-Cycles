using System.Collections.Generic;
using UnityEngine;

namespace RainCycles.Core;

public static class RoomEffectsApplier
{
    // Mapea cada LightSource en room.updateList a su índice en placedObjects.
    private static readonly Dictionary<LightSource, int> _lightIndex = new Dictionary<LightSource, int>();

    // ────────────────────

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

    // ────────────────────

    public static void ApplyDecalsFromSnapshot(Room room, string path)
    {
        var snap = SettingsSnapshot.FromFileWithTemplate(path, room.abstractRoom.name);
        ApplyDecalOpacities(room, snap);
    }

    public static void ApplyLightBeamsFromSnapshot(Room room, string path)
    {
        var snap = SettingsSnapshot.FromFileWithTemplate(path, room.abstractRoom.name);
        ApplyLightBeams(room, snap);
    }

    public static void ApplyLightSourcesFromSnapshot(Room room, string path)
    {
        var snap = SettingsSnapshot.FromFileWithTemplate(path, room.abstractRoom.name);
        BuildLightIndex(room);
        ApplyLightSources(room, snap);
    }

    // ────────────────────

    internal static void ApplyDecalOpacities(Room room, SettingsSnapshot lerped)
    {
        if (lerped.DecalOpacities.Count == 0) return;

        // Los índices en DecalOpacities son posiciones dentro de PlacedObjectLines
        var decalSnapIndices = new System.Collections.Generic.List<int>();
        for (int i = 0; i < lerped.PlacedObjectLines.Count; i++)
            if (lerped.PlacedObjectLines[i].StartsWith("CustomDecal><"))
                decalSnapIndices.Add(i);

        int decalCount = 0;
        for (int i = 0; i < room.updateList.Count; i++)
        {
            var decal = room.updateList[i] as CustomDecal;
            if (decal == null) continue;

            if (decalCount < decalSnapIndices.Count)
            {
                int snapIdx = decalSnapIndices[decalCount];
                float[] ops;
                if (lerped.DecalOpacities.TryGetValue(snapIdx, out ops))
                {
                    var data = decal.placedObject.data as PlacedObject.CustomDecalData;
                    if (data != null)
                    {
                        for (int j = 0; j < 4 && j < ops.Length; j++)
                        {
                            float op = ops[j];
                            if (op < 0f || op > 1f)
                            {
                                RSPlugin.log.LogWarning(
                                    $"[RoomEffectsApplier] CustomDecal snapIdx={snapIdx} vertex[{j}] " +
                                    $"opacity={op:F4} out of [0,1] — clamping. " +
                                    $"Check ExtractDecalOpacities token indices (t[12,14,16,18]).");
                                op = Mathf.Clamp01(op);
                            }
                            data.vertices[j, 0] = op;
                        }
                        decal.meshDirty = true;
                    }
                }
            }
            decalCount++;
        }
    }

    // Replica RoomCamera.DarkPalette (propiedad privada).
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

    // Calcula el pixel de paletteTexture en (x, y) interpolando directamente
    private static Color InterpolatedPalPixel(int x, int y, float t, float darkPalette, float paletteBlend)
    {
        const int W = 32; // ancho de fadeTexA/B

        var pA1 = BlendTextureManager.PxA_s1;
        var pA2 = BlendTextureManager.PxA_s2;
        var pB1 = BlendTextureManager.PxB_s1;
        var pB2 = BlendTextureManager.PxB_s2;

        // fadeTexA interpolado entre los dos snapshots
        Color32 a1_lit  = pA1[( y + 8) * W + x];
        Color32 a2_lit  = pA2[( y + 8) * W + x];
        Color32 a1_dark = pA1[  y       * W + x];
        Color32 a2_dark = pA2[  y       * W + x];

        Color fadeA_lit  = new Color(
            (a1_lit.r  + (a2_lit.r  - a1_lit.r)  * t) / 255f,
            (a1_lit.g  + (a2_lit.g  - a1_lit.g)  * t) / 255f,
            (a1_lit.b  + (a2_lit.b  - a1_lit.b)  * t) / 255f);
        Color fadeA_dark = new Color(
            (a1_dark.r + (a2_dark.r - a1_dark.r) * t) / 255f,
            (a1_dark.g + (a2_dark.g - a1_dark.g) * t) / 255f,
            (a1_dark.b + (a2_dark.b - a1_dark.b) * t) / 255f);
        Color fadeA = Color.Lerp(fadeA_lit, fadeA_dark, darkPalette);

        if (pB1 == null || pB2 == null || paletteBlend <= 0f)
            return fadeA;

        // fadeTexB interpolado entre los dos snapshots
        Color32 b1_lit  = pB1[( y + 8) * W + x];
        Color32 b2_lit  = pB2[( y + 8) * W + x];
        Color32 b1_dark = pB1[  y       * W + x];
        Color32 b2_dark = pB2[  y       * W + x];

        Color fadeB_lit  = new Color(
            (b1_lit.r  + (b2_lit.r  - b1_lit.r)  * t) / 255f,
            (b1_lit.g  + (b2_lit.g  - b1_lit.g)  * t) / 255f,
            (b1_lit.b  + (b2_lit.b  - b1_lit.b)  * t) / 255f);
        Color fadeB_dark = new Color(
            (b1_dark.r + (b2_dark.r - b1_dark.r) * t) / 255f,
            (b1_dark.g + (b2_dark.g - b1_dark.g) * t) / 255f,
            (b1_dark.b + (b2_dark.b - b1_dark.b) * t) / 255f);
        Color fadeB = Color.Lerp(fadeB_lit, fadeB_dark, darkPalette);

        return Color.Lerp(fadeA, fadeB, paletteBlend);
    }

    // Replica PixelColorAtCoordinate usando paleta interpolada en memoria.
    private static Color PixelColorInterpolated(RoomCamera cam, Vector2 pos, float t)
    {
        Vector2 vector = pos - cam.CamPos(cam.currentCameraPosition);
        Color pixel = cam.levelTexture.GetPixel(Mathf.FloorToInt(vector.x), Mathf.FloorToInt(vector.y));

        float dark    = GetDarkPalette(cam);
        float palBlend = cam.paletteBlend;

        if (pixel.r == 1f && pixel.g == 1f && pixel.b == 1f)
            return InterpolatedPalPixel(0, 7, t, dark, palBlend);

        int num = Mathf.FloorToInt(pixel.r * 255f);
        float num2 = 0f;
        if (num > 90) num -= 90;
        else          num2 = 1f;

        int num3 = Mathf.FloorToInt((float)num / 30f);
        int num4 = (num - 1) % 30;

        Color c0  = InterpolatedPalPixel(num4, num3,     t, dark, palBlend);
        Color c1  = InterpolatedPalPixel(num4, num3 + 3, t, dark, palBlend);
        Color sky = InterpolatedPalPixel(1, 7,            t, dark, palBlend);
        float skyF = InterpolatedPalPixel(9, 7,           t, dark, palBlend).r;
        float t2   = (float)num4 * (1f - skyF) / 30f;

        return Color.Lerp(Color.Lerp(c1, c0, num2), sky, t2);
    }

    internal static void ApplyLightSources(Room room, SettingsSnapshot lerped)
    {
        if (lerped.LightIntensities.Count == 0) return;

        var cam = room.game?.cameras?[0];
        bool canInterpolateColor = cam != null
                                && BlendTextureManager.Ready
                                && BlendTextureManager.TexA_s1 != null
                                && BlendTextureManager.TexA_s2 != null;

        float t = SettingsBlendController.ForcedT;

        foreach (var kv in _lightIndex)
        {
            var light   = kv.Key;
            int snapIdx = kv.Value;
            if (light == null || light.slatedForDeletetion) continue;

            float targetAlpha;
            if (lerped.LightIntensities.TryGetValue(snapIdx, out targetAlpha))
            {
                // Alpha debe estar en [0, 1]. Valores > 1 indican que el parser
                if (targetAlpha < 0f || targetAlpha > 1f)
                {
                    RSPlugin.log.LogWarning(
                        $"[RoomEffectsApplier] LightSource snapIdx={snapIdx} alpha={targetAlpha:F4} " +
                        $"out of [0,1] — clamping. Check ExtractLightIntensity token index.");
                    targetAlpha = Mathf.Clamp01(targetAlpha);
                }
                light.alpha = targetAlpha;
            }

            // Sobreescribir color post-orig: la luz ya leyó paletteTexture física
            if (canInterpolateColor && light.colorFromEnvironment)
                light.color = PixelColorInterpolated(cam, light.Pos, t);
        }
    }

    internal static void ApplyLightBeams(Room room, SettingsSnapshot lerped)
    {
        if (lerped.LightBeams.Count == 0) return;

        // Mismo principio que ApplyDecalOpacities: índices posicionales por tipo.
        var beamSnapIndices = new System.Collections.Generic.List<int>();
        for (int i = 0; i < lerped.PlacedObjectLines.Count; i++)
            if (lerped.PlacedObjectLines[i].StartsWith("LightBeam><"))
                beamSnapIndices.Add(i);

        int beamCount = 0;
        for (int i = 0; i < room.updateList.Count; i++)
        {
            var beam = room.updateList[i] as LightBeam;
            if (beam == null || beam.placedObject == null) continue;

            if (beamCount < beamSnapIndices.Count)
            {
                int snapIdx = beamSnapIndices[beamCount];
                LightBeamData snapData;
                if (lerped.LightBeams.TryGetValue(snapIdx, out snapData))
                {
                    var beamData = beam.placedObject.data as LightBeam.LightBeamData;
                    if (beamData != null)
                    {
                        float opacity = snapData.Opacity;
                        float colorA  = snapData.ColorA;
                        float colorB  = snapData.ColorB;

                        if (opacity < 0f || opacity > 1f)
                        {
                            RSPlugin.log.LogWarning(
                                $"[RoomEffectsApplier] LightBeam snapIdx={snapIdx} Opacity={opacity:F4} " +
                                $"out of [0,1] — clamping. Check ExtractLightBeam token indices (t[8]).");
                            opacity = Mathf.Clamp01(opacity);
                        }
                        if (colorA < 0f || colorA > 1f)
                        {
                            RSPlugin.log.LogWarning(
                                $"[RoomEffectsApplier] LightBeam snapIdx={snapIdx} ColorA={colorA:F4} " +
                                $"out of [0,1] — clamping. Check token t[9].");
                            colorA = Mathf.Clamp01(colorA);
                        }
                        if (colorB < 0f || colorB > 1f)
                        {
                            RSPlugin.log.LogWarning(
                                $"[RoomEffectsApplier] LightBeam snapIdx={snapIdx} ColorB={colorB:F4} " +
                                $"out of [0,1] — clamping. Check token t[10].");
                            colorB = Mathf.Clamp01(colorB);
                        }

                        beamData.alpha  = opacity;
                        beamData.colorA = colorA;
                        beamData.colorB = colorB;
                        beam.meshDirty  = true;
                    }
                }
            }
            beamCount++;
        }
    }

    internal static void ApplyShaderGlobals(SettingsSnapshot lerped)
    {
        Shader.SetGlobalFloat(RainWorld.ShadPropGrime, lerped.Grime);

        // Los globals de fondo se aplican desde OverrideBackgroundGlobalsIfActive
    }

    // Aplica terrain palette desde un snapshot actualizando roomSettings.
    // Deja que ApplyPalette vanilla construya/actualice cam.terrainPalette normalmente.
    // Para blend: interpola la opacidad del fade por screen en terrainFadePalette.fades.
    //
    // Bug 1 fix: si snap no tiene fade, se limpia terrainFadePalette → ApplyPalette lo descarta.
    // Bug 2 fix: RC no reconstruye cam.terrainPalette — evita conflicto con ApplyPalette vanilla.
    // Bug 3 fix: sin reconstrucción → sin parpadeo en el frame de swap t=0.5.
    internal static void ApplyTerrainPalette(RoomCamera cam, SettingsSnapshot snap)
    {
        if (cam == null || snap == null) return;
        if (!snap._hasTerrainPalette || string.IsNullOrEmpty(snap.TerrainPaletteName)) return;

        var rs = cam.room?.roomSettings;
        if (rs == null) return;

        // Actualizar nombre principal — ApplyPalette lo usa para construir/reconstruir
        rs.TerrainPalette = snap.TerrainPaletteName;

        if (snap._hasTerrainFadePalette && !string.IsNullOrEmpty(snap.TerrainFadePaletteName))
        {
            // Opacidad interpolada para el screen actual
            int   camIdx = cam.currentCameraPosition;
            float fade   = camIdx < snap.TerrainFadeOpacities.Length
                           ? snap.TerrainFadeOpacities[camIdx] : 0f;

            // Reutilizar terrainFadePalette existente si el nombre no cambió
            string currentFadeName = rs.terrainFadePalette?.palette;
            if (rs.terrainFadePalette == null || currentFadeName != snap.TerrainFadePaletteName)
            {
                // Crear con array de fades del tamaño adecuado
                int screens = Mathf.Max(snap.TerrainFadeOpacities.Length, 1);
                rs.terrainFadePalette = new RoomSettings.TerrainFadePalette(
                    snap.TerrainFadePaletteName, screens);
            }

            // Escribir opacidad interpolada en todas las posiciones
            // (ApplyPalette leerá fades[currentCameraPosition])
            for (int i = 0; i < rs.terrainFadePalette.fades.Length; i++)
                rs.terrainFadePalette.fades[i] = camIdx == i ? fade :
                    (i < snap.TerrainFadeOpacities.Length ? snap.TerrainFadeOpacities[i] : 0f);
        }
        else
        {
            // Sin fade en este snapshot → limpiar para que ApplyPalette no aplique fade
            rs.terrainFadePalette = null;
        }

        // ChangeTerrainPaletteFade → ApplyPalette solo reconstruye/actualiza terrainPalette
        // Es más ligero que ApplyPalette completo (evita recalcular spriteLeasers etc.)
        if (cam.room != null)
            cam.ChangeTerrainPaletteFade();
    }

    // Extrae los colores de fondo en orden de prioridad: 1. RC_TINT del snapshot activo (blend o idle) — control explícito del modder 2. Fallback a skyColor/fogColor de currentPalette  RC_TINT se declara en el settings file como: RC_TINT: #RRGGBB #RRGGBB #RRGGBB El primer hex es MultiplyColor (tinte de edificios/sprites Background), el segundo es AtmosphereColor (tinte atmosférico de edificios lejanos). El tercer hex es TintCloudAtmosphere (AboveCloudsView.atmosphereColor), aplicado directamente en OnAboveCloudsViewUpdate, no aquí. Si solo se declara uno o dos, el resto usa el fallback de paleta. Si no se declara ninguno, todos usan el fallback. Durante el blend, los colores se interpolan automáticamente por SettingsSnapshotLerp.
    internal static void CalcBackgroundColors(RoomCamera cam, out Color multiply, out Color atmosphere)
    {
        if (cam == null)
        {
            multiply   = Color.white;
            atmosphere = new Color(0.16078432f, 0.23137255f, 0.31764707f);
            return;
        }

        // Durante el frame de MoveCamera, cam.room ya apunta a la sala nueva pero
        SettingsSnapshot snap = null;
        if (SettingsBlendController.MoveCameraThisFrame && SettingsBlendController.ActiveSnapshot != null)
        {
            snap = SettingsBlendController.ActiveSnapshot;
        }
        else if (cam.room != null && BlendSettingsLoader.Active != null)
        {
            string roomName = cam.room.abstractRoom?.name;
            if (roomName != null && BlendSettingsLoader.Active.IncludesRoom(roomName))
                snap = SettingsBlendController.ActiveSnapshot;
        }
        if (snap != null)
        {
            bool hasMul = snap.TintMultiply.HasValue;
            bool hasAtm = snap.TintAtmosphere.HasValue;

            if (hasMul && hasAtm)
            {
                multiply   = snap.TintMultiply.Value;
                atmosphere = snap.TintAtmosphere.Value;
                return;
            }

            // Prioridad 2: leer sky/fog desde texturas de blend en memoria.
            var blendRoom = SettingsBlendController.ActiveRoom;
            bool camIsInBlendRoom = blendRoom != null && cam.room == blendRoom;

            if (camIsInBlendRoom && BlendTextureManager.Ready && BlendClock.IsRunning)
            {
                float t = SettingsBlendController.ForcedT;
                float darkPal = 0f;
                Color skyFromTex = InterpolatedPalPixel(1, 7, t, darkPal, cam.paletteBlend);
                Color fogFromTex = InterpolatedPalPixel(2, 7, t, darkPal, cam.paletteBlend);

                multiply   = hasMul ? snap.TintMultiply.Value   : skyFromTex;
                atmosphere = hasAtm ? snap.TintAtmosphere.Value : fogFromTex;
                return;
            }

            // Fallback con snapshot pero sin texturas activas (Idle entre halvs)
            multiply   = hasMul ? snap.TintMultiply.Value   : cam.currentPalette.skyColor;
            atmosphere = hasAtm ? snap.TintAtmosphere.Value : cam.currentPalette.fogColor;
            return;
        }

        // Sin snapshot activo: usar currentPalette (sala no gestionada o clock parado)
        multiply   = cam.currentPalette.skyColor;
        atmosphere = cam.currentPalette.fogColor;
    }

    internal static void ApplyScalarEffects(Room room, SettingsSnapshot lerped)
    {
        ApplyEffect(room, RoomSettings.RoomEffect.Type.Darkness,          lerped.EffectDarkness);
        ApplyEffect(room, RoomSettings.RoomEffect.Type.Brightness,        lerped.EffectBrightness);
        ApplyEffect(room, RoomSettings.RoomEffect.Type.Contrast,          lerped.EffectContrast);
        ApplyEffect(room, RoomSettings.RoomEffect.Type.Desaturation,      lerped.EffectDesaturation);
        ApplyEffect(room, RoomSettings.RoomEffect.Type.Hue,               lerped.EffectHue);
        ApplyEffect(room, RoomSettings.RoomEffect.Type.DarkenLights,      lerped.EffectDarkenLights);
        ApplyEffect(room, RoomSettings.RoomEffect.Type.Fog,               lerped.EffectFog);
        ApplyEffect(room, RoomSettings.RoomEffect.Type.SkyBloom,          lerped.EffectSkyBloom);
        ApplyEffect(room, RoomSettings.RoomEffect.Type.SkyAndLightBloom,  lerped.EffectSkyAndLightBloom);
        ApplyEffect(room, RoomSettings.RoomEffect.Type.LightBurn,         lerped.EffectLightBurn);
        ApplyEffect(room, RoomSettings.RoomEffect.Type.Bloom,             lerped.EffectBloom);
        ApplyEffect(room, new RoomSettings.RoomEffect.Type("SurfaceSandstorm"), lerped.EffectSurfaceSandstorm);
    }

    // Prioridad de efectos mutuamente exclusivos (mayor índice = mayor prioridad)
    // Si hay un efecto de mayor prioridad activo con amount > 0, no crear uno de menor prioridad.
    private static readonly RoomSettings.RoomEffect.Type[] _exclusivePriority = new[]
    {
        RoomSettings.RoomEffect.Type.Bloom,
        RoomSettings.RoomEffect.Type.VoidMelt,   // no lerpeado por RC, pero incluido por completitud
        RoomSettings.RoomEffect.Type.Fog,
        RoomSettings.RoomEffect.Type.LightBurn,
        RoomSettings.RoomEffect.Type.SkyAndLightBloom,
        RoomSettings.RoomEffect.Type.SkyBloom,
    };

    private static bool IsOverriddenByHigherPriority(RoomSettings rs, RoomSettings.RoomEffect.Type type)
    {
        int myPriority = System.Array.IndexOf(_exclusivePriority, type);
        if (myPriority < 0) return false; // no exclusivo
        for (int i = myPriority + 1; i < _exclusivePriority.Length; i++)
        {
            var higher = rs.GetEffect(_exclusivePriority[i]);
            if (higher != null && higher.amount > 0f) return true;
        }
        return false;
    }

    private static void ApplyEffect(Room room, RoomSettings.RoomEffect.Type type, float amount)
    {
        if (amount <= 0f)
        {
            var existing = room.roomSettings.GetEffect(type);
            if (existing != null) existing.amount = 0f;
            return;
        }
        // No crear efecto de menor prioridad si hay uno de mayor prioridad activo
        if (IsOverriddenByHigherPriority(room.roomSettings, type)) return;
        var effect = room.roomSettings.GetEffect(type);
        if (effect != null)
            effect.amount = amount;
        else
            room.roomSettings.effects.Add(new RoomSettings.RoomEffect(type, amount, false));
    }

    // Aplica terrain scalars — campos directos de RoomSettings, no RoomEffect.
    // Solo escribe si el valor fue declarado (>= 0) en al menos uno de los snaps.
    // Llamar desde MixAndApply igual que ApplyScalarEffects.
    internal static void ApplyTerrainScalars(Room room, SettingsSnapshot lerped)
    {
        if (room?.roomSettings == null) return;
        var rs = room.roomSettings;

        if (lerped.TerrainWaves.HasValue)          rs.TerrainWaves          = lerped.TerrainWaves.Value;
        if (lerped.TerrainLight.HasValue)          rs.TerrainLight          = lerped.TerrainLight.Value;
        if (lerped.TerrainGrain.HasValue)          rs.TerrainGrain          = lerped.TerrainGrain.Value;
        if (lerped.TerrainDepth.HasValue)          rs.TerrainDepth          = lerped.TerrainDepth.Value;
        if (lerped.TerrainSkyFade.HasValue)        rs.TerrainSkyFade        = lerped.TerrainSkyFade.Value;
        if (lerped.TerrainEdgeRadius.HasValue)     rs.TerrainEdgeRadius     = lerped.TerrainEdgeRadius.Value;
        if (lerped.TerrainGooHeight.HasValue)      rs.TerrainGooHeight      = lerped.TerrainGooHeight.Value;
        if (lerped.TerrainStainAmount.HasValue)    rs.TerrainStainAmount    = lerped.TerrainStainAmount.Value;
        if (lerped.TerrainStainBrightness.HasValue) rs.TerrainStainBrightness = lerped.TerrainStainBrightness.Value;
        if (lerped.TerrainStainHeight.HasValue)    rs.TerrainStainHeight    = lerped.TerrainStainHeight.Value;
    }
}