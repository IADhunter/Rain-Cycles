using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DevInterface;
using UnityEngine;
using RainCycles.Settings;
using RainCycles.Core;

namespace FilesSetting;

public class RCPanel_RegionPage : RectangularDevUINode, IDevUISignals
{
    private const float MARGIN = 5f;
    private const float ROW_HEIGHT = 22f;
    private const float BUTTON_WIDTH = 45f;
    private const float MOD_BUTTON_WIDTH = 120f;
    private const float BKG_BUTTON_WIDTH = 30f;
    private const float FIELD_WIDTH = 60f;
    
    private const float VIEWTYPE_ARROW_X = 5f;
    private const float VIEWTYPE_LABEL_X = 26f;
    private const float VIEWTYPE_ARROW2_X = 61f;
    private const float VIEWTYPE_Y = 78f;
    
    private const float SLOT_ARROW_X = 100f;
    private const float SLOT_LABEL_X = 121f;
    private const float SLOT_ARROW2_X = 156f;
    
    private const float TRIGGER_ARROW_X = 100f;
    private const float TRIGGER_LABEL_X = 121f;
    private const float TRIGGER_ARROW2_X = 156f;
    
    private const float SETTING_ARROW_X = 100f;
    private const float SETTING_LABEL_X = 121f;
    private const float SETTING_ARROW2_X = 156f;
    private const float SETTING_Y = 120f;
    
    private const float WAITTIME_X = 100f;
    
    private const float ROW_CLOCK_Y = 180f;
    private const float ROW_MODE_Y = 155f;
    private const float ROW_IDLE_Y = 128f;
    private const float ROW_DURATION_Y = 103f;
    private const float ROW_MOD_Y = 53f;
    private const float ROW_BKG_Y = 28f;
    
    public const string DEFAULT_MOD_SENTINEL = "\0__RC_DEFAULT__";
    public const string DEFAULT_MOD_DISPLAY_NAME = "Default";
    
    public RCPanel ParentPanel { get; set; }
    private RegionLogic _logic;
    
    private ClockToggleButton _clockToggle;
    private EditModeButton _editModeButton;

    private ArrowButton _modePrevArrow;
    private ArrowButton _modeNextArrow;
    private DevUILabel _modeLabel;
    private EditableFloatField _idleField;
    private EditableFloatField _durationField;
    private Button _modButton;
    private Button[] _bkgButtons;
    
    private ArrowButton _viewTypePrevArrow;
    private ArrowButton _viewTypeNextArrow;
    private DevUILabel _viewTypeLabel;
    
    private ArrowButton _slotPrevArrow;
    private ArrowButton _slotNextArrow;
    private DevUILabel _slotLabel;
    private int _currentSlot = 0;
    
    private ArrowButton _triggerPrevArrow;
    private ArrowButton _triggerNextArrow;
    private DevUILabel _triggerLabel;
    private EditableFloatField _waitTimeField;
    
    private ArrowButton _settingPrevArrow;
    private ArrowButton _settingNextArrow;
    private DevUILabel _settingLabel;
    
    private ModSelectPanel _modSelectPanel;
    private ImageSelectPanel _imageSelectPanel;
    private int _editingBkgIndex = -1;
    
    private static readonly Color BKG_COLOR_HAS_IMAGE = new Color(0.2f, 0.7f, 0.3f);
    private static readonly Color BKG_COLOR_NO_IMAGE = new Color(1f, 1f, 1f);
    private static readonly Color BKG_COLOR_HAS_FOG = new Color(0.2f, 0.5f, 0.8f);
    private static readonly Color BKG_COLOR_HAS_SUN = new Color(0.8f, 0.5f, 0.2f);

    public RCPanel_RegionPage(RCPanel parent)
        : base(parent.Owner, "RC_RegionPage_Internal", parent, Vector2.zero, parent.size)
    {
        ParentPanel = parent;
        _logic = new RegionLogic(this);
        _bkgButtons = new Button[4];
        
        _logic.LoadFromBlendSettings();
        CreateContent();
        UpdateSlotSelector();
    }

