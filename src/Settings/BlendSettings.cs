using System.Collections.Generic;

namespace RainCycles.Settings;

public enum BlendMode
{
    Loop,
    Cycle,
    EndCycle
}

public enum LoopTrigger
{
    None,
    Cycle,
    Rain
}

public partial class BlendSettings
{
    // ============================================================
    // CONFIGURACIÓN PRINCIPAL
    // ============================================================
    public BlendMode Mode = BlendMode.Loop;
    public bool Clock = false;
    public float IdleTime = 5f;
    public float Duration = 10f;

    // ============================================================
    // TRIGGER DE ACTIVACIÓN PARA LOOP
    // ============================================================
    public LoopTrigger Trigger = LoopTrigger.None;
    public float WaitTime = 0f;

    // ============================================================
    // REDIRECCIÓN DE ESTADO VANILLA
    // ============================================================
    public int Setting = 0;

    // ============================================================
    // MOD QUE PROVEE LAS IMÁGENES
    // ============================================================
    public string SelectedModName { get; set; } = "";

    // ============================================================
    // BACKGROUNDS
    // ============================================================
    public Dictionary<ViewType, Dictionary<string, string>> BackgroundAliases =
        new Dictionary<ViewType, Dictionary<string, string>>();

    public bool HasBackgroundsSection = false;

    // ============================================================
    // MÉTODOS PÚBLICOS
    // ============================================================
    public string GetBkgFileForState(int state, ViewType view)
    {
        if (!HasBackgroundsSection) return null;
        if (state < 1 || state > 4) return null;
        
        string alias = $"bkg{state:00}";
        if (!BackgroundAliases.TryGetValue(view, out var aliasDict)) return null;
        if (!aliasDict.TryGetValue(alias, out string file) || string.IsNullOrEmpty(file)) return null;
        return file;
    }

    public string GetBkgFogForState(int state)
    {
        if (!HasBackgroundsSection) return null;
        if (state < 1 || state > 4) return null;
        
        string alias = $"bkg{state:00}_fog";
        if (!BackgroundAliases.TryGetValue(ViewType.PSV, out var aliasDict)) return null;
        if (!aliasDict.TryGetValue(alias, out string file) || string.IsNullOrEmpty(file)) return null;
        return file;
    }

    public string GetBkgSunForState(int state)
    {
        if (!HasBackgroundsSection) return null;
        if (state < 1 || state > 4) return null;
        
        string alias = $"bkg{state:00}_sun";
        if (!BackgroundAliases.TryGetValue(ViewType.PSV, out var aliasDict)) return null;
        if (!aliasDict.TryGetValue(alias, out string file) || string.IsNullOrEmpty(file)) return null;
        return file;
    }
}

public enum SkyType { None, RTV, ACV, PSV }