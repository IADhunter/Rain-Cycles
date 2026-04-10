using UnityEngine;
using RainCycles.Settings;
using RainCycles.Snapshot;
using RainCycles.Sky;
using RainCycles.Clock;
using RainCycles.Core;

namespace RainCycles.Core;

// RC_TINT: inyección y preservación en settings files
public static partial class SettingsBlendController
{
    private static void OnDevUIUpdate(On.DevInterface.DevUI.orig_Update orig, DevInterface.DevUI self)
    {
        orig(self);

        // Inyectar RC_TINT en el settings file activo si aún no tiene la línea.
        // Ocurre una vez por archivo por sesión, en cuanto DevTools está abierto
        // en una sala declarada en [ROOMS].
        var cam = self.game?.cameras?[0];
        if (cam?.room != null)
        {
            var settings = BlendSettingsLoader.Active;
            if (settings != null && settings.IncludesRoom(cam.room.abstractRoom?.name ?? ""))
            {
                string filePath = cam.room.roomSettings?.filePath;
                if (filePath != null && !_rcTintInjected.Contains(filePath))
                {
                    _rcTintInjected.Add(filePath);
                    InjectRcTintIfMissing(filePath);
                }
            }
        }

        if (!_active || _room == null) return;

        // Solo aplica blend en modo manual (slider). La detección de cambio de sala
        // es responsabilidad exclusiva de OnMoveCamera (primario) y del SafetyNet
        // de BlendClockUpdater (último recurso). No duplicar aquí.
        if (_externalT) return;

        float t = BlendSlider.BlendFactor;
        if (Mathf.Abs(t - _lastT) >= 0.005f)
        {
            _lastT = t;
            ApplyBlend(t);
        }
    }

    // Agrega RC_TINT al final del settings file si no existe.
    // El formato vacío deja los valores listos para que el modder los complete.
    private static void InjectRcTintIfMissing(string filePath)
    {
        try
        {
            if (!System.IO.File.Exists(filePath)) return;

            string content = System.IO.File.ReadAllText(filePath, System.Text.Encoding.UTF8);
            if (content.Contains("RC_TINT:")) return;

            string suffix = content.EndsWith("\n") ? "" : "\n";
            System.IO.File.AppendAllText(filePath, suffix + "RC_TINT: #ffffff #ffffff #ffffff\n", System.Text.Encoding.UTF8);
        }
        catch (System.Exception e)
        {
            RSPlugin.log.LogWarning($"[RC_TINT] No se pudo escribir en {filePath}: {e.Message}");
        }
    }

    // Lee la línea RC_TINT del archivo ANTES de que Save() la borre. Devuelve la línea completa, o "RC_TINT: #ffffff #ffffff #ffffff" si no existe.
    public static string ExtractRcTintLine(string filePath)
    {
        try
        {
            if (!System.IO.File.Exists(filePath)) return "RC_TINT: #ffffff #ffffff #ffffff";
            foreach (var line in System.IO.File.ReadAllLines(filePath, System.Text.Encoding.UTF8))
            {
                if (line.TrimEnd('\r').StartsWith("RC_TINT:"))
                    return line.TrimEnd('\r');
            }
        }
        catch (System.Exception e)
        {
            RSPlugin.log.LogWarning($"[RC_TINT] ExtractRcTintLine excepción: {e.Message}");
        }
        return "RC_TINT: #ffffff #ffffff #ffffff";
    }

    public static void ReappendRcTint(string filePath, string rcTintLine)
    {
        try
        {
            if (!System.IO.File.Exists(filePath)) return;
            string content = System.IO.File.ReadAllText(filePath, System.Text.Encoding.UTF8);

            if (content.Contains("RC_TINT:")) return;

            string suffix = content.EndsWith("\n") ? "" : "\n";
            System.IO.File.AppendAllText(filePath, suffix + rcTintLine + "\n", System.Text.Encoding.UTF8);
            _rcTintInjected.Add(filePath);
        }
        catch (System.Exception e)
        {
            RSPlugin.log.LogWarning($"[RC_TINT] ReappendRcTint falló en {filePath}: {e.Message}");
        }
    }

}