    private void CreateContent()
    {
        _clockToggle = new ClockToggleButton(owner, "RC_ClockToggle", this,
            new Vector2(MARGIN, ROW_CLOCK_Y), 60f, _logic.ClockEnabled);
        subNodes.Add(_clockToggle);
        
        _editModeButton = new EditModeButton(owner, "RC_EditMode", this,
            new Vector2(5f, 5f), 30f);
        subNodes.Add(_editModeButton);
        
        _modePrevArrow = new ArrowButton(owner, "RC_Mode_Prev", this,
            new Vector2(MARGIN, ROW_MODE_Y), 270f);
        _modeNextArrow = new ArrowButton(owner, "RC_Mode_Next", this,
            new Vector2(MARGIN + 56f, ROW_MODE_Y), 90f);
        _modeLabel = new DevUILabel(owner, "RC_Mode_Label", this,
            new Vector2(MARGIN + 21f, ROW_MODE_Y), 30f, _logic.GetModeDisplay());
        subNodes.Add(_modePrevArrow);
        subNodes.Add(_modeNextArrow);
        subNodes.Add(_modeLabel);
        
        _idleField = new EditableFloatField(owner, "RC_IdleField", this,
            new Vector2(MARGIN, ROW_IDLE_Y), FIELD_WIDTH, _logic.IdleValue, 0f, 999f);
        _idleField.OnSubmit = (float newValue) => {
            if (!BlendClock.EditMode) return;
            _logic.IdleValue = newValue;
            _logic.SaveToBlendSettings();
        };
        subNodes.Add(_idleField);
        
        _durationField = new EditableFloatField(owner, "RC_DurationField", this,
            new Vector2(MARGIN, ROW_DURATION_Y), FIELD_WIDTH, _logic.DurationValue, 0f, 999f);
        _durationField.OnSubmit = (float newValue) => {
            if (!BlendClock.EditMode) return;
            _logic.DurationValue = newValue;
            _logic.SaveToBlendSettings();
        };
        subNodes.Add(_durationField);
        
        _waitTimeField = new EditableFloatField(owner, "RC_WaitTimeField", this,
            new Vector2(WAITTIME_X, ROW_IDLE_Y), FIELD_WIDTH, _logic.WaitTimeValue, 0f, 999f);
        _waitTimeField.OnSubmit = (float newValue) => {
            if (!BlendClock.EditMode) return;
            _logic.WaitTimeValue = newValue;
            _logic.SaveToBlendSettings();
        };
        subNodes.Add(_waitTimeField);
        
        _triggerPrevArrow = new ArrowButton(owner, "RC_Trigger_Prev", this,
            new Vector2(TRIGGER_ARROW_X, ROW_MODE_Y), 270f);
        _triggerNextArrow = new ArrowButton(owner, "RC_Trigger_Next", this,
            new Vector2(TRIGGER_ARROW2_X, ROW_MODE_Y), 90f);
        _triggerLabel = new DevUILabel(owner, "RC_Trigger_Label", this,
            new Vector2(TRIGGER_LABEL_X, ROW_MODE_Y), 30f, _logic.GetTriggerDisplay());
        subNodes.Add(_triggerPrevArrow);
        subNodes.Add(_triggerNextArrow);
        subNodes.Add(_triggerLabel);
        
        _settingPrevArrow = new ArrowButton(owner, "RC_Setting_Prev", this,
            new Vector2(SETTING_ARROW_X, SETTING_Y), 270f);
        _settingNextArrow = new ArrowButton(owner, "RC_Setting_Next", this,
            new Vector2(SETTING_ARROW2_X, SETTING_Y), 90f);
        _settingLabel = new DevUILabel(owner, "RC_Setting_Label", this,
            new Vector2(SETTING_LABEL_X, SETTING_Y), 30f, "St:" + _logic.SettingValue);
        subNodes.Add(_settingPrevArrow);
        subNodes.Add(_settingNextArrow);
        subNodes.Add(_settingLabel);
        
        _viewTypePrevArrow = new ArrowButton(owner, "RC_ViewType_Prev", this,
            new Vector2(VIEWTYPE_ARROW_X, VIEWTYPE_Y), 270f);
        _viewTypeNextArrow = new ArrowButton(owner, "RC_ViewType_Next", this,
            new Vector2(VIEWTYPE_ARROW2_X, VIEWTYPE_Y), 90f);
        _viewTypeLabel = new DevUILabel(owner, "RC_ViewType_Label", this,
            new Vector2(VIEWTYPE_LABEL_X, VIEWTYPE_Y), 30f, GetViewTypeDisplay());
        subNodes.Add(_viewTypePrevArrow);
        subNodes.Add(_viewTypeNextArrow);
        subNodes.Add(_viewTypeLabel);
        
        _slotPrevArrow = new ArrowButton(owner, "RC_Slot_Prev", this,
            new Vector2(SLOT_ARROW_X, VIEWTYPE_Y), 270f);
        _slotNextArrow = new ArrowButton(owner, "RC_Slot_Next", this,
            new Vector2(SLOT_ARROW2_X, VIEWTYPE_Y), 90f);
        _slotLabel = new DevUILabel(owner, "RC_Slot_Label", this,
            new Vector2(SLOT_LABEL_X, VIEWTYPE_Y), 30f, "Sky");
        subNodes.Add(_slotPrevArrow);
        subNodes.Add(_slotNextArrow);
        subNodes.Add(_slotLabel);
        
        _modButton = new Button(owner, "RC_ModButton", this,
            new Vector2(MARGIN, ROW_MOD_Y), MOD_BUTTON_WIDTH, _logic.SelectedModName);
        subNodes.Add(_modButton);
        
        for (int i = 0; i < 4; i++)
        {
            int state = i + 1;
            float x = MARGIN + i * (BKG_BUTTON_WIDTH + 5f);
            
            _bkgButtons[i] = new Button(owner, $"RC_BkgButton_{state}", this,
                new Vector2(x, ROW_BKG_Y), BKG_BUTTON_WIDTH, $"bkg{state}");
            UpdateBkgButtonColor(i);
            subNodes.Add(_bkgButtons[i]);
        }
    }
    
