using System;
using System.IO;
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
    private const float FIELD_WIDTH = 60f;
    
    private const float TRIGGER_ARROW_X = 100f;
    private const float TRIGGER_LABEL_X = 121f;
    private const float TRIGGER_ARROW2_X = 156f;
    
    private const float SETTING_ARROW_X = 100f;
    private const float SETTING_LABEL_X = 121f;
    private const float SETTING_ARROW2_X = 156f;
    private const float SETTING_Y = 103f;
    
    private const float WAITTIME_X = 100f;
    
    private const float ROW_CLOCK_Y = 180f;
    private const float ROW_MODE_Y = 155f;
    private const float ROW_IDLE_Y = 128f;
    private const float ROW_DURATION_Y = 103f;
    // Banda libre y=21..102 (eje y-up): 10 px bajo el recuadro Duration
    private const float ROW_ANCESTOR_Y = 77f;
    
    public const string DEFAULT_MOD_SENTINEL = "\0__RC_DEFAULT__";
    public const string DEFAULT_MOD_DISPLAY_NAME = "Default";
    
    public RCPanel ParentPanel { get; set; }
    private RegionLogic _logic;
    
    private ClockToggleButton _clockToggle;
    private EditModeButton _editModeButton;
    private Button _ancestorButton;

    private ArrowButton _modePrevArrow;
    private ArrowButton _modeNextArrow;
    private DevUILabel _modeLabel;
    private EditableFloatField _idleField;
    private EditableFloatField _durationField;
    
    private ArrowButton _triggerPrevArrow;
    private ArrowButton _triggerNextArrow;
    private DevUILabel _triggerLabel;
    private EditableFloatField _waitTimeField;
    
    private ArrowButton _settingPrevArrow;
    private ArrowButton _settingNextArrow;
    private DevUILabel _settingLabel;
    
    private DevUILabel _activeBlendFileLabel;

    public RCPanel_RegionPage(RCPanel parent)
        : base(parent.Owner, "RC_RegionPage_Internal", parent, Vector2.zero, parent.size)
    {
        ParentPanel = parent;
        _logic = new RegionLogic(this);
        _logic.OnSaved = UpdateBlendFileLabel;
        
        _logic.LoadFromBlendSettings();
        CreateContent();
    }

    private void CreateContent()
    {
        _clockToggle = new ClockToggleButton(owner, "RC_ClockToggle", this,
            new Vector2(MARGIN, ROW_CLOCK_Y), 60f, _logic.ClockEnabled);
        subNodes.Add(_clockToggle);
        
        _editModeButton = new EditModeButton(owner, "RC_EditMode", this,
            new Vector2(5f, 5f), 30f);
        subNodes.Add(_editModeButton);

        string blendFileName = Path.GetFileName(_logic.BlendSettingsPath ?? "").ToLowerInvariant();
        _activeBlendFileLabel = new DevUILabel(owner, "RC_ActiveBlendFile", this,
            new Vector2(40f, 5f), 170f, blendFileName);
        subNodes.Add(_activeBlendFileLabel);
        
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
        
        _ancestorButton = new Button(owner, "RC_AncestorFiles", this,
            new Vector2(MARGIN, ROW_ANCESTOR_Y), 70f, "Ancestor");
        subNodes.Add(_ancestorButton);
        
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

    private void UpdateBlendFileLabel()
    {
        if (_activeBlendFileLabel != null)
            _activeBlendFileLabel.Text = Path.GetFileName(_logic.BlendSettingsPath ?? "").ToLowerInvariant();
    }
    
    private void CycleSetting(int delta)
    {
        _logic.SettingValue += delta;
        if (_logic.SettingValue < 0) _logic.SettingValue = 4;
        if (_logic.SettingValue > 4) _logic.SettingValue = 0;
        UpdateSettingLabel();
        _logic.SaveToBlendSettings();
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
        
        if (sender.IDstring == "RC_AncestorFiles")
        {
            string regionName = owner.room?.world?.region?.name;
            if (!string.IsNullOrEmpty(regionName))
                AncestorResolver.EnsureAncestorFilesExist(regionName);
            return;
        }
        
        if (sender.IDstring == "RC_Mode_Prev")
        {
            if (!BlendClock.EditMode) return;
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
            if (!BlendClock.EditMode) return;
            _logic.CycleMode(1);
            UpdateModeLabel();
            UpdateTriggerLabel();
            _logic.SaveToBlendSettings();
            SettingsBlendController.ResetFull();
            if (BlendClock.IsRunning) BlendClock.Stop();
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
            if (!BlendClock.EditMode) return;
            CycleSetting(-1);
            return;
        }
        
        if (sender.IDstring == "RC_Setting_Next")
        {
            if (!BlendClock.EditMode) return;
            CycleSetting(1);
            return;
        }
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