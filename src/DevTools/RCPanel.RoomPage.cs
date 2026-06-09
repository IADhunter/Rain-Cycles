using DevInterface;
using UnityEngine;
using RainCycles.Snapshot;
using RainCycles.Core;
using RainCycles.Patches;

namespace FilesSetting;

public class RCPanel_RoomPage : RectangularDevUINode, IDevUISignals
{
    private const float RC_TYPE_BTN_W = 35f;
    private const float BUTTON_SPACING = 5f;
    private const float MARGIN = 5f;
    private const float REGION_ROW_Y = 30f;

    public RCPanel ParentPanel { get; set; }

    private RcTypeButton _staticTypeBtn;
    private RcTypeButton _blendTypeBtn;
    private RcTypeButton _vanillaTypeBtn;

    public RCPanel_RoomPage(RCPanel parent)
        : base(parent.Owner, "RC_RoomPage_Internal", parent, Vector2.zero, parent.size)
    {
        ParentPanel = parent;
        CreateContent();
    }

    private void CreateContent()
    {
        RcType currentRcType = ParentPanel.CurrentRoom.roomSettings.HasRcType() 
            ? ParentPanel.CurrentRoom.roomSettings.GetRcType() 
            : RcType.None;

        float rcTypeStartX = size.x - MARGIN - (RC_TYPE_BTN_W * 3 + BUTTON_SPACING * 2);

        _staticTypeBtn = new RcTypeButton(owner, "RC_Type_Static", this,
            new Vector2(rcTypeStartX, REGION_ROW_Y), RC_TYPE_BTN_W, "Static", currentRcType == RcType.Static);
        _blendTypeBtn = new RcTypeButton(owner, "RC_Type_Blend", this,
            new Vector2(rcTypeStartX + RC_TYPE_BTN_W + BUTTON_SPACING, REGION_ROW_Y), RC_TYPE_BTN_W, "Blend", currentRcType == RcType.Blend);
        _vanillaTypeBtn = new RcTypeButton(owner, "RC_Type_Vanilla", this,
            new Vector2(rcTypeStartX + (RC_TYPE_BTN_W + BUTTON_SPACING) * 2, REGION_ROW_Y), RC_TYPE_BTN_W, "Vanilla", currentRcType == RcType.None);

        subNodes.Add(_staticTypeBtn);
        subNodes.Add(_blendTypeBtn);
        subNodes.Add(_vanillaTypeBtn);
    }

    public void Signal(DevUISignalType type, DevUINode sender, string message)
    {
        if (type != DevUISignalType.ButtonClick) return;

        if (sender.IDstring == "RC_Type_Static")
        {
            if (!BlendClock.EditMode && BlendClock.IsRunning) return;
            ParentPanel.CurrentRoom.roomSettings.SetRcType(RcType.Static);
            var snap = SettingsSnapshot.FromFile(ParentPanel.CurrentRoom.roomSettings.filePath);
            SettingsBlendController.SetActiveSnapshot(snap);
            ParentPanel.ApplyTintsFromSnapshot(snap);
            RefreshButtons();
            return;
        }

        if (sender.IDstring == "RC_Type_Blend")
        {
            if (!BlendClock.EditMode && BlendClock.IsRunning) return;
            ParentPanel.CurrentRoom.roomSettings.SetRcType(RcType.Blend);
            var snap = SettingsSnapshot.FromFile(ParentPanel.CurrentRoom.roomSettings.filePath);
            SettingsBlendController.SetActiveSnapshot(snap);
            ParentPanel.ApplyTintsFromSnapshot(snap);
            RefreshButtons();
            return;
        }

        if (sender.IDstring == "RC_Type_Vanilla")
        {
            if (!BlendClock.EditMode && BlendClock.IsRunning) return;
            ParentPanel.CurrentRoom.roomSettings.SetRcType(RcType.None);
            ParentPanel.CurrentRoom.roomSettings.SetViewType(ViewType.None);
            ParentPanel.CurrentRoom.roomSettings.SetTintMultiply(null);
            ParentPanel.CurrentRoom.roomSettings.SetTintAtmosphere(null);
            ParentPanel.CurrentRoom.roomSettings.SetTintCloudAtmosphere(null);
            var snap = SettingsSnapshot.FromFile(ParentPanel.CurrentRoom.roomSettings.filePath);
            SettingsBlendController.SetActiveSnapshot(snap);
            ParentPanel.ApplyTintsFromSnapshot(snap);
            RefreshButtons();
            return;
        }
    }

    private void RefreshButtons()
    {
        RcType current = ParentPanel.CurrentRoom.roomSettings.HasRcType() 
            ? ParentPanel.CurrentRoom.roomSettings.GetRcType() 
            : RcType.None;
        
        if (_staticTypeBtn != null) _staticTypeBtn.SetActive(current == RcType.Static);
        if (_blendTypeBtn != null) _blendTypeBtn.SetActive(current == RcType.Blend);
        if (_vanillaTypeBtn != null) _vanillaTypeBtn.SetActive(current == RcType.None);
    }

    public override void Update()
    {
        base.Update();
        RefreshButtons();
    }
}