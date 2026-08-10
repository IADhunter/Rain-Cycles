using System;
using DevInterface;
using UnityEngine;
using MonoMod.RuntimeDetour;

namespace FilesSetting;

// ================================================================
// RC INPUT GUARD
// Bloquea el input del juego mientras un RCStringControl tiene foco,
// copia exacta del guard de POM/RegionKit (Pom.InputHooks.cs):
//  - Hooks: Input.GetKey / GetKeyDown / GetKeyUp (string y KeyCode)
//  - Mientras hay foco, SOLO pasa Escape (idem POM)
//  - El foco se limpia solo si se cierra el devUI
// ================================================================

public static class RCInputGuard
{
    private static bool _hooksInitialized = false;
    private static Hook[] _hooks;

    public static void Init()
    {
        if (_hooksInitialized) return;
        _hooksInitialized = true;

        try
        {
            Type inputType = typeof(Input);
            Type[] stringArg = { typeof(string) };
            Type[] keyCodeArg = { typeof(KeyCode) };

            _hooks = new Hook[]
            {
                new Hook(inputType.GetMethod(nameof(Input.GetKey), stringArg), (Func<Func<string, bool>, string, bool>)GuardKeyString),
                new Hook(inputType.GetMethod(nameof(Input.GetKey), keyCodeArg), (Func<Func<KeyCode, bool>, KeyCode, bool>)GuardKeyKeyCode),
                new Hook(inputType.GetMethod(nameof(Input.GetKeyDown), stringArg), (Func<Func<string, bool>, string, bool>)GuardKeyString),
                new Hook(inputType.GetMethod(nameof(Input.GetKeyDown), keyCodeArg), (Func<Func<KeyCode, bool>, KeyCode, bool>)GuardKeyKeyCode),
                new Hook(inputType.GetMethod(nameof(Input.GetKeyUp), stringArg), (Func<Func<string, bool>, string, bool>)GuardKeyString),
                new Hook(inputType.GetMethod(nameof(Input.GetKeyUp), keyCodeArg), (Func<Func<KeyCode, bool>, KeyCode, bool>)GuardKeyKeyCode),
            };

            On.RainWorldGame.RawUpdate += OnRainWorldGameRawUpdate;
        }
        catch (Exception ex)
        {
            RSPlugin.log.LogWarning($"[RCInputGuard] Error creating hooks: {ex.Message}");
        }
    }

    private static bool GuardKeyString(Func<string, bool> orig, string name)
    {
        if (RCStringControl.Active != null && !IsWhitelistedString(name))
            return false;
        return orig(name);
    }

    private static bool GuardKeyKeyCode(Func<KeyCode, bool> orig, KeyCode key)
    {
        if (RCStringControl.Active != null && !IsWhitelistedKey(key))
            return false;
        return orig(key);
    }

    private static bool IsWhitelistedString(string name)
    {
        string lower = name.ToLower();
        if (lower == "escape" || lower == "left ctrl" || lower == "right ctrl")
            return true;
        if (lower == "c" || lower == "v")
            return IsCtrlHeld();
        return false;
    }

    private static bool IsWhitelistedKey(KeyCode key)
    {
        if (key == KeyCode.Escape || key == KeyCode.LeftControl || key == KeyCode.RightControl)
            return true;
        if (key == KeyCode.C || key == KeyCode.V)
            return IsCtrlHeld();
        return false;
    }

    private static bool IsCtrlHeld()
        => Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

    private static void OnRainWorldGameRawUpdate(On.RainWorldGame.orig_RawUpdate orig, RainWorldGame self, float dt)
    {
        orig(self, dt);
        if (self.devUI == null && RCStringControl.Active != null)
        {
            RCStringControl.ReleaseFocus();
        }
    }
}

// ================================================================
// RC STRING CONTROL
// Port autocontenido del StringControl de RegionKit (que a su vez
// usa ManagedStringControl de POM). Diferencias vs el original:
//  - Sin signals: usa delegados OnSubmit/OnCancel/OnValueChanged
//  - Escape cancela (RK no lo maneja)
//  - Clipboard Ctrl+C/V preservado de nuestro campo anterior
// Render: hereda de DevUILabel (anchor 0,0 + MoveLabel) -> texto nitido
// ================================================================

public class RCStringControl : DevUILabel
{
    /// <summary>Unico control con foco global (modelo POM/RK).</summary>
    public static RCStringControl Active { get; private set; }

    protected bool clickedLastUpdate;

    /// <summary>Ultimo valor valido (el que se restaura al cancelar/commitar).</summary>
    protected string actualValue;

    /// <summary>Delegado de validacion en vivo. True/false colorea verde/rojo.</summary>
    public Func<string, bool> isTextValid;

    /// <summary>Disparado al hacer commit (Enter / click-fuera), con el ultimo valor valido.</summary>
    public Action<string> OnSubmit;
    /// <summary>Disparado al cancelar con Escape (el texto se revierte).</summary>
    public Action OnCancel;
    /// <summary>Disparado en vivo cuando el texto pasa a ser valido (newValue, oldValue).</summary>
    public Action<string, string> OnValueChanged;

