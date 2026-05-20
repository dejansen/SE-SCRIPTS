# R.O.S — Rev Operating System

**Module:** Dock Control / Proximity Scanner / Fleet Operations / Dock Radar  
**Author:** RevGamer (Simba "Davy" Jones)  
**Version:** 1.7  
**Language:** C# — Space Engineers Programmable Block

---

## Overview

R.O.S is a base station management system for Space Engineers. It drives four separate LCD panels simultaneously — a docking status display, a proximity scanner, a fleet operations tracker, and a 2D radar display. Ships equipped with the companion [R.O.S Fleet Broadcast](../ROS-FleetBroadcast/ROS-FleetBroadcast.md) script broadcast telemetry back to base over IGC, giving you live cargo, battery, fuel and status readouts without leaving the cockpit.

---

## Features

- **Dock Control** — live connector states (LOCKED / READY / IDLE / OFFLINE), docked vessel names with scrolling, per-vessel battery bar with percentage, battery charge time remaining, base power bar
- **Proximity Scanner** — camera raycasting up to 1km, IGC miner contacts, elevation indicators (above / coplanar / below dock plane), contact fade-out, sequential runway lights on approach, sound alert
- **Fleet Operations** — miner fleet tracking with CARGO / BATTERY / FUEL bars, cargo fill trend arrows, PARKED / IDLE / MINING / TRANSIT / DOCKED status, live update age, distance display
- **Dock Radar** — 2D top-down radar with range rings, bearing ticks, rotating sweep line, contact blips with elevation shapes, short name labels, contact fade
- Living display shimmer — all sprites pulse gently for an active screen feel
- Auto font scaling — dock table adjusts font size to fit any number of connectors
- Uptime timer in every display header
- Boot sequence on all LCDs at script load
- Auto-resume after server restart (`Save()` + `UpdateFrequency` persistence)
- Instant LCD redraw on connector state change
- Sequential runway lights activate when approach contacts detected
- Docked grids filtered from approach list automatically
- `InvariantCulture` parsing — works correctly on all server locales
- Block scan caching — all block lists rebuilt every ~10 seconds, not every tick

---

## Files

| File | Description |
|------|-------------|
| `ROS_DockControl_v1.7.cs` | Paste into the base station Programmable Block |

---

## Block Setup

Tag your blocks by adding the following strings to their names:

| Tag | Block Type | Purpose |
|-----|-----------|---------|
| `[DOCK]` | Ship Connector | Monitored docking port |
| `[DockStatus]` | LCD Panel | Dock Control display |
| `[RosScan]` | LCD Panel | Proximity Scanner display |
| `[RosFleet]` | LCD Panel | Fleet Operations display |
| `[RosRadar]` | LCD Panel | Dock Radar display |
| `[RosCam]` | Camera | Approach corridor camera (multiple supported) |
| `[RosSound]` | Sound Block | Plays on approach contact |
| `[DockLight 1]`, `[DockLight 2]`... | Light Block | Sequential runway lights |

**Examples:**
```
Connector [DOCK] Bay 1
LCD Wide [DockStatus]
LCD Wide [RosScan]
LCD Wide [RosFleet]
LCD Wide [RosRadar]
Camera [RosCam] Forward
Sound Block [RosSound]
Interior Light [DockLight 1]
Interior Light [DockLight 2]
Interior Light [DockLight 3]
```

> Lights are sorted alphabetically by name, so `[DockLight 1]`, `[DockLight 2]`, `[DockLight 3]` will sequence in order.

> Connector names longer than 12 characters will scroll in the PORT column automatically. You do not need to shorten them.

> The `[RosRadar]` LCD is optional. If not present, the script runs normally without radar output. Multiple radar LCDs are supported.

---

## LCD Configuration

No CustomData configuration required on the LCDs — the script detects them by name tag and sets `ContentType`, `BackgroundColor` and `ScriptForegroundColor` automatically on first run.

Set the LCD `Font Size` slider to any value — the script uses sprite-based rendering and ignores the font size setting entirely.

---

## Miner Integration

Install [R.O.S Fleet Broadcast](../ROS-FleetBroadcast/ROS-FleetBroadcast.md) on the miner's Programmable Block. The miner broadcasts on IGC channel `DOCK_APPROACH`. R.O.S receives and displays:

