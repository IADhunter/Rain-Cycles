namespace RainCycles.Settings;

public enum BlendMode
{
    Loop,
    Cycle,
    Rain
}

public enum LoopTrigger
{
    None,
    Cycle,
    Rain
}

public partial class BlendSettings
{
    // CONFIGURACIÓN PRINCIPAL
    public BlendMode Mode = BlendMode.Loop;
    public bool Clock = false;
    public float IdleTime = 5f;
    public float Duration = 10f;

    // TRIGGER DE ACTIVACIÓN PARA LOOP
    public LoopTrigger Trigger = LoopTrigger.None;
    public float WaitTime = 0f;
}

public enum SkyType { None, RTV, ACV, PSV, ORV }
