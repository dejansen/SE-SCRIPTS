# AutoGrid Manager — Reference

**Script:** AutoGrid Manager v1.3+
**Author:** RevGamer
**Version:** 1.3+
**SE Script Tag:** `[AGM-S]`

---

## Overview

AutoGrid Manager (AGM) is a Space Engineers Programmable Block script. It runs on one PB and manages:

- Inventory sorting and auto-assignment
- Automated production — assembler queuing, refinery sorting, autocrafting quotas
- Power monitoring — batteries, reactors, solar, wind, hydrogen engines
- Reactor refuel monitoring and automation
- Battery/reactor automation — charges batteries via reactors, turns reactors off when full
- Fuel and life support — H2/O2 tanks, generators, vent leak detection
- Stock dashboards — ores, ingots, components, ammo, tools, bottles
- Alert system — warning lights and corner LCD displays

---

## Script Architecture

### Entry points

| Method | Purpose |
|---|---|
| `Program()` | Constructor — initialises block lists, writes default config, sets Update10/100 |
| `Main(argument, updateSource)` | Entry point — runs staged work and draw every tick |
| `Save()` | Called on game save — no persistent data currently used |

### Update frequency

- `Update10` — staged work, draw cycle (~6x per second)
- `Update100` — block rescan (~once per 1.67 seconds)

### Staged work

`RunStagedWork()` cycles through work stages each tick to spread CPU load:

| Stage | Work |
|---|---|
| 0 | Power stats |
| 1 | Fuel/life support |
| 2 | Logistics sort pass |
| 3 | Production monitoring |
| 4 | Stock counting |
| 5 | Alert lights and corner LCDs |

### Key lists

| Field | Type | Contents |
|---|---|---|
| `_blocks` | `List<IMyTerminalBlock>` | All same-construct terminal blocks |
| `_screens` | `List<IMyTerminalBlock>` | All `[AGM-S]` tagged text surface providers |
| `_alertLcds` | `List<AlertLcdEntry>` | Alert corner LCDs — drawn every tick |
| `_assemblers` | `List<IMyAssembler>` | All managed assemblers |
| `_basicAssemblers` | `List<IMyAssembler>` | Basic assemblers only |
| `_advAssemblers` | `List<IMyAssembler>` | Advanced assemblers only |
| `_refineries` | `List<IMyRefinery>` | All managed refineries |
| `_reactorsCtl` | `List<IMyReactor>` | Reactors under power control |
| `_batteriesCtl` | `List<IMyBatteryBlock>` | Batteries under power control |
| `_cargos` | `List<CargoInfo>` | Tagged cargo containers |
| `_sources` | `List<SourceInfo>` | Source inventories for sorting |

---

## LCD System

### Screen detection

`ScanBlocks()` adds blocks to `_screens` when:
- Block name contains `[AGM-S]`
- OR block Custom Data contains a recognised dashboard command
- AND block is an `IMyTextSurfaceProvider`
- AND block Custom Data does NOT contain `[AGM-LIGHT]`

### Drawing

`DrawScreen()` is called for each screen every tick. It reads the Custom Data command and routes to the correct draw method.

### Alert LCD system

`[AGM-LIGHT]` blocks are never in `_screens`. They are tracked in `_alertLcds` (populated by `RunWarningLights()`) and drawn every tick by `DrawAlertLcds()` — separate from the staged draw cycle so they never flicker.

---

## Logistics / Auto-Sorter

### How sorting works

Each `Main()` cycle, `RunLogistics()` picks one source block from `_sources` and checks its inventory for items that belong in a typed container. If found, it moves up to `max_moves_per_run` items to the correct destination.

### Destination detection

Destination containers are matched by tag in the block name:

| Tag | Destination for |
|---|---|
| `{Ore N}` | `MyObjectBuilder_Ore` |
| `{Ingot N}` | `MyObjectBuilder_Ingot` |
| `{Component N}` | `MyObjectBuilder_Component` |
| `{Ammo N}` | `MyObjectBuilder_AmmoMagazine` |
| `{Tool N}` | `MyObjectBuilder_PhysicalGunObject` |
| `{Bottle N}` | `MyObjectBuilder_GasContainerObject`, `MyObjectBuilder_OxygenContainerObject` |

