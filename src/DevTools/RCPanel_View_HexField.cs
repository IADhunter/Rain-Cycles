using DevInterface;
using UnityEngine;

namespace FilesSetting;

// ================================================================
// CLASE: HEX TEXT FIELD
// Campo de texto hex basado en el nuevo RCStringControl (port del
// StringControl de RegionKit/POM). Misma API que el campo antiguo
// (OnSubmit/OnCancel/Text) para que ColorEditor no cambie.
// ================================================================

public class HexTextField : RCStringControl
{
    private const int MAX_HEX = 6;

    public HexTextField(DevUI owner, string IDstring, DevUINode parentNode, Vector2 pos, float width, float height, string defaultValue)
        : base(owner, IDstring, parentNode, pos, width, defaultValue, IsValidHexInput)
    {
    }

    private static bool IsValidHexInput(string value)
    {
        string s = value;
        if (s.StartsWith("#"))
            s = s.Substring(1);
        if (s.Length > MAX_HEX)
            return false;
        foreach (char c in s)
        {
            if (!((c >= '0' && c <= '9') || (c >= 'A' && c <= 'F') || (c >= 'a' && c <= 'f')))
                return false;
        }
        return true;
    }

    protected override char SanitizeChar(char c) => char.ToUpper(c);

    /// <summary>
    /// Solo se permite anadir el caracter si el texto resultante sigue
    /// siendo valido: impide que los caracteres extra (o no-hex) lleguen
    /// siquiera a mostrarse y desborden el campo.
    /// </summary>
    protected override bool ShouldAppendChar(char c, string newText) => IsValidHexInput(newText);

    /// <summary>
    /// Pegado saneado: filtra solo caracteres hex, trunca a 6 digitos
    /// y normaliza el prefijo '#' (comportamiento del campo hex antiguo).
    /// </summary>
    protected override void PasteText(string clipboard)
    {
        string clean = clipboard.Trim();
        if (clean.StartsWith("#"))
            clean = clean.Substring(1);

        string hexOnly = "";
        foreach (char ch in clean)
        {
            if ((ch >= '0' && ch <= '9') || (ch >= 'A' && ch <= 'F') || (ch >= 'a' && ch <= 'f'))
                hexOnly += char.ToUpper(ch);
            if (hexOnly.Length >= MAX_HEX)
                break;
        }

        if (hexOnly.Length > 0)
        {
            Text = "#" + hexOnly.PadRight(MAX_HEX, '0').Substring(0, MAX_HEX);
            TrySetValue(Text, false);
        }
    }

    /// <summary>
    /// El '#' inicial queda anclado: backspace nunca lo borra
    /// (el texto minimo es "#").
    /// </summary>
    protected override bool CanDeleteChar() => base.Text.Length > 1;

    /// <summary>
    /// El setter externo (ColorEditor sincroniza el campo tras un submit
    /// o mover un slider) debe actualizar tambien el valor commiteado
    /// para que el enfoque posterior restaure el canonico.
    /// </summary>
    public new string Text
    {
        get => base.Text;
        set
        {
            base.Text = value;
            actualValue = value;
            if (fLabels.Count > 0)
                fLabels[0].color = Color.black;
        }
    }
}
