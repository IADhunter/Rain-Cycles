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
    private const float BKG_BUTTON_WIDTH = 70f;
    private const float FIELD_WIDTH = 60f;
    
    private const float VIEWTYPE_ARROW_X = 4f;
    private const float VIEWTYPE_LABEL_X = 25f;
    private const float VIEWTYPE_ARROW2_X = 60f;
    private const float VIEWTYPE_Y = 78f;
    
    private const float SLOT_ARROW_X = 100f;
    private const float SLOT_LABEL_X = 121f;
    private const float SLOT_ARROW2_X = 156f;
    
    private const float ROW_CLOCK_Y = 180f;
    private const float ROW_MODE_Y = 155f;
    private const float ROW_IDLE_Y = 128f;
    private const float ROW_DURATION_Y = 103f;
    private const float ROW_MOD_Y = 53f;
    private const float ROW_BKG_Y = 28f;
    
    public RCPanel ParentPanel { get; set; }
    private RegionLogic _logic;
    
    private ClockToggleButton _clockToggle;
    private ModeButton _loopModeBtn;
    private ModeButton _rainModeBtn;
    private ModeButton _cycleModeBtn;
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
    
    private ModSelectPanel _modSelectPanel;
    private ImageSelectPanel _imageSelectPanel;
    private int _editingBkgIndex = -1;
    
    private static readonly Color BKG_COLOR_HAS_IMAGE = new Color(0.2f, 0.7f, 0.3f);
    private static readonly Color BKG_COLOR_NO_IMAGE = new Color(0.5f, 0.5f, 0.5f);
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
        
        _loopModeBtn = new ModeButton(owner, "RC_Mode_Loop", this,
            new Vector2(MARGIN, ROW_MODE_Y), BUTTON_WIDTH, BlendMode.Loop, _logic.CurrentMode == BlendMode.Loop);
        _rainModeBtn = new ModeButton(owner, "RC_Mode_EndCycle", this,
            new Vector2(MARGIN + BUTTON_WIDTH + 5f, ROW_MODE_Y), BUTTON_WIDTH, BlendMode.EndCycle, _logic.CurrentMode == BlendMode.EndCycle);
        _cycleModeBtn = new ModeButton(owner, "RC_Mode_Cycle", this,
            new Vector2(MARGIN + (BUTTON_WIDTH + 5f) * 2, ROW_MODE_Y), BUTTON_WIDTH, BlendMode.Cycle, _logic.CurrentMode == BlendMode.Cycle);
        subNodes.Add(_loopModeBtn);
        subNodes.Add(_rainModeBtn);
        subNodes.Add(_cycleModeBtn);
        
        _idleField = new EditableFloatField(owner, "RC_IdleField", this,
            new Vector2(MARGIN, ROW_IDLE_Y), FIELD_WIDTH, _logic.IdleValue, 0f, 999f);
        _idleField.OnSubmit = (float newValue) => {
            _logic.IdleValue = newValue;
            _logic.SaveToBlendSettings();
        };
        subNodes.Add(_idleField);
        
        _durationField = new EditableFloatField(owner, "RC_DurationField", this,
            new Vector2(MARGIN, ROW_DURATION_Y), FIELD_WIDTH, _logic.DurationValue, 0f, 999f);
        _durationField.OnSubmit = (float newValue) => {
            _logic.DurationValue = newValue;
            _logic.SaveToBlendSettings();
        };
        subNodes.Add(_durationField);
        
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
            float x = MARGIN + (i % 2) * (BKG_BUTTON_WIDTH + 10f);
            float y = ROW_BKG_Y - (i / 2) * (ROW_HEIGHT + 5f);
            
            _bkgButtons[i] = new Button(owner, $"RC_BkgButton_{state}", this,
                new Vector2(x, y), BKG_BUTTON_WIDTH, $"bkg0{state}");
            UpdateBkgButtonColor(i);
            subNodes.Add(_bkgButtons[i]);
        }
    }
    
    private string GetViewTypeDisplay()
    {
        return _logic.CurrentViewType == ViewType.ACV ? "ACV" :
               _logic.CurrentViewType == ViewType.RTV ? "RTV" : "PSV";
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
    
    private void CycleSlot(int delta)
    {
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
        
        if (sender.IDstring == "RC_ClockToggle")
        {
            _logic.ClockEnabled = !_logic.ClockEnabled;
            _clockToggle.SetEnabled(_logic.ClockEnabled);
            _logic.SaveToBlendSettings();
            return;
        }
        
        if (sender.IDstring == "RC_Mode_Loop")
        {
            if (!BlendClock.EditMode && BlendClock.IsRunning) return;
            _logic.CurrentMode = BlendMode.Loop;
            _loopModeBtn.SetActive(true);
            _rainModeBtn.SetActive(false);
            _cycleModeBtn.SetActive(false);
            _logic.SaveToBlendSettings();
            SettingsBlendController.ResetFull();
            if (BlendClock.IsRunning) BlendClock.Stop();
            return;
        }
        
        if (sender.IDstring == "RC_Mode_EndCycle")
        {
            if (!BlendClock.EditMode && BlendClock.IsRunning) return;
            _logic.CurrentMode = BlendMode.EndCycle;
            _loopModeBtn.SetActive(false);
            _rainModeBtn.SetActive(true);
            _cycleModeBtn.SetActive(false);
            _logic.SaveToBlendSettings();
            SettingsBlendController.ResetFull();
            if (BlendClock.IsRunning) BlendClock.Stop();
            return;
        }
        
        if (sender.IDstring == "RC_Mode_Cycle")
        {
            if (!BlendClock.EditMode && BlendClock.IsRunning) return;
            _logic.CurrentMode = BlendMode.Cycle;
            _loopModeBtn.SetActive(false);
            _rainModeBtn.SetActive(false);
            _cycleModeBtn.SetActive(true);
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
            _logic.SelectedModPath = message;
            _logic.SelectedModName = Path.GetFileName(_logic.SelectedModPath);
            _logic.SavedModName = _logic.SelectedModName;
            if (string.IsNullOrEmpty(_logic.SelectedModName)) _logic.SelectedModName = "Select Mod";
            _modButton.Text = _logic.SelectedModName;
            
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
            _logic.SelectedModPath = "";
            _logic.SelectedModName = "Select Mod";
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

    private void OpenModSelectPanel()
    {
        if (_modSelectPanel != null)
        {
            _modSelectPanel.ClosePanel();
            _modSelectPanel = null;
            return;
        }
        
        var mods = GetModsWithIllustrations();
        if (mods.Length == 0)
        {
            RSPlugin.log.LogWarning("[RegionPage] No mods with Illustrations folder found");
            return;
        }
        
        Vector2 panelPos = new Vector2(_modButton.pos.x + 10f, _modButton.pos.y - 150f);
        
        _modSelectPanel = new ModSelectPanel(owner, "RC_ModSelectPanel", this, panelPos, mods, _logic.SelectedModPath);
        subNodes.Add(_modSelectPanel);
        _modSelectPanel.Refresh();
    }
    
    private void OpenImageSelectPanel(int bkgIndex)
    {
        if (_imageSelectPanel != null)
        {
            _imageSelectPanel.ClosePanel();
            _imageSelectPanel = null;
            _editingBkgIndex = -1;
            return;
        }
        
        if (string.IsNullOrEmpty(_logic.SelectedModPath))
        {
            if (!string.IsNullOrEmpty(_logic.SavedModName))
            {
                _logic.SelectedModPath = ResolveModPathFromName(_logic.SavedModName);
            }
        }
        
        if (string.IsNullOrEmpty(_logic.SelectedModPath))
        {
            RSPlugin.log.LogWarning("[RegionPage] No mod selected");
            return;
        }
        
        string illustrationsPath = Path.Combine(_logic.SelectedModPath, "Illustrations");
        if (!Directory.Exists(illustrationsPath))
        {
            RSPlugin.log.LogWarning($"[RegionPage] Illustrations not found: {illustrationsPath}");
            return;
        }
        
        var images = Directory.GetFiles(illustrationsPath, "*.png")
            .Select(f => Path.GetFileName(f))
            .ToArray();
        
        if (images.Length == 0)
        {
            RSPlugin.log.LogWarning($"[RegionPage] No PNG images in {illustrationsPath}");
            return;
        }
        
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
    
    private string[] GetModsWithIllustrations()
    {
        var mods = new List<string>();
        foreach (var mod in ModManager.ActiveMods)
        {
            if (Directory.Exists(Path.Combine(mod.path, "Illustrations")))
                mods.Add(mod.path);
        }
        return mods.ToArray();
    }
    
    private string ResolveModPathFromName(string modName)
    {
        if (string.IsNullOrEmpty(modName) || ModManager.ActiveMods == null)
            return "";
        
        foreach (var mod in ModManager.ActiveMods)
        {
            if (string.Equals(Path.GetFileName(mod.path), modName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(mod.name, modName, StringComparison.OrdinalIgnoreCase))
            {
                return mod.path;
            }
        }
        return "";
    }

    public override void Update()
    {
        base.Update();
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
    }
}