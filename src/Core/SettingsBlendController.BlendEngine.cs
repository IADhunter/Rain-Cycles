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
        if (_room != null && IsStaticViewRoom(_room))
        {
            return;
        }

        if (_snapA == null || _snapB == null || _room == null)
            return;

        var cam = _room.game?.cameras?[0];
        if (cam == null)
            return;

        var blendData = cam.GetBlendData();
        if (blendData == null || !blendData.isBlendActive)
            cam.SetBlendActive(_room.abstractRoom.name);
        
        cam.UpdateBlendPalette();

        Color vanillaMultiply = Color.white;
        Color vanillaAtmosphere = Color.white;
        if (_room != null)
        {
            TintManager.TryGetOriginalColors(_room, out vanillaMultiply, out vanillaAtmosphere);
        }

        var lerped = TintManager.InterpolateTints(_snapA, _snapB, t, vanillaMultiply, vanillaAtmosphere);
        _activeSnapshot = lerped;

        _room.ApplyTerrainScalars(_snapA, _snapB, t);

        if (lerped.TintMultiply.HasValue)
        {
            var c = lerped.TintMultiply.Value;
            Shader.SetGlobalVector(RainWorld.ShadPropMultiplyColor, new Vector4(c.r, c.g, c.b, 1f));
        }
        if (lerped.TintAtmosphere.HasValue)
        {
            var c = lerped.TintAtmosphere.Value;
            Shader.SetGlobalVector(RainWorld.ShadPropAboveCloudsAtmosphereColor, new Vector4(c.r, c.g, c.b, 1f));
            
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

        _room.ApplyRoomScalars(_snapA, _snapB, t);

        if (IsBlendRoom(_room))
        {
            var lerpedDecals = RoomCameraExtensions.LerpDecals(_snapA, _snapB, t);
            _room.ApplyDecalOpacities(lerpedDecals);
        }

        _room.ApplyScalarEffects(_snapA, _snapB, t);

        if (_room != null)
        {
            var skyType = GetViewFromLoadedSettings(_room);
            if (skyType != SkyType.None)
                ApplyRcSlotsAlpha(skyType, t, isBlending: true);
        }
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

        var blendData = self.GetBlendData();
        if (blendData != null && blendData.isBlendActive &&
            self.room != null && IsBlendRoom(self.room) &&
            blendData.terrainBlendedTexture != null)
        {
            Shader.SetGlobalTexture("_terrainPalette", blendData.terrainBlendedTexture);
        }
    }

    public static void OnApplyFade(On.RoomCamera.orig_ApplyFade orig, RoomCamera self)
    {
        orig(self);
    }
}