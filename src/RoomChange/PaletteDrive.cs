using System;
using System.Linq;
using UnityEngine;


namespace RoomChange;

public partial class PaletteDrive
{
    private static RoomChange.PaletteData activeRegionPalette;
    private static float actualTime;

    public static bool activateEffectFade = false;

    public static void Terminate()
    {
        On.RoomCamera.UpdateDayNightPalette -= UpdateRainStatePaletteRoom;
    }
    
    public static void Init()
    {
        On.RoomCamera.UpdateDayNightPalette += UpdateRainStatePaletteRoom;
    }

    private static void UpdateRainStatePaletteRoom(On.RoomCamera.orig_UpdateDayNightPalette orig, RoomCamera self)
    {
        // Si el BlendController está activo, cedemos el control completamente.
        // Llamar a orig aquí resetearía paletteBlend y destruiría el blend del slider.
        if (FilesSetting.SettingsBlendController.IsActive)
        {
            return;
        }

        if (self == null || self.room == null || self.game.GetStorySession == null)
        {
            orig(self);
            return;
        }

        string roomKey = "";
        if (!PaletteInfo.IsRegionPaletteAvailable(self.room, ref roomKey))
        {
            orig(self);
            return;
        }

        PaletteInfo.SetRainCycleLength(self.room.world.rainCycle.cycleLength);

        activeRegionPalette = PaletteInfo.GetCyclePalette(roomKey, self.game.GetStorySession.saveState.cycleNumber);
        actualTime = self.room.world.rainCycle.timer;
        activateEffectFade = false;

        RoomCamera camera = self.room.game.cameras[0];

        if (activeRegionPalette.BaseLength > 0)
        {
            ApplyBasePalette(camera, activeRegionPalette, actualTime);
        }

        if (activeRegionPalette.EffectALength > 0)
        {
            ApplyEffectAPalette(camera, activeRegionPalette, actualTime);
        }

        if (activeRegionPalette.EffectBLength > 0)
        {
            ApplyEffectBPalette(camera, activeRegionPalette, actualTime);
        }

        if (activateEffectFade)
        {
            camera.ApplyFade();
        }

    }

    private static void ApplyBasePalette(RoomCamera camera, PaletteData data, float currentTime)
    {
        var sequence = PaletteInfo.GetBasePaletteSequence(data);
        var interval = PaletteInfo.CalculateIntervals(currentTime, sequence);

        if (interval.IsLastPalette)
        {
            camera.ChangeMainPalette(sequence.Palettes[interval.CurrentIndex]);
            return;
        }

        PaintRoom.ChangeBothPalettes(
            camera, 
            sequence.Palettes[interval.PrevIndex], 
            sequence.Palettes[interval.NextIndex], 
            interval.BlendFactor
        );
    }

    private static void ApplyEffectAPalette(RoomCamera camera, PaletteData data, float currentTime)
    {
        var sequence = PaletteInfo.GetEffectAPaletteSequence(data);
        if (!sequence.IsValid()) return;

        var interval = PaletteInfo.CalculateIntervals(currentTime, sequence);
        PaintRoom.ChangeEffectAPalette(camera, sequence.Palettes[interval.PrevIndex], sequence.Palettes[interval.NextIndex], interval.BlendFactor);
    }

    private static void ApplyEffectBPalette(RoomCamera camera, PaletteData data, float currentTime)
    {
        var sequence = PaletteInfo.GetEffectBPaletteSequence(data);
        if (!sequence.IsValid()) return;

        var interval = PaletteInfo.CalculateIntervals(currentTime, sequence);
        PaintRoom.ChangeEffectBPalette(
            camera, 
            sequence.Palettes[interval.PrevIndex], 
            sequence.Palettes[interval.NextIndex], 
            interval.BlendFactor
        );
    }

    private static void ApplyTerrainPalette(RoomCamera camera, PaletteData data, float currentTime)
    {
        var sequence = PaletteInfo.GetTerrainPaletteSequence(data);
        if (!sequence.IsValid()) return;

        var interval = PaletteInfo.CalculateIntervals(currentTime, sequence);
    }
}

public static class PaintRoom
{
    public static Color HexToColor(string hex)
    {
        if (hex.StartsWith("#"))
        {
            hex = hex.Substring(1);
        }

        if (hex.Length == 3)
        {
            hex = string.Format("{0}{0}{1}{1}{2}{2}", hex[0], hex[1], hex[2]);
        }

        if (hex.Length == 6)
        {
            try
            {
                int r = Convert.ToInt32(hex.Substring(0, 2), 16);
                int g = Convert.ToInt32(hex.Substring(2, 2), 16);
                int b = Convert.ToInt32(hex.Substring(4, 2), 16);
                return new Color(r / 255f, g / 255f, b / 255f, 1f);
            }
            catch (Exception e)
            {
                Plugin.RSPlugin.log.LogWarning($"[PaintRoom] Failed to parse hex color '{hex}': {e.Message}");
                return Color.white;
            }
        }

        Plugin.RSPlugin.log.LogWarning($"[PaintRoom] Invalid hex color format '{hex}'.");
        return Color.white;
    }

    public static void ChangeBothPalettes(RoomCamera camera, int prevPalette, int nextPalette, float blend)
    {
        camera.ChangeBothPalettes(prevPalette, nextPalette, blend);
    }

    public static void ChangeEffectAPalette(RoomCamera camera, string prevColorHex, string nextColorHex, float blendFactor)
    {
        Texture2D textureA = camera.fadeTexA;
        Texture2D textureB = camera.fadeTexB;

        Color prevColor = HexToColor(prevColorHex);
        Color nextColor = HexToColor(nextColorHex);
        Color blendedColor = Color.Lerp(prevColor, nextColor, blendFactor);

        Color[] effectColors = new Color[4]
        {
            blendedColor, blendedColor,
            blendedColor, blendedColor
        };

        textureA.SetPixels(30, 4, 2, 2, effectColors, 0);
        textureA.SetPixels(30, 12, 2, 2, effectColors, 0);
        textureB.SetPixels(30, 4, 2, 2, effectColors, 0);
        textureB.SetPixels(30, 12, 2, 2, effectColors, 0);
        PaletteDrive.activateEffectFade = true;
    }

    public static void ChangeEffectBPalette(RoomCamera camera, string prevColorHex, string nextColorHex, float blendFactor)
    {
        Texture2D textureA = camera.fadeTexA;
        Texture2D textureB = camera.fadeTexB;
        
        Color prevColor = HexToColor(prevColorHex);
        Color nextColor = HexToColor(nextColorHex);
        Color blendedColor = Color.Lerp(prevColor, nextColor, blendFactor);

        Color[] effectColors = new Color[4]
        {
            blendedColor, blendedColor,
            blendedColor, blendedColor
        };
        
        textureA.SetPixels(30, 2, 2, 2, effectColors, 0);
        textureA.SetPixels(30, 10, 2, 2, effectColors, 0);
        textureB.SetPixels(30, 2, 2, 2, effectColors, 0);
        textureB.SetPixels(30, 10, 2, 2, effectColors, 0);

        PaletteDrive.activateEffectFade = true;
    }

    public static void ChangeMainPalette(RoomCamera camera, int paletteIndex)
    {
        camera.ChangeMainPalette(paletteIndex);
    }
}