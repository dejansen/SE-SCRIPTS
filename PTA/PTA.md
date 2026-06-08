# PTA — Planetary Travel Assistant

Autopilot assistant for planetary flight. Features can be enabled independently via hotbar commands. The system shows live status on any tagged LCD, cockpit screen, or the PB's own screen.

---

## Features

- **Horizon stabilizer** — uses gyro override to keep the ship level with the planetary horizon. Corrects pitch and roll; releases gyros when already aligned so manual control is unaffected.
- **Cruise altitude** — uses upward thruster override to hold a fixed altitude above the terrain surface. Adapts automatically to mountains and valleys.
- **Cruise mode** — convenience command that disables brake thrusters (so the ship coasts freely) and enables both horizon and altitude hold in one press.
- **Display** — animated boot screen on startup, live status panel with colour-coded state, shown on any tagged LCD or cockpit screen.

---

## Hotbar Commands

### `PTA_ON`
Initialises the system. Scans the grid for controllers, gyroscopes, and thrusters. Reads config from Custom Data. Plays a boot animation on all displays. **No features are enabled after boot** — use the individual commands or `CRUISE_ON` to start flying.

Run this once after loading into a save, or after making changes to Custom Data.

---

### `PTA_OFF`
Master off switch. Immediately:
- Disables horizon stabilizer and releases all gyro overrides
- Disables altitude hold and releases all thruster overrides
- Re-enables any brake thrusters that were disabled by `CRUISE_ON`
- Stops the update loop

Use this to regain full manual control at any time. Safe to press at any speed or altitude.

---

### `HORIZON_ON`
Enables the horizon stabilizer. The script runs every ~1.67 seconds and checks how far the ship has drifted from level. If the tilt exceeds the `threshold`, it applies gyro override to correct it, then releases the gyros once aligned.

The ship can still be steered manually between correction ticks.

Reads current config from Custom Data on activation.

---

### `HORIZON_OFF`
Disables the horizon stabilizer. Releases all gyro overrides immediately. The ship returns to normal physics and manual control.

---

### `ALTITUDE_ON`
Enables cruise altitude hold. The script calculates the hover thrust needed to counteract gravity, then applies a proportional-derivative correction to reach and maintain the target altitude above the terrain surface.

The target altitude is read from `[altitude] target` in Custom Data. Use `SET_ALTITUDE` first to set a meaningful target.

Reads current config from Custom Data on activation.

---

### `ALTITUDE_OFF`
Disables altitude hold. Releases all upward thruster overrides. The ship returns to normal dampener or manual thruster control.

---

### `SET_ALTITUDE`
Reads the ship's current altitude above the terrain surface and saves it as the cruise target. Writes the value directly to `[altitude] target` in Custom Data so it persists across recompiles and save reloads.

**Typical use:** fly to the altitude you want to cruise at, then press this before pressing `ALTITUDE_ON` or `CRUISE_ON`.

Requires the ship to be near a planet with a detectable surface.

---

### `CRUISE_ON`
One-press cruise mode. Does the following in order:
1. Reads config from Custom Data
2. Scans the grid for blocks
3. Identifies and **disables** brake thrusters (the ones pointing forward that slow the ship down)
4. Enables horizon stabilizer
5. Enables altitude hold

With brakes off, the ship coasts at whatever speed it was going. Use your forward thrusters to set speed, then let PTA keep you level and at altitude.

---

### `CRUISE_OFF`
Exits cruise mode. Does the following:
1. Disables horizon stabilizer
2. Disables altitude hold
3. Releases all gyro and thruster overrides
4. Re-enables all brake thrusters

Returns full manual control. The ship will start decelerating again via dampeners once brakes are restored.

---

## Typical Workflow

1. Press `PTA_ON` — system scans the grid and shows boot screen
2. Fly to the altitude you want to cruise at
3. Press `SET_ALTITUDE` — locks current height as target
4. Accelerate to cruise speed
5. Press `CRUISE_ON` — brakes off, horizon + altitude hold active
6. The ship coasts and stays level; use forward thrusters to adjust speed
7. Press `CRUISE_OFF` or `PTA_OFF` to regain full control

---

## In-Game Setup

