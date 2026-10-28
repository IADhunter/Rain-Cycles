using DevInterface;
using UnityEngine;

namespace FilesSetting;

// CLASE: HEX TEXT FIELD

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

    protected override bool ShouldAppendChar(char c, string newText) => IsValidHexInput(newText);

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

    protected override bool CanDeleteChar() => base.Text.Length > 1;

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
