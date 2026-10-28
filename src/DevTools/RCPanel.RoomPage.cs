using DevInterface;
using UnityEngine;
using RainCycles.Snapshot;
using RainCycles.Core;
using RainCycles.Patches;

namespace FilesSetting;

public partial class RCPanel_RoomPage : RectangularDevUINode, IDevUISignals
{
    private const float RC_TYPE_BTN_W = 35f;
    private const float BUTTON_SPACING = 5f;
    private const float MARGIN = 5f;
    private const float REGION_ROW_Y = 30f;

    private const float STATE_LABEL_X = 5f;
    private const float STATE_LABEL_W = 34f;
    private const float STATE_FIELD_X = 41f;
    private const float STATE_FIELD_W = 20f;

    public RCPanel ParentPanel { get; set; }

    private RcTypeButton _staticTypeBtn;
    private RcTypeButton _blendTypeBtn;
    private RcTypeButton _vanillaTypeBtn;

    private DevUILabel _stateLabel;
    private EditableIntField _stateField;

    public RCPanel_RoomPage(RCPanel parent)
        : base(parent.Owner, "RC_RoomPage_Internal", parent, Vector2.zero, parent.size)
    {
        ParentPanel = parent;
        CreateContent();
    }

    private void CreateContent()
    {
        RcType currentRcType = GetRcTypeFromCurrentFile();

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

        CreateStateSelector();
    }

    // SELECTOR DE STATE — se escribe en disco al instante (excepción de sala)

    private void CreateStateSelector()
    {
        _stateLabel = new DevUILabel(owner, "RC_State_Label", this,
            new Vector2(STATE_LABEL_X, REGION_ROW_Y), STATE_LABEL_W, "State:");

        _stateField = new EditableIntField(owner, "RC_State_Field", this,
            new Vector2(STATE_FIELD_X, REGION_ROW_Y), STATE_FIELD_W,
            StateFileResolver.GetBaseState(ParentPanel.CurrentRoomName), 0, 4);
        _stateField.OnSubmit = (int newValue) => {
            if (!BlendClock.EditMode) return;
            ApplyStateValue(newValue);
        };

        subNodes.Add(_stateLabel);
        subNodes.Add(_stateField);
    }

    private void SyncStateField()
    {
        if (_stateField == null) return;
        if (RCStringControl.Active == _stateField) return;

        int state = StateFileResolver.GetBaseState(ParentPanel.CurrentRoomName);
        if (_stateField.Value != state)
            _stateField.SetValue(state);
    }

    public override void Update()
    {
        base.Update();
        SyncStateField();
    }

    private void ApplyStateValue(int next)
    {
        string roomName = ParentPanel.CurrentRoomName;
        if (string.IsNullOrEmpty(roomName)) return;
        if (next < 0 || next > 4) return;

        int current = StateFileResolver.GetBaseState(roomName);
        if (next == current) return;
        if (!StateFileResolver.WriteBaseState(roomName, next)) return;

        if (next >= 1)
        {
            // El base toma el puesto: el numerado de ese número se borra de inmediato
            string numbered = StateFileResolver.ResolveSettingsPath(roomName, next);
            if (numbered != null && !StateFileResolver.IsBaseSettingsFile(numbered) && System.IO.File.Exists(numbered))
            {
                try
                {
                    System.IO.File.Delete(numbered);
                }
                catch (System.Exception ex)
                {
                    RSPlugin.log.LogWarning($"[RCPanel] No se pudo borrar {numbered}: {ex.Message}");
                }

                StateFileResolver.InvalidatePathCache();
                BkgResolver.InvalidatePaths(roomName);
            }

            RCPanel.ButtonSelectedA = next;
            ParentPanel.RebuildStateButtons();
            ParentPanel.ApplyStateA();
        }
        else
        {
            // Sin State: el base deja de ser estado; se recarga si era el archivo cargado
            var rs = ParentPanel.CurrentRoom?.roomSettings;
            if (rs != null && StateFileResolver.IsBaseSettingsFile(rs.filePath))
                ParentPanel.ReloadCurrentSettings();

            ParentPanel.EnsureValidSelection();
            ParentPanel.RebuildStateButtons();
            ParentPanel.ApplyStateA();
        }

        SyncStateField();
    }

    private bool IsBaseWithoutState()
    {
        var roomSettings = ParentPanel.CurrentRoom?.roomSettings;
        if (roomSettings == null) return false;
        if (!StateFileResolver.IsBaseSettingsFile(roomSettings.filePath)) return false;
        return StateFileResolver.GetBaseState(ParentPanel.CurrentRoomName) == 0;
    }

    private RcType GetRcTypeFromCurrentFile()
    {
        var room = ParentPanel.CurrentRoom;
        if (room == null) return RcType.None;

        var roomSettings = room.roomSettings;

        if (roomSettings != null && roomSettings.IsRcStateLoaded())
            return roomSettings.GetRcType();

        string filePath = roomSettings?.filePath;
        if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
            return RcType.None;

        var snap = SettingsSnapshot.FromFile(filePath);
        if (snap == null) return RcType.None;

        return snap.HasRcType ? snap.RcType : RcType.None;
    }

    public void RefreshButtons()
    {
        RcType current = GetRcTypeFromCurrentFile();

        if (_staticTypeBtn != null)
            _staticTypeBtn.SetActive(current == RcType.Static);
        if (_blendTypeBtn != null)
            _blendTypeBtn.SetActive(current == RcType.Blend);
        if (_vanillaTypeBtn != null)
            _vanillaTypeBtn.SetActive(current == RcType.None);

        SyncStateField();
    }

    public void Signal(DevUISignalType type, DevUINode sender, string message)
    {
        if (type != DevUISignalType.ButtonClick) return;

        // BASE SIN STATE: Rain Cycles no escribe en él
        if (IsBaseWithoutState()) return;

        if (sender.IDstring == "RC_Type_Static")
        {
            if (!BlendClock.EditMode) return;
            var roomSettings = ParentPanel.CurrentRoom.roomSettings;
            roomSettings.SetRcType(RcType.Static);
            var snap = SettingsSnapshot.FromFile(roomSettings.filePath);
            SettingsBlendController.SetActiveSnapshot(snap);
            ParentPanel.ApplyTintsFromSnapshot(snap);
            RefreshButtons();
            return;
        }

        if (sender.IDstring == "RC_Type_Blend")
        {
            if (!BlendClock.EditMode) return;
            var roomSettings = ParentPanel.CurrentRoom.roomSettings;
            roomSettings.SetRcType(RcType.Blend);
            var snap = SettingsSnapshot.FromFile(roomSettings.filePath);
            SettingsBlendController.SetActiveSnapshot(snap);
            ParentPanel.ApplyTintsFromSnapshot(snap);
            RefreshButtons();
            return;
        }

        if (sender.IDstring == "RC_Type_Vanilla")
        {
            if (!BlendClock.EditMode) return;
            var roomSettings = ParentPanel.CurrentRoom.roomSettings;
            roomSettings.ClearExtendedData();
            var snap = SettingsSnapshot.FromFile(roomSettings.filePath);
            SettingsBlendController.SetActiveSnapshot(snap);
            ParentPanel.ApplyTintsFromSnapshot(snap);
            RefreshButtons();
            return;
        }
    }
}
