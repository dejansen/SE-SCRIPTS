# AutoGrid Manager v1.5

**Author:** RevGamer
**Tag:** `[AGM-S]`

---

## Overview

One programmable block manages:

- Inventory sorting and auto-assignment
- Automated production -- assembler queuing, refinery sorting, autocrafting quotas
- Power monitoring -- batteries, reactors, solar, wind, hydrogen engines
- Reactor refuel monitoring
- Battery/reactor automation
- Fuel and life support -- H2/O2 tanks, generators, vent leak detection
- Stock dashboards -- Ore, Ingot, Component, Ammo, Tool, Bottle, Food, Seed, Ingredient
- Alert system -- warning lights and corner LCD displays

---

## Script Architecture

### Entry Points

| Method | Purpose |
|--------|---------|
| `Program()` | Constructor -- block lists, default config, Update10/100 |
| `Main(argument, updateSource)` | Entry point -- staged work and draw |
| `Save()` | Game save -- no persistent data |

### Update Frequency

- `Update10` -- staged work, draw cycle
- `Update100` -- block rescan

### Staged Work

`RunStagedWork()` cycles through stages to spread CPU load:

| Stage | Work |
|-------|------|
| 0 | RunLogistics |
| 1 | RunProduction |
| 2 | RunFuelScan |
| 3 | RunPowerControl |
| 4 | RunAlerts |
| 5 | RunWarningLights |
| 6 | SortRefineryInputs |
| 7+ | DrawScreen (2 screens per stage) |

### Key Lists

| Field | Contents |
|-------|---------|
| `_blocks` | All same-construct terminal blocks |
| `_screens` | All [AGM-S] text surface providers |
| `_alertLcds` | Alert corner LCDs -- drawn every tick |
| `_assemblers` | All managed assemblers |
| `_basicAssemblers` | Basic assemblers |
| `_advAssemblers` | Advanced assemblers |
| `_refineries` | All managed refineries |
| `_cargos` | Tagged cargo containers |
| `_sources` | Source inventories for sorting |

---

## Logistics / Auto-Sorter

Destination containers matched by tag in block name OR Custom Data (v1.5):

| Tag | Item type |
|-----|-----------|
| `{Ore N}` | `MyObjectBuilder_Ore` |
| `{Ingot N}` | `MyObjectBuilder_Ingot` |
| `{Component N}` | `MyObjectBuilder_Component` |
| `{Ammo N}` | `MyObjectBuilder_AmmoMagazine` |
| `{Tools N}` | `MyObjectBuilder_PhysicalGunObject` |
| `{Bottle N}` | `MyObjectBuilder_GasContainerObject / OxygenContainerObject` |
| `{Food N}` | `MyObjectBuilder_ConsumableItem / Consumable` |
| `{Seed N}` | `MyObjectBuilder_TreeObject` |
| `{Ingredient N}` | Ingredient items |

N is priority -- lower fills first. Spills to next N at 98% full.

### Docked Grid Exclusion (v1.5)

`[No Sorting]` in connector Custom Data OR block name excludes entire docked grid from all scanning.

---

## Automated Production

### Assembler Routing

1. Basic component -> `_basicAssemblers` first
2. Advanced component -> `_advAssemblers` first
3. Fallback to `_assemblers`
4. Skip `CooperativeMode=true` assemblers
5. `QueueToAllMasters()` -- queues to every idle non-coop master

### Assembly Mode Check (v1.5)

Before queuing, `QueueToAllMasters()` checks if assembler is in Disassembly mode. If so and not producing, switches to Assembly mode first. Prevents autocrafting silently stopping after a disassembly run.

### Autocrafting

`QueueCompQuotas()` runs when `monitor_only=false`:

1. Check stock + already queued vs quota
2. If below quota, queue deficit (capped at `max_queue_amount`)
3. Max `max_queue_per_run` per cycle

### Disassembly (v1.5)

`DisassembleExcess()` skips any component with assembly queued. Only disassembles when stock > quota AND no assembly in flight. Cannot fight autocrafting.

---

## Item Categories (v1.5)

| Category | TypeId ends with |
|----------|-----------------|
| Ore | `_Ore` |
| Ingot | `_Ingot` |
| Component | `_Component` |
| Ammo | `_AmmoMagazine` |
| Tool | `_PhysicalGunObject` |
| Bottle | `_GasContainerObject`, `_OxygenContainerObject` |
| Food | `_ConsumableItem`, `_Consumable` |
| Seed | `_TreeObject` |
| Ingredient | `IsFoodIngredient()` check |

---

## Alert System

Alert levels: `ALERT_OK = 0`, `ALERT_WARNING = 1`, `ALERT_CRITICAL = 2`

`_alertOverall` = highest of all individual alerts.

`RunWarningLights()` scans for `[AGM-LIGHT]` in Custom Data or block name.
`DrawAlertLcds()` runs every tick -- never flickers.

---

## Changelog

| Version | Notes |
|---------|-------|
| 1.5 | Docked grid exclusion fix; assembly mode check in QueueToAllMasters; DisassembleExcess skips assembly-queued items; Food/Seed/Ingredient full support; cargo type from Custom Data |
| 1.4 | Assembler details display; basic assembler routing |
| 1.3 | Unified single-script; production dashboard v2 |
| 1.2 | Power dashboard v2; reactor refuel; battery automation |
| 1.1 | Alert dashboard; warning lights |
| 1.0 | Initial release |