    private string GetViewTypeDisplay()
    {
        return _logic.CurrentViewType == ViewType.ACV ? "ACV" :
               _logic.CurrentViewType == ViewType.RTV ? "RTV" :
               _logic.CurrentViewType == ViewType.PSV ? "PSV" : "ORV";
    }
    
    private void UpdateViewLabel()
    {
        _viewTypeLabel.Text = GetViewTypeDisplay();
    }
    
    private void UpdateSlotSelector()
    {
        if (_logic.CurrentViewType != ViewType.PSV && _currentSlot != 0)
        {
            _currentSlot = 0;
        }
        
        string slotName = _currentSlot == 0 ? "Sky" : (_currentSlot == 1 ? "Fog" : "Sun");
        _slotLabel.Text = slotName;
    }
    
    private void UpdateModeLabel()
    {
        _modeLabel.Text = _logic.GetModeDisplay();
    }

    private void UpdateTriggerLabel()
    {
        _triggerLabel.Text = _logic.GetTriggerDisplay();
    }
    
    private void UpdateSettingLabel()
    {
        _settingLabel.Text = "St:" + _logic.SettingValue;
    }
    
    private void CycleSetting(int delta)
    {
        _logic.SettingValue += delta;
        if (_logic.SettingValue < 0) _logic.SettingValue = 4;
        if (_logic.SettingValue > 4) _logic.SettingValue = 0;
        UpdateSettingLabel();
        _logic.SaveToBlendSettings();
    }
    
    private void CycleSlot(int delta)
    {
        if (!BlendClock.EditMode) return;
        if (_logic.CurrentViewType != ViewType.PSV) return;
        
        _currentSlot += delta;
        if (_currentSlot < 0) _currentSlot = 2;
        if (_currentSlot > 2) _currentSlot = 0;
        
        string slotName = _currentSlot == 0 ? "Sky" : (_currentSlot == 1 ? "Fog" : "Sun");
        _slotLabel.Text = slotName;
        
        RefreshBkgButtons();
    }
    
