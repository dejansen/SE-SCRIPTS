# PTA — Planetary Travel Assistant `v1.6`

Autopilot assistant for planetary flight and orbit transitions. Features can be enabled independently via hotbar commands. The system shows live status on any tagged LCD, cockpit screen, or the PB's own screen.

---

## Features

- **Horizon stabilizer** — uses gyro override to keep the ship level with the planetary horizon. Corrects pitch and roll; releases gyros when already aligned so manual control is unaffected.
- **Cruise altitude** — uses upward thruster override to hold a fixed altitude above the terrain surface. Adapts automatically to mountains and valleys. When descending at speed, pitches the nose down for a natural glide rather than firing down thrusters; falls back to thrust-based descent at low speed.
- **Cruise mode** — convenience command that disables brake thrusters (so the ship coasts freely) and enables horizon and altitude hold in one press. Works in space too — in zero gravity only the brake thrusters are toggled.
- **Ascend mode** — full-thrust climb to orbit using configured hydrogen thruster groups. Keeps ship level during climb; auto-completes when gravity drops near zero.
- **Descend mode** — gravity-powered descent from orbit or high altitude. Disables up thrusters so the ship falls freely; keeps ship level via horizon hold. Auto-completes at 3000 m.
- **Display** — animated boot screen on startup, live status panel with colour-coded state, shown on any tagged LCD or cockpit screen. Flash messages confirm altitude changes and mode completions or aborts.

---

## Hotbar Commands

### `PTA_ON`
Initialises the system. Scans the grid for controllers, gyroscopes, and thrusters. Reads config from Custom Data. Plays a boot animation on all displays. **No features are enabled after boot** — use the individual commands or `CRUISE_ON` to start flying.

Run this once after loading into a save, or after making changes to Custom Data.

---

### `PTA_OFF`
Master off switch. Immediately:
- Disables all active features and releases gyro overrides
- Releases all thruster overrides
- Re-enables any brake thrusters that were disabled by `CRUISE_ON`
- Re-enables any thrusters that were disabled by `ASCEND_ON`
- Stops the update loop

Safe to press at any speed or altitude.

---

### `HORIZON_ON`
Enables the horizon stabilizer. The script runs every ~1.67 seconds and checks how far the ship has drifted from level. If the tilt exceeds the `threshold`, it applies gyro override to correct it, then releases the gyros once aligned.

The ship can still be steered manually between correction ticks.

Reads current config from Custom Data on activation.

---

### `HORIZON_OFF`
Disables the horizon stabilizer. Releases all gyro overrides immediately.

---

### `ALTITUDE_ON`
Enables cruise altitude hold. The script calculates the hover thrust needed to counteract gravity, then applies a proportional-derivative correction to reach and maintain the target altitude above the terrain surface.

The target altitude is read from `[altitude] target` in Custom Data. Use `SET_ALTITUDE` first to set a meaningful target.

Reads current config from Custom Data on activation.

---

### `ALTITUDE_OFF`
Disables altitude hold. Releases all upward thruster overrides.

---

### `SET_ALTITUDE`
Reads the ship's current altitude above the terrain surface and saves it as the cruise target. Writes the value directly to `[altitude] target` in Custom Data so it persists across recompiles and save reloads.

**Typical use:** fly to the altitude you want to cruise at, then press this before pressing `ALTITUDE_ON` or `CRUISE_ON`.

Requires the ship to be near a planet with a detectable surface.

### `SET_ALTITUDE <meters>`
Sets the cruise altitude to a specific value without needing to physically fly there. Example: `SET_ALTITUDE 2000` locks the target at 2000 m.

Both forms show a confirmation screen on the display immediately after the target is saved.

---

### `CRUISE_ON`
One-press cruise mode. Does the following in order:
1. Reads config from Custom Data
2. Scans the grid for blocks
3. Identifies and **disables** brake thrusters
4. In planetary gravity: enables horizon stabilizer and altitude hold
5. In space (zero gravity): only disables brakes — no horizon or altitude needed

With brakes off, the ship coasts at whatever speed it was going. Use your forward thrusters to set speed.

---

### `CRUISE_OFF`
Exits cruise mode:
1. Disables horizon stabilizer and altitude hold
2. Releases all gyro and thruster overrides
3. Re-enables all brake thrusters

---

### `ASCEND_ON`
Activates ascent to orbit. Requires `up_group` and `down_group` to be set in Custom Data, hydrogen thrusters, and non-empty hydrogen tanks.

On activation:
- Turns off stockpile mode on all hydrogen tanks
- Disables down-facing thrusters (so the ship can actually climb)
- Sets up thrusters to full override
- Enables horizon stabilizer to keep ship level during climb
- Throttles back to coast at 95 m/s to avoid overheating

Auto-completes when gravity drops below 0.04 m/s² (edge of atmosphere). Re-enables all thruster groups and releases control.

---

### `ASCEND_OFF`
Manually ends ascent mode early. Re-enables all thruster groups and releases gyro control. Shows an **ASCEND ABORTED** message on the display (as opposed to the **ASCEND COMPLETE** shown on automatic completion).

---

### `DESCEND_ON`
Activates gravity-powered descent. Only works in planetary gravity.

On activation:
- Sets up thrusters (from `up_group`) to near-zero override so dampeners cannot counteract the fall
- Disables altitude hold if it was running
- Enables horizon stabilizer to keep ship level during descent

Auto-completes at 3000 m altitude. At that point the up thrusters are released and horizon is turned off — ready for manual landing approach.

Requires `up_group` to be set in Custom Data.

---

