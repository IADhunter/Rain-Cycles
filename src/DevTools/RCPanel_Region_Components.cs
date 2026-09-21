using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DevInterface;
using UnityEngine;
using RWCustom;

namespace FilesSetting;

// ================================================================
// RC STRING CONTROL
// Port autocontenido del StringControl de RegionKit (que a su vez
// usa ManagedStringControl de POM). Diferencias vs el original:
//  - Sin signals: usa delegados OnSubmit/OnCancel/OnValueChanged
//  - Escape cancela (RK no lo maneja)
//  - Clipboard Ctrl+C/V preservado de nuestro campo anterior
// Render: hereda de DevUILabel (anchor 0,0 + MoveLabel) -> texto nitido
// ================================================================

public class RCStringControl : DevUILabel
{
    /// <summary>Unico control con foco global (modelo POM/RK).</summary>
    public static RCStringControl Active { get; private set; }

    protected bool clickedLastUpdate;

    /// <summary>Ultimo valor valido (el que se restaura al cancelar/commitar).</summary>
    protected string actualValue;

    /// <summary>Delegado de validacion en vivo. True/false colorea verde/rojo.</summary>
    public Func<string, bool> isTextValid;

    /// <summary>Disparado al hacer commit (Enter / click-fuera), con el ultimo valor valido.</summary>
    public Action<string> OnSubmit;
    /// <summary>Disparado al cancelar con Escape (el texto se revierte).</summary>
    public Action OnCancel;
    /// <summary>Disparado en vivo cuando el texto pasa a ser valido (newValue, oldValue).</summary>
    public Action<string, string> OnValueChanged;

    public RCStringControl(DevUI owner, string IDstring, DevUINode parentNode, Vector2 pos, float width, string text, Func<string, bool> validate)
        : base(owner, IDstring, parentNode, pos, width, text)
    {
        isTextValid = validate;
        actualValue = text;
        Refresh();
    }

    public override void Refresh()
    {
        base.Refresh();
    }

    public override void Update()
    {
        base.Update();

        if (owner.mouseClick && !clickedLastUpdate)
        {
            if (MouseOver && Active != this && CanTakeFocus())
            {
                // robar el foco: el control anterior commitea su edicion
                // (mejora vs POM/RK, que la pierden silenciosamente)
                Active?.TrySetValue(Active.Text, true);
                Text = actualValue;
                Active = this;
                SetLabelColor(new Color(0.1f, 0.4f, 0.2f));
            }
            else if (Active == this)
            {
                // click sobre el mismo control -> commit y soltar foco
                TrySetValue(Text, true);
                Active = null;
                SetLabelColor(Color.black);
            }

            clickedLastUpdate = true;
        }
        else if (!owner.mouseClick)
        {
            clickedLastUpdate = false;
        }

        if (Active == this)
        {
            // Clipboard (Ctrl+C copia, Ctrl+V reemplaza y valida en vivo)
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
            {
                if (Input.GetKeyDown(KeyCode.C))
                {
                    GUIUtility.systemCopyBuffer = Text;
                }
                else if (Input.GetKeyDown(KeyCode.V))
                {
                    string cb = GUIUtility.systemCopyBuffer;
                    if (!string.IsNullOrEmpty(cb))
                    {
                        PasteText(cb);
                    }
                }
            }

            foreach (char c in Input.inputString)
            {
                if (c == '\b')
                {
                    if (Text.Length != 0 && CanDeleteChar())
                    {
                        Text = Text.Substring(0, Text.Length - 1);
                        TrySetValue(Text, false);
                    }
                }
                else if (c == '\n' || c == '\r')
                {
                    TrySetValue(Text, true);
                    Active = null;
                    SetLabelColor(Color.black);
                }
                else if (c == 27)
                {
                    CancelEditing();
                }
                else
                {
                    char sc = SanitizeChar(c);
                    string newText = Text + sc;
                    if (ShouldAppendChar(sc, newText))
                    {
                        Text = newText;
                        TrySetValue(Text, false);
                    }
                }
            }
        }
    }

    private void CancelEditing()
    {
        Text = actualValue;
        Active = null;
        SetLabelColor(Color.black);
        OnCancel?.Invoke();
    }

    private void SetLabelColor(Color c)
    {
        if (fLabels.Count > 0)
            fLabels[0].color = c;
    }

    /// <summary>
    /// Decide si un caracter puede anadirse al texto actual. El default
    /// (fiel a RK/POM) permite todo y la validacion se muestra en rojo;
    /// las subclases pueden restringirlo para bloquear entradas invalidas.
    /// </summary>
    protected virtual bool ShouldAppendChar(char c, string newText) => true;

    /// <summary>
    /// Transforma el caracter antes de anadirse al texto. Default: sin
    /// cambios. Las subclases pueden normalizarlo (ej. mayusculas en hex).
    /// </summary>
    protected virtual char SanitizeChar(char c) => c;

