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

public partial class RCPanel_ViewPage : RectangularDevUINode, IDevUISignals
{
    private const float VIEWTYPE_ARROW_X = 5f;
    private const float VIEWTYPE_LABEL_X = 26f;
    private const float VIEWTYPE_ARROW2_X = 61f;
    private const float VIEWTYPE_Y = 145f;

    private const float TINT_ROW_Y = 59f;
    private const float TINT_ARROW_X = 5f;
    private const float TINT_LABEL_X = 26f;
    private const float TINT_LABEL_WIDTH = 30f;
    private const float TINT_ARROW2_X = 61f;

    private const float TINT_TOGGLE_WIDTH = 30f;
    private const float TINT_TOGGLE_X = 116f - 5f - TINT_TOGGLE_WIDTH;

    private const float HSV_SLIDER_X = 5f;
    private const float HSV_SLIDER_Y = 82f;

    private const float HEX_FIELD_X = 154f;
    private const float HEX_FIELD_Y = 146f;
    private const float HEX_FIELD_WIDTH = 56f;

    private const float FREE_COLOR_PICKER_X = 155f;
    private const float FREE_COLOR_PICKER_Y = 83f;
    private const float FREE_COLOR_PICKER_SIZE = 56f;

    private const float COLOR_PICKER_X = 116f;
    private const float COLOR_PICKER_Y = 145f;
    private const float COLOR_PICKER_SIZE = 16f;

    private const float COLOR_PREVIEW_X = 137f;
    private const float COLOR_PREVIEW_Y = 145f;
    private const float COLOR_PREVIEW_SIZE = 18f;

    private static readonly Color COLOR_TINT_ON = new Color(0.2f, 0.7f, 0.3f);
    private static readonly Color COLOR_TINT_OFF = new Color(1f, 1f, 1f);

    public RCPanel ParentPanel { get; set; }

    private ArrowButton _viewTypePrevArrow;
    private ArrowButton _viewTypeNextArrow;
    private DevUILabel _viewTypeLabel;
    private ViewType _currentViewType;
    private readonly ViewType[] _viewTypes = { ViewType.None, ViewType.ACV, ViewType.RTV, ViewType.PSV, ViewType.AUV, ViewType.ORV };
    private int _viewTypeIndex = 0;

    private int _activeTint = 0;
    private ArrowButton _tintPrevArrow;
    private ArrowButton _tintNextArrow;
    private DevUILabel _tintLabel;
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

        _tintPrevArrow = new ArrowButton(owner, "RC_Tint_Prev", this,
            new Vector2(TINT_ARROW_X, TINT_ROW_Y), 270f);
        _tintNextArrow = new ArrowButton(owner, "RC_Tint_Next", this,
            new Vector2(TINT_ARROW2_X, TINT_ROW_Y), 90f);
        _tintLabel = new DevUILabel(owner, "RC_Tint_Label", this,
            new Vector2(TINT_LABEL_X, TINT_ROW_Y), TINT_LABEL_WIDTH, "Multi");

        subNodes.Add(_tintPrevArrow);
        subNodes.Add(_tintNextArrow);
        subNodes.Add(_tintLabel);

        UpdateTintSelector();

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

