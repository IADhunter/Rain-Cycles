using DevInterface;
using UnityEngine;

namespace FilesSetting;

/// <summary>
/// Slider de blend para el panel RC.
/// Notifica al RCPanel cuando el usuario empieza a moverlo.
/// Incluye botón de reset (volver a 0).
/// </summary>
public class BlendSlider : PositionedDevUINode, IDevUISignals
{
    // Slider A (manual / reloj carril activo)
    public static float BlendFactor  = 0f;
    // Slider B (carril inactivo en modo Loop — mostrar estado del otro carril)
    public static float BlendFactorB = 0f;

    private const float SLIDER_WIDTH  = 160f;
    private const float HEIGHT        = 15f;
    private const float RESET_WIDTH   = 25f;
    private const float RESET_SPACING = 5f;

    private DevUILabel _label;
    private bool _dragging  = false;
    private bool _wasMoving = false;
    private bool _isB;        // true = este slider representa el carril B
    private bool _locked    = false;  // true = bloqueado, no responde al mouse

    private RCPanel _panel;

    public BlendSlider(DevUI owner, string IDstring, DevUINode parentNode, Vector2 pos)
        : base(owner, IDstring, parentNode, pos)
    {
        _panel = parentNode as RCPanel;
        _isB   = IDstring.EndsWith("B");

        string prefix = _isB ? "Blend B: 0%" : "Blend A: 0%";
        _label = new DevUILabel(owner, IDstring + "_Label", this,
            new Vector2(0f, 0f), (int)SLIDER_WIDTH, prefix);
        subNodes.Add(_label);

        // Solo el slider A tiene botón R — resetea ambos sliders y el sistema completo.
        // El slider B no necesita su propio R porque el de A ya cubre ese caso.
        if (!_isB)
            subNodes.Add(new Button(owner, IDstring + "_Reset", this,
                new Vector2(SLIDER_WIDTH + RESET_SPACING, 0f), RESET_WIDTH, "R"));
    }

    public void Signal(DevUISignalType type, DevUINode sender, string message)
    {
        if (sender.IDstring == IDstring + "_Reset")
        {
            // R siempre resetea ambos sliders y el sistema completo
            BlendFactor  = 0f;
            BlendFactorB = 0f;
            _wasMoving   = false;
            SettingsBlendController.Detach();
            if (_panel != null)
                _panel.ResetRelaySystem();
        }
    }

    /// <summary>Bloquea o desbloquea el slider. Un slider bloqueado no responde al mouse.</summary>
    public void SetLocked(bool locked)
    {
        _locked = locked;
        // Si se bloquea, resetear visualmente a 0
        if (locked) SetDisplayT(0f);
    }

    /// <summary>Actualiza el display del slider desde código (BlendClockUpdater).</summary>
    public void SetDisplayT(float t)
    {
        if (_isB) BlendFactorB = t;
        else      BlendFactor  = t;

        int pct = Mathf.RoundToInt(t * 100f);
        _label.Text = (_isB ? "Blend B: " : "Blend A: ") + pct + "%";
    }

    // Flag estático: qué slider tiene el drag activo ("A", "B", o null)
    // Evita que B se active accidentalmente mientras A está siendo arrastrado.
    private static string _activeDrag = null;

    public override void Update()
    {
        base.Update();

        // Bloqueo de input según modo:
        // - Edit Mode apagado + clock corriendo → solo lectura (clock tiene control)
        // - Edit Mode apagado + modo auto (Cycle/EndCycle) → solo lectura aunque
        //   el clock aún no haya arrancado (esperando trigger o deathRainHasHit)
        // - Edit Mode encendido → siempre interactivo (clock está parado)
        if (!BlendClock.EditMode)
        {
            if (BlendClock.IsRunning) return;

            // Modos automáticos: el usuario nunca controla el slider directamente
            var settings = BlendSettingsLoader.Active;
            if (settings != null &&
                (settings.Mode == BlendMode.Cycle || settings.Mode == BlendMode.EndCycle))
                return;
        }

        if (_locked && !BlendClock.EditMode) return;

        Vector2 mPos = owner.mousePos;
        Vector2 aPos = absPos;
        bool mouseDown = owner.mouseDown;

        bool over = mPos.x >= aPos.x && mPos.x <= aPos.x + SLIDER_WIDTH &&
                    mPos.y >= aPos.y && mPos.y <= aPos.y + HEIGHT;

        string myId = _isB ? "B" : "A";

        // Adquirir drag solo si nadie más lo tiene
        if (mouseDown && over && _activeDrag == null)
            _activeDrag = myId;

        // Soltar drag al levantar el mouse
        if (!mouseDown && _activeDrag == myId)
        {
            _activeDrag = null;
            _dragging   = false;
        }

        _dragging = (_activeDrag == myId) && mouseDown;

        if (!_dragging) return;

        float newT = Mathf.Clamp01((mPos.x - aPos.x) / SLIDER_WIDTH);
        int   pct  = Mathf.RoundToInt(newT * 100f);

        if (_isB)
        {
            // Primera vez que B empieza a moverse desde 0 → relevo: resetear A
            if (!_wasMoving && newT > 0f)
            {
                BlendFactor = 0f;
                // Resetear display del slider A
                if (_panel != null)
                {
                    foreach (var node in _panel.subNodes)
                    {
                        if (node is BlendSlider sa && sa.IDstring == "RC_BlendSlider")
                            sa.SetDisplayT(0f);
                    }
                    _panel.OnSliderBStarted();
                }
            }

            bool wasMovingPrev = _wasMoving;
            BlendFactorB = newT;
            _label.Text  = "Blend B: " + pct + "%";
            _wasMoving   = newT > 0f;

            if (_panel != null) _panel.OnSliderBMoved(newT);
        }
        else
        {
            // Primera vez que A empieza a moverse desde 0 → resetear B
            if (!_wasMoving && newT > 0f)
            {
                BlendFactorB = 0f;
                // Resetear display del slider B
                if (_panel != null)
                {
                    foreach (var node in _panel.subNodes)
                    {
                        if (node is BlendSlider sb && sb.IDstring == "RC_BlendSliderB")
                            sb.SetDisplayT(0f);
                    }
                    _panel.OnSliderAStarted();
                }
            }

            bool wasMovingPrev = _wasMoving;
            BlendFactor = newT;
            _label.Text = "Blend A: " + pct + "%";
            _wasMoving  = newT > 0f;

            if (wasMovingPrev)
                _panel?.OnSliderAMoved(newT);
        }
    }

    public static void Reset()
    {
        BlendFactor  = 0f;
        BlendFactorB = 0f;
    }
}