using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DevInterface;
using UnityEngine;
using RWCustom;

namespace FilesSetting;

// ================================================================
// CLOCK TOGGLE BUTTON
// ================================================================
public class ClockToggleButton : Button
{
    private static readonly Color COLOR_ON = new Color(0.2f, 0.7f, 0.3f);
    private static readonly Color COLOR_OFF = new Color(0.5f, 0.5f, 0.5f);
    private bool _enabled;
    
    public ClockToggleButton(DevUI owner, string IDstring, DevUINode parentNode, Vector2 pos, float width, bool enabled)
        : base(owner, IDstring, parentNode, pos, width, "Clock")
    {
        _enabled = enabled;
        SetEnabled(enabled);
    }
    
    public void SetEnabled(bool enabled)
    {
        _enabled = enabled;
        colorA = enabled ? COLOR_ON : COLOR_OFF;
    }
    
    public void Toggle()
    {
        SetEnabled(!_enabled);
    }
}

// ================================================================
// EDITABLE FLOAT FIELD
// ================================================================
public class EditableFloatField : PositionedDevUINode
{
    private float _value;
    private float _minValue;
    private float _maxValue;
    private bool _isEditing = false;
    private FSprite _bgSprite;
    private FLabel _label;
    private FSprite _cursorSprite;
    private float _cursorAlpha = 0f;
    private string _editText;
    private float _width;
    private float _height = 20f;
    private int _cursorPos = 0;
    
    public float Value => _value;
    public Action<float> OnSubmit { get; set; }
    public Action OnCancel { get; set; }
    
    public EditableFloatField(DevUI owner, string IDstring, DevUINode parentNode, Vector2 pos, float width, float defaultValue, float min = 0f, float max = 999f)
        : base(owner, IDstring, parentNode, pos)
    {
        _value = Mathf.Clamp(defaultValue, min, max);
        _minValue = min;
        _maxValue = max;
        _width = width;
        _editText = _value.ToString("F1");
        
        _bgSprite = new FSprite("pixel");
        _bgSprite.scaleX = width;
        _bgSprite.scaleY = _height;
        _bgSprite.anchorX = 0f;
        _bgSprite.anchorY = 0f;
        _bgSprite.color = new Color(1f, 1f, 1f);
        _bgSprite.alpha = 0.5f;
        Futile.stage.AddChild(_bgSprite);
        fSprites.Add(_bgSprite);
        
        _label = new FLabel(Custom.GetFont(), _value.ToString("F1"));
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
    
    public void SetValue(float value)
    {
        _value = Mathf.Clamp(value, _minValue, _maxValue);
        _label.text = _value.ToString("F1");
        _editText = _value.ToString("F1");
    }
    
    private void StartEditing()
    {
        if (!BlendClock.EditMode) return;
        
        _isEditing = true;
        _cursorAlpha = 1f;
        _cursorPos = _editText.Length;
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
        {
            if (float.TryParse(_editText, out float newVal))
            {
                _value = Mathf.Clamp(newVal, _minValue, _maxValue);
                _label.text = _value.ToString("F1");
                OnSubmit?.Invoke(_value);
            }
            else
            {
                _editText = _value.ToString("F1");
                _label.text = _editText;
            }
        }
        else
        {
            OnCancel?.Invoke();
        }
    }
    
    private void InsertChar(char c)
    {
        _editText = _editText.Substring(0, _cursorPos) + c + _editText.Substring(_cursorPos);
        _cursorPos++;
        UpdateCursorPosition();
        _label.text = _editText + "_";
    }
    
    private void DeleteChar()
    {
        if (_cursorPos > 0)
        {
            _editText = _editText.Substring(0, _cursorPos - 1) + _editText.Substring(_cursorPos);
            _cursorPos--;
            UpdateCursorPosition();
            _label.text = _editText + "_";
        }
    }
    
    private void UpdateCursorPosition()
    {
        float textWidth = _cursorPos * 8f;
        _cursorSprite.x = absPos.x + 2f + textWidth;
        _cursorSprite.y = absPos.y + 2f;
    }
    
    public override void Update()
    {
        base.Update();
        
        if (_isEditing)
        {
            _cursorAlpha -= 0.05f;
            if (_cursorAlpha < 0f) _cursorAlpha = 1f;
            _cursorSprite.alpha = _cursorAlpha;
            
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                if (_cursorPos > 0) _cursorPos--;
                UpdateCursorPosition();
                _label.text = _editText + "_";
            }
            else if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                if (_cursorPos < _editText.Length) _cursorPos++;
                UpdateCursorPosition();
                _label.text = _editText + "_";
            }
            
            if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.C))
            {
                GUIUtility.systemCopyBuffer = _editText;
            }
            else if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.V))
            {
                string clipboard = GUIUtility.systemCopyBuffer;
                if (!string.IsNullOrEmpty(clipboard))
                {
                    _editText = clipboard;
                    _cursorPos = _editText.Length;
                    UpdateCursorPosition();
                    _label.text = _editText + "_";
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
                else if ((c >= '0' && c <= '9') || c == '.')
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
            
            return;
        }
        
        bool over = owner.mousePos.x >= absPos.x && owner.mousePos.x <= absPos.x + _width &&
                    owner.mousePos.y >= absPos.y && owner.mousePos.y <= absPos.y + _height;
        
        _bgSprite.color = over ? new Color(0.8f, 0.8f, 1f) : new Color(1f, 1f, 1f);
        
        if (owner.mouseClick && over)
        {
            StartEditing();
        }
    }
    
    public override void Refresh()
    {
        base.Refresh();
        _bgSprite.x = absPos.x;
        _bgSprite.y = absPos.y;
        _label.x = absPos.x + 2f;
        _label.y = absPos.y + _height - 4f;
        _cursorSprite.x = absPos.x + 2f;
        _cursorSprite.y = absPos.y + 2f;
    }
    
    public void Destroy()
    {
        if (_bgSprite != null) _bgSprite.RemoveFromContainer();
        if (_label != null) _label.RemoveFromContainer();
        if (_cursorSprite != null) _cursorSprite.RemoveFromContainer();
    }
}

