# R.O.S — Fleet Broadcast

**Module:** Miner Telemetry Broadcast  
**Author:** RevGamer (Simba "Davy" Jones)  
**Version:** 1.1  
**Language:** C# — Space Engineers Programmable Block

---

## Overview

R.O.S Fleet Broadcast is the miner-side companion script for [R.O.S — Rev Operating System](../ROS-DockingMonitor/ROS-DockingMonitor.md). Install it on the Programmable Block of any mining ship. It broadcasts live telemetry over IGC to the base station, drives a proper status display on the PB screen, and optionally enforces an approach speed limit when near the base.

---

## Features

- Broadcasts speed, position, drill state, cargo %, battery %, fuel % and status over IGC
- Status auto-detection: **PARKED** (on terrain or stationary in space), **IDLE** (airborne hover), **MINING** (drills active), **TRANSIT** (moving)
- Auto approach speed limit — slows to 15 m/s within 300m of base (requires Remote Control block)
- Boot screen on PB display with progress bar and random mining quotes
- Live status screen after boot showing all telemetry with colour-coded bars
- Error screen if antenna is missing or offline
- `InvariantCulture` formatting — works on all server locales

---

## Files

| File | Description |
|------|-------------|
| `MinerBroadcast.cs` | Paste into the miner Programmable Block |

---

## Block Requirements

| Block | Required | Purpose |
|-------|----------|---------|
| Radio Antenna | **Yes** | Must be enabled and set to broadcasting |
| Ship Controller (Cockpit / Remote) | **Yes** | Speed and gravity readings |
| Remote Control | No | Auto approach speed limiting |
| Battery Blocks | No | Battery % telemetry |
| Hydrogen Tanks | No | Fuel % telemetry (hidden if absent) |
| Ship Drills | No | Drill state detection |
| Cargo Containers | No | Cargo fill % (scans all inventory blocks) |

---

## Setup

1. Build a **Radio Antenna** on the miner. Enable it and turn on **Broadcasting**.
2. Paste `MinerBroadcast.cs` into the miner's **Programmable Block** and compile.
3. The script runs automatically on `Update10`. No CustomData configuration needed.
4. Ensure [R.O.S — Rev Operating System](../ROS-DockingMonitor/ROS-DockingMonitor.md) is running on the base station.

> **Antenna range:** The antenna must have enough range to reach the base station. The script checks `_antenna.Enabled && _antenna.IsBroadcasting` every tick and shows an error screen if either is false.

---

## PB Display

### Boot Screen

On script load, the PB display shows an animated boot sequence for approximately 3 seconds:

```
         R.O.S
     FLEET BROADCAST
          v1.1
  ─────────────────────
  RevGamer (Simba "Davy" Jones)

  Syncing with base station...
  ████████████████░░░░░░  80%
          ◌
```

Random quotes cycle during boot:
- *Warming up drill systems...*
- *Calibrating ore scanners...*
- *Checking conveyor network...*
- *Syncing with base station...*
- *Loading mining protocols...*
- *Pressurising cargo bays...*
- *Engaging thruster systems...*
- *All systems go.*

### Status Screen

After boot, the PB display shows live telemetry updated every tick:

```
R.O.S                              v1.1  ◌
  FLEET BROADCAST
  RevGamer (Simba "Davy" Jones)
─────────────────────────────────────────
RGH GroundHog

STATUS                              IDLE
SPEED                             0 m/s
DRILL                               OFF
─────────────────────────────────────────
CARGO                              77%
████████████████████░░░░░░░░░░░░░░░░░
BATTERY                            85%
█████████████████████████░░░░░░░░░░░░
─────────────────────────────────────────
BASE                               23m
```

When approach mode is active:
```
BASE                               15m
>> APPROACH MODE 15m/s <<
```

### Error Screen

If the antenna is missing or offline:

```
         ⚠
    Antenna offline
  Enable antenna and set
    broadcasting ON
```

---

## IGC Broadcast Format

The script broadcasts on channel `DOCK_APPROACH` every `Update10` tick. The message is a pipe-separated string:

```
[GridName]|[Speed]|[PosX]|[PosY]|[PosZ]|[Drilling]|[Cargo%]|[Fuel%]|[Battery%]|[Status]
```

| Index | Field | Example |
|-------|-------|---------|
| 0 | Grid name | `[RGH] GroundHog` |
| 1 | Speed (m/s, integer) | `0` |
| 2 | Position X | `-12345` |
| 3 | Position Y | `8765` |
| 4 | Position Z | `3210` |
| 5 | Drilling (1/0) | `0` |
| 6 | Cargo fill % | `77` |
| 7 | Fuel % (`-1` if no H2 tanks) | `-1` |
| 8 | Battery % (`-1` if no batteries) | `85` |
| 9 | Status string | `IDLE` |

The base station replies on `DOCK_REPLY` with `[GridName]|[DistanceMetres]`.

---

## Status Values

| Status | Condition |
|--------|-----------|
| `MINING` | Any drill is enabled |
| `PARKED` | Speed < 1 m/s AND (on terrain below 5m elevation OR stationary in space) |
| `IDLE` | Speed < 1 m/s AND airborne on a planet |
| `TRANSIT` | Speed ≥ 1 m/s |

---

## Approach Speed Limiting

If a **Remote Control** block is present and the base station is replying with distance:

- Within **300m** of base → speed limit set to **15 m/s**
- Beyond **300m** → speed limit reset to **100 m/s**

The PB display shows `>> APPROACH MODE 15m/s <<` in red when active.

Both thresholds are configurable at the top of the script:

```csharp
const double APPROACH_DIST = 300.0;  // metres
const float  APPROACH_SPD  = 15.0f;  // m/s
```

---

## Cargo Scanning

Cargo fill is calculated by scanning all inventory blocks on the same construct, excluding:
- Ship Controllers (cockpits, remote controls)
- Ship Tool Bases (drills, welders, grinders)

This gives an accurate reading of the actual cargo containers and storage.

---

## Changelog

### v1.1
- Boot screen on PB display with animated progress bar and random quotes
- Live status screen with colour-coded CARGO / BATTERY / FUEL bars
- Error screen when antenna is missing or offline
- `InvariantCulture` on all `ToString("F0")` — broadcast numbers consistent across locales
- Spinner indicator on status screen to confirm script is running

### v1.0
- Initial release — broadcast only, raw `Echo` terminal output
