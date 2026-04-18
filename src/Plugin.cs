using System;
using System.Security;
using System.Security.Permissions;
using BepInEx;
using BepInEx.Logging;
using RainCycles.Core;
using RainCycles.Settings;
using RainCycles.Clock;
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
    public const string VER  = "1.0";

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

            RCDEVTools.Init();
            StateFileResolver.Init();
            CustomModeState.Init();
            SettingsBlendController.Init();
            BlendSettingsLoader.Init();
            BlendClockUpdater.Init();
            ArenaBlendController.Init();

            Logger.LogInfo($"[{NAME}] {VER} loaded successfully!");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);
        }
    }
}