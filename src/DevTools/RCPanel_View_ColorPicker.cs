using System;
using DevInterface;
using UnityEngine;

namespace FilesSetting;

// ================================================================
// CLASE: COLOR PREVIEW (Cuadrado con color sólido y marco de contraste)
// ================================================================

public class ColorPreview : PositionedDevUINode
{
    private float _size;
    private FTexture _texture;
    private Texture2D _colorTexture;
    private Color _currentColor = Color.white;
    private string _textureSalt = "RC_ColorPreview_";
    private int _saltSeed = 0;
    
    public ColorPreview(DevUI owner, string IDstring, DevUINode parentNode, Vector2 pos, float size)
        : base(owner, IDstring, parentNode, pos)
    {
        _size = size;
        _saltSeed = UnityEngine.Random.Range(0, 100000);
        CreateTexture();
    }
    
    private void CreateTexture()
    {
        int sizeInt = (int)_size;
        _colorTexture = new Texture2D(sizeInt, sizeInt, TextureFormat.RGBA32, false);
        UpdateTexturePixels();
        _colorTexture.Apply();
        
        if (_texture != null)
        {
            _texture.Destroy();
            fSprites.Remove(_texture);
        }
        
        _saltSeed++;
        _texture = new FTexture(_colorTexture, _textureSalt + _saltSeed.ToString());
        _texture.anchorX = 0f;
        _texture.anchorY = 0f;
        _texture.x = absPos.x;
        _texture.y = absPos.y;
        Futile.stage.AddChild(_texture);
        fSprites.Add(_texture);
    }
    
    private void UpdateTexturePixels()
    {
        int sizeInt = (int)_size;
        Color contrastColor = GetContrastColor(_currentColor);
        
        for (int x = 0; x < sizeInt; x++)
        {
            for (int y = 0; y < sizeInt; y++)
            {
                if (x == 0 || y == 0 || x == sizeInt - 1 || y == sizeInt - 1)
                    _colorTexture.SetPixel(x, y, contrastColor);
                else
                    _colorTexture.SetPixel(x, y, _currentColor);
            }
        }
    }
    
    private Color GetContrastColor(Color backgroundColor)
    {
        float luminance = backgroundColor.r * 0.299f + backgroundColor.g * 0.587f + backgroundColor.b * 0.114f;
        return luminance > 0.5f ? Color.black : Color.white;
    }
    
    public void SetColor(Color color)
    {
        _currentColor = color;
        
        int sizeInt = (int)_size;
        Color contrastColor = GetContrastColor(_currentColor);
        
        for (int x = 0; x < sizeInt; x++)
        {
            for (int y = 0; y < sizeInt; y++)
            {
                if (x == 0 || y == 0 || x == sizeInt - 1 || y == sizeInt - 1)
                    _colorTexture.SetPixel(x, y, contrastColor);
                else
                    _colorTexture.SetPixel(x, y, _currentColor);
            }
        }
        _colorTexture.Apply();
        _texture.SetTexture(_colorTexture);
    }
    
    public void SetPosition(float x, float y)
    {
        if (_texture != null)
        {
            _texture.x = x;
            _texture.y = y;
        }
    }
    
    public override void Refresh()
    {
        base.Refresh();
        if (_texture != null)
        {
            _texture.x = absPos.x;
            _texture.y = absPos.y;
        }
    }
    
    public void Destroy()
    {
        if (_texture != null)
            _texture.Destroy();
    }
}

// ================================================================
// CLASE: FLOATING COLOR PREVIEW
// ================================================================

public class FloatingColorPreview : PositionedDevUINode
{
    private float _size;
    private FTexture _texture;
    private Texture2D _colorTexture;
    private Color _currentColor = Color.white;
    private string _textureSalt = "RC_FloatingPreview_";
    private int _saltSeed = 0;
    
    public FloatingColorPreview(DevUI owner, string IDstring, DevUINode parentNode, Vector2 pos, float size)
        : base(owner, IDstring, parentNode, pos)
    {
        _size = size;
        _saltSeed = UnityEngine.Random.Range(0, 100000);
        CreateTexture();
    }
    
