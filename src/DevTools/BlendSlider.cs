using DevInterface;
using UnityEngine;

namespace FilesSetting;

// Slider único de blend — estilo visual vanilla.
public class BlendSlider : PositionedDevUINode, IDevUISignals
{
    public static float BlendFactor = 0f;

    private const float SLIDER_WIDTH   = 100f;
    private const float HEIGHT         = 16f;
    private const float NUB_WIDTH      = 8f;
    private const float CLEAR_WIDTH    = 30f;
    private const float LABEL_WIDTH    = 22f;
    private const float GAP            = 5f;

    // Clear pegado a la derecha, luego slider, luego label
    private const float LABEL_X   = 43f;
    private const float SLIDER_X  = 70f;
    private const float CLEAR_X   = 175f;

    private float SliderStartX => absPos.x + SLIDER_X;

    private DevUILabel _label;
    private bool _dragging  = false;
    private bool _wasMoving = false;
    private bool _locked    = false;
    private RCPanel _panel;

    public BlendSlider(DevUI owner, string IDstring, DevUINode parentNode, Vector2 pos)
        : base(owner, IDstring, parentNode, pos)
    {
        _panel = parentNode as RCPanel;

        subNodes.Add(new DevUILabel(owner, IDstring + "_Title", this,
            new Vector2(0f, 0f), 33f, "Blend"));

        _label = new DevUILabel(owner, IDstring + "_Label", this,
            new Vector2(LABEL_X, 0f), LABEL_WIDTH, "0");
        subNodes.Add(_label);

        subNodes.Add(new Button(owner, IDstring + "_Reset", this,
            new Vector2(CLEAR_X, 0f), CLEAR_WIDTH, "Clear"));

        // Barra de fondo
        fSprites.Add(new FSprite("pixel"));
        fSprites[0].scaleX = SLIDER_WIDTH;
        fSprites[0].scaleY = HEIGHT;
        fSprites[0].anchorX = 0f;
        fSprites[0].anchorY = 0f;
        fSprites[0].color = new Color(1f, 1f, 1f);
        fSprites[0].alpha = 0.5f;
        Futile.stage.AddChild(fSprites[0]);

        // Línea indicadora
        fSprites.Add(new FSprite("pixel"));
        fSprites[1].scaleX = SLIDER_WIDTH;
        fSprites[1].scaleY = 2f;
        fSprites[1].anchorX = 0f;
        fSprites[1].anchorY = 0f;
        fSprites[1].color = new Color(0f, 0f, 0f);
        Futile.stage.AddChild(fSprites[1]);

        // Nub
        fSprites.Add(new FSprite("pixel"));
        fSprites[2].scaleX = NUB_WIDTH;
        fSprites[2].scaleY = HEIGHT;
        fSprites[2].anchorX = 0f;
        fSprites[2].anchorY = 0f;
        fSprites[2].color = new Color(0f, 0f, 0f);
        Futile.stage.AddChild(fSprites[2]);
    }

    public void Signal(DevUISignalType type, DevUINode sender, string message)
    {
        if (sender.IDstring == IDstring + "_Reset")
        {
            if (!BlendClock.EditMode && BlendClock.IsRunning) return;
            BlendFactor = 0f;
            _wasMoving  = false;
            SettingsBlendController.Detach();
            // Clear manual - mantiene el estado actual
            _panel?.ClearBlendOnly();
        }
    }

    public void SetLocked(bool locked)
    {
        _locked = locked;
        if (locked) SetDisplayT(0f);
    }

    public void SetDisplayT(float t)
    {
        BlendFactor = t;
        RefreshLabel();
        Refresh();
    }

    public void SetExternalT(float t)
    {
        BlendFactor = t;
        RefreshLabel();
        Refresh();
    }

    private void RefreshLabel()
    {
        _label.Text = Mathf.RoundToInt(BlendFactor * 100f) + "%";
    }

    public override void Update()
    {
        base.Update();

        if (!BlendClock.EditMode)
        {
            if (BlendClock.IsRunning) return;
            var s = BlendSettingsLoader.Active;
            if (s != null && (s.Mode == BlendMode.Cycle || s.Mode == BlendMode.EndCycle)) return;
        }
        if (_locked && !BlendClock.EditMode) return;

        float sliderStartX = SliderStartX;
        Vector2 mPos = owner.mousePos;

        bool over = mPos.x >= sliderStartX && mPos.x <= sliderStartX + SLIDER_WIDTH &&
                    mPos.y >= absPos.y && mPos.y <= absPos.y + HEIGHT;

        if (_dragging)
            fSprites[2].color = new Color(0f, 0f, 1f);
        else if (over)
            fSprites[2].color = new Color(1f, 0f, 0f);
        else
            fSprites[2].color = new Color(0f, 0f, 0f);

        if (owner.mouseClick && over)
            _dragging = true;
        if (_dragging && !owner.mouseDown)
            _dragging = false;

        if (!_dragging) return;

        float newT = Mathf.Clamp01((mPos.x - sliderStartX) / (SLIDER_WIDTH - NUB_WIDTH));

        // Iniciar la fase si es la primera vez que se mueve desde 0
        if (!_wasMoving && newT > 0f) 
            _panel?.OnSliderStarted();
        
        BlendFactor = newT;
        _label.Text = Mathf.RoundToInt(newT * 100f) + "%";
        _wasMoving = newT > 0f;
        
        _panel?.OnSliderMoved(newT);
        
        Refresh();
    }

    public override void Refresh()
    {
        base.Refresh();
        float sliderStartX = SliderStartX;
        float nubX = sliderStartX + BlendFactor * (SLIDER_WIDTH - NUB_WIDTH);
        
        MoveSprite(0, new Vector2(sliderStartX, absPos.y));
        MoveSprite(1, new Vector2(sliderStartX, absPos.y + 7f));
        MoveSprite(2, new Vector2(nubX, absPos.y));
    }

    public static void Reset() { BlendFactor = 0f; }
}