    private void UpdateBkgButtonColor(int index)
    {
        if (_logic.CurrentViewType == ViewType.PSV)
        {
            bool hasSky = !string.IsNullOrEmpty(_logic.BkgValues[index]);
            bool hasFog = !string.IsNullOrEmpty(_logic.FogValues[index]);
            bool hasSun = !string.IsNullOrEmpty(_logic.SunValues[index]);
            
            if (_currentSlot == 0)
                _bkgButtons[index].colorA = hasSky ? BKG_COLOR_HAS_IMAGE : BKG_COLOR_NO_IMAGE;
            else if (_currentSlot == 1)
                _bkgButtons[index].colorA = hasFog ? BKG_COLOR_HAS_FOG : BKG_COLOR_NO_IMAGE;
            else
                _bkgButtons[index].colorA = hasSun ? BKG_COLOR_HAS_SUN : BKG_COLOR_NO_IMAGE;
        }
        else
        {
            bool hasImage = !string.IsNullOrEmpty(_logic.BkgValues[index]);
            _bkgButtons[index].colorA = hasImage ? BKG_COLOR_HAS_IMAGE : BKG_COLOR_NO_IMAGE;
        }
    }
    
    private void RefreshBkgButtons()
    {
        for (int i = 0; i < 4; i++)
            UpdateBkgButtonColor(i);
    }