    private void CreateTexture()
    {
        int sizeInt = (int)_size;
        _colorTexture = new Texture2D(sizeInt, sizeInt, TextureFormat.RGBA32, false);
        UpdateTexturePixels();
        _colorTexture.Apply();
        
        if (_texture != null)
        {
            _texture.Destroy();
            fSprites.Remove(_texture);
        }
        
        _saltSeed++;
        _texture = new FTexture(_colorTexture, _textureSalt + _saltSeed.ToString());
        _texture.anchorX = 0f;
        _texture.anchorY = 0f;
        _texture.x = absPos.x;
        _texture.y = absPos.y;
        Futile.stage.AddChild(_texture);
        fSprites.Add(_texture);
    }
    
    private void UpdateTexturePixels()
    {
        int sizeInt = (int)_size;
        Color contrastColor = GetContrastColor(_currentColor);
        
        for (int x = 0; x < sizeInt; x++)
        {
            for (int y = 0; y < sizeInt; y++)
            {
                if (x == 0 || y == 0 || x == sizeInt - 1 || y == sizeInt - 1)
                    _colorTexture.SetPixel(x, y, contrastColor);
                else
                    _colorTexture.SetPixel(x, y, _currentColor);
            }
        }
    }
    
    private Color GetContrastColor(Color backgroundColor)
    {
        float luminance = backgroundColor.r * 0.299f + backgroundColor.g * 0.587f + backgroundColor.b * 0.114f;
        return luminance > 0.5f ? Color.black : Color.white;
    }
    
    public void SetColor(Color color)
    {
        _currentColor = color;
        
        int sizeInt = (int)_size;
        Color contrastColor = GetContrastColor(_currentColor);
        
        for (int x = 0; x < sizeInt; x++)
        {
            for (int y = 0; y < sizeInt; y++)
            {
                if (x == 0 || y == 0 || x == sizeInt - 1 || y == sizeInt - 1)
                    _colorTexture.SetPixel(x, y, contrastColor);
                else
                    _colorTexture.SetPixel(x, y, _currentColor);
            }
        }
        _colorTexture.Apply();
        _texture.SetTexture(_colorTexture);
    }
    
    public void SetPosition(float x, float y)
    {
        if (_texture != null)
        {
            _texture.x = x;
            _texture.y = y;
        }
    }
    
    public override void Refresh()
    {
        base.Refresh();
        if (_texture != null)
        {
            _texture.x = absPos.x;
            _texture.y = absPos.y;
        }
    }
    
    public void Destroy()
    {
        if (_texture != null)
            _texture.Destroy();
    }
}

// ================================================================
// CLASE: FREE COLOR PICKER (Hue como estado independiente del gradiente)
// ================================================================

public class FreeColorPicker : PositionedDevUINode
{
    private float _size;
    private FTexture _bgTexture;
    private FSprite _cursorSprite;
    private bool _dragging;
    private Color _currentColor = Color.white;
    
    // HSV como estado propio e independiente
    private float _hue = 0f;        // 0-1, solo controla el tono del gradiente
    private float _saturation = 0f;  // 0-1, coordenada X del cursor
    private float _value = 1f;       // 0-1, coordenada Y del cursor
    
    private Texture2D _gradientTexture;
    private bool _textureDirty = true;
    private string _textureSalt = "RC_FreeColorPicker_";
    private int _saltSeed = 0;
    
    public Action<Color> OnColorSelected { get; set; }
    
    public FreeColorPicker(DevUI owner, string IDstring, DevUINode parentNode, Vector2 pos, float size)
        : base(owner, IDstring, parentNode, pos)
    {
        _size = size;
        _saltSeed = UnityEngine.Random.Range(0, 100000);
        CreateTexture();
        
        _cursorSprite = new FSprite("pixel");
        _cursorSprite.scaleX = 4f;
        _cursorSprite.scaleY = 4f;
        _cursorSprite.anchorX = 0.5f;
        _cursorSprite.anchorY = 0.5f;
        _cursorSprite.color = Color.white;
        Futile.stage.AddChild(_cursorSprite);
        fSprites.Add(_cursorSprite);
        
        UpdateCursorPosition(_saturation, _value);
    }
    
