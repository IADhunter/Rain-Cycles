using System;
using DevInterface;
using HUD;
using UnityEngine;
using MoreSlugcats;

namespace RainCycles.Patches;

public static class RainTimerHudController
{
    private static bool _initialized = false;
    
    // Registrar el nuevo efecto
    public static readonly RoomSettings.RoomEffect.Type RainTimerHudEffect = 
        new RoomSettings.RoomEffect.Type("RainTimerHud", true);

    public static void Init()
    {
        if (_initialized) return;

        On.HUD.RainMeter.Update += OnRainMeterUpdate;
        
        // Hook para que el efecto aparezca en la categoría Gameplay
        On.DevInterface.RoomSettingsPage.DevEffectGetCategoryFromEffectType += OnDevEffectGetCategory;
        
        _initialized = true;
        RSPlugin.log.LogInfo("[RainTimerHudController] Inicializado con efecto RainTimerHud");
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
        // Verificar efecto ANTES de llamar a orig
        float effectAmount = 0f;
        if (self.hud?.owner is Player player && player.room != null)
        {
            effectAmount = player.room.roomSettings.GetEffectAmount(RainTimerHudEffect);
        }
        
        // Guardar estado original de MMF.cfgHideRainMeterNoThreat si existe
        bool originalMMF = false;
        bool hasMMF = ModManager.MMF;
        if (hasMMF)
        {
            originalMMF = MMF.cfgHideRainMeterNoThreat.Value;
        }
        
        // Forzar según el efecto
        if (effectAmount >= 0.5f)
        {
            // ON: forzar modo normal (flag = false) - descongelado
            if (hasMMF)
                MMF.cfgHideRainMeterNoThreat.Value = false;
        }
        else if (effectAmount > 0f && effectAmount < 0.5f)
        {
            // OFF: forzar modo simplificado (flag = true) - congelado
            if (hasMMF)
                MMF.cfgHideRainMeterNoThreat.Value = true;
        }
        // Si effectAmount == 0, no hacer nada (comportamiento vanilla)
        
        // Llamar al update original
        orig(self);
        
        // Restaurar estado original de MMF
        if (hasMMF)
        {
            MMF.cfgHideRainMeterNoThreat.Value = originalMMF;
        }
    }
}