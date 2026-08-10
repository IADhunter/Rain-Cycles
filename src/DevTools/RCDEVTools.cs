using System;
using UnityEngine;
using DevInterface;
using MonoMod.RuntimeDetour;

namespace FilesSetting;

// ================================================================
// RC INPUT GUARD
// Bloquea el input del juego mientras un RCStringControl tiene foco,
// copia exacta del guard de POM/RegionKit (Pom.InputHooks.cs):
//  - Hooks: Input.GetKey / GetKeyDown / GetKeyUp (string y KeyCode)
//  - Mientras hay foco, SOLO pasa Escape (idem POM)
//  - El foco se limpia solo si se cierra el devUI
// ================================================================

public static class RCInputGuard
{
    private static bool _hooksInitialized = false;
    private static Hook[] _hooks;

    public static void Init()
    {
        if (_hooksInitialized) return;
        _hooksInitialized = true;

        try
        {
            Type inputType = typeof(Input);
            Type[] stringArg = { typeof(string) };
            Type[] keyCodeArg = { typeof(KeyCode) };

            _hooks = new Hook[]
            {
                new Hook(inputType.GetMethod(nameof(Input.GetKey), stringArg), (Func<Func<string, bool>, string, bool>)GuardKeyString),
                new Hook(inputType.GetMethod(nameof(Input.GetKey), keyCodeArg), (Func<Func<KeyCode, bool>, KeyCode, bool>)GuardKeyKeyCode),
                new Hook(inputType.GetMethod(nameof(Input.GetKeyDown), stringArg), (Func<Func<string, bool>, string, bool>)GuardKeyString),
                new Hook(inputType.GetMethod(nameof(Input.GetKeyDown), keyCodeArg), (Func<Func<KeyCode, bool>, KeyCode, bool>)GuardKeyKeyCode),
                new Hook(inputType.GetMethod(nameof(Input.GetKeyUp), stringArg), (Func<Func<string, bool>, string, bool>)GuardKeyString),
                new Hook(inputType.GetMethod(nameof(Input.GetKeyUp), keyCodeArg), (Func<Func<KeyCode, bool>, KeyCode, bool>)GuardKeyKeyCode),
            };

            On.RainWorldGame.RawUpdate += OnRainWorldGameRawUpdate;
        }
        catch (Exception ex)
        {
            RSPlugin.log.LogWarning($"[RCInputGuard] Error creating hooks: {ex.Message}");
        }
    }

    private static bool GuardKeyString(Func<string, bool> orig, string name)
    {
        if (RCStringControl.Active != null && !IsWhitelistedString(name))
            return false;
        return orig(name);
    }

    private static bool GuardKeyKeyCode(Func<KeyCode, bool> orig, KeyCode key)
    {
        if (RCStringControl.Active != null && !IsWhitelistedKey(key))
            return false;
        return orig(key);
    }

    private static bool IsWhitelistedString(string name)
    {
        string lower = name.ToLower();
        if (lower == "escape" || lower == "left ctrl" || lower == "right ctrl")
            return true;
        if (lower == "c" || lower == "v")
            return IsCtrlHeld();
        return false;
    }

    private static bool IsWhitelistedKey(KeyCode key)
    {
        if (key == KeyCode.Escape || key == KeyCode.LeftControl || key == KeyCode.RightControl)
            return true;
        if (key == KeyCode.C || key == KeyCode.V)
            return IsCtrlHeld();
        return false;
    }

    private static bool IsCtrlHeld()
        => Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

    private static void OnRainWorldGameRawUpdate(On.RainWorldGame.orig_RawUpdate orig, RainWorldGame self, float dt)
    {
        orig(self, dt);
        if (self.devUI == null && RCStringControl.Active != null)
        {
            RCStringControl.ReleaseFocus();
        }
    }
}

public class RCDEVTools
{
    public static void Init()
    {
        RCInputGuard.Init();
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