        CreateBkgRow();
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
            // Refrescar AMBOS canales desde el archivo del estado nuevo: si solo
            // se carga el activo, ApplyMemoryTintsToShaders vuelca el canal
            // inactivo con el valor del estado anterior (fix 09/2026: con atmos
            // activo, multi no seguía al cambio de estado; atmos sobrevivía solo
            // porque el lock de static redirige su global).
            LoadChannelFromSettings(0, updateCurrent: _activeTint == 0);
            LoadChannelFromSettings(1, updateCurrent: _activeTint == 1);
            ApplyMemoryTintsToShaders();
        }

        UpdateUIFromColor();
        UpdateTintToggleVisual();
        UpdateTintSelector();

        // La fila bkg lee el estado seleccionado (y descarta el buffer local
        // de mod pendiente: al cambiar de estado deja de ser válido).
        CloseBkgPickers();
        _pendingMod = null;
        RefreshBkgRow();
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

        // Si la view de la sala deja el canal seleccionado inerte, saltar al
        // otro. El recargo de color lo hace el llamador (ctor → LoadCurrentColors,
        // refresh → UpdateUIFromColor), no aquí: en CreateContent el editor aún
        // no existe.
        EnsureValidActiveTint();
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

        if (EnsureValidActiveTint())
        {
            LoadCurrentColors();
            UpdateUIFromColor();
        }

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
            // Cada canal conserva su base vanilla por separado: al activar el
            // tinte no se contamina un canal con el color del otro (fix 08/2026:
            // "mover atmos mueve multi" — ambos compartian _currentColor).
            Color defMultiply = Color.white;
            Color defAtmosphere = Color.white;
            if (TintManager.TryGetOriginalColors(ParentPanel.CurrentRoom, out Color vanillaMult, out Color vanillaAtmo))
            {
                defMultiply = vanillaMult;
                defAtmosphere = vanillaAtmo;
            }

            _hasMemMultiply = true;
            _hasMemAtmosphere = true;
            _memMultiply = defMultiply;
            _memAtmosphere = defAtmosphere;

            roomSettings.SetTintMultiply(defMultiply);
            roomSettings.SetTintAtmosphere(defAtmosphere);
        }
        else
        {
            // Al apagar el tinte se restaura el color vanilla default del
            // setting (el que no tiene tintes declarados), no un blanco puro.
            Color defMultiply = Color.white;
            Color defAtmosphere = Color.white;
            if (TintManager.TryGetOriginalColors(ParentPanel.CurrentRoom, out Color vanillaMult, out Color vanillaAtmo))
            {
                defMultiply = vanillaMult;
                defAtmosphere = vanillaAtmo;
            }

            _hasMemMultiply = false;
            _hasMemAtmosphere = false;
            _memMultiply = defMultiply;
            _memAtmosphere = defAtmosphere;
            _currentColor = _activeTint == 0 ? defMultiply : defAtmosphere;

            roomSettings.ClearTint();

            Shader.SetGlobalVector(RainWorld.ShadPropMultiplyColor, new Vector4(defMultiply.r, defMultiply.g, defMultiply.b, 1f));
            Shader.SetGlobalVector(RainWorld.ShadPropAboveCloudsAtmosphereColor, new Vector4(defAtmosphere.r, defAtmosphere.g, defAtmosphere.b, 1f));

            if (ParentPanel.CurrentRoom != null)
            {
                for (int i = 0; i < ParentPanel.CurrentRoom.updateList.Count; i++)
                {
                    if (ParentPanel.CurrentRoom.updateList[i] is AboveCloudsView acv)
                    {
                        acv.atmosphereColor = defAtmosphere;
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

                // En Static el lock debe actualizarse ANTES de escribir: si no,
                // OnSetGlobalVector redirige el global al lock viejo y FORCE-FIELD
                // revierte el campo de AboveCloudsView cada frame (edición "muda").
                TintManager.SetStaticLock(_currentColor);

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

    // ============================================================
    // PERSISTENCIA DE TINTES AL ARCHIVO DEL ESTADO
    // ============================================================

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
        
        LoadChannelFromSettings(_activeTint, updateCurrent: true);
    }

    // Carga de un canal desde la ext-data del archivo (o mem de sesión como
    // fallback) refrescando _mem*; updateCurrent alimenta además _currentColor,
    // que solo debe moverse para el canal activo.
    private void LoadChannelFromSettings(int tint, bool updateCurrent)
    {
        var roomSettings = ParentPanel.CurrentRoom?.roomSettings;
        if (roomSettings == null) return;

        switch (tint)
        {
            case 0:
                if (roomSettings.GetTintMultiply().HasValue)
                {
                    _memMultiply = roomSettings.GetTintMultiply().Value;
                    _hasMemMultiply = true;
                    if (updateCurrent) _currentColor = _memMultiply;
                }
                else if (_hasMemMultiply)
                {
                    // La ext-data perdio el valor (Load/parse sin tinte) pero la sesion
                    // conserva el color editado: restauarlo en vez de mostrar blanco,
                    // y resincronizarlo para que el toggle/save no lo descarte
                    // (fix 08/2026: indicadores en blanco al volver a un canal).
                    roomSettings.SetTintMultiply(_memMultiply);
                    if (updateCurrent) _currentColor = _memMultiply;
                }
                else
                {
                    _hasMemMultiply = false;
                    if (updateCurrent) _currentColor = Color.white;
                }
                break;
            case 1:
                if (roomSettings.GetTintAtmosphere().HasValue)
                {
                    _memAtmosphere = roomSettings.GetTintAtmosphere().Value;
                    _hasMemAtmosphere = true;
                    if (updateCurrent) _currentColor = _memAtmosphere;
                }
                else if (_hasMemAtmosphere)
                {
                    roomSettings.SetTintAtmosphere(_memAtmosphere);
                    if (updateCurrent) _currentColor = _memAtmosphere;
                }
                else
                {
                    _hasMemAtmosphere = false;
                    if (updateCurrent) _currentColor = Color.white;
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

    // ============================================================
    // SELECTOR DE CANAL (Multi / Atmos)
    // ============================================================

    // Multi es inerte en ORV (ningún shader consume el global) y Atmos es
    // inerte en RTV/AUV: en esos casos el canal no debe poder seleccionarse.
    private bool IsTintValid(int tint)
    {
        if (tint == 0) return _currentViewType != ViewType.ORV;
        return _currentViewType != ViewType.RTV && _currentViewType != ViewType.AUV;
    }

    private bool EnsureValidActiveTint()
    {
        if (IsTintValid(_activeTint)) return false;
        _activeTint = _activeTint == 0 ? 1 : 0;
        UpdateTintSelector();
        return true;
    }

    private void CycleActiveTint(int delta)
    {
        // Solo hay 2 canales y la misma view nunca los indefine a los dos a la
        // vez, pero el bucle evita quedarse sin salida si eso cambia en el futuro.
        int tint = _activeTint;
        for (int i = 0; i < 2; i++)
        {
            tint = (tint + delta + 2) % 2;
            if (IsTintValid(tint))
            {
                SetActiveTint(tint);
                return;
            }
        }
    }

    private void UpdateTintSelector()
    {
        if (_tintLabel == null) return;
        _tintLabel.Text = _activeTint == 0 ? "Multi" : "Atmos";
    }

    private void SetActiveTint(int tint)
    {
        _activeTint = tint;
        UpdateTintSelector();
        LoadCurrentColors();
        UpdateUIFromColor();
    }

    public void Signal(DevUISignalType type, DevUINode sender, string message)
    {
        if (type != DevUISignalType.ButtonClick) return;
        if (!BlendClock.EditMode) return;

        if (HandleBkgSignal(sender, message)) return;

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
        
        if (sender.IDstring == "RC_Tint_Prev")
        {
            CycleActiveTint(-1);
            return;
        }
        if (sender.IDstring == "RC_Tint_Next")
        {
            CycleActiveTint(1);
            return;
        }
        
        if (sender.IDstring == "RC_ColorPicker")
        {
            OnColorPickerClicked();
            return;
        }
    }
}