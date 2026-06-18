using System;
using System.Security;
using System.Security.Permissions;
using BepInEx;
using BepInEx.Logging;
using RainCycles.Core;
using RainCycles.Settings;
using RainCycles.Clock;
using RainCycles.Patches;
using RainCycles.Blend;
using FilesSetting;

#pragma warning disable CS0618

[module: UnverifiableCode]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]

namespace RainCycles;

[BepInPlugin(ID, NAME, VER)]
public class RSPlugin : BaseUnityPlugin
{
    public const string ID   = "raincycles";
    public const string NAME = "Rain Cycles";
    public const string VER  = "1.5";

    public static ManualLogSource log;

    public static Configurable<bool> randomCycles => _options?.randomCycles;
    private static RCOptions _options;

    private void OnEnable()
    {
        log = base.Logger;
        On.RainWorld.OnModsInit += RainWorldOnOnModsInit;
    }

    private bool IsInit;
    private void RainWorldOnOnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
    {
        orig(self);
        if (IsInit) return;

        try
        {
            IsInit = true;

            _options = new RCOptions();
            bool ok = MachineConnector.SetRegisteredOI(ID, _options);
            Logger.LogDebug($"[RC] SetRegisteredOI result: {ok}");

            // ════════════════════════════════════════════════════════════════
            // CREAR TEXTURA TRANSPARENTE PARA PLACEHOLDER DE SLOTS
            // ════════════════════════════════════════════════════════════════
            CreateTransparentPlaceholder();

            // NUEVO: Sistema centralizado de limpieza
            ModResetter.Init();
            
            RCDEVTools.Init();
            StateFileResolver.Init();
            SettingsBlendController.Init();
            StaticTintManager.Init();
            BlendSettingsLoader.Init();
            BlendClockUpdater.Init();
            ArenaBlendController.Init();
            RoomSettingsPatches.Init();
            RainTimerHudController.Init();
            RoomCameraExtensions.InitPreloadHooks();
            TintManager.Init();
            
            Logger.LogInfo($"[{NAME}] {VER} loaded successfully!");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // CREAR TEXTURA TRANSPARENTE DE 1x1 PARA USAR COMO PLACEHOLDER
    // EN SLOTS DE BACKGROUND ANTES DE QUE SE ASIGNE LA IMAGEN REAL
    // ════════════════════════════════════════════════════════════════════
    private static void CreateTransparentPlaceholder()
    {
        try
        {
            // Crear textura de 1x1 completamente transparente
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, new Color(0f, 0f, 0f, 0f));
            tex.Apply();
            
            // Registrar en Futile como atlas
            Futile.atlasManager.LoadAtlasFromTexture("RC_Transparent", tex, false);
            
            log.LogInfo("[RC] Textura transparente RC_Transparent creada para placeholder de slots");
        }
        catch (Exception ex)
        {
            log.LogWarning($"[RC] Error creando textura transparente: {ex.Message}");
        }
    }
}