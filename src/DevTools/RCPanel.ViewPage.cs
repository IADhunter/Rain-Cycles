using System;
using DevInterface;
using UnityEngine;
using RainCycles.Snapshot;
using RainCycles.Core;
using RainCycles.Patches;

namespace FilesSetting;

// ================================================================
// RCPanel_ViewPage
// ================================================================

public class RCPanel_ViewPage : RectangularDevUINode, IDevUISignals
{
    private const float VIEWTYPE_ARROW_X = 4f;
    private const float VIEWTYPE_LABEL_X = 25f;
    private const float VIEWTYPE_ARROW2_X = 60f;
    private const float VIEWTYPE_Y = 119f;

    private const float TINT_BTN_Y = 29f;
    private const float TINT_BTN_WIDTH = 60f;
    private const float TINT_BTN_SPACING = 5f;
    private const float TINT_MULTIPLY_X = 5f;
    private const float TINT_ATMOSPHERE_X = TINT_MULTIPLY_X + TINT_BTN_WIDTH + TINT_BTN_SPACING;

    private const float TINT_TOGGLE_WIDTH = 30f;
    private const float TINT_TOGGLE_X = 115f - 5f - TINT_TOGGLE_WIDTH;

    private const float HSV_SLIDER_X = 5f;
    private const float HSV_SLIDER_Y = 56f;

    private const float HEX_FIELD_X = 154f;
    private const float HEX_FIELD_Y = 120f;
    private const float HEX_FIELD_WIDTH = 56f;

    private const float FREE_COLOR_PICKER_X = 155f;
    private const float FREE_COLOR_PICKER_Y = 57f;
    private const float FREE_COLOR_PICKER_SIZE = 56f;

    private const float COLOR_PICKER_X = 115f;
    private const float COLOR_PICKER_Y = 119f;
    private const float COLOR_PICKER_SIZE = 16f;

    private const float COLOR_PREVIEW_X = 137f;
    private const float COLOR_PREVIEW_Y = 119f;
    private const float COLOR_PREVIEW_SIZE = 18f;

    private static readonly Color COLOR_TINT_ON = new Color(0.2f, 0.7f, 0.3f);
    private static readonly Color COLOR_TINT_OFF = new Color(1f, 1f, 1f);

    public RCPanel ParentPanel { get; set; }

    private ArrowButton _viewTypePrevArrow;
    private ArrowButton _viewTypeNextArrow;
    private DevUILabel _viewTypeLabel;
    private ViewType _currentViewType;
    private readonly ViewType[] _viewTypes = { ViewType.None, ViewType.ACV, ViewType.RTV, ViewType.PSV };
    private int _viewTypeIndex = 0;

    private int _activeTint = 0;
    private Button _multiplyBtn;
    private Button _atmosphereBtn;
    private Color _currentColor = Color.white;

    private Button _tintToggleBtn;
    private bool _tintEnabled = false;

    private Color _memMultiply = Color.white;
    private Color _memAtmosphere = Color.white;
    private bool _hasMemMultiply = false;
    private bool _hasMemAtmosphere = false;

    private ColorEditor _colorEditor;
    private FreeColorPicker _freeColorPicker;
    private Button _colorPickerBtn;
    private ColorPreview _colorPreview;

    public RCPanel_ViewPage(RCPanel parent)
        : base(parent.Owner, "RC_ViewPage_Internal", parent, Vector2.zero, parent.size)
    {
        ParentPanel = parent;
        CreateContent();
        LoadCurrentViewType();
        LoadCurrentColors();
        LoadTintToggleState();
    }

