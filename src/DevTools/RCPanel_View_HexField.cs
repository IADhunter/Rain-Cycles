using System;
using DevInterface;
using UnityEngine;
using MonoMod.RuntimeDetour;
using RWCustom;

namespace FilesSetting;

// ================================================================
// CLASE: INPUT BLOCKER
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
// ================================================================

public class HexTextField : PositionedDevUINode
{
    private string _text;
    private int _cursorPos = 1;
    private bool _isEditing = false;
    private FSprite _bgSprite;
    private FLabel _label;
    private FSprite _cursorSprite;
    private float _cursorAlpha = 0f;
    private float _lastCursorAlpha = 0f;
    private float _width;
    private float _height;
    private const int MAX_LEN = 7;
    
    public string Text 
    { 
        get => _text;
        set 
        {
            _text = value;
            _cursorPos = _text.Length;
            if (_label != null) _label.text = _text;
        }
    }
    
    public Action<string> OnSubmit { get; set; }
    public Action OnCancel { get; set; }
    
    public HexTextField(DevUI owner, string IDstring, DevUINode parentNode, Vector2 pos, float width, float height, string defaultValue)
        : base(owner, IDstring, parentNode, pos)
    {
        _width = width;
        _height = height;
        _text = defaultValue;
        _cursorPos = _text.Length;
        
        _bgSprite = new FSprite("pixel");
        _bgSprite.scaleX = width;
        _bgSprite.scaleY = height;
        _bgSprite.anchorX = 0f;
        _bgSprite.anchorY = 0f;
        _bgSprite.color = new Color(1f, 1f, 1f);
        _bgSprite.alpha = 0.5f;
        Futile.stage.AddChild(_bgSprite);
        fSprites.Add(_bgSprite);
        
        _label = new FLabel(Custom.GetFont(), _text);
        _label.anchorX = 0f;
        _label.anchorY = 0.80f;
        _label.color = Color.black;
        Futile.stage.AddChild(_label);
        fLabels.Add(_label);
        
        _cursorSprite = new FSprite("pixel");
        _cursorSprite.scaleX = 2f;
        _cursorSprite.scaleY = _height - 4f;
        _cursorSprite.anchorX = 0f;
        _cursorSprite.anchorY = 0f;
        _cursorSprite.color = Color.black;
        _cursorSprite.alpha = 0f;
        Futile.stage.AddChild(_cursorSprite);
        fSprites.Add(_cursorSprite);
        
        Refresh();
    }
    
    private void StartEditing()
    {
        _isEditing = true;
        _cursorAlpha = 1f;
        _cursorPos = _text.Length;
        _label.color = Color.white;
        _bgSprite.color = new Color(0.2f, 0.2f, 0.8f);
        _cursorSprite.alpha = 1f;
        
        InputBlocker.Block();
        UpdateCursorPosition();
    }
    
    private void StopEditing(bool submit)
    {
        _isEditing = false;
        _cursorAlpha = 0f;
        _label.color = Color.black;
        _bgSprite.color = new Color(1f, 1f, 1f);
        _cursorSprite.alpha = 0f;
        
        InputBlocker.Unblock();
        
        if (submit)
            OnSubmit?.Invoke(_text);
        else
            OnCancel?.Invoke();
    }
    
    private bool IsValidHexChar(char c)
    {
        return (c >= '0' && c <= '9') || (c >= 'A' && c <= 'F') || (c >= 'a' && c <= 'f');
    }
    
    private void InsertChar(char c)
    {
        if (_text.Length >= MAX_LEN) return;
        char upper = char.ToUpper(c);
        string newText = _text.Substring(0, _cursorPos) + upper + _text.Substring(_cursorPos);
        if (newText.Length <= MAX_LEN)
        {
            _text = newText;
            _cursorPos++;
            _label.text = _text;
            UpdateCursorPosition();
        }
    }
    
    private void DeleteChar()
    {
        if (_cursorPos <= 1) return;
        _text = _text.Substring(0, _cursorPos - 1) + _text.Substring(_cursorPos);
        _cursorPos--;
        _label.text = _text;
        UpdateCursorPosition();
    }
    
    private void UpdateCursorPosition()
    {
        float textWidth = (_cursorPos - 1) * 8f;
        _cursorSprite.x = absPos.x + 5f + textWidth;
        _cursorSprite.y = absPos.y + 2f;
    }
    
    public override void Update()
    {
        base.Update();
        
        _lastCursorAlpha = _cursorAlpha;
        if (_isEditing)
        {
            _cursorAlpha -= 0.05f;
            if (_cursorAlpha < 0f) _cursorAlpha = 1f;
        }
        _cursorSprite.alpha = _cursorAlpha;
        
        bool over = owner.mousePos.x >= absPos.x && owner.mousePos.x <= absPos.x + _width &&
                    owner.mousePos.y >= absPos.y && owner.mousePos.y <= absPos.y + _height;
        
        if (owner.mouseClick && over && !_isEditing)
        {
            StartEditing();
            return;
        }
        
        if (_isEditing && owner.mouseClick && !over)
        {
            StopEditing(true);
            return;
        }
        
        if (_isEditing)
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                if (_cursorPos > 1) _cursorPos--;
                UpdateCursorPosition();
            }
            else if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                if (_cursorPos < _text.Length) _cursorPos++;
                UpdateCursorPosition();
            }
            
            if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.C))
            {
                GUIUtility.systemCopyBuffer = _text;
            }
            else if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.V))
            {
                string clipboard = GUIUtility.systemCopyBuffer;
                if (!string.IsNullOrEmpty(clipboard))
                {
                    string clean = clipboard.Trim();
                    if (clean.StartsWith("#"))
                        clean = clean.Substring(1);
                    string hexOnly = "";
                    foreach (char ch in clean)
                    {
                        if ((ch >= '0' && ch <= '9') || (ch >= 'A' && ch <= 'F') || (ch >= 'a' && ch <= 'f'))
                            hexOnly += char.ToUpper(ch);
                        if (hexOnly.Length >= 6) break;
                    }
                    if (hexOnly.Length > 0)
                    {
                        _text = "#" + hexOnly.PadRight(6, '0').Substring(0, 6);
                        _cursorPos = _text.Length;
                        _label.text = _text;
                        UpdateCursorPosition();
                    }
                }
            }
            
            foreach (char c in Input.inputString)
            {
                if (c == '\b')
                {
                    DeleteChar();
                }
                else if (c == '\n' || c == '\r')
                {
                    StopEditing(true);
                    return;
                }
                else if (c == 27)
                {
                    StopEditing(false);
                    return;
                }
                else if (IsValidHexChar(c))
                {
                    InsertChar(c);
                }
            }
            
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                StopEditing(true);
                return;
            }
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                StopEditing(false);
                return;
            }
        }
    }
    
    public override void Refresh()
    {
        base.Refresh();
        _bgSprite.x = absPos.x;
        _bgSprite.y = absPos.y;
        _label.x = absPos.x + 2f;
        _label.y = absPos.y + _height - 4f;
        UpdateCursorPosition();
    }
    
    public void Destroy()
    {
        if (_bgSprite != null) _bgSprite.RemoveFromContainer();
        if (_label != null) _label.RemoveFromContainer();
        if (_cursorSprite != null) _cursorSprite.RemoveFromContainer();
    }
}