    private void CreateTexture()
    {
        int sizeInt = (int)_size;
        _gradientTexture = new Texture2D(sizeInt, sizeInt, TextureFormat.RGBA32, false);
        UpdateGradientPixels();
        _gradientTexture.Apply();
        
        if (_bgTexture != null)
        {
            _bgTexture.Destroy();
            fSprites.Remove(_bgTexture);
        }
        
        _saltSeed++;
        _bgTexture = new FTexture(_gradientTexture, _textureSalt + _saltSeed.ToString());
        _bgTexture.anchorX = 0f;
        _bgTexture.anchorY = 0f;
        _bgTexture.x = absPos.x;
        _bgTexture.y = absPos.y;
        Futile.stage.AddChild(_bgTexture);
        fSprites.Add(_bgTexture);
        
        _textureDirty = false;
    }
    
    private void UpdateGradientPixels()
    {
        int sizeInt = (int)_size;
        for (int x = 0; x < sizeInt; x++)
        {
            for (int y = 0; y < sizeInt; y++)
            {
                float s = (float)x / (sizeInt - 1);
                float v = (float)y / (sizeInt - 1);
                // La esquina superior derecha (x=max, y=max) siempre usa _hue puro
                Color color = Color.HSVToRGB(_hue, s, v);
                _gradientTexture.SetPixel(x, y, color);
            }
        }
    }
    
    private void UpdateTexture()
    {
        if (!_textureDirty) return;
        
        UpdateGradientPixels();
        _gradientTexture.Apply();
        _bgTexture.SetTexture(_gradientTexture);
        _textureDirty = false;
        
        // Traer cursor al frente
        if (_cursorSprite != null)
        {
            _cursorSprite.RemoveFromContainer();
            Futile.stage.AddChild(_cursorSprite);
        }
    }
    
    // Llamado desde el slider H de ColorEditor. Solo cambia el tono del gradiente.
    public void SetHue(float hue01)
    {
        _hue = Mathf.Clamp01(hue01);
        _textureDirty = true;
        UpdateTexture();
        
        // Recalcular color con S/V actuales del cursor
        _currentColor = Color.HSVToRGB(_hue, _saturation, _value);
        UpdateCursorPosition(_saturation, _value);
    }
    
    // Llamado cuando llega un Color desde fuera (cambio de tinte, hex, etc.)
    public void SetColor(Color color)
    {
        Color.RGBToHSV(color, out float h, out float s, out float v);
        _saturation = s;
        _value = v;
        
        // Solo adoptamos H del color si es cromático. Si no, preservamos el nuestro.
        if (s > 0.001f && v > 0.001f)
            _hue = h;
        
        _currentColor = Color.HSVToRGB(_hue, _saturation, _value);
        UpdateCursorPosition(_saturation, _value);
        _textureDirty = true;
        UpdateTexture();
    }
    
    // Para sincronización directa HSV si es necesario
    public void SetHSV(float h, float s, float v)
    {
        _hue = Mathf.Clamp01(h);
        _saturation = Mathf.Clamp01(s);
        _value = Mathf.Clamp01(v);
        _currentColor = Color.HSVToRGB(_hue, _saturation, _value);
        UpdateCursorPosition(_saturation, _value);
        _textureDirty = true;
        UpdateTexture();
    }
    
    private void UpdateCursorPosition(float saturation, float value)
    {
        if (_cursorSprite == null) return;
        float x = absPos.x + saturation * _size;
        float y = absPos.y + value * _size;
        _cursorSprite.x = x;
        _cursorSprite.y = y;
        
        Color bgColor = Color.HSVToRGB(_hue, saturation, value);
        _cursorSprite.color = GetContrastColor(bgColor);
    }
    
    private Color GetContrastColor(Color backgroundColor)
    {
        float luminance = backgroundColor.r * 0.299f + backgroundColor.g * 0.587f + backgroundColor.b * 0.114f;
        return luminance > 0.5f ? Color.black : Color.white;
    }
    
    private void OnColorSelectedAtPosition(float x, float y)
    {
        _saturation = Mathf.Clamp01(x / _size);
        _value = Mathf.Clamp01(y / _size);
        _currentColor = Color.HSVToRGB(_hue, _saturation, _value);
        UpdateCursorPosition(_saturation, _value);
        OnColorSelected?.Invoke(_currentColor);
    }
    
    public override void Update()
    {
        base.Update();
        
        if (owner == null || _cursorSprite == null) return;
        
        bool over = owner.mousePos.x >= absPos.x && owner.mousePos.x <= absPos.x + _size &&
                    owner.mousePos.y >= absPos.y && owner.mousePos.y <= absPos.y + _size;
        
        if (owner.mouseClick && over)
            _dragging = true;
        if (_dragging && !owner.mouseDown)
            _dragging = false;
        
        if (_dragging && over)
        {
            float localX = owner.mousePos.x - absPos.x;
            float localY = owner.mousePos.y - absPos.y;
            OnColorSelectedAtPosition(localX, localY);
        }
        
        Color bgColor = Color.HSVToRGB(_hue, _saturation, _value);
        _cursorSprite.color = GetContrastColor(bgColor);
    }
    
