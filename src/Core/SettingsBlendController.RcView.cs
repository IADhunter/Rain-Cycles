using RainCycles.Settings;

namespace RainCycles.Core;

// Inyección y preservación de RC_TYPE, RC_VIEW y RC_TINT en settings files.
public static partial class SettingsBlendController
{
    private static void OnDevUIUpdate(On.DevInterface.DevUI.orig_Update orig, DevInterface.DevUI self)
    {

    if (SettingsBlendController.IsActive && SettingsBlendController.IsExternalT)
    {
        RSPlugin.log.LogDebug($"[OnDevUIUpdate] ExternalT active, BlendFactor={BlendSlider.BlendFactor:F3}, _forcedT={_forcedT:F3}");
    }

        orig(self);

        var cam = self.game?.cameras?[0];
        if (cam?.room != null)
        {
            // Verificar si la sala es blend usando IsBlendRoom (basado en RC_TYPE)
            if (IsBlendRoom(cam.room))
            {
                string filePath = cam.room.roomSettings?.filePath;
                if (filePath != null && !_rcViewInjected.Contains(filePath))
                {
                    _rcViewInjected.Add(filePath);
                    InjectRcViewBlockIfMissing(filePath);
                }
            }
        }

        if (!_active || _room == null) return;
        if (_externalT) return;

        float t = BlendSlider.BlendFactor;
        if (UnityEngine.Mathf.Abs(t - _lastT) >= 0.005f)
        {
            _lastT = t;
            ApplyBlend(t);
        }
    }

    private static void InjectRcViewBlockIfMissing(string filePath)
    {
        try
        {
            if (!System.IO.File.Exists(filePath)) return;
            string content = System.IO.File.ReadAllText(filePath, System.Text.Encoding.UTF8);
            bool hasType = content.Contains("RC_TYPE:");
            bool hasView = content.Contains("RC_VIEW:");
            bool hasTint = content.Contains("RC_TINT:");
            
            if (hasType && hasView && hasTint) return;

            string suffix = content.EndsWith("\n") ? "" : "\n";
            var sb = new System.Text.StringBuilder();
            if (!hasType) sb.Append("RC_TYPE: None\n");
            if (!hasView) sb.Append("RC_VIEW: NONE\n");
            if (!hasTint) sb.Append("RC_TINT: #ffffff #ffffff #ffffff\n");

            if (sb.Length > 0)
                System.IO.File.AppendAllText(filePath, suffix + sb.ToString(), System.Text.Encoding.UTF8);
        }
        catch (System.Exception e)
        {
            RSPlugin.log.LogWarning($"[RC_VIEW] No se pudo escribir en {filePath}: {e.Message}");
        }
    }

    public static string ExtractRcViewBlock(string filePath)
    {
        try
        {
            if (!System.IO.File.Exists(filePath))
                return "RC_TYPE: None\nRC_VIEW: NONE\nRC_TINT: #ffffff #ffffff #ffffff";

            string typeLine = null, viewLine = null, tintLine = null;
            foreach (var line in System.IO.File.ReadAllLines(filePath, System.Text.Encoding.UTF8))
            {
                string trimmed = line.TrimEnd('\r');
                if (trimmed.StartsWith("RC_TYPE:")) typeLine = trimmed;
                else if (trimmed.StartsWith("RC_VIEW:")) viewLine = trimmed;
                else if (trimmed.StartsWith("RC_TINT:")) tintLine = trimmed;
            }
            typeLine ??= "RC_TYPE: None";
            viewLine ??= "RC_VIEW: NONE";
            tintLine ??= "RC_TINT: #ffffff #ffffff #ffffff";
            return typeLine + "\n" + viewLine + "\n" + tintLine;
        }
        catch (System.Exception e)
        {
            RSPlugin.log.LogWarning($"[RC_VIEW] ExtractRcViewBlock excepción: {e.Message}");
        }
        return "RC_TYPE: None\nRC_VIEW: NONE\nRC_TINT: #ffffff #ffffff #ffffff";
    }

    public static void ReappendRcViewBlock(string filePath, string rcViewBlock)
    {
        try
        {
            if (!System.IO.File.Exists(filePath)) return;
            string content = System.IO.File.ReadAllText(filePath, System.Text.Encoding.UTF8);
            
            // Verificar si ya tiene RC_TYPE (o RC_VIEW/RC_TINT) para decidir si reescribir
            // Si ya tiene alguno de los tres, asumimos que el bloque está completo
            if (content.Contains("RC_TYPE:") || content.Contains("RC_VIEW:") || content.Contains("RC_TINT:"))
                return;
                
            string suffix = content.EndsWith("\n") ? "" : "\n";
            System.IO.File.AppendAllText(filePath, suffix + rcViewBlock + "\n", System.Text.Encoding.UTF8);
            _rcViewInjected.Add(filePath);
        }
        catch (System.Exception e)
        {
            RSPlugin.log.LogWarning($"[RC_VIEW] ReappendRcViewBlock falló en {filePath}: {e.Message}");
        }
    }
}