    private void CreateContent()
    {
        _viewTypePrevArrow = new ArrowButton(owner, "RC_ViewType_Prev", this,
            new Vector2(VIEWTYPE_ARROW_X, VIEWTYPE_Y), 270f);
        _viewTypeNextArrow = new ArrowButton(owner, "RC_ViewType_Next", this,
            new Vector2(VIEWTYPE_ARROW2_X, VIEWTYPE_Y), 90f);
        _viewTypeLabel = new DevUILabel(owner, "RC_ViewType_Label", this,
            new Vector2(VIEWTYPE_LABEL_X, VIEWTYPE_Y), 30f, "NONE");
        
        subNodes.Add(_viewTypePrevArrow);
        subNodes.Add(_viewTypeNextArrow);
        subNodes.Add(_viewTypeLabel);
        
        LoadCurrentViewType();

        _tintToggleBtn = new Button(owner, "RC_Tint_Toggle", this,
            new Vector2(TINT_TOGGLE_X, COLOR_PICKER_Y), TINT_TOGGLE_WIDTH, "Tint");
        subNodes.Add(_tintToggleBtn);

        _multiplyBtn = new Button(owner, "RC_Tint_Multiply", this,
            new Vector2(TINT_MULTIPLY_X, TINT_BTN_Y), TINT_BTN_WIDTH, "Multiply");
        _atmosphereBtn = new Button(owner, "RC_Tint_Atmosphere", this,
            new Vector2(TINT_ATMOSPHERE_X, TINT_BTN_Y), TINT_BTN_WIDTH, "Atmosphere");
        
        subNodes.Add(_multiplyBtn);
        subNodes.Add(_atmosphereBtn);
        
        UpdateTintButtonsHighlight();

        _colorEditor = new ColorEditor(owner, this,
            new Vector2(HEX_FIELD_X, HEX_FIELD_Y), HEX_FIELD_WIDTH,
            new Vector2(HSV_SLIDER_X, HSV_SLIDER_Y));
        _colorEditor.OnColorChanged = OnColorEditorChanged;
        subNodes.Add(_colorEditor);

        _freeColorPicker = new FreeColorPicker(owner, "RC_FreeColorPicker", this,
            new Vector2(FREE_COLOR_PICKER_X, FREE_COLOR_PICKER_Y), FREE_COLOR_PICKER_SIZE);
        _freeColorPicker.OnColorSelected = OnFreeColorSelected;
        subNodes.Add(_freeColorPicker);

        _colorPickerBtn = new Button(owner, "RC_ColorPicker", this,
            new Vector2(COLOR_PICKER_X, COLOR_PICKER_Y), COLOR_PICKER_SIZE, "Sc");
        subNodes.Add(_colorPickerBtn);

        _colorPreview = new ColorPreview(owner, "RC_ColorPreview", this,
            new Vector2(COLOR_PREVIEW_X, COLOR_PREVIEW_Y), COLOR_PREVIEW_SIZE);
        subNodes.Add(_colorPreview);
        
        UpdateColorPreview();
        UpdateTintToggleVisual();
    }

    // ============================================================
    // REFRESH DESDE EL ESTADO ACTUAL
    // ============================================================
    public void RefreshFromCurrentState()
    {
        LoadCurrentViewType();
        LoadTintToggleState();

        if (!_tintEnabled)
        {
            if (TintManager.TryGetOriginalColors(ParentPanel.CurrentRoom, out Color vanillaMult, out Color vanillaAtmo))
            {
                _currentColor = _activeTint == 0 ? vanillaMult : vanillaAtmo;
                _memMultiply = vanillaMult;
                _memAtmosphere = vanillaAtmo;
            }
            else
            {
                _currentColor = Color.white;
                _memMultiply = Color.white;
                _memAtmosphere = Color.white;
            }
            
            _hasMemMultiply = false;
            _hasMemAtmosphere = false;
            ApplyMemoryTintsToShaders();
        }
        else
        {
            LoadCurrentColors();
            ApplyMemoryTintsToShaders();
        }

        UpdateUIFromColor();
        UpdateTintToggleVisual();
        UpdateTintButtonsHighlight();
    }

    private void LoadCurrentViewType()
    {
        var roomSettings = ParentPanel.CurrentRoom?.roomSettings;
        if (roomSettings != null && roomSettings.HasView())
            _currentViewType = roomSettings.GetViewType();
        else
            _currentViewType = ViewType.None;
        
        _viewTypeIndex = Array.IndexOf(_viewTypes, _currentViewType);
        if (_viewTypeIndex < 0) _viewTypeIndex = 0;
        UpdateViewTypeLabel();
    }

    private void UpdateViewTypeLabel()
    {
        string display = _currentViewType == ViewType.None ? "NONE" : _currentViewType.ToString();
        _viewTypeLabel.Text = display;
    }

    private void SetViewType(int delta)
    {
        if (!BlendClock.EditMode) return;
        
        _viewTypeIndex += delta;
        if (_viewTypeIndex < 0) _viewTypeIndex = _viewTypes.Length - 1;
        if (_viewTypeIndex >= _viewTypes.Length) _viewTypeIndex = 0;
        
        _currentViewType = _viewTypes[_viewTypeIndex];
        UpdateViewTypeLabel();
        
        ParentPanel.CurrentRoom.roomSettings.SetViewType(_currentViewType);
        var snap = SettingsSnapshot.FromFile(ParentPanel.CurrentRoom.roomSettings.filePath);
        SettingsBlendController.SetActiveSnapshot(snap);
        ParentPanel.ApplyTintsFromSnapshot(snap);
        LoadTintToggleState();
    }