// ================================================================
// MOD SELECT PANEL - Ahora muestra nombres de modinfo.json
// ================================================================
public class ModSelectPanel : Panel, IDevUISignals
{
    private const float ITEM_HEIGHT = 20f;
    private const float PANEL_WIDTH = 200f;
    private const float PANEL_HEIGHT = 150f;
    
    private ModInfo[] _mods;
    private string _selectedModName;
    private int _scrollOffset;
    private int _visibleItems;
    private RCPanel_RegionPage _parentPage;
    
    // Estructura para almacenar información del mod
    private struct ModInfo
    {
        public string Path;
        public string DisplayName;
        public string ModId;
    }
    
    public ModSelectPanel(DevUI owner, string id, RCPanel_RegionPage parent, Vector2 pos, string[] modPaths, string selectedModName)
        : base(owner, id, parent, pos, new Vector2(PANEL_WIDTH, PANEL_HEIGHT), "Select Mod")
    {
        _parentPage = parent;
        _selectedModName = selectedModName;
        _visibleItems = (int)((PANEL_HEIGHT - 40f) / ITEM_HEIGHT);
        _scrollOffset = 0;
        
        // Convertir rutas a ModInfo
        var modList = new List<ModInfo>();
        foreach (string path in modPaths)
        {
            string displayName = GetModNameFromModInfo(path);
            if (!string.IsNullOrEmpty(displayName))
            {
                modList.Add(new ModInfo
                {
                    Path = path,
                    DisplayName = displayName,
                    ModId = Path.GetFileName(path)
                });
            }
        }
        _mods = modList.ToArray();
        
        PopulateItems();
    }
    
    // ================================================================
    // OBTENER NOMBRE REAL DEL MOD DESDE MODINFO.JSON
    // ================================================================
    private string GetModNameFromModInfo(string modPath)
    {
        try
        {
            string modInfoPath = Path.Combine(modPath, "modinfo.json");
            if (!File.Exists(modInfoPath)) return null;

            string json = File.ReadAllText(modInfoPath);
            
            int nameIndex = json.IndexOf("\"name\"", StringComparison.OrdinalIgnoreCase);
            if (nameIndex < 0) return null;

            int colonIndex = json.IndexOf(':', nameIndex);
            if (colonIndex < 0) return null;

            int startQuote = json.IndexOf('"', colonIndex + 1);
            if (startQuote < 0) return null;

            int endQuote = json.IndexOf('"', startQuote + 1);
            if (endQuote < 0) return null;

            return json.Substring(startQuote + 1, endQuote - startQuote - 1);
        }
        catch
        {
            return null;
        }
    }
    
