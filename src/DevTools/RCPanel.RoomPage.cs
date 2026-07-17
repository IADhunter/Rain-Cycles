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
    }

    // ════════════════════════════════════════════════════════════════════
    // GetRcTypeFromCurrentFile lee roomSettings.GetRcType() primero. Esto
    // ya queda correctamente sincronizado por RoomSettingsPatches.OnLoad
    // (hookeado a RoomSettings.Load_Timeline) cada vez que ApplyStateA()
    // o Signal() llaman a owner.room.roomSettings.Load(...) — ese hook
    // hace ClearExtendedData() + reparsea "RainCycles:" del archivo que
    // se acaba de asignar a self.filePath. No hace falta releer el disco
    // aquí para "confirmar"; el fallback de disco solo cubre el caso raro
    // de que roomSettings aún no tenga nada cargado en memoria.
    // ════════════════════════════════════════════════════════════════════
    private RcType GetRcTypeFromCurrentFile()
    {
        var room = ParentPanel.CurrentRoom;
        if (room == null) return RcType.None;

        var roomSettings = room.roomSettings;
        if (roomSettings != null)
        {
            RcType memoryType = roomSettings.GetRcType();
            if (memoryType != RcType.None)
                return memoryType;
        }

        string filePath = roomSettings?.filePath;
        if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
            return RcType.None;

        var snap = SettingsSnapshot.FromFile(filePath);
        if (snap == null) return RcType.None;

        return snap.HasRcType ? snap.RcType : RcType.None;
    }

    // ════════════════════════════════════════════════════════════════════
    // FIX: público para que RCPanel pueda llamarlo tras ApplyStateA()/
    // Signal() (RCA_), y así la UI se actualice al valor real ya
    // sincronizado en memoria por el hook OnLoad.
    // ════════════════════════════════════════════════════════════════════
    public void RefreshButtons()
    {
        RcType current = GetRcTypeFromCurrentFile();

        if (_staticTypeBtn != null)
            _staticTypeBtn.SetActive(current == RcType.Static);
        if (_blendTypeBtn != null)
            _blendTypeBtn.SetActive(current == RcType.Blend);
        if (_vanillaTypeBtn != null)
            _vanillaTypeBtn.SetActive(current == RcType.None);
    }

    public void Signal(DevUISignalType type, DevUINode sender, string message)
    {
        if (type != DevUISignalType.ButtonClick) return;

        if (sender.IDstring == "RC_Type_Static")
        {
            if (!BlendClock.EditMode && BlendClock.IsRunning) return;
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
            if (!BlendClock.EditMode && BlendClock.IsRunning) return;
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
            if (!BlendClock.EditMode && BlendClock.IsRunning) return;
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