using System;
using DevInterface;
using UnityEngine;
using RWCustom;

namespace FilesSetting;

// ================================================================
// CLASE: HSV SLIDER
// ================================================================

public class HSVSlider : PositionedDevUINode
{
    private const float SLIDER_WIDTH = 100f;
    private const float HEIGHT = 16f;
    private const float NUB_WIDTH = 8f;
    
    private float _minValue;
    private float _maxValue;
    private float _currentValue;
    private bool _dragging;
    private FSprite _bgSprite;
    private FSprite _lineSprite;
    private FSprite _nubSprite;
    private DevUILabel _titleLabel;
    private DevUILabel _valueLabel;
    
    public Action<float> OnValueChanged { get; set; }
    
    public HSVSlider(DevUI owner, string IDstring, DevUINode parentNode, Vector2 pos, string title, float minValue, float maxValue, float defaultValue)
        : base(owner, IDstring, parentNode, pos)
    {
        _minValue = minValue;
        _maxValue = maxValue;
        _currentValue = defaultValue;
        
        _titleLabel = new DevUILabel(owner, IDstring + "_Title", this,
            new Vector2(0f, 0f), 16f, title + ":");
        subNodes.Add(_titleLabel);
        
        _valueLabel = new DevUILabel(owner, IDstring + "_Value", this,
            new Vector2(21f, 0f), 20f, GetValueText());
        subNodes.Add(_valueLabel);
        
        float sliderStartX = 41f;
        
        _bgSprite = new FSprite("pixel");
        _bgSprite.scaleX = SLIDER_WIDTH;
        _bgSprite.scaleY = HEIGHT;
        _bgSprite.anchorX = 0f;
        _bgSprite.anchorY = 0f;
        _bgSprite.color = new Color(1f, 1f, 1f);
        _bgSprite.alpha = 0.5f;
        _bgSprite.x = absPos.x + sliderStartX + 0.01f;
        _bgSprite.y = absPos.y + 0.01f;
        Futile.stage.AddChild(_bgSprite);
        fSprites.Add(_bgSprite);
        
        _lineSprite = new FSprite("pixel");
        _lineSprite.scaleX = SLIDER_WIDTH;
        _lineSprite.scaleY = 3f;
        _lineSprite.anchorX = 0f;
        _lineSprite.anchorY = 0f;
        _lineSprite.color = new Color(0f, 0f, 0f);
        _lineSprite.x = absPos.x + sliderStartX + 0.01f;
        _lineSprite.y = absPos.y + 7f;
        Futile.stage.AddChild(_lineSprite);
        fSprites.Add(_lineSprite);
        
        _nubSprite = new FSprite("pixel");
        _nubSprite.scaleX = NUB_WIDTH;
        _nubSprite.scaleY = HEIGHT;
        _nubSprite.anchorX = 0f;
        _nubSprite.anchorY = 0f;
        _nubSprite.color = new Color(0f, 0f, 0f);
        Futile.stage.AddChild(_nubSprite);
        fSprites.Add(_nubSprite);
        
        UpdateNubPosition();
    }
    
    private string GetValueText()
    {
        if (_maxValue == 360f)
            return Mathf.RoundToInt(_currentValue).ToString();
        else
            return Mathf.RoundToInt(_currentValue * 100f).ToString();
    }
    
    private void UpdateNubPosition()
    {
        float sliderStartX = 41f;
        float t = (_currentValue - _minValue) / (_maxValue - _minValue);
        float nubX = absPos.x + sliderStartX + t * (SLIDER_WIDTH - NUB_WIDTH);
        _nubSprite.x = nubX + 0.01f;
        _nubSprite.y = absPos.y + 0.01f;
        
        _lineSprite.x = absPos.x + sliderStartX + 0.01f;
        _lineSprite.y = absPos.y + 7f;
        _bgSprite.x = absPos.x + sliderStartX + 0.01f;
        _bgSprite.y = absPos.y + 0.01f;
        
        _valueLabel.Text = GetValueText();
    }
    
    public void SetValue(float value)
    {
        _currentValue = Mathf.Clamp(value, _minValue, _maxValue);
        UpdateNubPosition();
    }
    
    public float GetValue()
    {
        return _currentValue;
    }
    
    public override void Update()
    {
        base.Update();
        
        if (owner == null) return;
        
        float sliderStartX = 41f;
        float sliderEndX = absPos.x + sliderStartX + SLIDER_WIDTH;
        
        bool over = owner.mousePos.x >= absPos.x + sliderStartX && owner.mousePos.x <= sliderEndX &&
                    owner.mousePos.y >= absPos.y && owner.mousePos.y <= absPos.y + HEIGHT;
        
        if (owner.mouseClick && over)
            _dragging = true;
        if (_dragging && !owner.mouseDown)
            _dragging = false;
        
        if (_dragging)
        {
            float t = Mathf.Clamp01((owner.mousePos.x - (absPos.x + sliderStartX)) / (SLIDER_WIDTH - NUB_WIDTH));
            _currentValue = _minValue + t * (_maxValue - _minValue);
            UpdateNubPosition();
            OnValueChanged?.Invoke(_currentValue);
        }
        
        _nubSprite.color = _dragging ? new Color(0f, 0f, 1f) : (over ? new Color(1f, 0f, 0f) : new Color(0f, 0f, 0f));
    }
    