    private void PopulateItems()
    {
        // Limpiar items existentes
        for (int i = subNodes.Count - 1; i >= 0; i--)
        {
            if (subNodes[i] is Button btn && (
                btn.IDstring.StartsWith("RC_ModSelect_") || 
                btn.IDstring == "RC_ModScroll_Prev" || 
                btn.IDstring == "RC_ModScroll_Next" ||
                btn.IDstring == "RC_ModClear"))
            {
                btn.ClearSprites();
                subNodes.RemoveAt(i);
            }
        }
        
        // Mostrar mods con su nombre real (de modinfo.json)
        for (int i = _scrollOffset; i < Math.Min(_scrollOffset + _visibleItems, _mods.Length); i++)
        {
            int idx = i;
            float y = size.y - 30f - (i - _scrollOffset) * ITEM_HEIGHT;
            bool isSelected = _mods[idx].DisplayName == _selectedModName;
            
            var btn = new Button(owner, $"RC_ModSelect_{idx}", this,
                new Vector2(5f, y), PANEL_WIDTH - 10f, _mods[idx].DisplayName);
            btn.colorA = isSelected ? new Color(0.2f, 0.7f, 0.3f) : new Color(1f, 1f, 1f);
            subNodes.Add(btn);
        }
        
        float btnWidth = (PANEL_WIDTH - 20f) / 3f;
        const float GAP = 5f;
        const float BOTTOM_Y = 10f;
        
        if (_mods.Length > _visibleItems)
        {
            var prevBtn = new Button(owner, "RC_ModScroll_Prev", this,
                new Vector2(GAP, BOTTOM_Y), btnWidth, "Prev");
            subNodes.Add(prevBtn);
        }
        
        if (_mods.Length > _visibleItems)
        {
            var nextBtn = new Button(owner, "RC_ModScroll_Next", this,
                new Vector2(GAP + btnWidth + GAP, BOTTOM_Y), btnWidth, "Next");
            subNodes.Add(nextBtn);
        }
        
        var clearBtn = new Button(owner, "RC_ModClear", this,
            new Vector2(GAP + (btnWidth + GAP) * 2, BOTTOM_Y), btnWidth, "Clear");
        clearBtn.colorA = new Color(0.8f, 0.3f, 0.3f);
        subNodes.Add(clearBtn);
    }
    
    public void Signal(DevUISignalType type, DevUINode sender, string message)
    {
        if (type != DevUISignalType.ButtonClick) return;
        if (!BlendClock.EditMode) return;
        
        if (sender.IDstring == "RC_ModScroll_Prev")
        {
            _scrollOffset = Math.Max(0, _scrollOffset - _visibleItems);
            PopulateItems();
            return;
        }
        
        if (sender.IDstring == "RC_ModScroll_Next")
        {
            int maxOffset = Math.Max(0, _mods.Length - _visibleItems);
            _scrollOffset = Math.Min(maxOffset, _scrollOffset + _visibleItems);
            PopulateItems();
            return;
        }
        
        if (sender.IDstring == "RC_ModClear")
        {
            _parentPage.Signal(DevUISignalType.ButtonClick, sender, "__CLEAR_MOD__");
            return;
        }
        
        if (sender.IDstring.StartsWith("RC_ModSelect_"))
        {
            int idx = int.Parse(sender.IDstring.Substring("RC_ModSelect_".Length));
            if (idx >= 0 && idx < _mods.Length)
            {
                // Enviamos el nombre real del mod (de modinfo.json), no la ruta
                _parentPage.Signal(DevUISignalType.ButtonClick, sender, _mods[idx].DisplayName);
            }
        }
    }
    
    public void ClosePanel()
    {
        foreach (var node in subNodes.ToList())
        {
            if (node is Button btn) btn.ClearSprites();
        }
        subNodes.Clear();
        
        if (parentNode != null)
            parentNode.subNodes.Remove(this);
        ClearSprites();
    }
}

// ================================================================
// IMAGE SELECT PANEL
// ================================================================
public class ImageSelectPanel : Panel, IDevUISignals
{
    private const float ITEM_HEIGHT = 20f;
    private const float PANEL_WIDTH = 250f;
    private const float PANEL_HEIGHT = 200f;
    
    private string[] _images;
    private string _selectedImage;
    private int _scrollOffset;
    private int _visibleItems;
    private RCPanel_RegionPage _parentPage;
    private int _targetSlot;
    