    // ============================================================
    // TINT TOGGLE
    // ============================================================

    private void LoadTintToggleState()
    {
        var roomSettings = ParentPanel.CurrentRoom?.roomSettings;
        if (roomSettings == null)
        {
            _tintEnabled = false;
            UpdateTintToggleVisual();
            return;
        }

        _tintEnabled = roomSettings.HasTint();
        UpdateTintToggleVisual();
    }

    private void UpdateTintToggleVisual()
    {
        if (_tintToggleBtn == null) return;
        _tintToggleBtn.colorA = _tintEnabled ? COLOR_TINT_ON : COLOR_TINT_OFF;
    }

    private void ToggleTint()
    {
        if (_currentViewType == ViewType.None) return;
        if (!BlendClock.EditMode) return;

        var roomSettings = ParentPanel.CurrentRoom?.roomSettings;
        if (roomSettings == null) return;

        _tintEnabled = !_tintEnabled;
        UpdateTintToggleVisual();

        if (_tintEnabled)
        {
            _hasMemMultiply = true;
            _hasMemAtmosphere = true;
            _memMultiply = _currentColor;
            _memAtmosphere = _currentColor;
            
            roomSettings.SetTintMultiply(_currentColor);
            roomSettings.SetTintAtmosphere(_currentColor);
        }
        else
        {
            _hasMemMultiply = false;
            _hasMemAtmosphere = false;
            _memMultiply = Color.white;
            _memAtmosphere = Color.white;
            _currentColor = Color.white;
            
            roomSettings.ClearTint();
            
            Shader.SetGlobalVector(RainWorld.ShadPropMultiplyColor, new Vector4(1f, 1f, 1f, 1f));
            Shader.SetGlobalVector(RainWorld.ShadPropAboveCloudsAtmosphereColor, new Vector4(1f, 1f, 1f, 1f));
            
            if (ParentPanel.CurrentRoom != null)
            {
                for (int i = 0; i < ParentPanel.CurrentRoom.updateList.Count; i++)
                {
                    if (ParentPanel.CurrentRoom.updateList[i] is AboveCloudsView acv)
                    {
                        acv.atmosphereColor = Color.white;
                        break;
                    }
                }
            }
            
            UpdateUIFromColor();
        }

        var snap = SettingsSnapshot.FromFile(roomSettings.filePath);
        SettingsBlendController.SetActiveSnapshot(snap);
        ParentPanel.ApplyTintsFromSnapshot(snap);
    }

    // ============================================================
    // COLOR MANAGEMENT
    // ============================================================

    private void ApplyMemoryTintsToShaders()
    {
        if (_hasMemMultiply)
        {
            var c = _memMultiply;
            Shader.SetGlobalVector(RainWorld.ShadPropMultiplyColor, new Vector4(c.r, c.g, c.b, 1f));
        }
        if (_hasMemAtmosphere)
        {
            var c = _memAtmosphere;
            Shader.SetGlobalVector(RainWorld.ShadPropAboveCloudsAtmosphereColor, new Vector4(c.r, c.g, c.b, 1f));
            
            if (ParentPanel.CurrentRoom != null)
            {
                for (int i = 0; i < ParentPanel.CurrentRoom.updateList.Count; i++)
                {
                    if (ParentPanel.CurrentRoom.updateList[i] is AboveCloudsView acv)
                    {
                        acv.atmosphereColor = c;
                        break;
                    }
                }
            }
        }
    }

    private void SaveCurrentColor()
    {
        if (!BlendClock.EditMode) return;
        if (!_tintEnabled) return;
        
        var roomSettings = ParentPanel.CurrentRoom?.roomSettings;
        if (roomSettings == null) return;
        
        switch (_activeTint)
        {
            case 0:
                _memMultiply = _currentColor;
                _hasMemMultiply = true;
                roomSettings.SetTintMultiply(_currentColor);
                Shader.SetGlobalVector(RainWorld.ShadPropMultiplyColor, 
                    new Vector4(_currentColor.r, _currentColor.g, _currentColor.b, 1f));
                break;
            case 1:
                _memAtmosphere = _currentColor;
                _hasMemAtmosphere = true;
                roomSettings.SetTintAtmosphere(_currentColor);
                Shader.SetGlobalVector(RainWorld.ShadPropAboveCloudsAtmosphereColor, 
                    new Vector4(_currentColor.r, _currentColor.g, _currentColor.b, 1f));
                
                if (ParentPanel.CurrentRoom != null)
                {
                    for (int i = 0; i < ParentPanel.CurrentRoom.updateList.Count; i++)
                    {
                        if (ParentPanel.CurrentRoom.updateList[i] is AboveCloudsView acv)
                        {
                            acv.atmosphereColor = _currentColor;
                            break;
                        }
                    }
                }
                break;
        }
    }

