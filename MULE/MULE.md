# MULE — Weight Guardian for Autonomous Cargo Drones

## Concept

MULE is a weight-management system for planetary cargo drones. It monitors cargo mass and volume, automatically controls sorters to prevent overload, and triggers timer blocks to initiate flights when cargo conditions are met.

MULE does **not** handle flight — the pilot controls flights manually via timers or the AI block UI. MULE's job is to:
1. **Protect the ship** from carrying too much cargo (would prevent takeoff)
2. **Control sorters** to stop loading/unloading at safe thresholds
3. **Trigger timers** to start flights automatically when cargo is full/empty and conditions are safe

---

## Features

- **Weight-based cargo limits**: calculates max liftable mass once during calibration and stores it permanently
- **Automatic sorter control**: turns off front sorter (loading) when cargo reaches 90% of max weight; turns off bottom sorter (unloading) when cargo drops below 5% of capacity
- **Automatic timer triggering**: starts dropoff flight timer when docked at pickup with full cargo; starts pickup flight timer when docked at dropoff with empty cargo
- **Battery safety**: halts all operations (sorter control and timer activation) if battery drops below 30%
- **Connector docking detection**: only triggers timers when actually docked
- **One-time calibration**: run once, settings persist across recompiles and server restarts
- **LCD and cockpit status display**: shows cargo level, battery %, docking status, and calibration info
- **All parameters configurable** in Custom Data

---

## Required Blocks

| Block | Name in Script | Purpose | Notes |
|---|---|---|---|
| Programmable Block | — | holds this script | — |
| Connector | `Front Connector` | docking point for flights | name configurable |
| Sorter | `Front Sorter` | controls loading | front/top facing, loads cargo |
| Sorter | `Bottom Sorter` | controls unloading | bottom facing, unloads cargo |
| Timer Block | `Start Dropoff Flight` | triggers dropoff flight | started by script when cargo full |
| Timer Block | `Start Pickup Flight` | triggers pickup flight | started by script when cargo empty |
| Cargo Container(s) | any | holds cargo | all containers on construct monitored |
| Battery(s) | any | power source | all batteries on construct monitored |
| Gyroscopes | any | stabilization | required for any flight |
| Thrusters | any | propulsion | atmospheric thrusters for planetary flight |
| LCD Panel | `Drone LCD` | status display | optional — can also use cockpit screen |
| Cockpit | any | status display | optional — uses screen 0 |

Block names for connector, sorters, and timers can be changed in Custom Data.

---

## Custom Data

After first compile the PB auto-populates its Custom Data with defaults:

```
[mule]
front_sorter_name        = Front Sorter
bottom_sorter_name       = Bottom Sorter
connector_name           = Front Connector
lcd_name                 = Drone LCD
cockpit_name             = 
dropoff_flight_timer     = Start Dropoff Flight
pickup_flight_timer      = Start Pickup Flight
cargo_threshold          = 90
empty_threshold          = 5
safety_factor            = 1.2
min_battery_to_fly       = 30
cockpit_screen           = 0
```

| Key | Default | Description |
|---|---|---|
| `front_sorter_name` | `Front Sorter` | Name of the sorter that loads cargo |
| `bottom_sorter_name` | `Bottom Sorter` | Name of the sorter that unloads cargo |
| `connector_name` | `Front Connector` | Name of the docking connector |
| `lcd_name` | `Drone LCD` | Name of the LCD panel for status display |
| `cockpit_name` | _(empty)_ | Name of the cockpit to use for display — leave empty to use the first one found |
| `dropoff_flight_timer` | `Start Dropoff Flight` | Name of the timer block that starts the dropoff flight |
| `pickup_flight_timer` | `Start Pickup Flight` | Name of the timer block that starts the pickup flight |
| `cockpit_screen` | `0` | Screen index on the cockpit (0 = first screen) |
| `cargo_threshold` | `90` | Cargo % of max safe lift capacity at which loading stops (front sorter turns off) |
| `empty_threshold` | `5` | Cargo volume % below which unloading stops (bottom sorter turns off) |
| `safety_factor` | `1.2` | Thrust headroom multiplier — 1.2 means only use 83% of max lift capacity |
| `min_battery_to_fly` | `30` | Minimum battery % to allow sorter control and timer activation — lower values prevent operations |

---

## In-Game Setup

### Step 1 — Build the drone

Build the drone with all required blocks:
- Two sorters: one facing forward/up (loading), one facing down (unloading)
- One connector for docking
- Two timer blocks: one to start dropoff, one to start pickup
- All cargo containers
- Battery banks and thrusters
- LCD or cockpit for display

Name the sorters `Front Sorter` and `Bottom Sorter`, the connector `Front Connector`, and the timers `Start Dropoff Flight` and `Start Pickup Flight`. (Or use different names and update Custom Data after compiling.)

### Step 2 — Paste and compile the script

Open the Programmable Block, paste the script, click **Check Code**, then **OK**. The PB populates its Custom Data with defaults. Adjust values if needed.

Verify that version displays in the K menu (Info): look for "MULE WEIGHT GUARDIAN vX.X" at the top.

### Step 3 — Calibrate the drone

The script calculates max liftable cargo based on upward thruster force and gravity. This is done once and stored permanently.

1. Park the drone on the ground (or any location with gravity)
2. Make sure **no cargo is loaded**
3. Make sure all thrusters face the correct directions (upward thrusters point up relative to gravity)
4. Run argument: `CALIBRATE`
5. LCD shows the result:

