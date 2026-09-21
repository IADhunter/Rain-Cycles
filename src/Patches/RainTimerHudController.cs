using System;
using DevInterface;
using HUD;
using UnityEngine;
using MoreSlugcats;

namespace RainCycles.Patches;

public static class RainTimerHudController
{
    private static bool _initialized = false;
    
    public static readonly RoomSettings.RoomEffect.Type RainTimerHudEffect = 
        new RoomSettings.RoomEffect.Type("RainTimerHud", true);

    public static void Init()
    {
        if (_initialized) return;

        On.HUD.RainMeter.Update += OnRainMeterUpdate;
        On.DevInterface.RoomSettingsPage.DevEffectGetCategoryFromEffectType += OnDevEffectGetCategory;

        _initialized = true;
    }

    private static DevInterface.RoomSettingsPage.DevEffectsCategories OnDevEffectGetCategory(
        On.DevInterface.RoomSettingsPage.orig_DevEffectGetCategoryFromEffectType orig,
        RoomSettingsPage self,
        RoomSettings.RoomEffect.Type type)
    {
        if (type == RainTimerHudEffect)
            return DevInterface.RoomSettingsPage.DevEffectsCategories.Gameplay;
        
        return orig(self, type);
    }

    private static void OnRainMeterUpdate(On.HUD.RainMeter.orig_Update orig, HUD.RainMeter self)
    {
        float effectAmount = 0f;
        if (self.hud?.owner is Player player && player.room != null)
        {
            effectAmount = player.room.roomSettings.GetEffectAmount(RainTimerHudEffect);
        }

        bool originalMMF = false;
        bool hasMMF = ModManager.MMF;
        if (hasMMF)
        {
            originalMMF = MMF.cfgHideRainMeterNoThreat.Value;
        }

        if (effectAmount >= 0.5f)
        {
            if (hasMMF)
                MMF.cfgHideRainMeterNoThreat.Value = false;
        }
        else if (effectAmount > 0f && effectAmount < 0.5f)
        {
            if (hasMMF)
                MMF.cfgHideRainMeterNoThreat.Value = true;
        }

        orig(self);

        if (hasMMF)
        {
            MMF.cfgHideRainMeterNoThreat.Value = originalMMF;
        }
    }
}