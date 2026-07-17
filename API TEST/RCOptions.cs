using Menu.Remix.MixedUI;
using UnityEngine;

namespace RainCycles;

// Interfaz Remix de Rain Cycles.
// Registrada en Plugin.cs via MachineConnector.SetRegisteredOI.
public class RCOptions : OptionInterface
{
    // El Configurable vive aquí — se crea en el ctor, antes de Initialize
    public readonly Configurable<bool> randomCycles;

    public RCOptions()
    {
        randomCycles = this.config.Bind(
            "randomCycles",
            false,
            new ConfigurableInfo(
                "Each cycle picks a random day-time setting instead of rotating in order. Same cycle number always loads the same setting.",
                null, "",
                "Random Cycles"));
    }

    public override void Initialize()
    {
        base.Initialize();

        var tab = new OpTab(this, "Main");
        Tabs = new[] { tab };

        var chk  = new OpCheckBox(randomCycles, new Vector2(20f, 520f));
        var lbl  = new OpLabel(55f, 524f, "Random Cycles", false);
        var desc = new OpLabel(20f, 495f,
            "Each cycle picks a random day-time setting instead of rotating in order.\nSame cycle number always loads the same setting.",
            false);
        desc.color = new Color(0.6f, 0.6f, 0.6f);

        tab.AddItems(chk, lbl, desc);
    }
}