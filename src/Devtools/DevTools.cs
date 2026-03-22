using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using DevInterface;
using Plugin;
using UnityEngine;
using System.Linq;

namespace FilesSetting;

public class RCDEVTools
{
    public static void Terminate()
    {
        On.DevInterface.Page.ctor -= DevInterface_Page_ctor;
    } 
    public static void Init()
    {
        On.DevInterface.Page.ctor += DevInterface_Page_ctor;
    }

    public static void DevInterface_Page_ctor(On.DevInterface.Page.orig_ctor orig, Page self, DevUI owner, string IDstring, DevUINode parentNode, string name)
    {
        orig(self, owner, IDstring, parentNode, name);

        if (owner != null)
        {
            // Crear blend_settings.txt para esta región si no existe todavía.
            // Lo hacemos aquí para que el archivo esté disponible antes de que
            // RCPanel intente leerlo.
            string roomName = owner.room?.abstractRoom?.name;
            if (!string.IsNullOrEmpty(roomName))
            {
                BlendSettingsWriter.EnsureFileExists(roomName);
                // Actualizar [SEQUENCES] si hay nuevos settings_N.txt desde la última apertura
                BlendSettingsWriter.UpdateSequences(roomName);
            }

            self.subNodes.Add(new RCPanel(owner, "RC_Panel", self, new Vector2(790, 580f), new Vector2(220f, 175f), "Rain Cycles"));
        }
    }
}