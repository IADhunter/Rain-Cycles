using System;
using DevInterface;
using UnityEngine;
using MonoMod.RuntimeDetour;
using RWCustom;

namespace FilesSetting;

// ================================================================
// CLASE: INPUT BLOCKER
// (Se mantiene para EditableFloatField, que aun lo usa)
// ================================================================

public static class InputBlocker
{
    private static bool _isBlocked = false;
    private static Hook _getKeyHook;
    private static Hook _getKeyDownHook;
    private static Hook _getKeyStringHook;
    private static Hook _getKeyDownStringHook;
    private static bool _hooksInitialized = false;
    
    public static bool IsBlocked => _isBlocked;
    
    public static void Block()
    {
        if (_isBlocked) return;
        _isBlocked = true;
        
        InitializeHooks();
    }
    
    private static void InitializeHooks()
    {
        if (_hooksInitialized) return;
        _hooksInitialized = true;
        
        try
        {
            _getKeyHook = new Hook(
                typeof(Input).GetMethod("GetKey", new Type[] { typeof(KeyCode) }),
                new Func<Func<KeyCode, bool>, KeyCode, bool>(InputGetKeyOverride));
            
            _getKeyDownHook = new Hook(
                typeof(Input).GetMethod("GetKeyDown", new Type[] { typeof(KeyCode) }),
                new Func<Func<KeyCode, bool>, KeyCode, bool>(InputGetKeyOverride));
            
            _getKeyStringHook = new Hook(
                typeof(Input).GetMethod("GetKey", new Type[] { typeof(string) }),
                new Func<Func<string, bool>, string, bool>(InputGetKeyStringOverride));
            
            _getKeyDownStringHook = new Hook(
                typeof(Input).GetMethod("GetKeyDown", new Type[] { typeof(string) }),
                new Func<Func<string, bool>, string, bool>(InputGetKeyStringOverride));
            
            On.RainWorldGame.RawUpdate += OnRainWorldGameRawUpdate;
        }
        catch (Exception ex)
        {
            RSPlugin.log.LogWarning($"[InputBlocker] Error creating hooks: {ex.Message}");
        }
    }
    
    private static bool InputGetKeyOverride(Func<KeyCode, bool> orig, KeyCode key)
    {
        if (_isBlocked)
        {
            if (key == KeyCode.Return || key == KeyCode.KeypadEnter || 
                key == KeyCode.Escape || key == KeyCode.Backspace ||
                key == KeyCode.LeftArrow || key == KeyCode.RightArrow ||
                key == KeyCode.LeftControl || key == KeyCode.RightControl ||
                key == KeyCode.C || key == KeyCode.V)
                return orig(key);
            return false;
        }
        return orig(key);
    }
    
    private static bool InputGetKeyStringOverride(Func<string, bool> orig, string name)
    {
        if (_isBlocked)
        {
            string lower = name.ToLower();
            if (lower == "return" || lower == "enter" || lower == "escape" || 
                lower == "backspace" || lower == "left" || lower == "right" ||
                lower == "left ctrl" || lower == "right ctrl")
                return orig(name);
            return false;
        }
        return orig(name);
    }
    
    private static void OnRainWorldGameRawUpdate(On.RainWorldGame.orig_RawUpdate orig, RainWorldGame self, float dt)
    {
        orig(self, dt);
        if (self.devUI == null && _isBlocked)
        {
            Unblock();
        }
    }
    
    public static void Unblock()
    {
        if (!_isBlocked) return;
        _isBlocked = false;
    }
    
    public static void Dispose()
    {
        Unblock();
        _getKeyHook?.Dispose();
        _getKeyDownHook?.Dispose();
        _getKeyStringHook?.Dispose();
        _getKeyDownStringHook?.Dispose();
        On.RainWorldGame.RawUpdate -= OnRainWorldGameRawUpdate;
        _getKeyHook = null;
        _getKeyDownHook = null;
        _getKeyStringHook = null;
        _getKeyDownStringHook = null;
        _hooksInitialized = false;
    }
}

// ================================================================
// CLASE: HEX TEXT FIELD
// Campo de texto hex basado en el nuevo RCStringControl (port del
// StringControl de RegionKit/POM). Misma API que el campo antiguo
// (OnSubmit/OnCancel/Text) para que ColorEditor no cambie.
// Sin cursor: se escribe directamente (Input.inputString), Backspace
// borra, Enter/click-fuera commitea, Escape cancela.
// ================================================================

public class HexTextField : RCStringControl
{
    private const int MAX_HEX = 6;

    public HexTextField(DevUI owner, string IDstring, DevUINode parentNode, Vector2 pos, float width, float height, string defaultValue)
        : base(owner, IDstring, parentNode, pos, width, defaultValue, IsValidHexInput)
    {
    }

    private static bool IsValidHexInput(string value)
    {
        string s = value;
        if (s.StartsWith("#"))
            s = s.Substring(1);
        if (s.Length > MAX_HEX)
            return false;
        foreach (char c in s)
        {
            if (!((c >= '0' && c <= '9') || (c >= 'A' && c <= 'F') || (c >= 'a' && c <= 'f')))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Solo se permite anadir el caracter si el texto resultante sigue
    /// siendo valido: impide que los caracteres extra (o no-hex) lleguen
    /// siquiera a mostrarse y desborden el campo.
    /// </summary>
    protected override bool ShouldAppendChar(char c, string newText) => IsValidHexInput(newText);

    /// <summary>
    /// Pegado saneado: filtra solo caracteres hex, trunca a 6 digitos
    /// y normaliza el prefijo '#' (comportamiento del campo hex antiguo).
    /// </summary>
    protected override void PasteText(string clipboard)
    {
        string clean = clipboard.Trim();
        if (clean.StartsWith("#"))
            clean = clean.Substring(1);

        string hexOnly = "";
        foreach (char ch in clean)
        {
            if ((ch >= '0' && ch <= '9') || (ch >= 'A' && ch <= 'F') || (ch >= 'a' && ch <= 'f'))
                hexOnly += char.ToUpper(ch);
            if (hexOnly.Length >= MAX_HEX)
                break;
        }

        if (hexOnly.Length > 0)
        {
            Text = "#" + hexOnly.PadRight(MAX_HEX, '0').Substring(0, MAX_HEX);
            TrySetValue(Text, false);
        }
    }

    /// <summary>
    /// El '#' inicial queda anclado: backspace nunca lo borra
    /// (el texto minimo es "#").
    /// </summary>
    protected override bool CanDeleteChar() => base.Text.Length > 1;

    /// <summary>
    /// El setter externo (ColorEditor sincroniza el campo tras un submit
    /// o mover un slider) debe actualizar tambien el valor commiteado
    /// para que el enfoque posterior restaure el canonico.
    /// </summary>
    public new string Text
    {
        get => base.Text;
        set
        {
            base.Text = value;
            actualValue = value;
            if (fLabels.Count > 0)
                fLabels[0].color = Color.black;
        }
    }
}