    public ImageSelectPanel(DevUI owner, string id, RCPanel_RegionPage parent, Vector2 pos, string[] images, string selectedImage, int targetSlot = -1)
        : base(owner, id, parent, pos, new Vector2(PANEL_WIDTH, PANEL_HEIGHT), "Select Image")
    {
        _parentPage = parent;
        _images = images;
        _selectedImage = selectedImage;
        _targetSlot = targetSlot;
        _visibleItems = (int)((PANEL_HEIGHT - 40f) / ITEM_HEIGHT);
        _scrollOffset = 0;
        PopulateItems();
    }
    
    private void PopulateItems()
    {
        for (int i = subNodes.Count - 1; i >= 0; i--)
        {
            if (subNodes[i] is Button btn && (
                btn.IDstring.StartsWith("RC_ImageSelect_") || 
                btn.IDstring == "RC_ImageScroll_Prev" || 
                btn.IDstring == "RC_ImageScroll_Next" || 
                btn.IDstring == "RC_ImageSelect_None" ||
                btn.IDstring == "RC_ImageNone"))
            {
                btn.ClearSprites();
                subNodes.RemoveAt(i);
            }
        }
        
        for (int i = _scrollOffset; i < Math.Min(_scrollOffset + _visibleItems, _images.Length); i++)
        {
            int idx = i;
            float y = size.y - 30f - (i - _scrollOffset) * ITEM_HEIGHT;
            string imageName = Path.GetFileNameWithoutExtension(_images[idx]);
            bool isSelected = imageName == _selectedImage;
            
            var btn = new Button(owner, $"RC_ImageSelect_{idx}", this,
                new Vector2(5f, y), PANEL_WIDTH - 10f, imageName);
            btn.colorA = isSelected ? new Color(0.2f, 0.7f, 0.3f) : new Color(1f, 1f, 1f);
            subNodes.Add(btn);
        }
        
        float btnWidth = (PANEL_WIDTH - 20f) / 3f;
        const float GAP = 5f;
        const float BOTTOM_Y = 10f;
        
        if (_images.Length > _visibleItems)
        {
            var prevBtn = new Button(owner, "RC_ImageScroll_Prev", this,
                new Vector2(GAP, BOTTOM_Y), btnWidth, "Prev");
            subNodes.Add(prevBtn);
        }
        
        if (_images.Length > _visibleItems)
        {
            var nextBtn = new Button(owner, "RC_ImageScroll_Next", this,
                new Vector2(GAP + btnWidth + GAP, BOTTOM_Y), btnWidth, "Next");
            subNodes.Add(nextBtn);
        }
        
        var noneBtn = new Button(owner, "RC_ImageNone", this,
            new Vector2(GAP + (btnWidth + GAP) * 2, BOTTOM_Y), btnWidth, "None");
        noneBtn.colorA = new Color(0.8f, 0.3f, 0.3f);
        subNodes.Add(noneBtn);
    }
    
    public void Signal(DevUISignalType type, DevUINode sender, string message)
    {
        if (type != DevUISignalType.ButtonClick) return;
        if (!BlendClock.EditMode) return;
        
        if (sender.IDstring == "RC_ImageScroll_Prev")
        {
            _scrollOffset = Math.Max(0, _scrollOffset - _visibleItems);
            PopulateItems();
            return;
        }
        
        if (sender.IDstring == "RC_ImageScroll_Next")
        {
            int maxOffset = Math.Max(0, _images.Length - _visibleItems);
            _scrollOffset = Math.Min(maxOffset, _scrollOffset + _visibleItems);
            PopulateItems();
            return;
        }
        
        if (sender.IDstring == "RC_ImageNone")
        {
            if (_targetSlot >= 0)
                _parentPage.Signal(DevUISignalType.ButtonClick, sender, $"__NONE__:{_targetSlot}");
            else
                _parentPage.Signal(DevUISignalType.ButtonClick, sender, "");
            return;
        }
        
        if (sender.IDstring.StartsWith("RC_ImageSelect_"))
        {
            int idx = int.Parse(sender.IDstring.Substring("RC_ImageSelect_".Length));
            string selectedImage = _images[idx];
            if (_targetSlot >= 0)
                _parentPage.Signal(DevUISignalType.ButtonClick, sender, $"{selectedImage}:{_targetSlot}");
            else
                _parentPage.Signal(DevUISignalType.ButtonClick, sender, selectedImage);
        }
    }
    
    public void ClosePanel()
    {
        foreach (var node in subNodes.ToList())
        {
            if (node is Button btn) btn.ClearSprites();
        }
        subNodes.Clear();
        
        if (parentNode != null)
            parentNode.subNodes.Remove(this);
        ClearSprites();
    }
}