    public override void Refresh()
    {
        base.Refresh();
        UpdateNubPosition();
    }
}

// ================================================================
// CLASE: COLOR EDITOR
// ================================================================

public class ColorEditor : RectangularDevUINode
{
    private HexTextField _hexField;
    private Color _currentColor = Color.white;
    
    private float _hue = 0f;
    private float _sat = 0f;
    private float _val = 1f;
    
    private HSVSlider _hueSlider;
    private HSVSlider _satSlider;
    private HSVSlider _valSlider;
    
    public Action<Color> OnColorChanged { get; set; }
    public float CurrentHue01 => _hue;
    
    public ColorEditor(DevUI owner, DevUINode parentNode, Vector2 hexPos, float hexWidth, Vector2 sliderPos)
        : base(owner, "RC_ColorEditor", parentNode, Vector2.zero, Vector2.zero)
    {
        _hexField = new HexTextField(owner, "RC_HexField", this, hexPos, hexWidth, 16f, "#FFFFFF");
        _hexField.OnSubmit += OnHexSubmitted;
        _hexField.OnCancel += OnHexCancelled;
        subNodes.Add(_hexField);
        
        _hueSlider = new HSVSlider(owner, "RC_HueSlider", this, new Vector2(sliderPos.x, sliderPos.y + 40f), "H", 0f, 360f, 0f);
        _satSlider = new HSVSlider(owner, "RC_SatSlider", this, new Vector2(sliderPos.x, sliderPos.y + 20f), "S", 0f, 1f, 0f);
        _valSlider = new HSVSlider(owner, "RC_ValSlider", this, new Vector2(sliderPos.x, sliderPos.y), "V", 0f, 1f, 1f);
        
        subNodes.Add(_hueSlider);
        subNodes.Add(_satSlider);
        subNodes.Add(_valSlider);
        
        _hueSlider.OnValueChanged = OnHueChanged;
        _satSlider.OnValueChanged = OnSatChanged;
        _valSlider.OnValueChanged = OnValChanged;
    }
    
    private void OnHexSubmitted(string hex)
    {
        if (!string.IsNullOrEmpty(hex) && hex.Length >= 7 && hex[0] == '#')
        {
            try
            {
                float r = Convert.ToInt32(hex.Substring(1, 2), 16) / 255f;
                float g = Convert.ToInt32(hex.Substring(3, 2), 16) / 255f;
                float b = Convert.ToInt32(hex.Substring(5, 2), 16) / 255f;
                SetColor(new Color(r, g, b));
                OnColorChanged?.Invoke(_currentColor);
            }
            catch { }
        }
        UpdateHexField();
    }
    
    private void OnHexCancelled()
    {
        UpdateHexField();
    }
    
    private void UpdateHexField()
    {
        string hex = $"#{Mathf.RoundToInt(_currentColor.r * 255f):X2}" +
                     $"{Mathf.RoundToInt(_currentColor.g * 255f):X2}" +
                     $"{Mathf.RoundToInt(_currentColor.b * 255f):X2}";
        _hexField.Text = hex;
    }
    
    public void SetColor(Color color)
    {
        Color.RGBToHSV(color, out float h, out float s, out float v);
        _sat = s;
        _val = v;
        
        if (s > 0.001f && v > 0.001f)
            _hue = h;
        
        _currentColor = Color.HSVToRGB(_hue, _sat, _val);
        UpdateUIFromState();
    }
    
    private void UpdateUIFromState()
    {
        UpdateHexField();
        
        _hueSlider.SetValue(_hue * 360f);
        _satSlider.SetValue(_sat);
        _valSlider.SetValue(_val);
    }
    
    private void OnHueChanged(float hue)
    {
        _hue = hue / 360f;
        _currentColor = Color.HSVToRGB(_hue, _sat, _val);
        UpdateHexField();
        OnColorChanged?.Invoke(_currentColor);
    }
    
    private void OnSatChanged(float sat)
    {
        _sat = sat;
        _currentColor = Color.HSVToRGB(_hue, _sat, _val);
        UpdateHexField();
        OnColorChanged?.Invoke(_currentColor);
    }
    
    private void OnValChanged(float val)
    {
        _val = val;
        _currentColor = Color.HSVToRGB(_hue, _sat, _val);
        UpdateHexField();
        OnColorChanged?.Invoke(_currentColor);
    }
    
    public override void Update()
    {
        base.Update();
    }
}