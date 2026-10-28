# Rain Cycles Documentation

Discord server
https://discord.com/invite/N4YjBdnSTx

Rain Cycles is a dependency designed to manage multiple room states.

---

## Main Modes

The mod operates in two main modes, which are controlled by the `<Type:...>` tag inside the `RainCycles:` line of each state file (`settings_N.txt`, or the room's base file when it declares `<State:N>`).

### Static Mode (`Type: Static`)

Loads a different state file each cycle without any visual transitions — `settings_N.txt`, or the room's base file when it is assigned with `<State:N>`. The state to load is selected by the mod's Remix menu options:

* **Cycle (default):** Makes the states advance in a fixed, sequential order, following your save's cycle number. A new story starts at state 1, and if you are mid-story it simply continues from the cycle you are on. The custom seed has no effect on this mode.
* **Procedural:** Changes the fixed sequence into something more dynamic, where each state is chosen by a calculation based on your cycle number and the seed. That means leaving and re-entering the same cycle keeps that state, and advancing to another cycle produces a pseudo-random state that will also be locked to that next cycle.
* **Random:** True randomness, with no rules or coherence. The state is picked unpredictably on each occasion, ignoring the cycle, your progress and previous plays.

**Ignore the Cycle** (only active in Procedural): Stops the state from following the cycle-based calculation. The calculation becomes more flexible and less predictable, but coherence is kept, meaning that if a cycle starts with a specific state, the game keeps that same state until you advance to the next cycle.

**Seed:** Lets you customize the seed used by Procedural Mode to pick the state. Has no effect on Cycle or Random modes.

### Blend Mode (`Type: Blend`)

Starts from the static mode rotation and performs smooth transitions between settings using real-time visual interpolation.

There are three sub-modes available:

* **Loop:** A continuous cycle of transitions that repeats infinitely.
  * It can start immediately or wait for a specific trigger.
* **Cycle:** A one-way linear transition that starts once a given percentage of the rain cycle has elapsed.
  * Traverses three states starting from the current initial state (e.g. starting from state 4: 4 → 1 → 2) and then stops.
* **Rain:** Triggers when the deadly rain begins.
  * Smooth transition towards the final state.
  * Useful for atmospheric changes during the storm.

> *All modes work regardless of whether it is raining in the region or not.*

---

## Trigger System

The `Loop` mode can be configured to start immediately or wait for a specific event. This is controlled by the `Trigger` and `wait_time` settings in `{region}_blend_settings.txt`. Triggers only apply in `Loop` mode.

```
Trigger: none          # none / cycle / rain
wait_time: 80.0        # Meaning depends on Trigger (value is fully configurable)
```

### Trigger: none (default)

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

> ✅ Both effects blend smoothly between states. When the effect is missing from a state, Snow Light defaults to `0.5` and Snow Sparkle to `0`.

### Plate Tree / Sentient Rot (Watcher)

In Blend rooms with the `SentientRotInfection` effect, `PlateTree` sprites are forced to black, isolating the rot color from the blended palettes.

---

## DayNight Blocker

Rain Cycles automatically blocks the vanilla DayNight effect and object in managed rooms (Static and Blend modes). This prevents visual conflicts and crashes that would otherwise occur.

> 💡 *If you need DayNight-like functionality, use the state system to create dynamic time-of-day transitions instead.*

---

## Expedition: `bur-blinded` Suspension

Rain Cycles suspends itself during Expedition runs with the **Blinded** burden (`bur-blinded`), so the run renders like vanilla. Switching between them needs no restart. Room configuration is not lost: the `RainCycles:` line is still preserved when saving with the vanilla DevTools **Save** button.

---

## State Files

Each state corresponds to a `settings_N.txt` file, where `N` is a number from 1 to 4 — or to the room's base file when it declares `<State:N>`.

* **Location:** `world/{region}-rooms/raincycles/` inside the mod that owns the region (or `StreamingAssets`). The base file lives in the room folder itself (`world/{region}-rooms/`).
* Maximum limit of 4 states per room.
* Files are generated via DevTools.
* The rotation order is intrinsically cyclical: 1, 2, 3, 4, 1...

A state file contains the complete visual configuration: palettes, effects, tints, decals, lights, terrain palettes, etc. It also carries the `RainCycles:` line (mode, view, tints and background images).

### Resolution

Rain Cycles automatically resolves the correct settings file based on the active DLCs and current slugcat.

**Resolution Priority:**
1. DLC + Slugcat (most specific)
2. Slugcat
3. DLC
4. Base (no suffix)

**Fallback to the base file:** if no `settings_N.txt` exists for state `N`, the room's base file is used when it declares `<State:N>`.

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

## The `RainCycles:` Line

Extended Rain Cycles data is stored as a single line inside each state file (`settings_N.txt`, or the room's base file promoted with `<State>`):

```
RainCycles: <State:2><Type:Static><View:ACV><Tint:#FFFFFF #AE4987><Mod:my_mod,my_image>
```

* `<State:1|2|3|4>` — marks the room's **base file** (`{room}_settings.txt`) as state `N`. Only valid in a base file, always first, and ignored in `settings_N.txt` files.
* `<Type:Static|Blend>` — room mode. Absent (or any other value) = Vanilla.
* `<View:ACV|RTV|PSV|AUV|ORV>` — active view. Ignored if `<Type>` does not appear earlier in the line.
* `<Tint:#RRGGBB #RRGGBB>` — Multiply and Atmosphere colors. Requires `<View>` to appear earlier.
* `<Mod:mod,sky[,sun,fog]>` — background images. Requires `<View>` to appear earlier.

Only one space is used, right after `RainCycles:`; every tag is glued with `><`.

The line is written by the vanilla **Save** button in DevTools (with **Edit** mode active). Rain Cycles never saves it automatically — the only exception is the `State` field, which writes immediately.

A base file **without** `<State>` is invisible to Rain Cycles: its tags are not read, nothing is written into it (not even the line when saving), and the Static / Blend / Vanilla buttons do nothing on it. The single exception is the `State` field itself, which is what gives the file a state.

---

## Per Region (`{region}_blend_settings.txt`)

**Location:** `world/{region}-rooms/raincycles/{region}_blend_settings.txt` (the file is created automatically when the Dev UI is opened).

Controls the global behavior of the region. Only these keys are recognized; anything else in the file is ignored:

* **Clock:** `true`/`false` — enables the automatic clock (default: `false`).
* **Mode:** `loop` / `cycle` / `rain` (default: `loop`).
* **Idle_time:** Meaning depends on the mode (default: `5.0`):
  * `loop`: seconds to wait before each blend starts. `0.0` uses the full rain cycle duration.
  * `cycle`: percentage (0–100) of the rain cycle at which the one-way blend starts. `<= 0` starts immediately.
  * `rain`: seconds to wait after the deadly rain begins. `<= 0` starts immediately.
* **Duration:** Blending duration in seconds (default: `10.0`). `0.0` uses the full rain cycle duration.
* **Trigger:** `none` / `cycle` / `rain` (default: `none`; only applies in `loop` mode).
* **wait_time:** Percentage (for `cycle`) or seconds (for `rain`) depending on Trigger (default: `0.0`).

---

## Base File as State (`<State:N>`)

A room's **base file** (`{room}_settings.txt`, the non-numbered one) can be used as one of its states instead of duplicating it into `settings_N.txt`:

```
world/{region}-rooms/{room}_settings.txt        ← RainCycles: <State:4>
world/{region}-rooms/raincycles/settings_1.txt
world/{region}-rooms/raincycles/settings_2.txt
world/{region}-rooms/raincycles/settings_3.txt
```

* **Assign:** write `RainCycles: <State:N>` (`N` = 1–4) in the base file, or use the **`State:`** field in DevTools (Room tab, **Edit** mode, `0` = not assigned).
* **Duplicate number:** assigning `State:N` when `settings_N.txt` already exists deletes that numbered file on the spot (the base takes its place). If the deleted file was the one loaded, the room reloads immediately.
* **Remove:** setting the field back to `0` removes just the `<State:N>` tag (the other tags stay; the line is deleted if nothing is left). Pressing `−` on that state removes the whole `RainCycles:` line. In both cases the base file itself is never deleted, and if it was the loaded state the room falls back to the highest remaining state, or to the vanilla base.
* **Vanilla button:** on an assigned base it removes `Type`/`View`/tints but keeps `State:N`, so the file is still state `N`.
* **Which base wins:** if several `{room}_settings.txt` exist (vanilla + mods), the one with `<State>` wins, then the one with any `RainCycles:` line, then the first one found (mods in reverse load order, then `StreamingAssets`). Rain Cycles always writes into that winner and never creates or copies base files.

---

## Ancestor Files (`ancestor_N.txt`)

Rooms that resolve to the vanilla root ancestor (`Template: NONE`) inherit the fields they don't declare from it. Rain Cycles inserts an ancestor per state in between:

```
Room → ancestor_N.txt (state N) → vanilla root ancestor
```

* **Location:** `world/{region}/raincycles/ancestor_N.txt` for rooms, `world/gates/raincycles/ancestor_N.txt` for gates (`N` = 1–4). Reading and writing use the same mod priority as state files; new files land in the Destination Mod if chosen, else in the mod that owns the folder, else vanilla.
* **Per-gate override:** `world/gates/raincycles/{gate}_ancestor_N.txt` (e.g. `gate_su_hi_ancestor_2.txt`) wins over the shared gate ancestor for that gate and state. Priority: `{gate}_ancestor_N.txt` → `ancestor_N.txt` → vanilla root ancestor. The per-gate files are opt-in (copy one and rename it).
* Create them with the **Ancestor** button (Region tab, **Edit** mode). Only missing files are written (vanilla values), never overwriting yours; edit them to change the inherited fields per state.
* A room or gate that declares (or, without a `Template:` line, inherits) a region template keeps that template; the `Template:` line inside an ancestor file is ignored.

---

## Arena Mode

Arena matches blend room states per round, per level.

* Configuration lives in `{level}_blend_settings.txt` inside `levels/raincycles/` of any active mod or `StreamingAssets` (searched recursively). The same `settings_N.txt` state files (up to 4) are resolved per level from the same location.
* Arena uses only `loop` and `cycle`: `rain` is forced to `loop` and triggers are forced to `none`.
* Arena levels also have a **base file**: `Levels/{level}_settings.txt` (vanilla `StreamingAssets/Levels/`, or a mod's `Levels/`). It can be assigned as a state with `RainCycles: <State:N>`.

---

## Gates

Gates use the blend settings of the region that is active in the gate, but their state files live in `world/gates/raincycles/`.

### How it works

* When a gate opens, vanilla activates the destination world (`OverWorld.SwitchWorlds`). The gate therefore uses the destination region's `{region}_blend_settings.txt` (`Clock`, `Mode`, `Idle_time`, `Duration`, `Trigger`, `wait_time`).
* The clock saves its state when the region changes. In `Loop` and `Rain` it restores the blend percentage when both regions share the same mode; `Cycle` does not transfer the percentage.
* **Ancestors are gate-owned too:** a gate takes its `ancestor_N.txt` from `world/gates/raincycles/`, never from the active region, so the same gate looks identical entering from either side (and does not change when the world switches mid-gate).

### State Files

State files (`settings_N.txt`) for gates live in `world/gates/raincycles/`, either at its root or in subfolders `1/`, `2/`, `3/`, `4/` (searched recursively).

The vanilla fallback for a gate is its own base file, `world/gates/GATE_..._settings.txt`, which can be promoted to a state with `RainCycles: <State:N>`.

### Example

For a gate between CC and UW, create:

```
world/gates/raincycles/
  1/gate_cc_uw_settings_1.txt
  2/gate_cc_uw_settings_2.txt
  ...
```

---

## Tints

Two independent tint channels, stored as `<Tint:#RRGGBB #RRGGBB>` (Multiply, Atmosphere) in the `RainCycles:` line of each state file (`settings_N.txt`, or the room's base file assigned with `<State:N>`):

* **Multiply:** Multiplies the color of the entire scene.
* **Atmosphere:** Affects fog, the sky in rooms with `AboveCloudsView` and atmosphere.

---

## RainTimerHud Effect

A room effect that controls the visibility of the cycle timer. Add this to the `Effects` line in `settings_N.txt` (or in the room's base file assigned with `<State:N>`):

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
* **AUV (Ancient Urban View):** Watcher DLC view; no configurable background images.
* **ORV (Outer Rim View):** Watcher DLC view. Sky images; replaces the vanilla `otr_sky` element.

> 💡 **Tint channels are not universal:** The Multiply channel is inert in `ORV`, and the Atmosphere channel is inert in `AUV` (no shader consumes those globals in those views). In `RTV`, the Atmosphere channel colors both the view elements and the global atmosphere.

---

## Background Images (per room and state)

Each state can define its own background image set through the `<Mod:...>` tag of its `RainCycles:` line:

```
RainCycles: <Type:Blend><View:ACV><Tint:#FFFFFF #FFFFFF><Mod:my_mod,my_image>
```

* **Format:** `<Mod:mod,sky[,sun,fog]>` — `sky` is required; `sun` and `fog` are optional and only used by **PSV**.
* The tag requires a `<View>` in the same line; without one, it is ignored.
* The same line can carry `<State:N>` when the file is the room's base.
* **Images:** PNG files inside the chosen mod's `Illustrations/` folder (subfolders included). The pseudo-mod `Default` reads from `StreamingAssets/Illustrations/{name}.png`.
* **Editing:** from the **View** tab of DevTools (Mod / Image buttons and the slot selector — Sky, Fog, Sun; Fog and Sun only for PSV). A View must be selected first, since the available image slots depend on the view type.
* While the room has a background, the vanilla sky/fog elements of the view are hidden automatically (and restored when it doesn't).

---

## Public API

Rain Cycles provides a public API for external mods to communicate with the system.

**Namespace:** `RainCycles.API`

### Events

* `OnRegionEnter(Action<RainCyclesRegionEventArgs>)` - Triggered when entering a managed region.
* `OnStateChanged(Action<RainCyclesStateEventArgs>)` - Triggered when the active state changes or when transitioning between Idle/Blending.

### Event Args (`RainCyclesRegionEventArgs`)

| Field | Type | Description |
|-------|------|-------------|
| `RegionCode` | `string` | Region code entered |
| `Mode` | `BlendMode?` | Current blend mode (`Loop`, `Cycle`, `Rain`) or `null` |
| `IsClockEnabled` | `bool` | Whether the clock is active |
| `InitialState` | `int` | Initial state (1-4) |

### Event Args (`RainCyclesStateEventArgs`)

| Field | Type | Description |
|-------|------|-------------|
| `State` | `int` | Active state (1-4) |
| `Progress` | `float` | Blend progress (0-1), `0` if `IsIdle` |
| `IsIdle` | `bool` | `true` if in Idle phase |
| `GlobalT` | `float` | Global cycle time (0-1) |
| `Phase` | `string` | `"Idle"` or `"Blending"` |

### Query Properties

| Property | Type | Description |
|----------|------|-------------|
| `CurrentState` | `int` | Active state (1-4) |
| `NextState` | `int` | Target state during blending |
| `CurrentProgress` | `float` | Real-time blend progress (0-1) |
| `IsIdle` | `bool` | `true` if in Idle phase |
| `CurrentGlobalT` | `float` | Global cycle time (0-1) |
| `CurrentRegion` | `string` | Current region code |
| `IsClockEnabled` | `bool` | `true` if clock is active |
| `CurrentMode` | `BlendMode?` | Current blend mode or `null` |
| `InitialState` | `int` | Initial state for the current region |

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
if (RainCyclesAPI.IsClockEnabled && RainCyclesAPI.CurrentState == 3)
{
    // Do something when state 3 is active
}
```

**Force notification:**
```csharp
RainCyclesAPI.ForceNotify(); // Immediately dispatches current state
```

---

## DevTools

Enable dev tools with `O` (requires dev tools enabled in the game setup or the DevTools mod), then open/close the Dev UI with `H`. The **Rain Cycles** panel is added to every page of the Dev UI and has three tabs, switched with the arrows in the panel's bottom-right corner:

* **Room:** Room type (Static / Blend / Vanilla), the **`State:`** field (assigns the room's base file as a state, `0` = not assigned) and the manual blend slider (shown only when the room has all 4 states).
* **View:** View selector (None/ACV/RTV/PSV/AUV/ORV), **Tint** toggle, tint channel selector (`Multi` / `Atmos`) with hex/HSV color editor, screen color picker, and the background row (Mod / Image / slot).
* **Region** (shown as **Arena** in arena matches): Clock, Mode (Loop / Cycle / Rain), `Idle_time`, `Duration`, Trigger + `wait_time`. Plus the **Ancestor** button, which creates the missing `ancestor_N.txt` files for the current region — or, inside a gate, the four shared gate ancestors in `world/gates/raincycles/`.
* The **Edit** toggle, the active state file label and the state selector (`1`–`4` with `+`/`-`) are shared by the **Room** and **View** tabs; **Region** has its own **Edit** toggle. Almost every control requires **Edit** mode to change values.

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

> **Note:** This tool only affects file writing. Reading always follows the standard mod priority order.

---

## Known Limitations

* Compatibility with `Forecast` has not been tested.

---

## Important Note

> ⚠️ **Dependency Only:** This mod does not add any visual changes on its own. It functions strictly as a tool/dependency. You must create the room states within another mod for Rain Cycles to load and manage them.

---

made with assistance from AI ヾ(•ω•`)o
