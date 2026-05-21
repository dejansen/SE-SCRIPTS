# RNB — Rev's Nanobot Bridge

A companion script for the **SKO Nanobot Build and Repair System (Maintained)** mod (v2.5.0+).  
Compatible with SKO's maintained fork only. **Will not work** with the original Dummy08 version.

Author: Simba 'Davy' Jones — 505th Expeditionary Force  
Version: v1.0.0

---

## What It Does

| Feature | Detail |
|---|---|
| BaR welder monitoring | Auto-detects all BaR welders on the construct — no config needed |
| Assembler auto-queuing | Pushes missing components into tagged assemblers automatically |
| Smart assembler routing | Basic assemblers only receive components they can produce; advanced handle the rest |
| Auto-produce mode fix | Switches assemblers out of Disassembly mode automatically when parts are missing |
| Boot sequence | Animated boot screen on PB's own LCD on compile/reboot |
| PB live screen | Compact status display on PB surface — no tag needed, always on |
| Styled LCD pages | Sprite-mode display, dark navy theme, one fixed page per LCD |
| Welder details | Per-welder: working/standby/off/damaged, BaR vs standard, on-target indicator |
| Assembler details | Per-assembler: mode, enabled state, output count, repeat flag, cooperative flag |
| Projector tracking | Build progress bar and remaining block count per projector |
| Weld progress bar | Latches peak queue count at job start, fills as blocks complete |
| Alert lights | Colour and blink rate reflects current system state |
| Auto-offline | Disables BaR welders after 10 min of zero activity |
| Argument control | `online` / `offline` / `info-only` via toolbar or timer block |

---

## Requirements

- Space Engineers with the **SKO Nanobot Build and Repair System (Maintained)** mod
- A Programmable Block on the same construct as the BaR welders
- LCD panels renamed with the appropriate tags (see below)

---

## Installation

1. Build a **Programmable Block** on your grid.
2. Open it → **Edit** → paste the full contents of `RNB.cs`.
3. Click **Check Code** — must show zero errors.
4. Click **OK**.

The script starts immediately. The PB's own LCD shows a boot sequence then switches to a live status panel. Check the PB detail panel for any warnings.

---

## Block Tags

Rename blocks in-game — no config editing required. The script rescans every **30 seconds** automatically. Recompile the PB for immediate effect.

### Non-LCD blocks

| Tag | Block type | What it does |
|---|---|---|
| `[RNBAssembler]` | Advanced Assembler | Receives all missing components |
| `[RNBBasicAssembler]` | Basic Assembler | Receives only basic-craftable components |
| `[RNBAlert]` | Any light | Colour/blink reflects current state |
| `[RNBProjector]` | Any Projector | Tracked on Projectors page |

### LCD blocks

Each tag maps to one fixed page. No cycling.

| Tag | Page |
|---|---|
| `[RNBStatus]` | System overview |
| `[RNBMissing]` | Missing components list |
| `[RNBWeld]` | Weld queue + progress bar |
| `[RNBGrind]` | Grind queue list |
| `[RNBWelders]` | Per-welder status detail |
| `[RNBAssemblers]` | Per-assembler status detail |
| `[RNBProjectors]` | Projector build progress |

Multiple LCDs can share the same tag — they all show the same page. The PB's own LCD is handled automatically with no tag required.

### Example block names

```
Status Screen [RNBStatus]
Missing Parts [RNBMissing]
Weld Queue [RNBWeld]
Grind Queue [RNBGrind]
Welder Panel [RNBWelders]
Assembler Panel [RNBAssemblers]
Build Progress [RNBProjectors]
Main Assembler [RNBAssembler]
Basic Assembler 1 [RNBBasicAssembler]
Alert Light [RNBAlert]
Ship Blueprint [RNBProjector]
```

---

## Pages

### Status `[RNBStatus]`
Full system overview — welder count, assembler count, current weld/grind target, all queue counts, missing type count, projector count.

### Missing `[RNBMissing]`
Two-column list of every component BaR cannot source with the required quantity. Red-coded. Shows "All components available" in green when clear.

### Weld Queue `[RNBWeld]`
Progress bar (built / peak) + scrollable list of blocks waiting to be welded.

### Grind Queue `[RNBGrind]`
Scrollable list of blocks waiting to be ground.

### Welders `[RNBWelders]`
Per-welder rows:
- Name — colour-coded by state (green=working, amber=standby, dim=off, red=damaged)
- Status badge — WORKING / STANDBY / OFF / DAMAGED
- Type — `BaR` (cyan) or `STD`, plus functional state
- ON TARGET — shown when BaR is actively welding