`N` is the priority number — lower fills first. When a container reaches 98% full, sorting moves to the next number.

### Auto-assign

When `auto_assign=true` and no destination exists for an item type, AGM renames the first available untagged unlocked container to assign it.

### Protected blocks

AGM never sorts from `IMyReactor`, `IMyGasGenerator`, or `IMyGasTank` inventories. It also respects `{Locked}`, `{Manual}`, `{Hidden}` tags and the `[No Sorting]` grid tag.

---

## Automated Production

### Assembler routing

`FindAsmFor()` selects the target assembler per component:

1. If component is a basic type → prefer `_basicAssemblers`
2. Else → prefer `_advAssemblers`
3. Fallback to `_assemblers` if preferred pool is empty
4. Skip assemblers where `CooperativeMode == true`

`QueueToAllMasters()` queues the same blueprint to every idle non-coop master in the pool — not just one.

### Basic component list

SteelPlate, InteriorPlate, Construction, SmallTube, LargeTube, Motor, Display, BulletproofGlass, Girder, MetalGrid

### Blueprint resolution

`FindBpFor()` tries these blueprint ID patterns in order:

```
MyObjectBuilder_BlueprintDefinition/{item}
MyObjectBuilder_BlueprintDefinition/{item}Component
MyObjectBuilder_BlueprintDefinition/Position0010_{item}
MyObjectBuilder_BlueprintDefinition/Position0010_{item}Component
```

### Autocrafting

When `monitor_only=false`, `QueueCompQuotas()` runs each cycle. For each configured quota:

1. Check current stock + already queued
2. If below quota, call `QueueToAllMasters()` with the deficit amount (capped at `max_queue_amount`)
3. Max `max_queue_per_run` quotas queued per cycle

---

## Power Management

### Power stats

`BuildPowerStats()` sums all blocks in the configured power profile:
- Batteries: stored power, capacity, input, output
- Reactors, solar, wind, H2 engines: max output, online count

### Battery/reactor automation

`RunPowerControl()` checks battery percentage against thresholds:
- Below `battery_low_percent` → turn control reactors ON
- Above `battery_full_percent` → check safety, turn reactors OFF
- Safety hold: never turn off if output load above `never_turn_off_reactors_if_output_above_percent`

---

## Alert System

### Alert levels

`ALERT_OK = 0`, `ALERT_WARNING = 1`, `ALERT_CRITICAL = 2`

Individual alert fields: `_alertBattery`, `_alertCargo`, `_alertHydrogen`, `_alertOxygen`, `_alertUranium`, `_alertProduction`, `_alertOverall`

`_alertOverall` is the highest of all individual alerts.

### Warning lights

`RunWarningLights()` scans all blocks for `[AGM-LIGHT]` in Custom Data or block name. For each match:
- Light blocks: sets colour and blink state
- Text surface providers: updates `_alertLcds` cache

`DrawAlertLcds()` runs every tick and redraws all cached alert surfaces.

---

## Version History

### v1.3+
- Basic Assembler auto-detection and routing
- QueueToAllMasters — spreads work across all idle masters
- Cooperative mode detection — [M] label, COOP status
- [AGM-LIGHT] in Custom Data — no block renaming needed
- Alert corner LCDs drawn every tick — no flicker
- Responsive layouts — small grid PB, wide LCD, normal LCD
- HasDashboardCmd() excludes [AGM-LIGHT] blocks from screens
- ScanBlocks() excludes [AGM-LIGHT] blocks from _screens

### v1.3
- Unified single-script architecture
- Production dashboard v2 — assembler and refinery details
- Component stock page parsing fixed
- Missing-resource warning spam disabled by default
- PB front screen animation

### v1.2
- Power dashboard v2
- Reactor refuel page
- Battery auto-reactor charging
- Reactor safety config

### v1.1
- Alert dashboard
- Warning light tags
- Battery/cargo/hydrogen/oxygen/uranium alerts

### v1.0
- Initial release
