# PTA — Planetary Travel Assistant `v2.0`

Autopilot assistant for planetary flight, orbit transitions, and autonomous connector docking. Features are enabled independently via hotbar commands. Live status is shown on any tagged LCD, cockpit screen, or the PB's own screen. Optional Broadcast Controller support for audio announcements when away from the cockpit.

---

## Features

- **Horizon stabilizer** — gyro override keeps the ship level with the planetary horizon. Releases gyros when already aligned.
- **Cruise altitude** — upward thruster override holds a fixed altitude above the terrain surface. Glides nose-down at speed; falls back to thrust-based descent at low speed.
- **Cruise mode** — disables brake thrusters and enables horizon + altitude hold in one press. In space only the brakes are toggled; horizon and altitude are not activated without gravity.
- **Ascend mode** — full-thrust hydrogen climb to orbit. Keeps ship level; auto-completes at the edge of the atmosphere. Only available in planetary gravity.
- **Descend mode** — gravity-powered free-fall from orbit or high altitude. Keeps ship level; auto-completes at the configured target altitude. Only available in planetary gravity.
- **Autodock** — teach-and-replay connector docking. Save a dock target while physically connected (`SAVE <name>`), then fly to approach distance and run `DOCK <name>` to dock autonomously. Works in space and on planets, for any connector orientation.
- **Display** — animated boot screen, live status panel with colour-coded state, dedicated screens for ASCEND, DESCEND, DOCK, and CONNECTED states.
- **Broadcast announcements** — optional Broadcast Controller support. Tag one controller `[PTA_BC]` and PTA will announce mode changes (cruise, ascend, descend, dock, on/off) over ship comms. Message text and slot index are fully configurable via Custom Data.

---

## Safety Guards

- **PTA offline** — when PTA is off, only `PTA_ON` works. All other commands are silently blocked.
- **Ship connected** — when a dock connector is connected, only `PTA_OFF`, `PTA_ON`, and `SAVE` work. The display shows **SHIP IS CONNECTED** in green. Attempting any blocked command flashes it red briefly.
- **Mode conflicts** — commands that conflict with an active mode (e.g. `CRUISE_ON` during ascent) are blocked and echo the reason to the PB detail panel.

---

## Hotbar Commands

### `PTA_ON`
Initialises the system. Scans the grid for controllers, gyroscopes, thrusters, and dock connectors. Reads config from Custom Data.

If the ship is already connected when `PTA_ON` is run, the boot animation is skipped and the **SHIP IS CONNECTED** screen is shown immediately — the connected guard is active from the first tick.

If not connected, plays a boot animation then shows the status panel. **No features are enabled after boot** — use the individual commands or `CRUISE_ON` to start flying.

---

### `PTA_OFF`
Master off switch. Always available regardless of connection or mode state. Immediately:
- Disables all active features and releases gyro overrides
- Releases all thruster overrides
- Re-enables brake thrusters disabled by `CRUISE_ON`
- Re-enables thrusters disabled by `ASCEND_ON`
- Stops the update loop

Safe to press at any speed or altitude.

---

### `HORIZON_ON`
Enables the horizon stabilizer. Corrects pitch and roll to keep the ship level with the planetary horizon. The ship can still be steered manually between correction ticks.

---

### `HORIZON_OFF`
Disables the horizon stabilizer. Releases all gyro overrides immediately.

---

### `ALTITUDE_ON`
Enables cruise altitude hold. Holds the target altitude above the terrain surface using upward thruster override. Target is read from `[altitude] target` in Custom Data — use `SET_ALTITUDE` first.

---

### `ALTITUDE_OFF`
Disables altitude hold. Releases all upward thruster overrides.

---

### `SET_ALTITUDE`
Reads the ship's current terrain altitude and saves it as the cruise target. Writes directly to Custom Data so it persists across recompiles.

**Typical use:** fly to your desired cruise altitude, press this, then press `ALTITUDE_ON` or `CRUISE_ON`.

### `SET_ALTITUDE <meters>`
Sets the cruise altitude to a specific value without flying there. Example: `SET_ALTITUDE 2000`.

Both forms show a confirmation flash on the display.

---

### `CRUISE_ON`
One-press cruise mode:
1. Reads config and scans the grid
2. Disables brake thrusters so the ship coasts freely
3. In gravity: enables horizon stabilizer and altitude hold
4. In space: only disables brakes — horizon and altitude are not activated

If you fly from a planet into space while cruise is active, horizon and altitude automatically deactivate on the next tick.

---

### `CRUISE_OFF`
Exits cruise mode: disables horizon and altitude, releases all overrides, re-enables brake thrusters.

---

### `ASCEND_ON`
Activates ascent to orbit. **Only available in planetary gravity** — shows a red flash alert if attempted in space.

Requires `up_group` and `down_group` set in Custom Data, hydrogen thrusters, and non-empty hydrogen tanks. On activation:
- Turns off stockpile on hydrogen tanks
- Disables down-facing thrusters
- Sets up thrusters to full override
- Enables horizon stabilizer during climb
- Throttles to coast at 95 m/s

