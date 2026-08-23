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
    public const string VER  = "1.6.0";

    public static ManualLogSource log;

    public static Configurable<string> cycleMode => _options?.cycleMode;
    public static Configurable<string> customSeed => _options?.customSeed;
    public static Configurable<bool> proceduralNoCycle => _options?.proceduralNoCycle;
    public static Configurable<string> saveModId => _options?.saveModId;
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
            MachineConnector.SetRegisteredOI(ID, _options);

            CreateTransparentPlaceholder();

            ModResetter.Init();
            RCDEVTools.Init();
            StateFileResolver.Init();
            SettingsBlendController.Init();
            BlendSettingsLoader.Init();
            BlendClockUpdater.Init();
            ArenaBlendController.Init();
            RoomSettingsPatches.Init();
            RainTimerHudController.Init();
            SnowLightController.Init();
            RoomCameraExtensions.InitPreloadHooks();
            TintManager.Init();
            RoomCameraExtensions.InitLights();
            DayNightBlocker.Init();
            PlateTreeRotPatch.Init();
            RainCyclesEventDispatcher.Init();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);
        }
    }

    // ============================================================
    // CREAR TEXTURA TRANSPARENTE PARA SLOTS
    // ============================================================
    private static void CreateTransparentPlaceholder()
    {
        try
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, new Color(0f, 0f, 0f, 0f));
            tex.Apply();
            Futile.atlasManager.LoadAtlasFromTexture("RC_Transparent", tex, false);
        }
        catch (Exception ex)
        {
            log.LogWarning($"[RC] Error creando textura transparente: {ex.Message}");
        }
    }
}