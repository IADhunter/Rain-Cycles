using System;

namespace RainCycles.Core;

public static class RCGate
{
    private const string BlindedBurden = "bur-blinded";

    public static bool Suspended { get; private set; }

    public static void Evaluate(RainWorld rw)
    {
        Suspended = false;
        if (rw == null || !rw.ExpeditionMode || !ModManager.Expedition) return;

        try
        {
            var unlocks = Expedition.ExpeditionGame.activeUnlocks;
            for (int i = 0; i < unlocks.Count; i++)
            {
                if (string.Equals(unlocks[i], BlindedBurden, StringComparison.OrdinalIgnoreCase))
                {
                    Suspended = true;
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            RSPlugin.log.LogWarning($"[RCGate] No se pudo leer activeUnlocks: {ex.Message}");
        }
    }

    public static void Reset()
    {
        Suspended = false;
    }
}