### `DESCEND_OFF`
Manually ends descent mode early. Releases up thruster overrides and gyro control. Shows a **DESCEND ABORTED** message on the display (as opposed to **DESCEND COMPLETE** shown on automatic completion at 3000 m).

---

## Typical Workflow

### Planetary cruise
1. Press `PTA_ON` — system scans the grid and shows boot screen
2. Fly to the altitude you want to cruise at
3. Press `SET_ALTITUDE` — locks current height as target
4. Accelerate to cruise speed
5. Press `CRUISE_ON` — brakes off, horizon + altitude hold active
6. Press `CRUISE_OFF` or `PTA_OFF` to regain full control

### Ascent to orbit
1. Press `PTA_ON`
2. Make sure hydrogen tanks are full and `up_group` / `down_group` are set
3. Point ship upward or let horizon handle it
4. Press `ASCEND_ON` — climbs to orbit automatically

### Descent from orbit
1. Make sure `up_group` is set in Custom Data
2. Press `DESCEND_ON` — ship falls freely under gravity, stays level
3. At 3000 m descent auto-stops — take manual control for landing

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
| 5 | `SET_ALTITUDE` | Set Alt (current) |
| 5 | `SET_ALTITUDE 2000` | Set Alt 2000m |
| 6 | `ASCEND_ON` | Ascend On |
| 7 | `ASCEND_OFF` | Ascend Off |
| 8 | `DESCEND_ON` | Descend On |
| 9 | `DESCEND_OFF` | Descend Off |

### Display setup

The PB's own screen always shows output. To add more screens:

- **LCD panel** — add `[PTA]` anywhere in the block's name (e.g. `LCD Panel [PTA]`)
- **Cockpit screen** — add `[PTA]` to the cockpit name, then set `cockpit_screen` in Custom Data to the screen index you want (0, 1, 2…). Check your cockpit's screen list in the terminal to find the right index.

Multiple screens are supported simultaneously.

---

## Themes

Set `theme` under `[display]` in Custom Data, then run `PTA_ON` to apply.

| Theme | Look |
|---|---|
| `cyber` | Dark navy background, bright cyan accents — default |
| `amber` | Black background, orange-amber text — classic terminal |
| `matrix` | Pure black background, vivid green text |
| `heat` | Very dark red-black background, orange accents |
| `royal` | Near-black background, purple/violet accents |

Unknown theme names fall back to `cyber`.

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

ASCEND and DESCEND modes have their own dedicated status screens showing altitude, gravity, and speed.

---

## Custom Data Reference

```
[cruise]
brake_group =        ; name of a block group containing your brake thrusters
                     ; leave empty to auto-detect by thruster orientation

[horizon]
correction = 0.5     ; how hard the gyros correct tilt
damping    = 0.2     ; how hard they brake to prevent overshoot
threshold  = 0.02    ; tilt dead-band in radians (~1 deg) — no correction below this

[altitude]
target     = 1000    ; cruise altitude in metres above terrain
correction = 0.005   ; thrust fraction added per metre of altitude error
damping    = 0.01    ; thrust fraction subtracted per m/s of vertical speed
threshold  = 5       ; altitude dead-band in metres — no PD correction below this
max_speed  = 15      ; maximum vertical speed in m/s — hard cap on top of PD
pitch_max  = 5       ; max nose-down angle in degrees when glide-descending
pitch_min_speed = 20 ; minimum forward speed (m/s) to use glide descent
pitch_gain = 0.002   ; degrees of nose-down per metre of altitude error

[ascend]
up_group   =         ; block group containing your upward (launch) thrusters
down_group =         ; block group containing your downward thrusters (disabled during ascent)

[display]
cockpit_screen = 0   ; which cockpit screen index to write to (0-based)
theme = cyber        ; colour theme: cyber (default), amber, matrix, heat, royal
```

Changes to Custom Data take effect the next time you run `HORIZON_ON`, `ALTITUDE_ON`, `CRUISE_ON`, `ASCEND_ON`, `DESCEND_ON`, or `PTA_ON`.
`SET_ALTITUDE` writes the new target directly to Custom Data so it persists across recompiles.

New keys added in a script update are automatically written to Custom Data with their default values on next activation — existing values are never overwritten.

---

## Tuning

### Horizon oscillates
Lower `correction` or raise `damping` in `[horizon]`. A value of `0.2` for damping and `0.5` for correction worked well in testing. If oscillation persists, try `correction = 0.2`.

### Altitude overshoots or bobs
Lower `correction` or raise `damping` in `[altitude]`. Start by halving `correction`.

### Glide descent feels too steep or too shallow
Adjust `pitch_max` (maximum nose-down angle in degrees) and `pitch_gain` (angle per metre of altitude error) in `[altitude]`. Raise `pitch_min_speed` if you only want glide to activate at higher cruise speeds.

### Altitude response is sluggish
Raise `correction` in `[altitude]`. On large or heavy ships with few upward thrusters, a higher value is needed.

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
| Brake thrusters not restored | Run `PTA_OFF` — it restores all brake thrusters unconditionally |
| Ship drifts in altitude slowly | Increase `correction` in `[altitude]`; the PD correction may be too weak for the ship's mass |
| Horizon corrects but overshoots | Lower `correction` and raise `damping` in `[horizon]` |
| ASCEND MODE UNAVAILABLE | `up_group` or `down_group` not set, no hydrogen thrusters, or tanks empty |
| DESCEND MODE UNAVAILABLE | No gravity (in space), or `up_group` not set / group not found |
| Ship doesn't fall during descent | `up_group` name doesn't match any block group — check spelling in Custom Data |
