using BepInEx;
using BepInEx.Logging;
using UnityEngine;

namespace Plugin;

public partial class DevTools
{
    private void Terminate()
    {

    }
    public static void Init()
    {
        //On.PlayerGraphics.DrawSprites += PlayerGraphics_DrawSprites;
        //On.Player.Update += Player_Update;
    }
    public static bool notify = true;
    public static int devTimer = 0;
    public static int pal = 0;
    public static int fpal = 0;
    public static float val = 0;

    public static int colorIndex = 0;

    private static void Player_Update(On.Player.orig_Update orig, Player self, bool eu)
    {
        orig(self, eu);
        if (notify)
        {
            PDEBUG.Log("Init dev tools palette");
            notify = false;
        }

        devTimer++;
        if (devTimer < 0)
        {
            return;
        }

    }

    private static void PlayerGraphics_DrawSprites(On.PlayerGraphics.orig_DrawSprites orig, PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
    {
        orig(self, sLeaser, rCam, timeStacker, camPos);
        sLeaser.sprites[2].color = UnityEngine.Color.blue;
    }
    //Regresive count in seconds
    private static void SetDevTimer(int seconds)
    {
        devTimer = -seconds * 40;
    }

    public static void Log(string message)
    {
        UnityEngine.Debug.Log($"[{RSPlugin.NAME}] {message}");
    }

    public static void LogWarn(string message)
    {
        UnityEngine.Debug.Log($"[Warn {RSPlugin.NAME}] {message}");
    }

    public static void LogErr(string message)
    {
        UnityEngine.Debug.Log($"[ERROR {RSPlugin.NAME}] {message}");
    }
}
