using DevInterface;
using UnityEngine;

namespace FilesSetting;

/// <summary>
/// Slider A — Cycle/EndCycle: blend 0→100%
/// Slider B — Loop/Custom:   full trip 0→100% (first half 0→50%, pause, second half 50→100%)
/// </summary>
public class BlendSlider : PositionedDevUINode, IDevUISignals
{
    public static float BlendFactor  = 0f;  // Slider A
    public static float BlendFactorB = 0f;  // Slider B

    private const float SLIDER_WIDTH  = 160f;
    private const float HEIGHT        = 15f;
    private const float RESET_WIDTH   = 25f;
    private const float RESET_SPACING = 5f;

    private DevUILabel _label;
    private bool _dragging  = false;
    private bool _wasMoving = false;
    private bool _isB;
    private bool _locked    = false;
    private RCPanel _panel;

    private static string _activeDrag = null;

    public BlendSlider(DevUI owner, string IDstring, DevUINode parentNode, Vector2 pos)
        : base(owner, IDstring, parentNode, pos)
    {
        _panel = parentNode as RCPanel;
        _isB   = IDstring.EndsWith("B");

        _label = new DevUILabel(owner, IDstring + "_Label", this,
            new Vector2(0f, 0f), (int)SLIDER_WIDTH, _isB ? "Blend B: 0%" : "Blend A: 0%");
        subNodes.Add(_label);

        if (!_isB)
            subNodes.Add(new Button(owner, IDstring + "_Reset", this,
                new Vector2(SLIDER_WIDTH + RESET_SPACING, 0f), RESET_WIDTH, "R"));
    }

    public void Signal(DevUISignalType type, DevUINode sender, string message)
    {
        if (sender.IDstring == IDstring + "_Reset")
        {
            BlendFactor = BlendFactorB = 0f;
            _wasMoving  = false;
            SettingsBlendController.Detach();
            _panel?.ResetRelaySystem();
        }
    }

    public void SetLocked(bool locked)
    {
        _locked = locked;
        if (locked) SetDisplayT(0f);
    }

    public void SetDisplayT(float t)
    {
        if (_isB) BlendFactorB = t;
        else      BlendFactor  = t;
        _label.Text = (_isB ? "Blend B: " : "Blend A: ") + Mathf.RoundToInt(t * 100f) + "%";
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

        Vector2 mPos = owner.mousePos;
        Vector2 aPos = absPos;
        bool down = owner.mouseDown;

        bool over = mPos.x >= aPos.x && mPos.x <= aPos.x + SLIDER_WIDTH &&
                    mPos.y >= aPos.y && mPos.y <= aPos.y + HEIGHT;

        string id = _isB ? "B" : "A";
        if (down && over && _activeDrag == null) _activeDrag = id;
        if (!down && _activeDrag == id) { _activeDrag = null; _dragging = false; }
        _dragging = (_activeDrag == id) && down;
        if (!_dragging) return;

        float newT = Mathf.Clamp01((mPos.x - aPos.x) / SLIDER_WIDTH);
        int   pct  = Mathf.RoundToInt(newT * 100f);

        if (_isB)
        {
            if (!_wasMoving && newT > 0f) _panel?.OnSliderBStarted();
            bool prev = _wasMoving;
            BlendFactorB = newT;
            _label.Text  = "Blend B: " + pct + "%";
            _wasMoving   = newT > 0f;
            if (prev || _wasMoving) _panel?.OnSliderBMoved(newT);
        }
        else
        {
            if (!_wasMoving && newT > 0f) _panel?.OnSliderAStarted();
            bool prev = _wasMoving;
            BlendFactor = newT;
            _label.Text = "Blend A: " + pct + "%";
            _wasMoving  = newT > 0f;
            if (prev) _panel?.OnSliderAMoved(newT);
        }
    }

    public static void Reset() { BlendFactor = BlendFactorB = 0f; }
}