1. Place a Programmable Block on your ship.
2. Paste the script and click **Check Code**, then **OK**.
3. On first compile the PB writes default values to Custom Data — review them.
4. Add the following hotbar actions for the Programmable Block:

| Slot | Command | Suggested label |
|---|---|---|
| 1 | `PTA_ON` | PTA Boot |
| 2 | `PTA_OFF` | PTA Off |
| 3 | `CRUISE_ON` | Cruise On |
| 4 | `CRUISE_OFF` | Cruise Off |
| 5 | `SET_ALTITUDE` | Set Alt |
| 6 | `HORIZON_ON` | Horizon On |
| 7 | `HORIZON_OFF` | Horizon Off |
| 8 | `ALTITUDE_ON` | Alt Hold On |
| 9 | `ALTITUDE_OFF` | Alt Hold Off |

### Display setup

The PB's own screen always shows output. To add more screens:

- **LCD panel** — add `[PTA]` anywhere in the block's name (e.g. `LCD Panel [PTA]`)
- **Cockpit screen** — add `[PTA]` to the cockpit name, then set `cockpit_screen` in Custom Data to the screen index you want (0, 1, 2…). Check your cockpit's screen list in the terminal to find the right index.

Multiple screens are supported simultaneously.

---

## Display Colour Codes

The panel border changes colour to indicate system state.

| Border colour | Meaning |
|---|---|
| *(no panel)* | Booting — logo and progress bar |
| Red | Offline — `PTA_ON` not yet run |
| Dim teal | Online, no features active |
| Bright cyan | Features active, all stable |
| Amber | One or more features actively correcting |

---

## Custom Data Reference

```
[cruise]
brake_group =      ; name of a block group containing your brake thrusters
                   ; leave empty to auto-detect by thruster orientation

[horizon]
correction = 0.5   ; how hard the gyros correct tilt
damping    = 0.2   ; how hard they brake to prevent overshoot
threshold  = 0.02  ; tilt dead-band in radians (~1 deg) — no correction below this

[altitude]
target     = 1000  ; cruise altitude in metres above terrain
correction = 0.005 ; thrust fraction added per metre of altitude error
damping    = 0.01  ; thrust fraction subtracted per m/s of vertical speed
threshold  = 5     ; altitude dead-band in metres — no PD correction below this

[display]
cockpit_screen = 0 ; which cockpit screen index to write to (0-based)
```

Changes to Custom Data take effect the next time you run `HORIZON_ON`, `ALTITUDE_ON`, `CRUISE_ON`, or `PTA_ON`.
`SET_ALTITUDE` writes the new target directly to Custom Data so it persists across recompiles.

---

## Tuning

### Horizon oscillates
Lower `correction` or raise `damping` in `[horizon]`. A value of `0.2` for damping and `0.5` for correction worked well in testing. If oscillation persists, try `correction = 0.2`.

### Altitude overshoots or bobs
Lower `correction` or raise `damping` in `[altitude]`. Start by halving `correction`.

### Altitude response is sluggish
Raise `correction` in `[altitude]`. On large or heavy ships with few upward thrusters, a higher value is needed.

### No upward thrusters found
The script identifies upward thrusters by orientation relative to gravity. Make sure the ship is roughly level when enabling altitude hold, and that upward-facing thrusters are powered and functional. In space or without gravity, altitude hold does nothing.

---

## Troubleshooting

| Symptom | Cause |
|---|---|
| "PTA: no ship controller found" | No cockpit, seat, or remote control on grid |
| "PTA: no gyroscopes found" | No gyroscopes on grid |
| Screen shows nothing after PTA_ON | The PB screen always works; on LCDs make sure the block is powered and functional |
| Wrong cockpit screen | Adjust `cockpit_screen` index in `[display]` section |
| Altitude hold does nothing | No upward-facing thrusters found, or `MaxEffectiveThrust` is zero (no power, wrong atmosphere type, or no atmo) |
| `SET_ALTITUDE` fails | Not near a planet with a detectable surface |
| Brake thrusters not restored | Run `PTA_OFF` — it restores all brake thrusters unconditionally regardless of state |
| Ship drifts in altitude slowly | Increase `correction` in `[altitude]`; the PD correction may be too weak for the ship's mass |
| Horizon corrects but overshoots repeatedly | The gyros are fighting each other; lower `correction` and raise `damping` |