- Grid name
- Speed and position
- Drill state
- Cargo fill % with trend arrow (filling / stable / emptying)
- Battery %
- Hydrogen fuel % (shown only if H2 tanks present)
- Status (PARKED / IDLE / MINING / TRANSIT / DOCKED)

The base replies on `DOCK_REPLY` with distance so the miner can activate approach speed limiting.

> **Name matching:** R.O.S strips `[bracket tags]` from grid names before matching. A miner named `[RGH] GroundHog` will correctly match a connector docked to `[RGH] GroundHog`.

---

## Screens

### Dock Control `[DockStatus]`

Five-column table. Font size scales automatically — 4 connectors use a larger font, 8+ use a smaller one, everything fits without clipping.

```
R.O.S                                        v1.7  0:17
  DOCK CONTROL
  RevGamer (Simba "Davy" Jones)
──────────────────────────────────────────────────────
● LOCKED:5   ● IDLE:2   ● READY:0

PORT          STATE      VESSEL        BATTERY    TIME
──────────────────────────────────────────────────────
● Connector  [IDLE]
● Connector  [LOCKED]   GroundHog     ████░  98%  FULL
● Connector  [LOCKED]   s's Groundho  ████░ 100%  FULL
● Connector  [LOCKED]   Groundhog 1   ████░ 100%  FULL
● Connector  [LOCKED]   1 Grid 2840   ████░ 100%  FULL
● Connector  [IDLE]
● miner 1 d  [LOCKED]   1 Grid 4826   ███░░  92%  --
──────────────────────────────────────────────────────
⚡ BASE POWER                                      90%
████████████████████████████████████████████░░░░░░░░░
```

**Column details:**

| Column | Width | Content |
|--------|-------|---------|
| PORT | 26% | Connector name — scrolls if >12 chars |
| STATE | 14% | Colored badge — LOCKED / IDLE / READY / OFFLINE |
| VESSEL | 23% | Docked grid name — scrolls if >12 chars |
| BATTERY | 22% | Bar fill + percentage. Green ≥70%, amber ≥30%, red <30% |
| TIME | 15% | Minutes to full charge. `FULL` / `42m` / `1h12m` / `--` (discharging) |

**TIME column** — calculated live from `CurrentInput - CurrentOutput` (net MW) against remaining capacity (MWh). Updates every draw cycle. Shows `--` if the ship is discharging or has no power input.

### Proximity Scanner `[RosScan]`

```
R.O.S                                        v1.7  0:17
  PROXIMITY SCANNER
  RevGamer (Simba "Davy" Jones)
──────────────────────────────────────────────────────
● CAMERA x2   RANGE 1km
● RUNWAY x4   SEQ 2/4
● ALERT x1    STANDBY

VESSEL                   DIST    SPD    SRC
──────────────────────────────────────────────────────
~ GroundHog 2            240m   8m/s   IGC
  CARGO 74%   BAT 92%   FUEL 88%
  ██████████████████░░░░░░░░░░░░░░░░░

^ IcePicker              580m  22m/s   RosCam
▼ Scout-A                910m  45m/s   RosCam

3 CONTACTS                              2 MINERS
```

**Elevation indicators** — shown left of every contact name:

| Symbol | Meaning |
|--------|---------|
| `~` | Coplanar with dock (within ±15°) |
| `^` | Above dock plane (green) |
| `v` | Below dock plane (pink) |

Elevation is computed from the contact's world position relative to the base grid's WorldMatrix. Works correctly for horizontal dock orientations.

**Contact fade-out** — contacts that leave scanner range fade out over 3 seconds rather than snapping off instantly.

**Distance display** — uses smart formatting: `240m`, `1.2km`, `4.5km`.

### Fleet Operations `[RosFleet]`

```
R.O.S                                        v1.7  0:17
  FLEET OPERATIONS
  RevGamer (Simba "Davy" Jones)
──────────────────────────────────────────────────────
● GroundHog 2                          240m   MINING
CARGO            BATTERY          FUEL
████████░░░░  74% ^   ████████░  92%   ████████░  88%
MINING   8m/s   UPD: LIVE
──────────────────────────────────────────────────────
● IcePicker                            580m   TRANSIT
CARGO            BATTERY
████░░░░░░░░  34% =   ████████░  91%
TRANSIT  22m/s   UPD: 3s
──────────────────────────────────────────────────────
2 UNITS ONLINE                              1 MINING
```

