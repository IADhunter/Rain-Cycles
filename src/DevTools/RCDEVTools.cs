using UnityEngine;
using DevInterface;

namespace FilesSetting;

public class RCDEVTools
{
    public static void Init()
    {
        On.DevInterface.Page.ctor += DevInterface_Page_ctor;
    }

    public static void DevInterface_Page_ctor(On.DevInterface.Page.orig_ctor orig, Page self, DevUI owner, string IDstring, DevUINode parentNode, string name)
    {
        orig(self, owner, IDstring, parentNode, name);

        if (owner != null && owner.room != null)
        {
            string roomName = owner.room.abstractRoom?.name;
            if (!string.IsNullOrEmpty(roomName))
            {
                BlendSettingsWriter.EnsureFileExists(roomName);
            }

            self.subNodes.Add(new RCPanel(owner, "RC_Panel", self, new Vector2(790, 460f), new Vector2(215f, 215f), "Rain Cycles"));
        }
    }
}