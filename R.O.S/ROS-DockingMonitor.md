# R.O.S — Rev Operating System

**Module:** Dock Control / Proximity Scanner / Fleet Operations  
**Author:** RevGamer (Simba "Davy" Jones)  
**Version:** 1.4  
**Language:** C# — Space Engineers Programmable Block

---

## Overview

R.O.S is a base station management system for Space Engineers. It drives three separate LCD panels simultaneously — a docking status display, a proximity scanner, and a fleet operations tracker for miner ships. Ships equipped with the companion [R.O.S Fleet Broadcast](../ROS-MinerBroadcast/ROS-MinerBroadcast.md) script broadcast telemetry back to base over IGC, giving you live cargo, battery, fuel and status readouts without leaving the cockpit.

---

## Features

- **Dock Control** — live connector states (LOCKED / READY / IDLE / OFFLINE), docked vessel names, per-vessel battery bars, docking time, base power bar
- **Proximity Scanner** — camera raycasting up to 1km, IGC miner contacts, sequential runway lights on approach, sound alert
- **Fleet Operations** — miner fleet tracking with CARGO / BATTERY / FUEL bars, PARKED / IDLE / MINING / TRANSIT / DOCKED status, live update age
- Boot sequence on all LCDs at script load
- Auto-resume after server restart (`Save()` + `UpdateFrequency` persistence)
- Instant LCD redraw on connector state change
- Sequential runway lights activate when approach contacts detected
- Docked grids filtered from approach list automatically
- `InvariantCulture` parsing — works correctly on all server locales

---

## Files

| File | Description |
|------|-------------|
| `DockingMonitor.cs` | Paste into the base station Programmable Block |

---

## Block Setup

Tag your blocks by adding the following strings to their names:

| Tag | Block Type | Purpose |
|-----|-----------|---------|
| `[DOCK]` | Ship Connector | Monitored docking port |
| `[DockStatus]` | LCD Panel | Dock Control display |
| `[DockMap]` | LCD Panel | Proximity Scanner display |
| `[MinerStatus]` | LCD Panel | Fleet Operations display |
| `[DockCam]` | Camera | Approach corridor camera (multiple supported) |
| `[DockAlert]` | Sound Block | Plays on approach contact |
| `[DockLight 1]`, `[DockLight 2]`... | Light Block | Sequential runway lights |

**Examples:**
```
Connector [DOCK] Bay 1
LCD Wide [DockStatus]
LCD Wide [DockMap]
LCD Wide [MinerStatus]
Camera [DockCam] Forward
Sound Block [DockAlert]
Interior Light [DockLight 1]
Interior Light [DockLight 2]
Interior Light [DockLight 3]
```

> Lights are sorted alphabetically by name, so `[DockLight 1]`, `[DockLight 2]`, `[DockLight 3]` will sequence in order.

---

## LCD Configuration

No CustomData configuration required on the LCDs — the script detects them by name tag and sets `ContentType`, `BackgroundColor` and `ScriptForegroundColor` automatically on first run.

Set the LCD `Font Size` slider to any value — the script uses fixed sprite scales and ignores the font size setting.

---

## Miner Integration

Install [R.O.S Fleet Broadcast](../ROS-MinerBroadcast/ROS-MinerBroadcast.md) on the miner's Programmable Block. The miner broadcasts on IGC channel `DOCK_APPROACH`. R.O.S receives and displays:

- Grid name
- Speed and position
- Drill state
- Cargo fill %
- Battery %
- Hydrogen fuel % (shown only if H2 tanks present)
- Status (PARKED / IDLE / MINING / TRANSIT / DOCKED)

The base replies on `DOCK_REPLY` with distance so the miner can activate approach speed limiting.

> **Name matching:** R.O.S strips `[bracket tags]` from grid names before matching. A miner named `[RGH] GroundHog` will correctly match a connector docked to `[RGH] GroundHog`.

---

## Screens

### Dock Control `[DockStatus]`

```
R.O.S                                    v1.4
  DOCK CONTROL
  RevGamer (Simba "Davy" Jones)
─────────────────────────────────────────────
LOCKED:3   IDLE:3   READY:0

PORT        STATE    VESSEL          BATTERY  TIME
─────────────────────────────────────────────────
● Bay 1    LOCKED   GroundHog  ████░   85%  4h31m
● Bay 2    LOCKED   Cross's G  ████░  100%  4h31m
● Bay 3    IDLE
● Bay 4    IDLE
─────────────────────────────────────────────────
⚡ BASE POWER                              68%
████████████████████████░░░░░░░░░░░░░░░░░░░░
```

### Proximity Scanner `[DockMap]`

```
R.O.S                                    v1.4
  PROXIMITY SCANNER
  RevGamer (Simba "Davy" Jones)
─────────────────────────────────────────────
● CAMERA x1   RANGE 1000m
● RUNWAY x3   SEQ 2/3
● ALERT x1    STANDBY

VESSEL              DISTANCE    SPEED    SOURCE
─────────────────────────────────────────────
● RGH GroundHog      23m       0m/s      IGC
  CARGO 77%   BATTERY 85%
  ██████████████████████░░░░░░░░░░░░░░░░░░░

1 CONTACT                              1 MINER
```

### Fleet Operations `[MinerStatus]`

```
R.O.S                                    v1.4
  FLEET OPERATIONS
  RevGamer (Simba "Davy" Jones)
─────────────────────────────────────────────
● RGH GroundHog                 23m   DOCKED
CARGO         BATTERY
███████░░░   ████████░░   77%   85%
DOCKED   0 m/s   UPDATED: LIVE
─────────────────────────────────────────────
1 UNIT ONLINE
```

---

## Boot Sequence

On script load (or server restart), all three LCDs display an animated boot screen for 3 seconds before switching to live data:

```
         R.O.S
      DOCK CONTROL
           v1.4
  ────────────────────
  RevGamer (Simba "Davy" Jones)

  Establishing IGC channels...
  ████████████████░░░░░░  80%
          ◌
```

Each LCD shows its own module name during boot so you can identify which display is which.

---

## Changelog

### v1.4
- `StripTags()` applied to miner grid names on receive — `[RGH] GroundHog` now matches correctly
- `CARGO` / `BATTERY` / `FUEL` full label text in Fleet Operations
- `InvariantCulture` on all `float.TryParse` and `ToString("F0")` — fixes locale-dependent parse failures
- Larger font sizes across all three displays
- Full vessel and port names — no truncation
- `InitLCDs()` sets `ContentType` and `Script` once per LCD — eliminates blank-frame flicker
- `UpdateMinerContacts()` polled every `Update10` tick — IGC callback alone was unreliable

### v1.3
- Top padding fix — header no longer clips above LCD boundary
- `RadialGauge` position offset fix
- Full text labels — no abbreviations

### v1.2
- Boot sequence on all LCDs at script load
- Right-edge text clipping fixed
- `FormatMass` unit suffix always visible

### v1.1
- `ScriptForegroundColor` set correctly — sprites now render
- Header compacted to ~40px

### v1.0
- Initial release

---

## Notes

- The script uses `Runtime.UpdateFrequency = UpdateFrequency.Update10` — it runs every 10 game ticks. This is intentional for smooth runway light sequencing and live fleet data.
- Camera raycasting consumes the camera's charge. Place multiple `[DockCam]` cameras if you need faster recharge.
- The sound block plays once on first approach contact detection, then resets when the contact list clears.
- `Save()` is implemented — the script auto-resumes after a server restart without manual recompile.