    public override void Refresh()
    {
        base.Refresh();
        if (_bgTexture != null)
        {
            _bgTexture.x = absPos.x;
            _bgTexture.y = absPos.y;
        }
        
        UpdateCursorPosition(_saturation, _value);
        if (_cursorSprite != null)
        {
            _cursorSprite.RemoveFromContainer();
            Futile.stage.AddChild(_cursorSprite);
        }
    }
    
    public void Destroy()
    {
        if (_bgTexture != null) _bgTexture.Destroy();
        if (_cursorSprite != null) _cursorSprite.RemoveFromContainer();
    }
}

// ================================================================
// CLASE: SCREEN COLOR PICKER (Pipeta)
// ================================================================

public static class ScreenColorPicker
{
    private static FloatingColorPreview _floatingPreview;
    private static bool _isActive = false;
    private static Action<Color> _onColorSelected;
    private static RainWorldGame _game;
    private static RoomCamera _camera;
    private static Color _currentColor = Color.white;
    private static bool _pendingStop = false;
    private static DevUI _devUI;
    
    public static void Start(DevUI devUI, RainWorldGame game, Action<Color> onColorSelected)
    {
        if (_isActive) return;
        
        _isActive = true;
        _devUI = devUI;
        _game = game;
        _camera = game.cameras[0];
        _onColorSelected = onColorSelected;
        _pendingStop = false;
        
        _floatingPreview = new FloatingColorPreview(null, "ScreenColorPreview", null, Vector2.zero, 19f);
        _floatingPreview.SetColor(Color.white);
        
        On.RainWorld.Update += OnUpdate;
        
        RSPlugin.log.LogInfo("[ScreenColorPicker] Activado - Haz clic para seleccionar color");
    }
    
    public static void Stop(bool applyColor)
    {
        if (!_isActive) return;
        
        _isActive = false;
        _pendingStop = false;
        
        if (_floatingPreview != null)
        {
            _floatingPreview.Destroy();
            _floatingPreview = null;
        }
        
        On.RainWorld.Update -= OnUpdate;
        
        if (applyColor && _onColorSelected != null)
        {
            _onColorSelected(_currentColor);
            RSPlugin.log.LogInfo($"[ScreenColorPicker] Color seleccionado: {_currentColor}");
        }
        else
        {
            RSPlugin.log.LogInfo("[ScreenColorPicker] Cancelado");
        }
        
        _onColorSelected = null;
        _game = null;
        _camera = null;
        _devUI = null;
    }
    
    public static bool IsActive => _isActive;
    
    private static void OnUpdate(On.RainWorld.orig_Update orig, RainWorld self)
    {
        orig(self);
        
        if (!_isActive || _floatingPreview == null || _camera == null || _camera.room == null || _devUI == null) return;
        
        Vector2 mouseGamePos = _devUI.mousePos;
        
        Color pixelColor = GetPixelColorFromRenderTexture(mouseGamePos);
        _currentColor = pixelColor;
        
        _floatingPreview.SetColor(pixelColor);
        _floatingPreview.SetPosition(mouseGamePos.x + 10, mouseGamePos.y + 10);
        
        if (_devUI.mouseClick && !_pendingStop)
        {
            _pendingStop = true;
            Stop(true);
        }
    }
    
    private static Color GetPixelColorFromRenderTexture(Vector2 gamePos)
    {
        try
        {
            RenderTexture renderTex = Futile.screen.renderTexture;
            if (renderTex == null) return Color.white;
            
            RenderTexture currentRT = RenderTexture.active;
            RenderTexture.active = renderTex;
            
            Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(gamePos.x, renderTex.height - gamePos.y, 1, 1), 0, 0);
            tex.Apply();
            
            Color pixelColor = tex.GetPixel(0, 0);
            
            RenderTexture.active = currentRT;
            UnityEngine.Object.Destroy(tex);
            
            return pixelColor;
        }
        catch
        {
            return Color.white;
        }
    }
}