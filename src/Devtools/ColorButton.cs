using DevInterface;
using UnityEngine;
using System.IO;
using RainCycles.Patches;

namespace FilesSetting;

public class SelectButton : Button
{
    private bool isSelected;

    public SelectButton(DevUI owner, string IDstring, DevUINode parentNode,
                        Vector2 pos, float width, string text, bool isSelected)
        : base(owner, IDstring, parentNode, pos, width, text)
    {
        this.isSelected = isSelected;
        SetSelected();
    }

    public override void Update()
    {
        base.Update();
        SetSelected();
    }

    public override void Clicked()
    {
        base.Clicked();
        if (!isSelected)
        {
            if (parentNode != null)
            {
                foreach (var node in parentNode.subNodes)
                {
                    if (node is SelectButton otherButton && otherButton != this)
                        otherButton.Deselect();
                }
            }
            isSelected = true;
            SetSelected();
        }
    }

    public void Deselect()
    {
        isSelected = false;
        SetSelected();
    }

    public void Select()
    {
        isSelected = true;
        if (parentNode != null)
        {
            foreach (var node in parentNode.subNodes)
            {
                if (node is SelectButton otherButton && otherButton != this)
                    otherButton.Deselect();
            }
        }
        SetSelected();
    }

    private void SetSelected()
    {
        this.colorA = isSelected ? new Color(0.2f, 0.6f, 0.2f) : new Color(1f, 1f, 1f);
    }
}

// ──────────────────────────────────────────────────────────────────────────

public class ModeButton : Button
{
    private static readonly Color COLOR_ACTIVE   = new Color(0.2f, 0.7f, 0.3f);
    private static readonly Color COLOR_INACTIVE = new Color(1f, 1f, 1f);

    public readonly BlendMode Mode;
    private bool _isActive;

    public ModeButton(DevUI owner, string IDstring, DevUINode parentNode,
                      Vector2 pos, float width, BlendMode mode, bool isActive)
        : base(owner, IDstring, parentNode, pos, width, ModeLabel(mode))
    {
        Mode = mode;
        _isActive = isActive;
        UpdateVisual();
    }

    public void SetActive(bool active)
    {
        _isActive = active;
        UpdateVisual();
    }

    private void UpdateVisual()
    {
        this.colorA = _isActive ? COLOR_ACTIVE : COLOR_INACTIVE;
    }

    private static string ModeLabel(BlendMode mode)
    {
        switch (mode)
        {
            case BlendMode.Loop:     return "Loop";
            case BlendMode.EndCycle: return "Rain";
            case BlendMode.Cycle:    return "Cycle";
            default:                 return mode.ToString();
        }
    }
}

// ──────────────────────────────────────────────────────────────────────────

public class EditModeButton : Button
{
    private static readonly Color COLOR_ON  = new Color(0.2f, 0.7f, 0.3f);
    private static readonly Color COLOR_OFF = new Color(1f, 1f, 1f);

    public EditModeButton(DevUI owner, string IDstring, DevUINode parentNode,
                          Vector2 pos, float width)
        : base(owner, IDstring, parentNode, pos, width, "Edit")
    {
        UpdateVisual();
    }

    public override void Clicked()
    {
        base.Clicked();
        BlendClock.SetEditMode(!BlendClock.EditMode);
        UpdateVisual();
    }

    public override void Update()
    {
        base.Update();
        UpdateVisual();
    }

    private void UpdateVisual()
    {
        this.colorA = BlendClock.EditMode ? COLOR_ON : COLOR_OFF;
    }
}

// ──────────────────────────────────────────────────────────────────────────

public class SkyTypeButton : Button
{
    private static readonly Color COLOR_ACTIVE   = new Color(0.2f, 0.7f, 0.3f);
    private static readonly Color COLOR_INACTIVE = new Color(1f, 1f, 1f);

    public readonly ViewType Type;
    private string _roomName;

    public SkyTypeButton(DevUI owner, string IDstring, DevUINode parentNode,
                         Vector2 pos, float width, ViewType type, string roomName)
        : base(owner, IDstring, parentNode, pos, width,
               type == ViewType.ACV ? "ACV" : (type == ViewType.RTV ? "RTV" : "PSV"))
    {
        Type = type;
        _roomName = roomName;
        UpdateVisual();
    }

    public override void Update()
    {
        base.Update();
        UpdateVisual();
    }

    public void Refresh(ViewType currentView)
    {
        this.colorA = currentView == Type ? COLOR_ACTIVE : COLOR_INACTIVE;
    }

    private void UpdateVisual()
    {
        Room room = null;
        var devUI = this.owner as DevUI;
        if (devUI?.room != null)
            room = devUI.room;

        if (room != null)
        {
            // Leer de memoria (RoomSettingsExtensions) en lugar del archivo
            var rs = room.roomSettings;
            if (rs != null)
            {
                ViewType currentView = rs.GetViewType();
                this.colorA = currentView == Type ? COLOR_ACTIVE : COLOR_INACTIVE;
                return;
            }
        }

        this.colorA = COLOR_INACTIVE;
    }
}

// ──────────────────────────────────────────────────────────────────────────
// RC_TYPE Button - Ahora lee de memoria (RoomSettingsExtensions)
// ──────────────────────────────────────────────────────────────────────────

public class RcTypeButton : Button
{
    private static readonly Color COLOR_ACTIVE   = new Color(0.2f, 0.7f, 0.3f);
    private static readonly Color COLOR_INACTIVE = new Color(1f, 1f, 1f);

    public readonly RcType Type;
    private bool _isActive;

    public RcTypeButton(DevUI owner, string IDstring, DevUINode parentNode,
                        Vector2 pos, float width, string text, bool isActive)
        : base(owner, IDstring, parentNode, pos, width, text)
    {
        Type = text.ToUpperInvariant() switch
        {
            "STATIC" => RcType.Static,
            "BLEND" => RcType.Blend,
            _ => RcType.None  // Vanilla
        };
        _isActive = isActive;
        UpdateVisual();
    }

    public void SetActive(bool active)
    {
        _isActive = active;
        UpdateVisual();
    }

    public override void Update()
    {
        base.Update();
        UpdateVisual();
    }

    private void UpdateVisual()
    {
        // ================================================================
        // LEER DE MEMORIA (RoomSettingsExtensions) en lugar del archivo
        // ================================================================
        Room room = null;
        var devUI = this.owner as DevUI;
        if (devUI?.room != null)
            room = devUI.room;

        RcType currentRcType = RcType.None;

        if (room != null)
        {
            var rs = room.roomSettings;
            if (rs != null && rs.HasRcType())
            {
                currentRcType = rs.GetRcType();
            }
        }

        bool shouldBeActive = (Type == RcType.Static && currentRcType == RcType.Static) ||
                              (Type == RcType.Blend && currentRcType == RcType.Blend) ||
                              (Type == RcType.None && currentRcType == RcType.None);

        this.colorA = shouldBeActive ? COLOR_ACTIVE : COLOR_INACTIVE;
    }
}