# MULE — Autonomous Planetary Cargo Drone

## Concept

MULE automates round-trip cargo runs between two docking points on a planet. It monitors cargo fill level, flies from a pickup location (e.g. a mobile ice mining rig) to a dropoff location (e.g. an ice/hydrogen processing station), waits to be unloaded, and returns — repeating indefinitely until stopped.

Cargo capacity is not measured by volume percentage but by the maximum mass the drone can safely lift, calculated from actual upward thruster force, gravity, and ship base mass. This is calibrated once on the ground and stored permanently.

---

## Features

- One-time setup: fly to each location, connect, run an argument — locations are saved permanently
- Thrust-based cargo limit: departs when cargo mass reaches a safe fraction of liftable capacity
- Empty detection: departs dropoff when cargo drops below a configurable volume threshold (station sorter pulls the ice)
- Battery safety hold: will not depart if battery is below minimum threshold
- Damage detection: will not depart if RC block or connector is non-functional
- LCD and cockpit screen show contextual output: setup confirmation, calibration results, or live status
- Antenna HUD text with current state — visible to players within broadcast range
- All key parameters configurable in Custom Data
- State, locations, and calibration persist across recompiles and server restarts
- No background processing while idle — update cycle only runs after START

---

## Required Blocks

| Block | Name in script | Notes |
|---|---|---|
| Programmable Block | — | holds this script |
| Remote Control | `Drone RC` | used for autopilot navigation |
| Connector | `Connector Front` | front-facing docking connector |
| Cargo Container(s) | any | all containers on construct are monitored |
| Battery(s) | any | all batteries on construct are monitored |
| Gyroscopes | any | must be present for flight stability |
| Thrusters | any | atmospheric thrusters for planetary flight |
| LCD Panel | `Drone LCD` | optional — status display |
| Cockpit | any | optional — screen 0 shows status |
| Antenna | any | optional — broadcasts state as HUD text |

Block names for RC, connector, and LCD can be changed in Custom Data.

---

## Custom Data

After first compile the PB auto-populates its Custom Data with defaults:

```
[drone]
rc_name          = Drone RC
connector_name   = Connector Front
lcd_name         = Drone LCD
cargo_threshold  = 90
empty_threshold  = 5
cruise_altitude  = 200
backup_distance  = 15
min_battery      = 20
safety_factor    = 1.2
```

| Key | Default | Description |
|---|---|---|
| `rc_name` | `Drone RC` | Name of the Remote Control block |
| `connector_name` | `Connector Front` | Name of the docking connector |
| `lcd_name` | `Drone LCD` | Name of the LCD panel |
| `cargo_threshold` | `90` | Depart pickup when cargo is at this % of max safe lift capacity |
| `empty_threshold` | `5` | Depart dropoff when cargo volume drops below this % |
| `cruise_altitude` | `200` | Meters above saved location to cruise at |
| `backup_distance` | `15` | Meters to reverse before climbing |
| `min_battery` | `20` | Do not depart if battery below this % |
| `safety_factor` | `1.2` | Thrust headroom multiplier — 1.2 means only use 83% of max lift capacity |
| `resume_battery` | `80` | Battery % required to resume after a low-battery emergency return |

---

## In-Game Setup

### Step 1 — Build the drone

Build the drone with all required blocks. Name the Remote Control block `Drone RC` and the front connector `Connector Front` (or use different names and update Custom Data).

### Step 2 — Paste and compile the script

Open the Programmable Block, paste the script, click **Check Code**, then **OK**. The PB populates its Custom Data with defaults. Adjust values if needed before continuing.

### Step 3 — Set the pickup location

1. Fly the drone manually to the mining rig
2. Align the front connector with the rig's connector
3. Connect (toolbar or terminal)
4. Run argument: `SET_PICKUP`
5. LCD and K menu confirm the saved coordinates and computed offsets
6. Disconnect and fly away

### Step 4 — Set the dropoff location

1. Fly to the processing station
2. Align and connect to the station's connector
3. Run argument: `SET_DROPOFF`
4. LCD and K menu confirm
5. Disconnect and fly away

### Step 5 — Calibrate

1. Land the drone on flat ground (or at the pickup location)
2. Make sure all thrusters are functional and the RC block is active
3. Run argument: `CALIBRATE`
4. LCD and K menu show the result:

```
=== MULE CALIBRATION ===
Status  : OK

Thrust  : 145.3 kN
Base    : 12400 kg
Max load: 9700 kg
Safety  : 1.2x
Gravity : 9.81 m/s²
```

Calibration only needs to be repeated if thrusters are added, removed, or replaced. Re-run `CALIBRATE` after any such change.