### Assemblers `[RNBAssemblers]`
Per-assembler rows (tagged assemblers only):
- Name — colour-coded by state
- Status — WORKING / STANDBY / OFF / DAMAGED
- Mode — ASSEMBLY (white) or DISASSEMBLY (amber)
- COOP badge if cooperative mode is on
- Output inventory item count
- REPEAT badge if repeat mode is on

### Projectors `[RNBProjectors]`
Per-projector rows (tagged projectors only):
- Name + BUILDING / IDLE state
- Progress bar filled as blocks are built
- Block count (built / total) and percentage

---

## Assembler Routing

The script automatically routes missing components to the correct assembler type:

**Basic assemblers** (`[RNBBasicAssembler]`) receive:
SteelPlate, InteriorPlate, Construction, SmallTube, LargeTube, Motor, Display, BulletproofGlass, Girder

**Advanced assemblers** (`[RNBAssembler]`) receive everything else — Computer, Superconductor, MetalGrid, Thrust components, etc.

If a block is tagged `[RNBBasicAssembler]` that tag takes priority. Otherwise the script infers basic vs advanced from the block's definition subtype.

---

## PB Boot Screen

On every compile or reboot the PB's own LCD (surface 0) shows a 3-second animated boot sequence — progress bar, animated dots, version info. After boot it switches to the live compact status panel showing welders, assemblers, queue counts, and the weld progress bar.

---

## Alert Light States

| State | Colour | Blink |
|---|---|---|
| Working | Green | Solid |
| Idle | Dim blue | Solid |
| Missing components | Red | Fast (1.5 s) |
| Offline | Amber | Slow (3 s) |

---

## Auto-Offline

Idle time is seconds since the last tick where any weld, grind, or collect target existed.

- Default: **10 minutes** — edit `IDLE_TIMEOUT_SECONDS` at the top of the script.
- On timeout: all BaR welders disabled, state → OFFLINE.
- Send `online` arg to re-enable and reset the clock.
- Send `offline` arg to force immediate shutdown.
- A forced offline is only cleared by `online`.

---

## Auto-Produce Mode Fix

When `AUTO_PRODUCE_FIX_MODE = true` (default), any tagged assembler in Disassembly mode is switched to Assembly mode automatically before components are queued. Logged to PB detail panel:

```
Auto-mode: 'Basic Assembler 1' → Assembly
```

---

## Toolbar Arguments

| Argument | Effect |
|---|---|
| `online` | Re-enables welders, clears forced-offline, resets idle clock |
| `offline` | Immediately disables welders, sets forced-offline |
| `info-only` | Skips assembler queuing this cycle only |

---

## Script Constants

| Constant | Default | Description |
|---|---|---|
| `IDLE_TIMEOUT_SECONDS` | `600.0` | Seconds of zero activity before auto-offline |
| `AUTO_PRODUCE_FIX_MODE` | `true` | Auto-switch assemblers from Disassembly → Assembly |
| `REINIT_INTERVAL` | `30.0` | Seconds between block tag rescans |
| `BOOT_DURATION` | `3.0` | Seconds the boot animation plays |

---

## Troubleshooting

| Symptom | Likely cause |
|---|---|
| No BaR welders found | BaR mod not installed, wrong version, or no welders on this construct |
| No assemblers registered | No assembler has an RNB tag |
| LCD black / no content | Recompile the PB to re-run surface setup |
| Welders disabled | Idle timeout fired — send `online` |
| OFFLINE won't clear | Forced via `offline` arg — send `online` |
| Projector shows IDLE | No blueprint loaded or blueprint already complete |
| Basic assembler not producing | Component may require an advanced assembler — check `[RNBMissing]` LCD |
| Wrong page on LCD | Tags are case-sensitive — check exact spelling |

**Debug:** Open the PB terminal detail panel — shows init counts, auto-mode switches, queue failures, and online/offline transitions.

---

## Compatibility Notes

- SKO maintained fork only — requires `BuildAndRepair.MissingComponents`, `BuildAndRepair.PossibleTargets`, `BuildAndRepair.ProductionBlock.EnsureQueued`.
- `IMyProjector.BuildProgress` and `IMyProjector.IsProjecting` are not in the SE scripting whitelist — not used. Progress derived from `TotalBlocks` / `RemainingBlocks`.
- `MyResourceDistributorComponent` (power draw) is not in the whitelist — power figures are not shown.
- No `static` fields — safe for SE's memory sandbox.