    /// <summary>
    /// Decide si el control puede tomar el foco. Default: siempre.
    /// Las subclases pueden restringirlo (ej. solo en EditMode).
    /// </summary>
    protected virtual bool CanTakeFocus() => true;

    /// <summary>
    /// Aplica un pegado de portapapeles. Default (RK/POM): reemplaza el
    /// texto y lo valida (rojo si invalido). Las subclases pueden sanearlo.
    /// </summary>
    protected virtual void PasteText(string clipboard)
    {
        Text = clipboard;
        TrySetValue(Text, false);
    }

    /// <summary>
    /// Decide si puede borrarse el ultimo caracter. Default: siempre.
    /// Las subclases pueden anclar prefijos (ej. '#' en el hex).
    /// </summary>
    protected virtual bool CanDeleteChar() => true;

    /// <summary>
    /// Valida el texto en vivo y actualiza colores. actualValue solo almacena
    /// valores validos, por lo que al terminar la transaccion el texto se
    /// restaura al ultimo valor valido (idem POM/RK).
    /// </summary>
    protected virtual void TrySetValue(string newValue, bool endTransaction)
    {
        if (isTextValid != null && isTextValid(newValue))
        {
            string oldValue = actualValue;
            actualValue = newValue;
            SetLabelColor(new Color(0.1f, 0.4f, 0.2f));
            OnValueChanged?.Invoke(newValue, oldValue);
        }
        else
        {
            SetLabelColor(Color.red);
        }

        if (endTransaction)
        {
            Text = actualValue;
            SetLabelColor(Color.black);
            Refresh();
            OnSubmit?.Invoke(actualValue);
        }
    }

    /// <summary>Establece el valor commiteado sin disparar eventos (sincroniza con la logica externa).</summary>
    public void SetCommittedValue(string value)
    {
        actualValue = value;
        Text = value;
        SetLabelColor(Color.black);
    }

    public static void ReleaseFocus()
    {
        Active = null;
    }
}

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
// Campo numerico basado en el RCStringControl (port del StringControl
// de RegionKit/POM). Misma API externa que el campo antiguo:
// OnSubmit (float) / Value / SetValue. Solo editable en EditMode.
// ================================================================
public class EditableFloatField : RCStringControl
{
    private float _value;
    private float _minValue;
    private float _maxValue;

    public float Value => _value;

    /// <summary>Disparado al commitar con un valor parseable (ya clampado).</summary>
    public new Action<float> OnSubmit;

    public EditableFloatField(DevUI owner, string IDstring, DevUINode parentNode, Vector2 pos, float width, float defaultValue, float min = 0f, float max = 999f)
        : base(owner, IDstring, parentNode, pos, width, Format(Mathf.Clamp(defaultValue, min, max)), IsValidFloatInput)
    {
        _minValue = min;
        _maxValue = max;
        _value = Mathf.Clamp(defaultValue, min, max);
    }

    private static string Format(float v) => v.ToString("F1");

    private static bool IsValidFloatInput(string value)
        => value.Length == 0 || float.TryParse(value, out _);

    protected override bool CanTakeFocus() => BlendClock.EditMode;

    /// <summary>
    /// Solo digitos y un punto decimal; el resultado debe seguir siendo
    /// un float parseable (o vacio). Bloquea letras, signos y puntos dobles.
    /// </summary>
    protected override bool ShouldAppendChar(char c, string newText)
    {
        if (c != '.' && (c < '0' || c > '9'))
            return false;
        return IsValidFloatInput(newText);
    }

    protected override void TrySetValue(string newValue, bool endTransaction)
    {
        base.TrySetValue(newValue, endTransaction);

        if (endTransaction)
        {
            if (float.TryParse(actualValue, out float parsed))
            {
                _value = Mathf.Clamp(parsed, _minValue, _maxValue);
                Text = Format(_value);
                actualValue = Format(_value);
                OnSubmit?.Invoke(_value);
            }
            else
            {
                // texto vacio/invalido: revertir al ultimo valor
                Text = Format(_value);
                actualValue = Format(_value);
            }
        }
    }

    public void SetValue(float value)
    {
        _value = Mathf.Clamp(value, _minValue, _maxValue);
        actualValue = Format(_value);
        Text = Format(_value);
    }
}

// ================================================================
// MOD SELECT PANEL
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
        
        var modList = new List<ModInfo>();
        foreach (string path in modPaths)
        {
            if (path == RCPanel_RegionPage.DEFAULT_MOD_SENTINEL)
            {
                modList.Add(new ModInfo
                {
                    Path = path,
                    DisplayName = RCPanel_RegionPage.DEFAULT_MOD_DISPLAY_NAME,
                    ModId = RCPanel_RegionPage.DEFAULT_MOD_DISPLAY_NAME
                });
                continue;
            }
            
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