Auto-completes when gravity drops below 0.04 m/s². Shows **ASCEND COMPLETE** on the display.

---

### `ASCEND_OFF`
Manually ends ascent. Re-enables all thruster groups and releases gyro control. Shows **ASCEND ABORTED**.

---

### `DESCEND_ON`
Activates gravity-powered descent. **Only available in planetary gravity** — shows a red flash alert if attempted in space.

Requires `up_group` set in Custom Data. On activation:
- Sets up thrusters to near-zero override so dampeners cannot counteract the fall
- Disables altitude hold if running
- Enables horizon stabilizer during descent

Auto-completes at the altitude set by `[descend] target` (default 3000 m). Shows **DESCEND COMPLETE**.

---

### `DESCEND_OFF`
Manually ends descent. Releases up thruster overrides and gyro control. Shows **DESCEND ABORTED**.

---

### `SAVE <name>`
Saves the current dock target. Run this while physically connected to the target connector.

The script finds which connector is connected (searching only connectors tagged `[PTA_DOCK]`, or all connectors if none are tagged), saves its world position, forward direction, and up direction under `[dock:name]` in Custom Data. Fails if zero or more than one dock connector is connected.

The piston extension at save time is the reference — if you use a piston-mounted connector, the piston must be at the same extension when docking.

---

### `DOCK <name>`
Autonomously docks to a saved target. Fly to within ~50–100 m of the target connector and run this command.

**Sequence:**
1. **ALIGN** — rotates the ship so the connector faces the saved approach direction and roll matches the saved orientation
2. **APPROACHING** — flies to a waypoint `waypoint_distance` metres in front of the target
3. **FINAL** — slow approach directly to the connector, correcting any lateral offset
4. **CONNECTING** — engages the connector when `Connectable` status is detected

Works in space and on planets, for horizontal, vertical, and sideways connector orientations.

When docking completes the display switches to **SHIP IS CONNECTED**.

---

## Dock Connector Tagging

If your ship has multiple connectors (e.g. drone bays, cargo connectors), tag only the connectors used for docking by adding `[PTA_DOCK]` to their name:

```
Large Connector [PTA_DOCK]
```

- `SAVE` searches only tagged connectors — drone connectors attached to drones are ignored
- `IsShipDocked` checks only tagged connectors — a drone docking does not lock out PTA commands
- If no connectors are tagged, the script falls back to all connectors

---

## Typical Workflow

### Planetary cruise
1. Press `PTA_ON`
2. Fly to cruise altitude and press `SET_ALTITUDE`
3. Accelerate to cruise speed
4. Press `CRUISE_ON` — brakes off, horizon + altitude active
5. Press `CRUISE_OFF` or `PTA_OFF` to regain full control

### Ascent to orbit
1. Press `PTA_ON`
2. Make sure hydrogen tanks are full and `up_group` / `down_group` are configured
3. Press `ASCEND_ON` — climbs to orbit automatically

### Descent from orbit
1. Make sure `up_group` is configured
2. Press `DESCEND_ON` — ship falls freely, stays level; hands back control at target altitude

### Save a dock target
1. Fly to the station and manually dock
2. Press `PTA_ON` (shows **SHIP IS CONNECTED** immediately)
3. Run `SAVE mybase` — saves the dock target to Custom Data
4. Undock — display returns to normal PTA status

### Autodock
1. Fly to within 50–100 m of the target connector facing roughly the right direction
2. Press `PTA_ON` if not already active
3. Run `DOCK mybase` — ship aligns, approaches, and connects automatically

---

## In-Game Setup

1. Place a Programmable Block on your ship.
2. Paste the script and click **Check Code**, then **OK**.
3. On first compile the PB writes default values to Custom Data — review them.
4. Optionally add `[PTA_DOCK]` to the name of your docking connector(s).
5. Add hotbar actions for the Programmable Block:

| Slot | Command | Label |
|---|---|---|
| 1 | `PTA_ON` | PTA Boot |
| 2 | `PTA_OFF` | PTA Off |
| 3 | `CRUISE_ON` | Cruise On |
| 4 | `CRUISE_OFF` | Cruise Off |
| 5 | `SET_ALTITUDE` | Set Alt |
| 6 | `ASCEND_ON` | Ascend On |
| 7 | `ASCEND_OFF` | Ascend Off |
| 8 | `DESCEND_ON` | Descend On |
| 9 | `DESCEND_OFF` | Descend Off |
| 10 | `SAVE mybase` | Save Dock |
| 11 | `DOCK mybase` | Dock |

### Display setup

The PB's own screen always shows output. To add more screens:

- **LCD panel** — add `[PTA]` anywhere in the block name (e.g. `LCD Panel [PTA]`)
- **Cockpit screen** — add `[PTA]` to the cockpit name, then set `cockpit_screen` in Custom Data to the screen index (0, 1, 2…)

### Broadcast Controller setup (optional)