```
=== MULE WEIGHT GUARDIAN ===
CALIBRATE RESULT

Status: OK
Thrust: 145.3 kN
Gravity: 9.81 m/s²
Base Mass: 12400 kg
Max Cargo: 9700 kg
Safety: 1.2x
```

If calibration fails, check:
- **"No gravity"** — fly to a planet surface (gravity must be present)
- **"No upward thrust"** — check thruster directions; at least one must point opposite to gravity

Calibration only needs to be repeated if thrusters are added, removed, or replaced. Re-run `CALIBRATE` after any such change.

### Step 4 — Test sorter control

1. Load cargo into the drone until it reaches ~80% capacity
2. Run argument: `STATUS` and watch the LCD
3. As cargo continues to load past 90%, the **front sorter should turn OFF automatically**
4. Unload some cargo
5. As cargo drops below 5%, the **bottom sorter should turn OFF automatically**

Both sorters should turn back ON when they drop below their thresholds.

### Step 5 — Set up timer blocks

Each timer block should be configured to:
1. **Start Dropoff Flight timer**: set up an action (e.g., enable a flight group or trigger an autopilot script)
2. **Start Pickup Flight timer**: set up a different action for the pickup flight

When the script detects:
- Drone is docked at pickup AND cargo ≥ 90% → **triggers Start Dropoff Flight timer**
- Drone is docked at dropoff AND cargo ≤ 5% → **triggers Start Pickup Flight timer**

(Unless battery is below `min_battery_to_fly`, in which case timers are not started.)

---

## Arguments

| Argument | Effect |
|---|---|
| `STATUS` | Display current cargo level, battery, docking status, and calibration info |
| `CALIBRATE` | Measure thrust and gravity to calculate max safe cargo load (run once) |
| `RESET` | Clear calibration data and return to uncalibrated state |

---

## LCD / Cockpit Display

The STATUS output shows:

```
=== MULE WEIGHT GUARDIAN v2.0 ===
Cargo  : 78%  (7500 / 9700 kg)
Volume : 45%
Battery: 85%
------------------------
Docked : YES
Front sorter : ON
Bottom sorter: OFF
------------------------
Calibrated
  Thrust  : 145.3 kN
  Base    : 12400 kg
  Max load: 9700 kg
```

If the battery is low, a warning appears:
```
Battery: 22%
  ⚠ LOW - Operations halted
```

---

## How It Works

### Weight Monitoring

The script measures:
- **Total cargo mass** (sum of all container volumes × item mass per unit volume)
- **Max liftable cargo** (upward thruster force ÷ gravity − drone base mass) × safety factor

When cargo mass ≥ 90% of max, the front sorter turns OFF to prevent overload.
When cargo volume ≤ 5%, the bottom sorter turns OFF to prevent underloading.

### Sorter Control

- **Front Sorter** (loading): turned ON initially by event controller; script turns it OFF when cargo is full
- **Bottom Sorter** (unloading): turned ON initially by event controller; script turns it OFF when cargo is empty

Both are turned back ON automatically when they drop below their thresholds.

### Timer Activation

When the script detects:
1. **Docked at pickup (via connector status) AND cargo ≥ cargo_threshold AND battery ≥ min_battery_to_fly**
   → Starts the dropoff flight timer
2. **Docked at dropoff (via connector status) AND cargo ≤ empty_threshold AND battery ≥ min_battery_to_fly**
   → Starts the pickup flight timer

The timer then executes its configured actions (e.g., enable autopilot, trigger a flight script).

---

## Troubleshooting

| Symptom | Likely Cause | Fix |
|---|---|---|
| LCD shows nothing | `lcd_name` in Custom Data doesn't match actual block name | Check block name (case-sensitive) and update Custom Data |
| Sorters don't turn off | Block names in Custom Data don't match actual sorter names | Update `front_sorter_name` and `bottom_sorter_name` in Custom Data |
| Battery warning appears but operations don't halt | Check `min_battery_to_fly` value — default is 30% | Raise the threshold in Custom Data if needed |
| Timers don't trigger when docked | Connector name doesn't match; or docking status not detected | Check `connector_name` matches actual connector block name |
| Calibration fails with "No gravity" | Drone is in space or on a low-gravity body | Land on a planetary surface with stronger gravity |
| Calibration fails with "No upward thrust" | Thrusters are not positioned correctly | Check that at least one thruster points upward (opposite gravity direction) |
| Cargo threshold feels wrong | Safety factor or mass calculation is off | Re-run `CALIBRATE` after ensuring drone is empty and level |
| Script shows version but nothing else | Custom Data parsing failed silently | Check Custom Data section syntax — colons and values must be exact |

---

## Reference

### Item Mass (per 1 L)

| Item | Mass (kg/L) |
|---|---|
| Ice | 0.92 |
| Stone | 0.70 |
| Iron Ore | 1.27 |
| Nickel Ore | 1.27 |
| Uranium Ore | 1.27 |
| Cobalt Ore | 1.27 |
| Silver Ore | 1.27 |
| Gold Ore | 1.27 |
| Magnesium Ore | 0.50 |
| Platinum Ore | 1.27 |

### Sorter Mode

Both sorters should be set to the same mode:
- **Store** — input/output based on container filter settings (recommended for autonomous use)
- **Collect All** — collects all items (useful for initial setup)

Use event controller or toolbar to set initial ON state. Script handles OFF state based on thresholds.

