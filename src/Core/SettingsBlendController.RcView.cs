using RainCycles.Settings;
using RainCycles.Patches;

namespace RainCycles.Core;

// Inyección y preservación de RainCycles: en settings files.
public static partial class SettingsBlendController
{
    private static void OnDevUIUpdate(On.DevInterface.DevUI.orig_Update orig, DevInterface.DevUI self)
    {
        orig(self);

        if (self?.game == null) return;

        var cam = self.game?.cameras?[0];
        if (cam?.room != null)
        {
            if (IsBlendRoom(cam.room))
            {
                string filePath = cam.room.roomSettings?.filePath;
                if (filePath != null && !_rcViewInjected.Contains(filePath))
                {
                    _rcViewInjected.Add(filePath);
                    InjectRainCyclesBlockIfMissing(filePath);
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

    // ============================================================
    // INYECCIÓN DE RAINCYCLES: - NUEVO FORMATO MODULAR
    // ============================================================

    private static void InjectRainCyclesBlockIfMissing(string filePath)
    {
        try
        {
            if (!System.IO.File.Exists(filePath)) return;
            string content = System.IO.File.ReadAllText(filePath, System.Text.Encoding.UTF8);

            if (content.Contains("RainCycles:")) return;

            string suffix = content.EndsWith("\n") ? "" : "\n";
            string defaultBlock = "RainCycles: <Type:None>";
            System.IO.File.AppendAllText(filePath, suffix + defaultBlock + "\n", System.Text.Encoding.UTF8);
            RSPlugin.log.LogDebug($"[RC_VIEW] RainCycles: inyectado en {filePath}");
        }
        catch (System.Exception e)
        {
            RSPlugin.log.LogWarning($"[RC_VIEW] No se pudo escribir en {filePath}: {e.Message}");
        }
    }
}