**Cargo trend arrows** — shown after the cargo percentage:

| Arrow | Meaning |
|-------|---------|
| `^^` | Filling fast (>15% change, red) |
| `^` | Filling (>2% change, amber) |
| `=` | Stable (dim) |
| `v` | Emptying / unloading (green) |

### Dock Radar `[RosRadar]`

2D top-down radar view centered on the base. Optional — place any LCD with `[RosRadar]` in the name to enable.

```
R.O.S                                        v1.7  0:17
  DOCK RADAR
  RevGamer (Simba "Davy" Jones)
──────────────────────────────────────────────────────
        N
   330  |  030
        |
W ------●------ E    rings: 2.5k/5km/7.5k/10km
        |
        S

○ miner   △ above   ▽ below   □ large   RNG 10km  1 contact
```

**Range rings:**

| Ring | Distance at default zoom | Color |
|------|--------------------------|-------|
| Inner | 2.5km | Red dim |
| 2nd | 5km | Orange dim |
| 3rd | 7.5km | Purple dim |
| Outer | 10km | Gray dim |

Ring labels update dynamically with the current zoom level.

**Contact blip shapes:**

| Shape | Meaning |
|-------|---------|
| `○` circle | Miner / IGC contact, coplanar with dock |
| `△` triangle up | Any contact above dock plane (green) |
| `▽` triangle down | Any contact below dock plane (pink) |
| `□` square | Large grid camera contact, coplanar (purple) |

Short name label (first 5 characters) appears next to each blip. Contacts fade out over the last 3 seconds before expiry, same as `[RosScan]`. Miners beyond the current zoom range are pinned to the radar edge.

**Zoom control** — run the base PB with these arguments to change radar range:

| Argument | Effect |
|----------|--------|
| `ZOOM IN` | Zoom in one step |
| `ZOOM OUT` | Zoom out one step |

**Zoom levels** (5 steps, default is 10km):

| Step | Range |
|------|-------|
| 1 (closest) | 500m |
| 2 | 1km |
| 3 | 2.5km |
| 4 | 5km |
| 5 (default) | 10km |

Set up toolbar buttons on any button panel pointing at the base Programmable Block with `ZOOM IN` and `ZOOM OUT` as the run arguments. Zoom level persists through server restarts.

---

## Boot Sequence

On script load (or server restart), all LCDs display an animated boot screen for 3 seconds before switching to live data:

```
            R.O.S
         DOCK CONTROL
              v1.7
  ────────────────────────────
  RevGamer (Simba "Davy" Jones)

  Establishing IGC channels...
  ████████████████░░░░░░  80%
              ◌
```

Each LCD shows its own module name during boot (`DOCK CONTROL`, `PROXIMITY SCANNER`, `FLEET OPERATIONS`, `DOCK RADAR`) so you can identify which display is which.

---

## Arguments

Run the base Programmable Block with these arguments — from a toolbar button, timer block, or the PB terminal:

| Argument | Effect |
|----------|--------|
| `ZOOM IN` | Zoom radar in one step (smaller range) |
| `ZOOM OUT` | Zoom radar out one step (larger range) |

**Toolbar setup example:**
```
Button Panel → Slot 1 → Run Program → ZOOM IN
Button Panel → Slot 2 → Run Program → ZOOM OUT
```

---

## Notes

- The script runs at `UpdateFrequency.Update10` — every 10 game ticks. This is intentional for smooth runway light sequencing and live fleet data.
- Block lists (connectors, batteries, cameras, lights, sensors, sounds) are rescanned every 100 ticks (~10 seconds) rather than every tick. Adding or removing tagged blocks takes effect within ~10 seconds without recompiling.
- Camera raycasting only fires when the camera is fully charged (`TimeUntilScan == 0`). Place multiple `[RosCam]` cameras for faster recharge coverage.
- Battery charge time in the TIME column is instantaneous — it reflects the current net charge rate. If the ship's reactor or solar output fluctuates, the reading updates accordingly.
- The sound block plays once on first approach contact detection, then resets when the contact list clears.
- `Save()` is implemented — the script auto-resumes after a server restart without manual recompile.
- Long port names and vessel names scroll automatically at 18 ticks per character. No manual renaming needed.