    public void Signal(DevUISignalType type, DevUINode sender, string message)
    {
        if (type != DevUISignalType.ButtonClick) return;
        
        bool blockIfNotEditMode = sender.IDstring != "RC_ClockToggle";
        
        if (blockIfNotEditMode && !BlendClock.EditMode) return;
        
        if (sender.IDstring == "RC_ClockToggle")
        {
            _logic.ClockEnabled = !_logic.ClockEnabled;
            _clockToggle.SetEnabled(_logic.ClockEnabled);
            _logic.SaveToBlendSettings();
            return;
        }
        
        if (sender.IDstring == "RC_Mode_Prev")
        {
            if (!BlendClock.EditMode && BlendClock.IsRunning) return;
            _logic.CycleMode(-1);
            UpdateModeLabel();
            UpdateTriggerLabel();
            _logic.SaveToBlendSettings();
            SettingsBlendController.ResetFull();
            if (BlendClock.IsRunning) BlendClock.Stop();
            return;
        }
        
        if (sender.IDstring == "RC_Mode_Next")
        {
            if (!BlendClock.EditMode && BlendClock.IsRunning) return;
            _logic.CycleMode(1);
            UpdateModeLabel();
            UpdateTriggerLabel();
            _logic.SaveToBlendSettings();
            SettingsBlendController.ResetFull();
            if (BlendClock.IsRunning) BlendClock.Stop();
            return;
        }
        
        if (sender.IDstring == "RC_ViewType_Prev")
        {
            _logic.CycleViewType(-1);
            UpdateViewLabel();
            UpdateSlotSelector();
            _logic.LoadImagesForCurrentView();
            RefreshBkgButtons();
            return;
        }
        
        if (sender.IDstring == "RC_ViewType_Next")
        {
            _logic.CycleViewType(1);
            UpdateViewLabel();
            UpdateSlotSelector();
            _logic.LoadImagesForCurrentView();
            RefreshBkgButtons();
            return;
        }
        
        if (sender.IDstring == "RC_Slot_Prev")
        {
            CycleSlot(-1);
            return;
        }
        
        if (sender.IDstring == "RC_Slot_Next")
        {
            CycleSlot(1);
            return;
        }
        
        if (sender.IDstring == "RC_Trigger_Prev")
        {
            _logic.CycleTrigger(-1);
            UpdateTriggerLabel();
            return;
        }
        
        if (sender.IDstring == "RC_Trigger_Next")
        {
            _logic.CycleTrigger(1);
            UpdateTriggerLabel();
            return;
        }
        
        if (sender.IDstring == "RC_Setting_Prev")
        {
            if (!BlendClock.EditMode && BlendClock.IsRunning) return;
            CycleSetting(-1);
            return;
        }
        
        if (sender.IDstring == "RC_Setting_Next")
        {
            if (!BlendClock.EditMode && BlendClock.IsRunning) return;
            CycleSetting(1);
            return;
        }
        
        if (sender.IDstring == "RC_ModButton")
        {
            OpenModSelectPanel();
            return;
        }
        
        for (int i = 0; i < 4; i++)
        {
            if (sender.IDstring == $"RC_BkgButton_{i + 1}")
            {
                _editingBkgIndex = i;
                OpenImageSelectPanel(i);
                return;
            }
        }
        
        if (sender.IDstring.StartsWith("RC_ModSelect_") && _modSelectPanel != null)
        {
            _logic.SelectedModName = message;
            _logic.SavedModName = message;
            _modButton.Text = string.IsNullOrEmpty(message) ? "Select Mod" : message;
            
            if (_modSelectPanel != null)
            {
                _modSelectPanel.ClosePanel();
                _modSelectPanel = null;
            }
            _logic.SaveToBlendSettings();
            return;
        }
        
        if (sender.IDstring.StartsWith("RC_ImageSelect_") && sender.IDstring != "RC_ImageSelect_None" && _imageSelectPanel != null)
        {
            string imageName = !string.IsNullOrEmpty(message) ? Path.GetFileNameWithoutExtension(message) : "";
            
            if (_logic.CurrentViewType == ViewType.PSV)
            {
                if (_currentSlot == 0)
                    _logic.BkgValues[_editingBkgIndex] = imageName;
                else if (_currentSlot == 1)
                    _logic.FogValues[_editingBkgIndex] = imageName;
                else
                    _logic.SunValues[_editingBkgIndex] = imageName;
            }
            else
            {
                _logic.BkgValues[_editingBkgIndex] = imageName;
            }
            
            UpdateBkgButtonColor(_editingBkgIndex);
            
            if (_imageSelectPanel != null)
            {
                _imageSelectPanel.ClosePanel();
                _imageSelectPanel = null;
            }
            _editingBkgIndex = -1;
            _logic.SaveToBlendSettings();
            return;
        }
        
        if ((sender.IDstring == "RC_ImageSelect_None" || sender.IDstring == "RC_ImageNone") && _imageSelectPanel != null)
        {
            if (_logic.CurrentViewType == ViewType.PSV)
            {
                if (_currentSlot == 0)
                    _logic.BkgValues[_editingBkgIndex] = "";
                else if (_currentSlot == 1)
                    _logic.FogValues[_editingBkgIndex] = "";
                else
                    _logic.SunValues[_editingBkgIndex] = "";
            }
            else
            {
                _logic.BkgValues[_editingBkgIndex] = "";
            }
            
            UpdateBkgButtonColor(_editingBkgIndex);
            
            if (_imageSelectPanel != null)
            {
                _imageSelectPanel.ClosePanel();
                _imageSelectPanel = null;
            }
            _editingBkgIndex = -1;
            _logic.SaveToBlendSettings();
            return;
        }
        
        if (sender.IDstring == "RC_ModClear")
        {
            _logic.SelectedModName = "";
            _logic.SavedModName = "";
            _logic.ClearAllBackgrounds();
            
            for (int i = 0; i < 4; i++)
            {
                _logic.BkgValues[i] = "";
                _logic.FogValues[i] = "";
                _logic.SunValues[i] = "";
            }
            
            _modButton.Text = "Select Mod";
            RefreshBkgButtons();
            
            if (_modSelectPanel != null)
            {
                _modSelectPanel.ClosePanel();
                _modSelectPanel = null;
            }
            
            _logic.SaveToBlendSettings();
            return;
        }
    }

    // ============================================================
    // OBTENER MODS CON CARPETA ILLUSTRATIONS
    // ============================================================
    private string[] GetModsWithIllustrations()
    {
        var mods = new List<string>();
        mods.Add(DEFAULT_MOD_SENTINEL);
        
        foreach (var mod in ModManager.ActiveMods)
        {
            if (Directory.Exists(Path.Combine(mod.path, "Illustrations")))
            {
                string modInfoPath = Path.Combine(mod.path, "modinfo.json");
                if (File.Exists(modInfoPath))
                {
                    mods.Add(mod.path);
                }
            }
        }
        return mods.ToArray();
    }

