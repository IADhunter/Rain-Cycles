using System;
using System.Security;
using System.Security.Permissions;
using BepInEx;
using BepInEx.Logging;


#pragma warning disable CS0618

[module: UnverifiableCode]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]

namespace Plugin;

[BepInPlugin(ID, NAME, VER)]
public class RSPlugin : BaseUnityPlugin 
{
    public const string ID = "skeq.raincycles";
    public const string NAME = "Rain Cycles";
    public const string VER = "0.4";

    public static ManualLogSource log;

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
<<<<<<< Updated upstream
            FilesSetting.RainStateFiles.Init();
            FilesSetting.CustomModeState.Init();
            DevTools.Init();
            FilesSetting.SettingsBlendController.Init();
            FilesSetting.BlendSettingsLoader.Init();
            FilesSetting.BlendClockUpdater.Init();
=======

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

>>>>>>> Stashed changes
            Logger.LogInfo($"[{NAME}] {VER} loaded successfully!");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);
        }
    }
}