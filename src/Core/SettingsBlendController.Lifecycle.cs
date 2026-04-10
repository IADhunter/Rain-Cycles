using UnityEngine;
using RainCycles.Settings;
using RainCycles.Snapshot;
using RainCycles.Sky;
using RainCycles.Clock;

namespace RainCycles.Core;

public static partial class SettingsBlendController
{
    public static void Attach(Room room, string pathA, string pathB)
    {
        _room         = room;
        _pathA        = pathA;
        _pathB        = pathB;
        _snapOriginal = SettingsSnapshot.FromFile(room.roomSettings.filePath ?? "");
        _snapA        = SettingsSnapshot.FromFileWithTemplate(pathA, room.abstractRoom.name);
        _snapB        = SettingsSnapshot.FromFileWithTemplate(pathB, room.abstractRoom.name);
        _active       = true;
        _lastT        = -1f;
        _lastPaletteT = -1f;

        var cam = room.game?.cameras?[0];
        if (cam != null)
        {
            BlendTextureManager.Load(cam, _snapA, _snapB, _snapOriginal, applyFade: false);
            // Load restaura paleta original → currentPalette no refleja snapA
            // MixPalettes(0) fuerza t=0 antes de ApplyBlend → evita flash blancuzco
            BlendTextureManager.MixPalettes(cam, 0f);
            cam.ApplyFade();
        }
        RoomEffectsApplier.BuildLightIndex(room);
        ApplyBlend(0f);
    }

    public static void Detach()
    {
        // paletteB = -1 → fuerza recarga en próximo ChangeBothPalettes
        // MixPalettes sobreescribe fadeTexB sin actualizar cam.paletteB
        // → ID puede coincidir accidentalmente → fadeTexB contaminado
        if (_room != null)
        {
            var cam = _room.game?.cameras?[0];
            if (cam != null) cam.paletteB = -1;
        }

        // capturar alphas desde _forcedT → más fiable que daySky.alpha
        // vanilla puede haber reseteado alpha en el orig() anterior
        if (_active && _externalT)
        {
            _savedDayAlpha   = 1f - _forcedT;
            _savedDuskAlpha  = 1f;
            _savedNightAlpha = 0f;
            _hasSavedAlphas  = true;
        }
        else if (BlendClock.IsRunning && BlendClock.CurrentPhase == BlendClock.Phase.Blending
                 && (_room != null ? ActiveSlotDay(_room.game?.cameras?[0]) : -1) == BlendClock.StateA)
        {
            // race: _externalT limpiado antes del tick → usar SubPhaseLocalT
            _savedDayAlpha   = 1f - BlendClock.SubPhaseLocalT;
            _savedDuskAlpha  = 1f;
            _savedNightAlpha = 0f;
            _hasSavedAlphas  = true;
        }
        else
        {
            _hasSavedAlphas = false;
        }

        _active            = false;
        _externalT         = false;
        _detachedThisFrame = true;
        _room              = null;
        _pathA             = null;
        _pathB             = null;
        _snapOriginal      = null;
        _lastLightT        = -1f;
        _pendingIdleRoom   = null;
        _pendingIdlePath   = null;
        _aboveCloudsView   = null;
        // _activeSnapshot preservado → CalcBackgroundColors lo usa en Idle entre halvs
        // _rtvScene/_acvScene/_skySlot* preservados → sobreviven entre blends
        // → se limpian en ResetFull() al cambiar sala/región
        RoomEffectsApplier.ClearLightIndex();
        BlendTextureManager.Destroy();
    }

    // Detach + restaura roomSettings al snapshot original
    public static void DetachAndRestore()
    {
        if (_room != null)
        {
            var cam = _room.game?.cameras?[0];
            if (cam != null)
            {
                var orig = _snapOriginal;
                var rs   = _room.roomSettings;
                if (orig != null && rs != null)
                {
                    rs.Grime                 = orig.Grime;
                    rs.Clouds                = orig.Clouds;
                    rs.CeilingDrips          = orig.CeilingDrips;
                    rs.BkgDroneVolume        = orig.BkgDroneVolume;
                    rs.RandomItemDensity     = orig.RandomItemDensity;
                    rs.RandomItemSpearChance = orig.RandomItemSpearChance;
                    rs.WaterReflectionAlpha  = orig.WaterReflectionAlpha;
                    Shader.SetGlobalFloat(RainWorld.ShadPropGrime, orig.Grime);
                    RoomEffectsApplier.ApplyScalarEffects(_room, orig);
                }
                BlendTextureManager.RestoreOriginalTextures(cam);
                cam.paletteBlend = 0f;
                // globals reseteados en OnMoveCamera post-orig → último frame usa valores correctos
            }
        }
        Detach();
    }