    // ============================================================
    // OBTENER NOMBRE DEL MOD DESDE MODINFO.JSON
    // ============================================================
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

    private void OpenModSelectPanel()
    {
        if (!BlendClock.EditMode) return;
        
        if (_modSelectPanel != null)
        {
            _modSelectPanel.ClosePanel();
            _modSelectPanel = null;
            return;
        }
        
        var modPaths = GetModsWithIllustrations();
        if (modPaths.Length == 0)
            return;
        
        Vector2 panelPos = new Vector2(_modButton.pos.x + 10f, _modButton.pos.y - 150f);
        
        _modSelectPanel = new ModSelectPanel(owner, "RC_ModSelectPanel", this, panelPos, modPaths, _logic.SelectedModName);
        subNodes.Add(_modSelectPanel);
        _modSelectPanel.Refresh();
    }
    
    private void OpenImageSelectPanel(int bkgIndex)
    {
        if (!BlendClock.EditMode) return;
        
        if (_imageSelectPanel != null)
        {
            _imageSelectPanel.ClosePanel();
            _imageSelectPanel = null;
            _editingBkgIndex = -1;
            return;
        }
        
        string illustrationsPath;
        if (string.Equals(_logic.SelectedModName, DEFAULT_MOD_DISPLAY_NAME, StringComparison.OrdinalIgnoreCase))
        {
            illustrationsPath = Path.Combine(Application.streamingAssetsPath, "Illustrations");
        }
        else
        {
            string modPath = ResolveModPathFromName(_logic.SelectedModName);
            if (string.IsNullOrEmpty(modPath))
                return;
            
            illustrationsPath = Path.Combine(modPath, "Illustrations");
        }
        
        if (!Directory.Exists(illustrationsPath))
            return;
        
        var images = Directory.GetFiles(illustrationsPath, "*.png")
            .Select(f => Path.GetFileName(f))
            .ToArray();
        
        if (images.Length == 0)
            return;
        
        string currentImage = "";
        if (_logic.CurrentViewType == ViewType.PSV)
        {
            if (_currentSlot == 0)
                currentImage = _logic.BkgValues[bkgIndex];
            else if (_currentSlot == 1)
                currentImage = _logic.FogValues[bkgIndex];
            else
                currentImage = _logic.SunValues[bkgIndex];
        }
        else
        {
            currentImage = _logic.BkgValues[bkgIndex];
        }
        
        Vector2 panelPos = new Vector2(_bkgButtons[bkgIndex].pos.x, _bkgButtons[bkgIndex].pos.y - 200f);
        
        _imageSelectPanel = new ImageSelectPanel(owner, "RC_ImageSelectPanel", this, panelPos, images, currentImage);
        subNodes.Add(_imageSelectPanel);
        _imageSelectPanel.Refresh();
    }
    
    // ============================================================
    // RESOLVER RUTA DEL MOD POR NOMBRE
    // ============================================================
    private string ResolveModPathFromName(string modName)
    {
        if (string.IsNullOrEmpty(modName)) return null;
        
        foreach (var mod in ModManager.ActiveMods)
        {
            string realName = GetModNameFromModInfo(mod.path);
            if (string.Equals(realName, modName, StringComparison.OrdinalIgnoreCase))
            {
                return mod.path;
            }
        }
        return null;
    }

    public override void Update()
    {
        base.Update();
        
        if (BlendClock.EditMode)
        {
            if (_idleField != null && Math.Abs(_logic.IdleValue - _idleField.Value) > 0.01f)
            {
                _logic.IdleValue = _idleField.Value;
                _logic.SaveToBlendSettings();
            }
            
            if (_durationField != null && Math.Abs(_logic.DurationValue - _durationField.Value) > 0.01f)
            {
                _logic.DurationValue = _durationField.Value;
                _logic.SaveToBlendSettings();
            }
            
            if (_waitTimeField != null && Math.Abs(_logic.WaitTimeValue - _waitTimeField.Value) > 0.01f)
            {
                _logic.WaitTimeValue = _waitTimeField.Value;
                _logic.SaveToBlendSettings();
            }
        }
    }
}