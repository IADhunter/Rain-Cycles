using System;
using DevInterface;
using UnityEngine;
using RainCycles.Snapshot;
using RainCycles.Core;
using RainCycles.Patches;

namespace FilesSetting;

// ================================================================
// CLASE: RCPanel_ViewPage
// ================================================================

public class RCPanel_ViewPage : RectangularDevUINode, IDevUISignals
{
    // Constantes de layout
    private const float VIEWTYPE_ARROW_X = 4f;
    private const float VIEWTYPE_LABEL_X = 25f;
    private const float VIEWTYPE_ARROW2_X = 60f;
    private const float VIEWTYPE_Y = 119f;

    // Botones de tinte (horizontal - misma fila)
    private const float TINT_BTN_Y = 29f;
    private const float TINT_BTN_WIDTH = 40f;
    private const float TINT_BTN_SPACING = 5f;
    private const float TINT_MULTIPLY_X = 5f;
    private const float TINT_ATMOSPHERE_X = TINT_MULTIPLY_X + TINT_BTN_WIDTH + TINT_BTN_SPACING;  // 54f
    private const float TINT_CLOUD_X = TINT_ATMOSPHERE_X + TINT_BTN_WIDTH + TINT_BTN_SPACING;      // 104f

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

    public RCPanel ParentPanel { get; set; }

    // ViewType selector
    private ArrowButton _viewTypePrevArrow;
    private ArrowButton _viewTypeNextArrow;
    private DevUILabel _viewTypeLabel;
    private ViewType _currentViewType;
    private readonly ViewType[] _viewTypes = { ViewType.None, ViewType.ACV, ViewType.RTV, ViewType.PSV };
    private int _viewTypeIndex = 0;

    // Tinte activo
    private int _activeTint = 0;
    private Button _multiplyBtn;
    private Button _atmosphereBtn;
    private Button _cloudBtn;
    private Color _currentColor = Color.white;

    // Color editor
    private ColorEditor _colorEditor;
    
    // Color picker components
    private FreeColorPicker _freeColorPicker;
    private Button _colorPickerBtn;
    private ColorPreview _colorPreview;

    public RCPanel_ViewPage(RCPanel parent)
        : base(parent.Owner, "RC_ViewPage_Internal", parent, Vector2.zero, parent.size)
    {
        ParentPanel = parent;
        CreateContent();
        LoadCurrentColors();
    }

    private void CreateContent()
    {
        // ViewType selector
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

        // Botones de tinte (horizontal - misma fila Y)
        _multiplyBtn = new Button(owner, "RC_Tint_Multiply", this,
            new Vector2(TINT_MULTIPLY_X, TINT_BTN_Y), TINT_BTN_WIDTH, "Multi");
        _atmosphereBtn = new Button(owner, "RC_Tint_Atmosphere", this,
            new Vector2(TINT_ATMOSPHERE_X, TINT_BTN_Y), TINT_BTN_WIDTH, "Atmos");
        _cloudBtn = new Button(owner, "RC_Tint_Cloud", this,
            new Vector2(TINT_CLOUD_X, TINT_BTN_Y), TINT_BTN_WIDTH, "Cloud");
        
        subNodes.Add(_multiplyBtn);
        subNodes.Add(_atmosphereBtn);
        subNodes.Add(_cloudBtn);
        
        UpdateTintButtonsHighlight();

        // Color Editor (Hex + HSV sliders horizontales)
        _colorEditor = new ColorEditor(owner, this,
            new Vector2(HEX_FIELD_X, HEX_FIELD_Y), HEX_FIELD_WIDTH,
            new Vector2(HSV_SLIDER_X, HSV_SLIDER_Y));
        _colorEditor.OnColorChanged = OnColorEditorChanged;
        subNodes.Add(_colorEditor);

        // Free Color Picker
        _freeColorPicker = new FreeColorPicker(owner, "RC_FreeColorPicker", this,
            new Vector2(FREE_COLOR_PICKER_X, FREE_COLOR_PICKER_Y), FREE_COLOR_PICKER_SIZE);
        _freeColorPicker.OnColorSelected = OnFreeColorSelected;
        subNodes.Add(_freeColorPicker);

        // Color Picker (pipeta)
        _colorPickerBtn = new Button(owner, "RC_ColorPicker", this,
            new Vector2(COLOR_PICKER_X, COLOR_PICKER_Y), COLOR_PICKER_SIZE, "Sc");
        subNodes.Add(_colorPickerBtn);

        // Color Preview
        _colorPreview = new ColorPreview(owner, "RC_ColorPreview", this,
            new Vector2(COLOR_PREVIEW_X, COLOR_PREVIEW_Y), COLOR_PREVIEW_SIZE);
        subNodes.Add(_colorPreview);
        
        UpdateColorPreview();
    }

    private void LoadCurrentViewType()
    {
        string path = ParentPanel.CurrentRoom?.roomSettings?.filePath;
        if (!string.IsNullOrEmpty(path))
        {
            var snap = StaticTintManager.GetCachedSnapshot(path, ParentPanel.CurrentRoomName);
            if (snap != null)
                _currentViewType = snap.ViewType;
        }
        
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
        _viewTypeIndex += delta;
        if (_viewTypeIndex < 0) _viewTypeIndex = _viewTypes.Length - 1;
        if (_viewTypeIndex >= _viewTypes.Length) _viewTypeIndex = 0;
        
        _currentViewType = _viewTypes[_viewTypeIndex];
        UpdateViewTypeLabel();
        
        ParentPanel.CurrentRoom.roomSettings.SetViewType(_currentViewType);
        var snap = SettingsSnapshot.FromFile(ParentPanel.CurrentRoom.roomSettings.filePath);
        SettingsBlendController.SetActiveSnapshot(snap);
        ParentPanel.ApplyTintsFromSnapshot(snap);
    }