    // DetachAndRestore + limpia toda la escena de sky
    public static void ResetFull()
    {
        _pendingOrigin = null;
        if (_active) DetachAndRestore();
        _rtvScene = null; _acvScene = null;
        _rtvSlotDay = _rtvSlotDusk = _rtvSlotNight = -1;
        _acvSlotDay = _acvSlotDusk = _acvSlotNight = -1;
        _lastIdleSkyState    = -1;
        _lastAtmosphereColor = new Color(0.16078432f, 0.23137255f, 0.31764707f);
        _hasLastGoodGlobals  = false;
        _hasSavedAlphas      = false;
        _entryFrameT         = -1f;
        _pendingSkySync      = false;
        _pendingSkyStateA    = -1;
        _pendingSkyStateB    = -1;
    }

    // aplica estado visual sin blend → clock en Idle
    public static void ApplyIdleState(Room room, string path, bool allowCameraOps = false)
    {
        if (room == null || path == null) return;
        var cam = room.game?.cameras?[0];
        if (cam == null) return;

        bool camIsHere = cam.room == room;

        var snap = SettingsSnapshot.FromFileWithTemplate(path, room.abstractRoom.name);
        if (snap == null) return;
        _activeSnapshot = snap;

        // _lastAtmosphereColor → ACV.Update fallback cuando slots desincronizados
        if (snap.TintCloudAtmosphere.HasValue)
            _lastAtmosphereColor = snap.TintCloudAtmosphere.Value;

        // actualizar _lastGood* sin depender de camIsHere
        // → OnUpdateDayNightPalette Caso 0 tiene valor correcto antes de que llegue la cámara
        if (snap.TintAtmosphere.HasValue)
        {
            _lastGoodAtmosphere = snap.TintAtmosphere.Value;
            _hasLastGoodGlobals = true;
            if (snap.TintMultiply.HasValue)
                _lastGoodMultiply = snap.TintMultiply.Value;
        }

        var rs = room.roomSettings;
        if (rs != null)
        {
            rs.EffectColorA = snap.EffectColorA;
            rs.EffectColorB = snap.EffectColorB;
        }
        RoomEffectsApplier.ApplyShaderGlobals(snap);
        RoomEffectsApplier.ApplyScalarEffects(room, snap);
        RoomEffectsApplier.BuildLightIndex(room);
        RoomEffectsApplier.ApplyLightSources(room, snap);
        RoomEffectsApplier.ApplyLightBeams(room, snap);

        int idleState = StateFileResolver.GetStateFromPath(path, room.abstractRoom?.name);
        if (idleState > 0 && idleState != _lastIdleSkyState)
        {
            _lastIdleSkyState = idleState;
            ApplySkyForState(idleState, room);
        }

        // operaciones de cámara solo si cam ya asentada en esta sala
        // updater background → allowCameraOps=false
        // OnMoveCamera con self.room==newRoom → allowCameraOps=true
        if (!allowCameraOps || !camIsHere) return;

        cam.ChangeMainPalette(snap.Palette);
        cam.ApplyEffectColorsToAllPaletteTextures(snap.EffectColorA, snap.EffectColorB);

        if (snap._hasFadePalette && snap.FadePaletteOpacities.Length > 0)
        {
            int camIdx = cam.currentCameraPosition;
            float opac = camIdx < snap.FadePaletteOpacities.Length
                         ? snap.FadePaletteOpacities[camIdx] : 0f;
            cam.ChangeFadePalette(snap.FadePaletteID, opac);
            cam.ApplyEffectColorsToAllPaletteTextures(snap.EffectColorA, snap.EffectColorB);
        }
        cam.ApplyFade();

        // globals de fondo solo en salas con sky → ShadPropMultiplyColor no tiñe salas normales
        if (cam.room != null && BlendSettingsLoader.Active != null)
        {
            string camRoomName = cam.room.abstractRoom?.name;
            if (camRoomName != null && BlendSettingsLoader.Active.IncludesRoom(camRoomName))
            {
                var skyType = BlendSettingsLoader.Active.GetSkyType(camRoomName);
                if (skyType != SkyType.None)
                {
                    RoomEffectsApplier.CalcBackgroundColors(cam, out Color multiply, out Color atmosphere);
                    Shader.SetGlobalVector(RainWorld.ShadPropMultiplyColor, multiply);
                    Shader.SetGlobalVector(RainWorld.ShadPropAboveCloudsAtmosphereColor, atmosphere);
                    _lastGoodMultiply   = multiply;
                    _lastGoodAtmosphere = atmosphere;
                    _hasLastGoodGlobals = true;
                }
            }
        }
    }
}
