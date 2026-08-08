using DevInterface;
using UnityEngine;
using System.IO;
using RainCycles.Patches;
using RainCycles.Snapshot;

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
        // Mismo guard que RCPanel.Signal: sin edit mode y con el reloj corriendo,
        // el click no debe seleccionar (evita que el botón se ponga verde).
        if (!BlendClock.EditMode && BlendClock.IsRunning) return;
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

// ════════════════════════════════════════════════════════════════════════
// EditModeButton - MODIFICADO
// ════════════════════════════════════════════════════════════════════════
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

        var panel = parentNode as RCPanel;

        if (BlendClock.EditMode)
        {
            // ════════════════════════════════════════════════════════════════
            // DESACTIVAR EDIT MODE: restaurar al estado original del ciclo
            // ════════════════════════════════════════════════════════════════
            if (panel != null)
            {
                panel.ResetToCycleState();
            }
            BlendClock.SetEditMode(false);
        }
        else
        {
            // ════════════════════════════════════════════════════════════════
            // ACTIVAR EDIT MODE: activar modo edición y seleccionar estado actual
            // ════════════════════════════════════════════════════════════════
            if (panel != null)
            {
                // 1. Activar modo edición
                BlendClock.SetEditMode(true);
                
                // 2. Obtener el estado actual del ciclo
                int currentState = StateFileResolver.GetCurrentCycleState();
                if (currentState < 1 || currentState > 4) currentState = 1;
                
                // 3. Buscar el botón correspondiente y simular click
                foreach (var node in panel.subNodes)
                {
                    if (node is SelectButton btn && btn.IDstring == $"RCA_{currentState}")
                    {
                        btn.Clicked();
                        break;
                    }
                }
            }
            else
            {
                BlendClock.SetEditMode(true);
            }
        }

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
    private bool _isActive;

    public SkyTypeButton(DevUI owner, string IDstring, DevUINode parentNode,
                         Vector2 pos, float width, ViewType type, bool isActive)
        : base(owner, IDstring, parentNode, pos, width,
               type == ViewType.ACV ? "ACV" : (type == ViewType.RTV ? "RTV" : "PSV"))
    {
        Type = type;
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
    }

    private void UpdateVisual()
    {
        this.colorA = _isActive ? COLOR_ACTIVE : COLOR_INACTIVE;
    }
}

// ──────────────────────────────────────────────────────────────────────────
// RC_TYPE Button - usa _isActive como fuente de verdad
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
            _ => RcType.None
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
    }

    private void UpdateVisual()
    {
        this.colorA = _isActive ? COLOR_ACTIVE : COLOR_INACTIVE;
    }
}