# Rain Cycles Documentation

Discord server
https://discord.com/invite/N4YjBdnSTx

Rain Cycles is a dependency designed to manage multiple room states, supporting a maximum limit of 4 states per room.

---

## Main Modes

The mod operates in two main modes, which are controlled by the `<Type:...>` tag inside the `RainCycles:` line of each state file (`settings_N.txt`). See [The `RainCycles:` Line](#the-raincycles-line).

### Static Mode (`Type: Static`)

Loads a different `settings_N.txt` file each cycle without any visual transitions. The state to load is selected by the mod's Remix menu options:

* **Cycle (default):** Makes the states advance in a fixed, sequential order, following your save's cycle number. A new story starts at state 1, and if you are mid-story it simply continues from the cycle you are on. The custom seed has no effect on this mode.
* **Procedural:** Changes the fixed sequence into something more dynamic, where each state is chosen by a calculation based on your cycle number and the seed. That means leaving and re-entering the same cycle keeps that state, and advancing to another cycle produces a pseudo-random state that will also be locked to that next cycle.
* **Random:** True randomness, with no rules or coherence. The state is picked unpredictably on each occasion, ignoring the cycle, your progress and previous plays.

**Ignore the Cycle** (only active in Procedural): Stops the state from following the cycle-based calculation. The calculation becomes more flexible and less predictable, but coherence is kept, meaning that if a cycle starts with a specific state, the game keeps that same state until you advance to the next cycle.

**Seed:** Lets you customize the seed used by Procedural Mode to pick the state. Has no effect on Cycle or Random modes.

**Key Features:**
* Ideal for complete environment changes between cycles.
* No visual blending or interpolation.
* Palettes and effects are applied instantly at the start of the cycle.

### Blend Mode (`Type: Blend`)

Starts from the static mode rotation and performs smooth transitions between settings using real-time visual interpolation.

The `Loop` mode can be configured to start immediately or wait for a specific trigger. See the [Trigger System](#trigger-system) section for details.

There are three sub-modes available in `{region}_blend_settings.txt`:

* **Loop:** A continuous cycle of transitions.
  * **Idle:** Wait time before blending starts.
  * **Duration:** Transition duration.
  * The cycle repeats infinitely.
  * Can be configured to start immediately or via a trigger.
* **Cycle:** A one-way linear transition.
  * Traverses three states starting from the current initial state (e.g. starting from state 4: 4 → 1 → 2) and then stops.
  * The blending advances progressively until completion.
* **Rain:** Triggers when the deadly rain begins (file value: `endcycle`).
  * Smooth transition towards the final state.
  * Useful for atmospheric changes during the storm.

#### All modes work regardless of whether it is raining in the region or not.

---

## Trigger System

The `Loop` mode can be configured to start immediately or wait for a specific event. This is controlled by the `Trigger` and `wait_time` settings in `{region}_blend_settings.txt`. Triggers only apply in `Loop` mode.

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
* Global tints (Multiply, Atmosphere)
* Background images (ACV, RTV, PSV, ORV)

---

## Special Effects

### Snow Light

Controls the lighting intensity on snow surfaces. Perfect for creating dynamic snow environments across different states.

### Snow Sparkle

Controls the sparkle/particle brightness on snow. Adds subtle or dramatic shimmer effects.

> ✅ Both effects blend smoothly between states as part of the scalar effects blend. When the effect is missing from a state, Snow Light defaults to `0.5` and Snow Sparkle to `0`.

### Plate Tree / Sentient Rot (Watcher)

In Blend rooms with the `SentientRotInfection` effect, `PlateTree` sprites are forced to black, isolating the rot color from the blended palettes.

---

## DayNight Blocker

Rain Cycles automatically blocks the vanilla DayNight effect and object in managed rooms (Static and Blend modes). This prevents visual conflicts and crashes that would otherwise occur.

> 💡 *If you need DayNight-like functionality, use the state system to create dynamic time-of-day transitions instead.*

---

## State Configuration

Each state corresponds to a `settings_N.txt` file, where `N` is a number from 1 to 4.

* **Location:** `world/{region}-rooms/raincycles/` inside the mod that owns the region (or `StreamingAssets`).
* Maximum limit of 4 states per room.
* Files are generated via DevTools.
* The rotation order is intrinsically cyclical: 1, 2, 3, 4, 1...

---

## The `RainCycles:` Line

Extended Rain Cycles data is stored as a single line inside each state file (`settings_N.txt`):

```
RainCycles: <Type:Static><View:ACV><Tint:#FFFFFF #AE4987><Mod:my_mod,my_image>
```

* `<Type:Static|Blend>` — room mode. Absent (or any other value) = Vanilla.
* `<View:ACV|RTV|PSV|AUV|ORV>` — active view. Ignored if `<Type>` does not appear earlier in the line.
* `<Tint:#RRGGBB #RRGGBB>` — Multiply and Atmosphere colors. Requires `<View>` to appear earlier.
* `<Mod:mod,sky[,sun,fog]>` — background images. Requires `<View>` to appear earlier.

The line is written automatically by the vanilla **Save** button in DevTools (with **Edit** mode active); there is no automatic saving.

---

## File Resolution

### Per Room (`settings_N.txt`)

Rain Cycles automatically resolves the correct settings file based on the active DLCs and current slugcat.

**Location:** `world/{region}-rooms/raincycles/` inside the mod that owns the region (or `StreamingAssets`).

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

**Subfolders:**
* The priority list only checks the folder root. If nothing matches, Rain Cycles searches the folder recursively, so files can also live in subfolders (e.g. `vanilla/`, `downpour/`, `1/`, `2/`...).

---

## Configuration Files

### Per Room (`settings_N.txt`)

Contains the complete visual configuration: palettes, effects, tints, decals, lights, terrain palettes, etc. It also carries the `RainCycles:` line (mode, view, tints and background images) — see [The `RainCycles:` Line](#the-raincycles-line).

### Per Region (`{region}_blend_settings.txt`)

**Location:** `world/{region}-rooms/raincycles/{region}_blend_settings.txt` (the file is created automatically when the Dev UI is opened).

Controls the global behavior of the region. Only these keys are recognized; anything else in the file is ignored:

* **Clock:** `true`/`false` — enables the automatic clock (default: `false`).
* **Mode:** `loop` / `cycle` / `endcycle` (default: `loop`). DevTools displays `endcycle` as **Rain**.
* **Idle_time:** Meaning depends on the mode (default: `5.0`):
  * `loop`: seconds to wait before each blend starts. `0.0` uses the full rain cycle duration.
  * `cycle`: percentage (0–100) of the rain cycle at which the one-way blend starts. `<= 0` starts immediately.
  * `endcycle`: seconds to wait after the deadly rain begins. `<= 0` starts immediately.
* **Duration:** Blending duration in seconds (default: `10.0`). `0.0` uses the full rain cycle duration.
* **Trigger:** `none` / `cycle` / `rain` (default: `none`; only applies in `loop` mode — see Trigger System above).
* **wait_time:** Percentage (for `cycle`) or seconds (for `rain`) depending on Trigger (default: `0.0`).

### Setting

* **Setting:** `0` to `4` (default: `0`).

Maps a state number to the vanilla `settings.txt` file. When the blend reaches a state that matches this value, Rain Cycles uses the vanilla settings file as a fallback for rooms that don't have a dedicated `settings_N.txt` file.

This avoids duplicating the vanilla settings across multiple numbered files. For example, if a region's vanilla settings work well for state 2, set `Setting: 2` and only create `settings_1.txt`, `settings_3.txt`, and `settings_4.txt` for the states that differ.

In DevTools, this value is shown as **St:** with arrows to adjust it (0–4).

### Ancestor Files (`ancestor_N.txt`)

Rooms without a template (`Template: NONE` or no `Template:` line) inherit the fields they don't declare from the vanilla ancestor. Rain Cycles inserts a regional ancestor per state in between:

```
Room → ancestor_N.txt (regional, state N) → vanilla ancestor
```

* **Location:** `world/{region}/raincycles/ancestor_N.txt` (`N` = 1–4), following the standard save rules: Destination Mod if chosen, else the region's owning mod, else vanilla.
* Create them with the **Ancestor** button in DevTools (Region tab, **Edit** mode). Only missing files are written (vanilla values), never overwriting yours — edit them to make inherited fields (palette, effect colors, waves, terrain, etc.) differ between states.
* They only apply to rooms that load their own state files; the `Template:` line inside an ancestor file is ignored.

---

## Arena Mode

Rain Cycles works in Arena matches, blending room states per round on a per-level basis.

* Configuration lives in `{level}_blend_settings.txt` inside `levels/raincycles/` of any active mod or `StreamingAssets` (searched recursively). The same `settings_N.txt` state files (up to 4) are resolved per level from the same location.
* Arena uses only `loop` and `cycle`: `endcycle` is forced to `loop`, triggers are forced to `none`, and `Setting` is forced to `0`.
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

* **File:** `gate_{room}_blend_settings.txt` (the room name in lower case — e.g. room `GATE_CC_UW` → `gate_cc_uw_blend_settings.txt`).
* **Location:** `world/gates/raincycles/` inside any active mod or `StreamingAssets`.

State files (`settings_N.txt`) for gates are resolved from the same `world/gates/raincycles/` folder, either at its root or in subfolders `1/`, `2/`, `3/`, `4/` (searched recursively).

### Example

For a gate between CC and UW, create:

```
world/gates/raincycles/
  gate_cc_uw_blend_settings.txt
  1/gate_cc_uw_settings_1.txt
  2/gate_cc_uw_settings_2.txt
  ...
```

> **Note:** Gates do not inherit settings from their parent region. Each gate is fully self-contained.

---

## Tints

Two independent tint channels, stored as `<Tint:#RRGGBB #RRGGBB>` (Multiply, Atmosphere) in the `RainCycles:` line of each state file:

* **Multiply:** Multiplies the color of the entire scene.
* **Atmosphere:** Affects fog, the sky in rooms with `AboveCloudsView` and atmosphere.

> 💡 *Edited in real time from the **View** tab of DevTools (channel selector: `Multi` / `Atmos`). Changes apply to the current session only: to persist them, enable **Edit** mode and press the vanilla **Save** button. There is no automatic saving.*

---

## RainTimerHud Effect

A room effect that controls the visibility of the cycle timer. Add this to the `Effects` line in `settings_N.txt`:

* **Value >= 0.5:** Forces the timer to be shown (continuous), even in regions that normally hide it.
* **Value > 0 and < 0.5:** Forces the hidden-timer display; it only has an effect in regions that hide the timer.
* **Value `0` or absent:** No effect — the player's Remix timer-hiding option applies.

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

## Background Images (per room and state)

Each state can define its own background image set through the `<Mod:...>` tag of its `RainCycles:` line:

```
RainCycles: <Type:Blend><View:ACV><Tint:#FFFFFF #FFFFFF><Mod:my_mod,my_image>
```

* **Format:** `<Mod:mod,sky[,sun,fog]>` — `sky` is required; `sun` and `fog` are optional and only used by **PSV**.
* The tag requires a `<View>` in the same line; without one, it is ignored.
* **Images:** PNG files inside the chosen mod's `Illustrations/` folder (subfolders included). The pseudo-mod `Default` reads from `StreamingAssets/Illustrations/{name}.png`.
* **Editing:** from the **View** tab of DevTools (Mod / Image buttons and the slot selector — Sky, Fog, Sun; Fog and Sun only for PSV).
* While the room has a background, the vanilla sky/fog elements of the view are hidden automatically (and restored when it doesn't).

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

Enable dev tools with `O` (requires dev tools enabled in the game setup or the DevTools mod), then open/close the Dev UI with `H`. The **Rain Cycles** panel is added to every page of the Dev UI and has three tabs, switched with the arrows in the panel's bottom-right corner:

* **Room:** Room type (Static / Blend / Vanilla) and the manual blend slider (shown only when the room has all 4 states).
* **View:** View selector (None/ACV/RTV/PSV/AUV/ORV), **Tint** toggle, tint channel selector (`Multi` / `Atmos`) with hex/HSV color editor, screen color picker, and the background row (Mod / Image / slot).
* **Region** (shown as **Arena** in arena matches): Clock, Mode (Loop / Cycle / Rain), `Idle_time`, `Duration`, Trigger + `wait_time`, and **St:** (Setting). Plus the **Ancestor** button, which creates the missing `ancestor_N.txt` files for the current region.
* The **Edit** toggle, the active state file label and the state selector (`1`–`4` with `+`/`-`) are shared by the **Room** and **View** tabs; **Region** has its own **Edit** toggle. Almost every control requires **Edit** mode to change values.

**Persistence:**
* **Region/Arena** controls write to the active blend settings file immediately (`{region}_blend_settings.txt`, or `{level}_blend_settings.txt` in Arena). Only the Clock toggle works without **Edit** mode.
* Room data (type, view, tints, background) is written to `settings_N.txt` only when you press the vanilla **Save** button with **Edit** mode active.

---

## Developer Tab in Remix Menu

The Developer tab provides tools for mod creators to batch-generate room states and control where files are saved. Access it from the Rain Cycles options in the Remix menu.

### Destination Mod

Select which active mod receives the generated `settings_N.txt` files. By default, files are written to the region's own mod folder (or vanilla if no mod owns the region).

* Use the search box to filter mods by ID or name.
* Click a mod to select it as the save destination.
* Click **X** to clear the selection and restore default behavior.

### Generate States

Batch-generates state files for a region: every `settings` file in the region's rooms folder is copied 4 times as `{name}_1.txt` … `{name}_4.txt` (so `settings.txt` becomes `settings_1.txt` … `settings_4.txt`).

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
