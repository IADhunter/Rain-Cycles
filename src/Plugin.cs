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
            FilesSetting.RainStateFiles.Init();
            DevTools.Init();
            FilesSetting.SettingsBlendController.Init();
            FilesSetting.BlendSettingsLoader.Init();
            FilesSetting.BlendClockUpdater.Init();
            Logger.LogInfo($"[{NAME}] {VER} loaded successfully!");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);
        }
    }
}