### Step 6 — Start autonomous operation

1. Fly the drone to the pickup connector and connect
2. Run argument: `START`
3. MULE takes over — it will wait to load, then run the full cycle automatically

---

## Arguments

| Argument | Effect |
|---|---|
| `SET_PICKUP` | Save current connector position as pickup point (must be connected) |
| `SET_DROPOFF` | Save current connector position as dropoff point (must be connected) |
| `CALIBRATE` | Measure thrust, gravity, and base mass to calculate max safe cargo load |
| `START` | Begin autonomous cargo cycle (requires both locations set and calibration done) |
| `STOP` | Stop immediately — disables autopilot and returns to IDLE |
| `STATUS` | Force a display refresh |

---

## LCD / Cockpit Display

During setup and calibration the LCD shows contextual confirmation output. Once `START` is run it switches to live status:

```
=== MULE CARGO DRONE ===
State  : LOADING
Cargo  : 67%  (6500 / 9700 kg)
Battery: 88%
------------------------
Pickup : -12340 : 1250 : -23410
Dropoff: -11980 : 1230 : -22870
------------------------
Runs   : 4
------------------------
Calibrated
  Thrust  : 145.3 kN
  Base    : 12400 kg
  Max load: 9700 kg
```

---

## Antenna HUD Text

The antenna broadcasts a short state label visible to players and grids within range:

```
MULE | LOADING
MULE | FLYING TO DROPOFF
MULE | ERROR: Battery 17% (min 20%)
```

---

## Flight Cycle

```
LOADING             ← docked at pickup, waiting for cargo mass >= threshold %
DEPARTING PICKUP    ← disconnected, reversing backup_distance meters
CLIMBING            ← ascending to cruise_altitude above pickup
FLYING TO DROPOFF   ← cruising to dropoff climb point
APPROACHING DROPOFF ← descending to approach waypoint
DOCKING AT DROPOFF  ← moving to connector, attempting connect
UNLOADING           ← docked at dropoff, waiting for cargo volume <= empty_threshold %
DEPARTING DROPOFF   ← reversing backup_distance meters
CLIMBING            ← ascending to cruise_altitude above dropoff
FLYING TO PICKUP    ← cruising back to pickup climb point
APPROACHING PICKUP  ← descending to approach waypoint
DOCKING AT PICKUP   ← moving to connector, attempting connect
→ back to LOADING
```

---

## Safety Holds

MULE will not depart (stops and sets ERROR) if:

- Battery below `min_battery` % at the time `START` is run
- Remote Control block is missing or damaged
- Connector is missing or damaged
- Pickup or dropoff location not configured
- `CALIBRATE` has not been run

**Low battery mid-flight:** if battery drops below `min_battery` while airborne, MULE does not stop — it aborts the current leg and returns to the dropoff location to dock and wait for charge. Once battery reaches `resume_battery` % the cycle resumes automatically. While docked at pickup waiting to load, MULE will also not depart until battery reaches `resume_battery` %.

---

## If the pickup grid moves

If the mining rig relocates, the saved pickup position becomes stale. To update it:

1. Fly the drone manually to the rig's new location
2. Align and connect to the rig's connector
3. Run argument: `SET_PICKUP`

The new position is saved immediately and used on the next run. No restart needed.

> **Note:** It is not possible for a script to track a moving grid's connector position automatically — the game does not expose other grids' block positions to PB scripting. A future version may use IGC so the rig broadcasts its position and MULE updates the pickup waypoint dynamically.

---

## Troubleshooting

| Symptom | Likely cause |
|---|---|
| `SET_PICKUP` / `SET_DROPOFF` does nothing | Connector not connected when argument was run |
| `START` refused with "Run CALIBRATE first" | `CALIBRATE` argument has not been run yet |
| `CALIBRATE` fails: no gravity | Drone is in space or gravity is too weak — land on a planet first |
| `CALIBRATE` fails: no upward thrust | Thrusters not functional, or none facing upward relative to gravity |
| Drone does not depart | Battery below threshold, or block missing/damaged |
| Drone departs too light | Lower `cargo_threshold` or lower `safety_factor` slightly |
| Drone struggles to climb | Raise `safety_factor` and re-run `CALIBRATE` to reduce max cargo load |
| Drone overshoots connector | Increase `backup_distance`; approach waypoint lands too close |
| Drone never docks | Connector alignment off; rerun `SET_PICKUP` or `SET_DROPOFF` with better alignment |
| LCD shows nothing | Check `lcd_name` in Custom Data matches exact block name (case-sensitive) |
| Cargo never reaches threshold | Containers on subgrid? Script only counts blocks on the same construct as the PB |
