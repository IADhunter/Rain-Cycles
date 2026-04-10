using DevInterface;

namespace FilesSetting;

public class SelectButton : Button
{
    private bool isSelected;
    public SelectButton(DevUI owner, string IDstring, DevUINode parentNode, Vector2 pos, float width, string text, bool isSelected) : base(owner, IDstring, parentNode, pos, width, text)
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
                // Who care about performance if we have few buttons
                foreach (var node in parentNode.subNodes)
                {
                    if (node is SelectButton otherButton && otherButton != this)
                    {
                        otherButton.Deselect();
                    }
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
            // Who care about performance if we have few buttons
            foreach (var node in parentNode.subNodes)
            {
                if (node is SelectButton otherButton && otherButton != this)
                {
                    otherButton.Deselect();
                }
            }
        }
        isSelected = true;
        SetSelected();
    }

    private void SetSelected()
    {
        if (isSelected)
            this.colorA = new Color(0.2f, 0.6f, 0.2f);
        else
            this.colorA = new Color(1f, 1f, 1f);
    }
}

// ════════════════════════════════════════════════════════════════════════
// ROOM TOGGLE BUTTON
// Botón que indica si la sala actual está registrada en blend_settings.txt.
// Verde = registrada, blanco = no registrada.
// ════════════════════════════════════════════════════════════════════════
public class RoomToggleButton : Button
{
    private static readonly Color COLOR_REGISTERED   = new Color(0.2f, 0.7f, 0.3f);  // verde
    private static readonly Color COLOR_UNREGISTERED = new Color(1f,   1f,   1f);     // blanco

    private bool _isRegistered;

    public RoomToggleButton(DevUI owner, string IDstring, DevUINode parentNode,
                            Vector2 pos, float width, bool isRegistered)
        : base(owner, IDstring, parentNode, pos, width,
               isRegistered ? "Room: ON" : "Room: OFF")
    {
        _isRegistered = isRegistered;
        UpdateVisual();
    }

    public void SetRegistered(bool registered)
    {
        _isRegistered = registered;
        this.Text     = registered ? "Room: ON" : "Room: OFF";
        UpdateVisual();
    }

    private void UpdateVisual()
    {
        this.colorA = _isRegistered ? COLOR_REGISTERED : COLOR_UNREGISTERED;
    }
}

// ════════════════════════════════════════════════════════════════════════
// MODE BUTTON
// Botón de selección de modo (Loop/Cycle/EndCycle/Custom).
// Verde = modo activo, blanco = inactivo.
// Mutuamente excluyente con otros ModeButton del mismo panel —
// la exclusión la gestiona RCPanel en Signal, no el botón mismo.
// ════════════════════════════════════════════════════════════════════════
public class ModeButton : Button
{
    private static readonly Color COLOR_ACTIVE   = new Color(0.2f, 0.7f, 0.3f);
    private static readonly Color COLOR_INACTIVE = new Color(1f,   1f,   1f);

    public readonly BlendMode Mode;
    private bool _isActive;

    public ModeButton(DevUI owner, string IDstring, DevUINode parentNode,
                      Vector2 pos, float width, BlendMode mode, bool isActive)
        : base(owner, IDstring, parentNode, pos, width, ModeLabel(mode))
    {
        Mode      = mode;
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
            case BlendMode.Cycle:    return "Cycle";
            case BlendMode.EndCycle: return "EndCyc";
            case BlendMode.Custom:   return "Custom";
            default:                 return mode.ToString();
        }
    }
}

// ════════════════════════════════════════════════════════════════════════
// EDIT MODE BUTTON
// Toggle global que suspende el clock y habilita los sliders para edición
// manual libre, independientemente del modo configurado.
// Verde = Edit Mode activo, blanco = modo automático normal.
// ════════════════════════════════════════════════════════════════════════
public class EditModeButton : Button
{
    private static readonly Color COLOR_ON  = new Color(0.2f, 0.7f, 0.3f);  // verde
    private static readonly Color COLOR_OFF = new Color(1f,   1f,   1f);     // blanco

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

// ════════════════════════════════════════════════════════════════════════
// SKY TYPE BUTTON
// Botón toggle que asigna o quita el tipo de cielo (RTV/ACV) a la sala.
// Verde = tipo activo (escrito en [ROOMS] para esta sala).
// Blanco = inactivo.
// Mutuamente exclusivo con el otro SkyTypeButton del mismo panel:
// si se activa ACV, RTV se desactiva automáticamente y viceversa.
// Si se pulsa el botón ya activo, el sufijo se elimina (→ None).
// ════════════════════════════════════════════════════════════════════════
public class SkyTypeButton : Button
{
    private static readonly Color COLOR_ACTIVE   = new Color(0.2f, 0.7f, 0.3f);
    private static readonly Color COLOR_INACTIVE = new Color(1f,   1f,   1f);

    public readonly SkyType Type;
    private string _roomName;

    public SkyTypeButton(DevUI owner, string IDstring, DevUINode parentNode,
                         Vector2 pos, float width, SkyType type, string roomName)
        : base(owner, IDstring, parentNode, pos, width,
               type == SkyType.ACV ? "ACV" : "RTV")
    {
        Type      = type;
        _roomName = roomName;
        UpdateVisual();
    }

    public override void Update()
    {
        base.Update();
        UpdateVisual();
    }

    /// <summary>Fuerza el refresco visual sin lógica de click.</summary>
    public void Refresh(SkyType currentSky)
    {
        this.colorA = currentSky == Type ? COLOR_ACTIVE : COLOR_INACTIVE;
    }

    private void UpdateVisual()
    {
        var settings = BlendSettingsLoader.Active;
        SkyType current = settings != null
            ? settings.GetSkyType(_roomName)
            : SkyType.None;
        this.colorA = current == Type ? COLOR_ACTIVE : COLOR_INACTIVE;
    }
}