---

## Changelog

### v1.7
- **Dock Radar `[RosRadar]`** — new 2D radar display. Tag any LCD with `[RosRadar]` to enable. Shows range rings, bearing ticks with N/S/E/W labels, rotating sweep line with fading trail, contact blips with elevation shapes (○ miner / △ above / ▽ below / □ large), short name labels, and a legend strip
- **Radar zoom** — `ZOOM IN` / `ZOOM OUT` PB arguments cycle through 5 range levels: 500m / 1km / 2.5km / 5km / 10km. Current range shown in legend. Zoom persists through server restarts
- **Tag rename** — `[DockMap]` → `[RosScan]`, `[MinerStatus]` → `[RosFleet]`, `[DockCam]` → `[RosCam]`, `[DockAlert]` → `[RosSound]`. Connectors `[DOCK]`, Dock Control `[DockStatus]`, and lights `[DockLight]` unchanged
- **IGC contacts no longer range-filtered** — miners broadcast beyond 1km are kept in the contact list and shown on radar (pinned to edge if beyond zoom range). Camera contacts still filtered at 1km
- **Radar position fix** — blip distance now uses true 3D distance normalized against zoom range, matching the distance shown in Fleet Operations
- **WorldPos field** on approach contacts — accurate world position stored for both IGC and camera contacts, used for radar blip placement
- **Trig lookup tables** — `float[360]` sin/cos tables built once in constructor. No `Math.Sin/Cos` calls during radar draw

### v1.6
- **Battery column redesign** — battery bar and percentage now sit in a dedicated BATTERY column. TIME column shows charge time remaining computed from live battery input/output rates
- **5-column dock table** — PORT | STATE | VESSEL | BATTERY | TIME with correct proportions (26/14/23/22/15%)
- **Port name scrolling** — connector names >12 chars scroll left in the PORT column
- **Auto font scaling** — `AutoFontSize()` scales the dock table font to fit any number of connectors without clipping
- **Living display shimmer** — `SaltColor()` applies a gentle alpha pulse to all sprites across all displays
- **FC() number formatter** — distances and values auto-scale: `1234m` → `1.2km`, `4500000` → `4.5M`
- **Uptime timer** — `FormatElapsed()` shows script runtime in every display header
- **Elevation indicators** — `^` / `~` / `v` on `[RosScan]` contacts shows whether approach is above, coplanar, or below the dock plane
- **Contact fade-out** — approach contacts fade over 3 seconds instead of snapping off
- **Vessel name scrolling** — long vessel names scroll in the VESSEL column (12-char window)
- **Cargo trend arrows** — `^^` `^` `=` `v` on Fleet Operations bars shows filling / stable / emptying

### v1.5
- **Block scan caching** — `ReScanBlocks()` now only fires every `SCAN_INTERVAL = 100` ticks (~10s), not every tick. Saves ~60% instruction count on large grids
- **Camera raycast fix** — `cam.TimeUntilScan(CAM_RANGE) > 0` checked before raycasting. Previously fired unconditionally, wasting charge and missing fast contacts
- **Grid charge caching** — `RebuildGridChargeCache()` builds a `Dictionary<long, float>` in one pass per cycle. Previously ran O(connectors × batteries) per frame
- **Duplicate call removed** — `UpdateMinerContacts()` was called twice per tick in v1.4. Fixed

### v1.4
- `StripTags()` applied to miner grid names on receive — `[RGH] GroundHog` now matches correctly
- `CARGO` / `BATTERY` / `FUEL` full label text in Fleet Operations
- `InvariantCulture` on all `float.TryParse` — fixes locale-dependent parse failures
- Larger font sizes across all three displays
- `InitLCDs()` sets `ContentType` and `Script` once per LCD — eliminates blank-frame flicker
- `UpdateMinerContacts()` polled every `Update10` tick — IGC callback alone was unreliable

### v1.3
- Top padding fix — header no longer clips above LCD boundary
- Full text labels — no abbreviations

### v1.2
- Boot sequence on all LCDs at script load
- Right-edge text clipping fixed

### v1.1
- `ScriptForegroundColor` set correctly — sprites now render
- Header compacted

### v1.0
- Initial release