    private void SaveCurrentColor()
    {
        var roomSettings = ParentPanel.CurrentRoom?.roomSettings;
        if (roomSettings == null) return;
        
        switch (_activeTint)
        {
            case 0:
                roomSettings.SetTintMultiply(_currentColor);
                RSPlugin.log.LogDebug($"[ViewPage] TintMultiply guardado en RoomSettings: {_currentColor}");
                break;
            case 1:
                roomSettings.SetTintAtmosphere(_currentColor);
                RSPlugin.log.LogDebug($"[ViewPage] TintAtmosphere guardado en RoomSettings: {_currentColor}");
                break;
            case 2:
                roomSettings.SetTintCloudAtmosphere(_currentColor);
                RSPlugin.log.LogDebug($"[ViewPage] TintCloudAtmosphere guardado en RoomSettings: {_currentColor}");
                
                // === MISMO PATRÓN QUE EL BLEND ===
                // 1. Actualizar la fuente de verdad
                SettingsBlendController.SetLastAtmosphereColor(_currentColor);
                
                // 2. Actualizar shader global
                Shader.SetGlobalVector(RainWorld.ShadPropAboveCloudsAtmosphereColor, 
                    new Vector4(_currentColor.r, _currentColor.g, _currentColor.b, 1f));
                
                // 3. Aplicar directamente al AboveCloudsView si existe
                if (ParentPanel.CurrentRoom != null)
                {
                    for (int i = 0; i < ParentPanel.CurrentRoom.updateList.Count; i++)
                    {
                        if (ParentPanel.CurrentRoom.updateList[i] is AboveCloudsView acv)
                        {
                            acv.atmosphereColor = _currentColor;
                            RSPlugin.log.LogDebug($"[ViewPage] TintCloudAtmosphere aplicado a ACV: {_currentColor}");
                            break;
                        }
                    }
                }
                break;
        }
        
        // Aplicar visualmente inmediatamente
        var snap = GetCurrentSnapshot();
        if (snap != null)
        {
            // Actualizar el snapshot en caché para que coincida con RoomSettings
            switch (_activeTint)
            {
                case 0:
                    snap.TintMultiply = _currentColor;
                    break;
                case 1:
                    snap.TintAtmosphere = _currentColor;
                    break;
                case 2:
                    snap.TintCloudAtmosphere = _currentColor;
                    break;
            }
            ParentPanel.ApplyTintsFromSnapshot(snap);
        }
    }

    private SettingsSnapshot GetCurrentSnapshot()
    {
        string path = ParentPanel.CurrentRoom?.roomSettings?.filePath;
        if (string.IsNullOrEmpty(path)) return null;
        return StaticTintManager.GetCachedSnapshot(path, ParentPanel.CurrentRoomName);
    }

    private void LoadCurrentColors()
    {
        var roomSettings = ParentPanel.CurrentRoom?.roomSettings;
        if (roomSettings == null) return;
        
        switch (_activeTint)
        {
            case 0:
                if (roomSettings.GetTintMultiply().HasValue)
                    _currentColor = roomSettings.GetTintMultiply().Value;
                break;
            case 1:
                if (roomSettings.GetTintAtmosphere().HasValue)
                    _currentColor = roomSettings.GetTintAtmosphere().Value;
                break;
            case 2:
                if (roomSettings.GetTintCloudAtmosphere().HasValue)
                    _currentColor = roomSettings.GetTintCloudAtmosphere().Value;
                break;
        }
        
        UpdateUIFromColor();
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

    // CRÍTICO: Cuando el slider H cambia, forzamos el hue al FreeColorPicker
    // porque el Color que viaja puede haber perdido H si S=0 o V=0
    private void OnColorEditorChanged(Color color)
    {
        _currentColor = color;
        
        // Sincronizar hue al picker para que el gradiente cambie independientemente de S/V
        _freeColorPicker.SetHue(_colorEditor.CurrentHue01);
        _freeColorPicker.SetColor(_currentColor);
        
        UpdateColorPreview();
        SaveCurrentColor();
    }

    private void OnFreeColorSelected(Color color)
    {
        _currentColor = color;
        _colorEditor.SetColor(_currentColor);
        UpdateColorPreview();
        SaveCurrentColor();
    }

    private void OnColorPickerClicked()
    {
        if (ScreenColorPicker.IsActive)
        {
            ScreenColorPicker.Stop(false);
            return;
        }
        
        if (ParentPanel?.Owner?.game == null || ParentPanel.Owner == null)
        {
            RSPlugin.log.LogWarning("[ViewPage] No game/devUI reference available for color picker");
            return;
        }
        
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
        _cloudBtn.colorA = _activeTint == 2 ? activeColor : normalColor;
    }

    private void SetActiveTint(int tint)
    {
        _activeTint = tint;
        UpdateTintButtonsHighlight();
        LoadCurrentColors();
    }

    public void Signal(DevUISignalType type, DevUINode sender, string message)
    {
        if (type != DevUISignalType.ButtonClick) return;
        
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
        if (sender.IDstring == "RC_Tint_Cloud")
        {
            SetActiveTint(2);
            return;
        }
        
        if (sender.IDstring == "RC_ColorPicker")
        {
            OnColorPickerClicked();
            return;
        }
    }

    public override void Update()
    {
        base.Update();
    }
}