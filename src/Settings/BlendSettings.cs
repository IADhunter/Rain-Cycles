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
}

public enum SkyType { None, RTV, ACV, PSV, ORV }