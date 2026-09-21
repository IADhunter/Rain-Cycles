# Rain Cycles Documentation

Discord server
https://discord.com/invite/N4YjBdnSTx

Rain Cycles is a dependency designed to manage multiple room states, supporting a maximum limit of 4 states per room.

---

## Main Modes

The mod operates in two main modes, which are controlled by the `Type` line in each room's configuration file.

### Static Mode (`Type: Static`)

Loads a different `settings_N.txt` file each cycle without any visual transitions. It supports two different setting selection modes:

* **Sequential Mode:** Progresses through the files in numerical order with every new cycle.
* **Random Mode:** Selects a file at random, driven by a seed that is linked to the current cycle number.
* This is done by checking a box in the remix menu.

**Key Features:**
* Ideal for complete environment changes between cycles.
* No visual blending or interpolation.
* Palettes and effects are applied instantly at the start of the cycle.

### Blend Mode (`Type: Blend`)

Starts from the static mode rotation and performs smooth transitions between settings using real-time visual interpolation.

The `Loop` mode can be configured to start immediately or wait for a specific trigger. See the [Trigger System](#trigger-system) section for details.

There are three sub-modes available in `REGION_blend_settings.txt`:

* **Loop:** A continuous cycle of transitions.
  * **Idle:** Wait time before blending starts.
  * **Duration:** Transition duration.
  * The cycle repeats infinitely.
  * Can be configured to start immediately or via a trigger.
* **Cycle:** A one-way linear transition.
  * Passes through states 1, 2, 3, and then stops.
  * The blending advances progressively until completion.
* **Rain:** Triggers when the deadly rain begins (`EndCycle`).
  * Smooth transition towards the final state.
  * Useful for atmospheric changes during the storm.

#### All modes work regardless of whether it is raining in the region or not.

---

## Trigger System

The `Loop` mode can be configured to start immediately or wait for a specific event. This is controlled by the `Trigger` and `wait_time` settings in `REGION_blend_settings.txt`.

```
Trigger: none          # none / cycle / rain
wait_time: 80.0        # Meaning depends on Trigger (value is fully configurable)
```

### Trigger: none (default)

The Loop starts immediately upon entering the region.

```
Entry → Idle (Idle_time) → Blend (Duration) → Idle → Blend → ...
```

### Trigger: cycle

The Loop waits until a specific percentage of the rain cycle has elapsed. The percentage is fully configurable via `wait_time` (any value from 0.0 to 100.0).

| wait_time | Behavior |
|-----------|----------|
| `80.0` (example) | Loop starts when 80% of the cycle has elapsed |
| `50.0` (example) | Loop starts at the halfway point of the cycle |
| `0.0` | Loop starts immediately (equivalent to `Trigger: none`) |

```
Entry → Wait until wait_time% → Jump directly to Blend (no initial Idle)
```

> **Note:** When using `Trigger: cycle`, the first idle (at 0%) is skipped — the Loop starts directly in blend. This avoids an unnecessary pause right after the wait.

### Trigger: rain

The Loop waits until the game triggers deadly rain (`deathRainHasHit`). The delay is fully configurable via `wait_time` (any positive value in seconds).

| wait_time | Behavior |
|-----------|----------|
| `5.0` (example) | Loop starts after waiting 5 seconds after rain begins, skipping the first idle |
| `0.0` | Loop starts immediately when rain begins, with normal Idle |

```
Entry → Wait for deathRain → [+ wait_time seconds] → Jump directly to Blend (if wait_time > 0)
Entry → Wait for deathRain → Normal Idle (if wait_time = 0)
```

### Quick Reference

| Trigger | wait_time | Activation | Skips first Idle? |
|---------|-----------|------------|-------------------|
| `none` | (ignored) | Immediate | ❌ No |
| `cycle` | `0.0` | Immediate | ✅ Yes |
| `cycle` | any `> 0.0` | At specified % of cycle | ✅ Yes |
| `rain` | `0.0` | At deathRain | ❌ No |
| `rain` | any `> 0.0` | Specified seconds after deathRain | ✅ Yes |

---

## Blended Visual Components

During Blend Mode, the following elements are interpolated:

* Room color palettes
* Terrain color palettes
* Scalar effects (brightness, contrast, fog, darkness, hue, etc.)
* Terrain scalar effects (Waves, Light, Grain, SkyFade, StainAmount, StainBrightness, StainHeight)
* Decal opacity
* Light intensity
* Environment light colors (with smooth interpolation)
* Global tints (Multiply, Atmosphere, Cloud)
* Background images (ACV, RTV, PSV, ORV)

---

## Special Effects

### Snow Light

Controls the lighting intensity on snow surfaces. Perfect for creating dynamic snow environments across different states.

### Snow Sparkle

Controls the sparkle/particle brightness on snow. Adds subtle or dramatic shimmer effects.

> ✅ Both effects blend smoothly between states as part of the scalar effects blend. When the effect is missing from a state, Snow Light defaults to `0.5` and Snow Sparkle to `0`.

### Plate Tree / Sentient Rot (Watcher)

In rooms with the `SentientRotInfection` effect, `PlateTree` sprites are forced to black, isolating the rot color from the blended palettes.

---

## DayNight Blocker

Rain Cycles automatically blocks the vanilla DayNight effect and object in managed rooms (Static and Blend modes). This prevents visual conflicts and crashes that would otherwise occur.

> 💡 *If you need DayNight-like functionality, use the state system to create dynamic time-of-day transitions instead.*

---

## State Configuration

Each state corresponds to a `settings_N.txt` file, where `N` is a number from 1 to 4.

* Maximum limit of 4 states per room.
* Files are generated via DevTools.
* The rotation order is intrinsically cyclical: 1, 2, 3, 4, 1...

---

## File Resolution

### Per Room (`settings_N.txt`)

Rain Cycles automatically resolves the correct settings file based on the active DLCs and current slugcat.

**Resolution Priority:**
1. DLC + Slugcat (most specific)
2. Slugcat
3. DLC
4. Base (no suffix)

**Examples:**
* Downpour + Rivulet: `-dwp-rivulet` → `-rivulet` → `-dwp` → base
* Watcher + Survivor: `-wtc` → base
* No DLC + Rivulet: `-rivulet` → base

**Supported suffixes:**
* `-dwp` (Downpour)
* `-wtc` (Watcher)
* `-slugcat` (e.g., `-rivulet`, `-saint`)

**Room Variants:**
* Rooms ending in `-2` (Downpour variants) are checked first before falling back to the base room name.

---

## Configuration Files

### Per Room (`settings_N.txt`)

Contains the complete visual configuration: palettes, effects, tints, decals, lights, terrain palettes, etc.

### Per Region (`REGION_blend_settings.txt`)

Controls the global behavior of the region:

* **Clock:** `true`/`false` (enables the automatic clock).
* **Mode:** `loop` / `cycle` / `rain`.
* **Idle_time:** Waiting time in seconds (for `loop` and `rain`). `0.0` uses the full rain cycle duration.
* **Duration:** Blending duration in seconds. `0.0` uses the full rain cycle duration.
* **Trigger:** `none` / `cycle` / `rain` (see Trigger System above).
* **wait_time:** Percentage (for `cycle`) or seconds (for `rain`) depending on Trigger. Fully configurable.
* **Mod:** Name of the mod containing the background images.
* **Acv / Rtv / Orv / Psv:** Sections to assign images to each state.

### Setting

* **Setting:** `0` to `4` (default: `0`).

Maps a state number to the vanilla `settings.txt` file. When the blend reaches a state that matches this value, Rain Cycles uses the vanilla settings file as a fallback for rooms that don't have a dedicated `settings_N.txt` file.

This avoids duplicating the vanilla settings across multiple numbered files. For example, if a region's vanilla settings work well for state 2, set `Setting: 2` and only create `settings_1.txt`, `settings_3.txt`, and `settings_4.txt` for the states that differ.

In DevTools, this value is shown as **St:** with arrows to adjust it (0–4).

---

## Arena Mode

Rain Cycles works in Arena matches, blending room states per round on a per-level basis.

* Configuration lives in `{level}_blend_settings.txt` inside `levels/raincycles/` of any active mod or `StreamingAssets` (searched recursively). The same `settings_N.txt` state files (up to 4) are resolved per level from the same location.
* DevTools EditMode works in Arena; the clock restarts when leaving EditMode.

---

## Gates

Rain Cycles treats gate rooms as independent mini-regions, each with their own blend configuration and state files.

### How it works

* Any room whose name starts with `GATE_` is automatically detected as a gate.
* When the player enters a gate, its blend settings replace the region's active settings.
* When the player leaves the gate, the region's blend settings are restored.

### Configuration Files

Gate blend settings follow the same format as region blend settings, but are stored separately:

* **File:** `gate_{region1}_{region2}_blend_settings.txt`
* **Location:** `world/gate-rooms/raincycles/` inside any active mod or `StreamingAssets`.

State files (`settings_N.txt`) for gates are also resolved from the same `gate-rooms/raincycles/` folder.

### Example

For a gate between CC and UW, create:

```
world/gate-rooms/raincycles/
  gate_cc_uw_blend_settings.txt
  gate_cc_uw_settings_1.txt
  gate_cc_uw_settings_2.txt
  ...
```

> **Note:** Gates do not inherit settings from their parent region. Each gate is fully self-contained.

---

## Tints (`RC_TINT`)

Two independent tint channels:

* **Multiply:** Multiplies the color of the entire scene.
* **Atmosphere:** Affects fog, the sky in rooms with `AboveCloudsView` and atmosphere.

> 💡 *These can be edited in real-time from DevTools and are saved permanently.*

---

## RainTimerHud Effect

A room effect that controls the visibility of the cycle timer. Add this to the `Effects` line in `settings_N.txt`:

* **Value >= 0.5:** Continuous timer.
* **Value < 0.5:** Paused timer.

---

## Views

Views are a system that adds images and tints to a specific depth layer of the room. Both objects are fully blendable in real-time.

**Supported views:**
* **ACV (Above Clouds View):** Sky images.
* **RTV (Roof Top View):** Rooftop sky images.
* **PSV (Pink Sky View):** Sky, Fog, and Sun layers.
* **AUV (Ancient Urban View):** Watcher DLC view. Supports tints; no configurable background images.
* **ORV (Outer Rim View):** Watcher DLC view. Sky images; replaces the vanilla `otr_sky` element.

> 💡 **Tint channels are not universal:** The Multiply channel is inert in `ORV`, and the Atmosphere channel is inert in `RTV` and `AUV` (no shader consumes those globals in those views).

---

## Public API

Rain Cycles provides a public API for external mods to communicate with the system.

**Namespace:** `RainCycles.API`

### Events

* `OnRegionEnter(Action<RainCyclesRegionEventArgs>)` - Triggered when entering a managed region.
* `OnStateChanged(Action<RainCyclesStateEventArgs>)` - Triggered when the active setting changes or when transitioning between Idle/Blending.

### Event Args (`RainCyclesRegionEventArgs`)

| Field | Type | Description |
|-------|------|-------------|
| `RegionCode` | `string` | Region code entered |
| `Mode` | `BlendMode?` | Current blend mode (`Loop`, `Cycle`, `EndCycle`) or `null` |
| `IsClockEnabled` | `bool` | Whether the clock is active |
| `InitialSetting` | `int` | Initial setting (1-4) |

### Event Args (`RainCyclesStateEventArgs`)

| Field | Type | Description |
|-------|------|-------------|
| `Setting` | `int` | Active setting (1-4) |
| `Progress` | `float` | Blend progress (0-1), `0` if `IsIdle` |
| `IsIdle` | `bool` | `true` if in Idle phase |
| `GlobalT` | `float` | Global cycle time (0-1) |
| `Phase` | `string` | `"Idle"` or `"Blending"` |

### Query Properties

| Property | Type | Description |
|----------|------|-------------|
| `CurrentSetting` | `int` | Active setting (1-4) |
| `NextSetting` | `int` | Target setting during blending |
| `CurrentProgress` | `float` | Real-time blend progress (0-1) |
| `IsIdle` | `bool` | `true` if in Idle phase |
| `CurrentGlobalT` | `float` | Global cycle time (0-1) |
| `CurrentRegion` | `string` | Current region code |
| `IsClockEnabled` | `bool` | `true` if clock is active |
| `CurrentMode` | `BlendMode?` | Current blend mode or `null` |
| `InitialSetting` | `int` | Initial setting for the current region |

### Methods

* `ForceNotify()` - Manually trigger an `OnStateChanged` event with the current state.

### Usage Examples

**Subscribe to events:**
```csharp
using RainCycles.API;

public static class MyMod
{
    public static void Init()
    {
        RainCyclesAPI.OnRegionEnter += OnRegionEnter;
        RainCyclesAPI.OnStateChanged += OnStateChanged;
    }

    private static void OnRegionEnter(RainCyclesRegionEventArgs e)
    {
        // React to region entry
    }

    private static void OnStateChanged(RainCyclesStateEventArgs e)
    {
        // React to state changes
    }
}
```

**Query current state:**
```csharp
if (RainCyclesAPI.IsClockEnabled && RainCyclesAPI.CurrentSetting == 3)
{
    // Do something when setting 3 is active
}
```

**Force notification:**
```csharp
RainCyclesAPI.ForceNotify(); // Immediately dispatches current state
```

---

## DevTools

Accessible from the developer menu (Default key: `O`). It includes:

* Room type selector (Static/Blend/Vanilla)
* View type selector (None/ACV/RTV/PSV/AUV/ORV)
* Settings selector
* Real-time tint editor with hex color selector and screen color picker
* Slot selector for images (Sky/Fog/Sun)
* Manual blending editor with a slider
* Mode and timer selector
* Trigger selector (None/Cycle/Rain) with wait_time editor
* Edit Mode with automatic state selection

---

## Developer Tab in Remix Menu

The Developer tab provides tools for mod creators to batch-generate room states and control where files are saved. Access it from the Rain Cycles options in the Remix menu.

### Destination Mod

Select which active mod receives the generated `settings_N.txt` files. By default, files are written to the region's own mod folder (or vanilla if no mod owns the region).

* Use the search box to filter mods by ID or name.
* Click a mod to select it as the save destination.
* Click **X** to clear the selection and restore default behavior.

### Generate States

Batch-generates the 4 state files (`settings_1.txt` through `settings_4.txt`) for a region. Each file is a copy of the existing `settings.txt` found in the region's rooms folder.

* Use the search box to filter regions by ID or name.
* Click a region to generate its states.
* If the region already has state files, it is skipped.

> **Note:** This tool only affects file writing. Reading always follows the standard mod priority order described in [File Resolution](#file-resolution).

---

## Known Limitations

* Compatibility with `Forecast` has not been tested.

---

## Important Note

> ⚠️ **Dependency Only:** This mod does not add any visual changes on its own. It functions strictly as a tool/dependency. You must create the room states within another mod for Rain Cycles to load and manage them.

---

> [!NOTE]
> This project is currently under active development. Features and documentation are subject to change.

made with assistance from Deepseek AI ヾ(•ω•`)o