    private void LoadCurrentColors()
    {
        var roomSettings = ParentPanel.CurrentRoom?.roomSettings;
        if (roomSettings == null) return;
        
        if (!_tintEnabled)
        {
            switch (_activeTint)
            {
                case 0:
                    _currentColor = _memMultiply;
                    break;
                case 1:
                    _currentColor = _memAtmosphere;
                    break;
            }
            return;
        }
        
        switch (_activeTint)
        {
            case 0:
                if (roomSettings.GetTintMultiply().HasValue)
                {
                    _currentColor = roomSettings.GetTintMultiply().Value;
                    _memMultiply = _currentColor;
                    _hasMemMultiply = true;
                }
                else
                {
                    _currentColor = Color.white;
                    _hasMemMultiply = false;
                }
                break;
            case 1:
                if (roomSettings.GetTintAtmosphere().HasValue)
                {
                    _currentColor = roomSettings.GetTintAtmosphere().Value;
                    _memAtmosphere = _currentColor;
                    _hasMemAtmosphere = true;
                }
                else
                {
                    _currentColor = Color.white;
                    _hasMemAtmosphere = false;
                }
                break;
        }
    }

    private void UpdateUIFromColor()
    {
        _colorEditor.SetColor(_currentColor);
        _freeColorPicker.SetColor(_currentColor);
        UpdateColorPreview();
    }

    private void UpdateColorPreview()
    {
        _colorPreview.SetColor(_currentColor);
    }

    private void OnColorEditorChanged(Color color)
    {
        if (!BlendClock.EditMode) return;
        
        _currentColor = color;
        _freeColorPicker.SetHue(_colorEditor.CurrentHue01);
        _freeColorPicker.SetColor(_currentColor);
        UpdateColorPreview();
        SaveCurrentColor();
    }

    private void OnFreeColorSelected(Color color)
    {
        if (!BlendClock.EditMode) return;
        
        _currentColor = color;
        _colorEditor.SetColor(_currentColor);
        UpdateColorPreview();
        SaveCurrentColor();
    }

    private void OnColorPickerClicked()
    {
        if (!BlendClock.EditMode) return;
        
        if (ScreenColorPicker.IsActive)
        {
            ScreenColorPicker.Stop(false);
            return;
        }
        
        if (ParentPanel?.Owner?.game == null || ParentPanel.Owner == null)
            return;
        
        ScreenColorPicker.Start(ParentPanel.Owner, ParentPanel.Owner.game, (Color pickedColor) =>
        {
            _currentColor = pickedColor;
            _colorEditor.SetColor(_currentColor);
            _freeColorPicker.SetColor(_currentColor);
            UpdateColorPreview();
            SaveCurrentColor();
        });
    }

    private void UpdateTintButtonsHighlight()
    {
        Color normalColor = new Color(1f, 1f, 1f);
        Color activeColor = new Color(0.2f, 0.7f, 0.3f);
        
        _multiplyBtn.colorA = _activeTint == 0 ? activeColor : normalColor;
        _atmosphereBtn.colorA = _activeTint == 1 ? activeColor : normalColor;
    }

    private void SetActiveTint(int tint)
    {
        _activeTint = tint;
        UpdateTintButtonsHighlight();
        LoadCurrentColors();
        UpdateUIFromColor();
    }

    public void Signal(DevUISignalType type, DevUINode sender, string message)
    {
        if (type != DevUISignalType.ButtonClick) return;
        if (!BlendClock.EditMode) return;
        
        if (sender.IDstring == "RC_ViewType_Prev")
        {
            SetViewType(-1);
            return;
        }
        if (sender.IDstring == "RC_ViewType_Next")
        {
            SetViewType(1);
            return;
        }

        if (sender.IDstring == "RC_Tint_Toggle")
        {
            if (_currentViewType == ViewType.None) return;
            ToggleTint();
            return;
        }
        
        if (sender.IDstring == "RC_Tint_Multiply")
        {
            SetActiveTint(0);
            return;
        }
        if (sender.IDstring == "RC_Tint_Atmosphere")
        {
            SetActiveTint(1);
            return;
        }
        
        if (sender.IDstring == "RC_ColorPicker")
        {
            OnColorPickerClicked();
            return;
        }
    }
}