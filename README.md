# Rain Cycles v1.5 Documentation

## Main Modes

**Rain Cycles** is a dependency designed to manage multiple room states, supporting a maximum limit of 4 states per room.

The mod operates in two main modes, which are controlled by the `RC_TYPE` line in each room's configuration file.

### Static Mode (`RC_TYPE: Static`)

Loads a different `settings_N.txt` file each cycle without any visual transitions. It supports two different setting selection modes:

* **Sequential Mode:** Progresses through the files in numerical order with every new cycle.
* **Random Mode:** Selects a file at random, driven by a seed that is linked to the current cycle number.
* This is done by checking a box in the remix menu.

**Key Features:**
* Ideal for complete environment changes between cycles.
* No visual blending or interpolation.
* Palettes and effects are applied instantly at the start of the cycle.

### Blend Mode (`RC_TYPE: Blend`)

Starts from the static mode rotation and performs smooth transitions between settings using real-time visual interpolation.

There are three sub-modes available in `REGION_blend_settings.txt`:

* **Loop:** A continuous cycle of transitions.
  * **Idle:** Wait time before blending starts.
  * **Duration:** Transition duration.
  * The cycle repeats infinitely.
* **Cycle:** A one-way linear transition.
  * Passes through states 1, 2, 3, and then stops.
  * The blending advances progressively until completion.
* **Rain:** Triggers when the deadly rain begins (`EndCycle`).
  * Smooth transition towards the final state.
  * Useful for atmospheric changes during the storm.

#### All modes work regardless of whether it is raining in the region or not.

---

## Blended Visual Components

During Blend Mode, the following elements are interpolated:

* Color palettes
* Scalar effects (brightness, contrast, fog, etc.)
* Decal opacity
* Light intensity
* Environment light colors (with smooth interpolation)
* Global tints (Multiply, Atmosphere, Cloud)
* Background images (ACV, RTV, PSV)

---

## State Configuration

Each state corresponds to a `settings_N.txt` file, where `N` is a number from 1 to 4.

* Maximum limit of 4 states per room.
* Files are generated via DevTools.
* The rotation order is intrinsically cyclical: 1, 2, 3, 4, 1...

---

## Configuration Files

### Per Room (`settings_N.txt`)

Contains the complete visual configuration: palettes, effects, tints, decals, lights, etc.

### Per Region (`REGION_blend_settings.txt`)

Controls the global behavior of the region:

* **Clock:** `true`/`false` (enables the automatic clock).
* **Mode:** `loop` / `cycle` / `rain`.
* **Idle_time:** Waiting time in seconds (for `loop` and `rain`).
* **Duration:** Blending duration in seconds.
* **Mod:** Name of the mod containing the background images.
* **Acv / Rtv / Psv:** Sections to assign images to each state.

---

## Tints (`RC_TINT`)

Three independent tint channels:

* **Multiply:** Multiplies the color of the entire scene.
* **Atmosphere:** Affects fog, the sky in rooms with `AboveCloudsView`and atmosphere.

> 💡 *These can be edited in real-time from DevTools and are saved permanently.*

---

## RainTimerHud Effect

A room effect that controls the visibility of the cycle timer. Add this to the `Effects` line in `settings_N.txt`:

* **Value >= 0.5:** Continuous timer.
* **Value < 0.5:** Paused timer.

---

## Views

Views are a system that adds images and tints to a specific depth layer of the room. Both objects are fully blendable in real-time.

---

## Known Limitations

* Does not work in Arena mode for now.
* `TerrainPalette` is non-functional in this version.
* Not compatible with vanilla `DayNight` (may cause crashes).
* Compatibility with `Forecast` has not been tested.

---

## DevTools

Accessible from the developer menu (Default key: `O`). It includes:

* Room type selector (Static/Blend/Vanilla)
* View type selector (None/ACV/RTV/PSV)
* Settings selector
* Real-time tint editor with hex color selector and screen color picker
* Slot selector for images (Sky/Fog/Sun)
* Manual blending editor with a slider
* Mode and timer selector

---

## Important Note
> ⚠️ **Dependency Only:** This mod does not add any visual changes on its own. It functions strictly as a tool/dependency. You must create the room states within another mod for Rain Cycles to load and manage them.

---
> [!NOTE]
> This project is currently under active development. Features and documentation are subject to change.

made with assistance from Deepseek AI ヾ(•ω•`)o