    public RCStringControl(DevUI owner, string IDstring, DevUINode parentNode, Vector2 pos, float width, string text, Func<string, bool> validate)
        : base(owner, IDstring, parentNode, pos, width, text)
    {
        isTextValid = validate;
        actualValue = text;
        Refresh();
    }

    public override void Refresh()
    {
        base.Refresh();
    }

    public override void Update()
    {
        base.Update();

        if (owner.mouseClick && !clickedLastUpdate)
        {
            if (MouseOver && Active != this && CanTakeFocus())
            {
                // robar el foco: el control anterior commitea su edicion
                // (mejora vs POM/RK, que la pierden silenciosamente)
                Active?.TrySetValue(Active.Text, true);
                Text = actualValue;
                Active = this;
                SetLabelColor(new Color(0.1f, 0.4f, 0.2f));
            }
            else if (Active == this)
            {
                // click sobre el mismo control -> commit y soltar foco
                TrySetValue(Text, true);
                Active = null;
                SetLabelColor(Color.black);
            }

            clickedLastUpdate = true;
        }
        else if (!owner.mouseClick)
        {
            clickedLastUpdate = false;
        }

        if (Active == this)
        {
            // Clipboard (Ctrl+C copia, Ctrl+V reemplaza y valida en vivo)
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
            {
                if (Input.GetKeyDown(KeyCode.C))
                {
                    GUIUtility.systemCopyBuffer = Text;
                }
                else if (Input.GetKeyDown(KeyCode.V))
                {
                    string cb = GUIUtility.systemCopyBuffer;
                    if (!string.IsNullOrEmpty(cb))
                    {
                        PasteText(cb);
                    }
                }
            }

            foreach (char c in Input.inputString)
            {
                if (c == '\b')
                {
                    if (Text.Length != 0 && CanDeleteChar())
                    {
                        Text = Text.Substring(0, Text.Length - 1);
                        TrySetValue(Text, false);
                    }
                }
                else if (c == '\n' || c == '\r')
                {
                    TrySetValue(Text, true);
                    Active = null;
                    SetLabelColor(Color.black);
                }
                else if (c == 27)
                {
                    CancelEditing();
                }
                else
                {
                    string newText = Text + c;
                    if (ShouldAppendChar(c, newText))
                    {
                        Text = newText;
                        TrySetValue(Text, false);
                    }
                }
            }
        }
    }

    private void CancelEditing()
    {
        Text = actualValue;
        Active = null;
        SetLabelColor(Color.black);
        OnCancel?.Invoke();
    }

    private void SetLabelColor(Color c)
    {
        if (fLabels.Count > 0)
            fLabels[0].color = c;
    }

    /// <summary>
    /// Decide si un caracter puede anadirse al texto actual. El default
    /// (fiel a RK/POM) permite todo y la validacion se muestra en rojo;
    /// las subclases pueden restringirlo para bloquear entradas invalidas.
    /// </summary>
    protected virtual bool ShouldAppendChar(char c, string newText) => true;

    /// <summary>
    /// Decide si el control puede tomar el foco. Default: siempre.
    /// Las subclases pueden restringirlo (ej. solo en EditMode).
    /// </summary>
    protected virtual bool CanTakeFocus() => true;

    /// <summary>
    /// Aplica un pegado de portapapeles. Default (RK/POM): reemplaza el
    /// texto y lo valida (rojo si invalido). Las subclases pueden sanearlo.
    /// </summary>
    protected virtual void PasteText(string clipboard)
    {
        Text = clipboard;
        TrySetValue(Text, false);
    }

    /// <summary>
    /// Decide si puede borrarse el ultimo caracter. Default: siempre.
    /// Las subclases pueden anclar prefijos (ej. '#' en el hex).
    /// </summary>
    protected virtual bool CanDeleteChar() => true;

    /// <summary>
    /// Valida el texto en vivo y actualiza colores. actualValue solo almacena
    /// valores validos, por lo que al terminar la transaccion el texto se
    /// restaura al ultimo valor valido (idem POM/RK).
    /// </summary>
    protected virtual void TrySetValue(string newValue, bool endTransaction)
    {
        if (isTextValid != null && isTextValid(newValue))
        {
            string oldValue = actualValue;
            actualValue = newValue;
            SetLabelColor(new Color(0.1f, 0.4f, 0.2f));
            OnValueChanged?.Invoke(newValue, oldValue);
        }
        else
        {
            SetLabelColor(Color.red);
        }

        if (endTransaction)
        {
            Text = actualValue;
            SetLabelColor(Color.black);
            Refresh();
            OnSubmit?.Invoke(actualValue);
        }
    }

    /// <summary>Establece el valor commiteado sin disparar eventos (sincroniza con la logica externa).</summary>
    public void SetCommittedValue(string value)
    {
        actualValue = value;
        Text = value;
        SetLabelColor(Color.black);
    }

    public static void ReleaseFocus()
    {
        Active = null;
    }
}