Add `[PTA_BC]` anywhere in the name of one Broadcast Controller. On first `PTA_ON` the `[broadcast]` section is written to Custom Data with default messages and slot index 8. PTA writes the message text to that slot before transmitting — your other slots (1–7) are untouched.

To customise:
- Edit message strings directly in Custom Data
- Change `index` to any slot 1–8
- Set `enabled = false` to silence all announcements without removing the block tag

---

## Themes

Set `theme` under `[display]` in Custom Data, then run `PTA_ON` to apply.

| Theme | Look |
|---|---|
| `cyber` | Dark navy background, bright cyan accents — default |
| `amber` | Black background, orange-amber text |
| `matrix` | Pure black background, vivid green text |
| `heat` | Very dark red-black background, orange accents |
| `royal` | Near-black background, purple accents |

---

## Display Colour Codes

| Border colour | Meaning |
|---|---|
| *(boot screen)* | Booting — logo and progress bar |
| Red | Offline — `PTA_ON` not yet run |
| Dim | Online, no features active |
| Bright cyan | Features active, stable |
| Amber | Features actively correcting |
| Green | Ship is connected |

ASCEND, DESCEND, DOCK, and CONNECTED states each have their own dedicated screen.

---

## Custom Data Reference

```
[cruise]
brake_group =           ; block group containing brake thrusters
                        ; leave empty to auto-detect by thruster orientation

[horizon]
correction = 0.5        ; gyro correction strength
damping    = 0.2        ; gyro damping to prevent overshoot
threshold  = 0.02       ; tilt dead-band in radians (~1 deg)

[altitude]
target          = 1000  ; cruise altitude in metres above terrain
correction      = 0.005 ; thrust per metre of altitude error
damping         = 0.01  ; thrust per m/s of vertical speed
threshold       = 5     ; altitude dead-band in metres
max_speed       = 15    ; maximum vertical speed cap (m/s)
pitch_max       = 5     ; max nose-down angle for glide descent (degrees)
pitch_min_speed = 20    ; minimum forward speed to use glide descent (m/s)
pitch_gain      = 0.002 ; nose-down degrees per metre of altitude error

[ascend]
up_group   =            ; block group: upward (launch) thrusters
down_group =            ; block group: downward thrusters (disabled during ascent)

[descend]
target = 3000           ; altitude (m) at which descent auto-completes

[dock]
approach_speed     = 10   ; max speed (m/s) during APPROACHING phase
final_speed        = 1.5  ; max speed (m/s) during FINAL phase
waypoint_distance  = 15   ; metres in front of target for approach waypoint

[display]
cockpit_screen = 0      ; cockpit screen index to use (0-based)
theme = cyber           ; colour theme: cyber, amber, matrix, heat, royal

[broadcast]
enabled         = true   ; set false to silence all announcements
index           = 8      ; broadcast controller slot to use (1-8); slots 1-7 are yours
pta_on          = Planetary Travel Assistant online
pta_off         = Planetary Travel Assistant offline
cruise_on       = Cruise activated
cruise_off      = Cruise off
ascend_on       = Ascending to orbit
ascend_complete = Orbit reached
ascend_abort    = Ascend aborted
descend_on      = Descending
descend_complete= Descent complete
descend_abort   = Descend aborted
dock_start      = Initiating docking
dock_complete   = Docking complete
dock_abort      = Docking aborted
```

Saved dock targets are stored as `[dock:name]` sections and are written by the `SAVE` command — do not edit them manually.

Changes to Custom Data take effect on the next `PTA_ON` or feature activation. New keys are written with default values automatically; existing values are never overwritten.

---

## Troubleshooting

| Symptom | Cause |
|---|---|
| "PTA: no ship controller found" | No cockpit, seat, or remote control on grid |
| "PTA: no gyroscopes found" | No gyroscopes on grid |
| Commands do nothing | PTA is offline — run `PTA_ON` first |
| Commands blocked with green screen | Ship is connected — undock first |
| ASCEND/DESCEND shows red flash | No planetary gravity — only works on planets |
| ASCEND MODE UNAVAILABLE | `up_group` or `down_group` not set, no hydrogen thrusters, or tanks empty |
| DESCEND MODE UNAVAILABLE | `up_group` not set or group not found |
| Ship doesn't fall during descent | `up_group` name doesn't match any block group — check spelling |
| SAVE fails: NOT CONNECTED | No tagged connector is connected — dock first |
| SAVE fails: MULTI CONNECT | More than one tagged connector is connected |
| DOCK ignores orientation | Connector piston extension differs from save time |
| Screen shows nothing | On LCDs make sure block is powered and named with `[PTA]` |
| Wrong cockpit screen | Adjust `cockpit_screen` index in `[display]` |
| Altitude hold does nothing | No upward thrusters found, or `MaxEffectiveThrust` is zero |
| Brake thrusters not restored | Run `PTA_OFF` — restores all brake thrusters unconditionally |
| Horizon oscillates | Lower `correction` or raise `damping` in `[horizon]` |
| Altitude overshoots | Lower `correction` or raise `damping` in `[altitude]` |
