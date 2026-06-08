# AutoGrid Manager — Full Guide

**Script:** AutoGrid Manager v1.3+
**Author:** RevGamer
**LCD Tag:** `[AGM-S]`

---

## What AGM Does

AutoGrid Manager is a base management script for Space Engineers. One programmable block handles:

- Inventory management and auto-sorting
- Automated production — assemblers, refineries, autocrafting
- Power monitoring and reactor automation
- Fuel and life support monitoring
- Stock dashboards
- Alert system with warning lights and corner LCDs

---

## Quick Start

1. Paste `AGM.cs` into a Programmable Block
2. Recompile — default Custom Data is written automatically
3. Edit PB Custom Data to point at your block groups
4. Add `[AGM-S]` to LCD names and put one dashboard command in each LCD Custom Data
5. Recompile again

---

## Block Groups

AGM works best with named block groups. Recommended:

```
Base Batteries
Base Reactors
Base Assemblers
Base Refineries
Base Ice Generators
Base Hydrogen Tanks
Base Oxygen Tanks
```

Reference groups in Custom Data with `G:` prefix:

```ini
batteries=G:Base Batteries
reactors=G:Base Reactors
```

---

## PB Custom Data — Full Reference

### [Core]

```ini
[Core]
enabled=true
power=true
logistics=true
production=true
global_pause=false
include_docked_grids=false
no_sorting_tag=[No Sorting]
locked_tag={Locked}
manual_tag={Manual}
hidden_tag={Hidden}
```

### [Alerts]

```ini
[Alerts]
enabled=true
warning_lights=true
battery_low_percent=25
hydrogen_low_percent=20
oxygen_low_percent=20
uranium_low_kg=5
cargo_warning_percent=90
cargo_full_percent=98
```

### [Power:Base]

```ini
[Power:Base]
batteries=G:Base Batteries
reactors=G:Base Reactors
solar=
wind=
hydrogen=
include_ungrouped=false
```

### [ReactorRefuel]

```ini
[ReactorRefuel]
enabled=true
min_uranium_per_reactor=2
target_uranium_per_reactor=10
uranium_low_warning_kg=5
auto_refuel=false
```

### [PowerControl]

```ini
[PowerControl]
enabled=true
auto_reactor_charge=true
battery_low_percent=25
battery_full_percent=100
control_reactors=G:Base Reactors
control_batteries=G:Base Batteries
turn_reactors_off_when_full=true
amber_while_charging=true
minimum_reactors_online=0
never_turn_off_reactors_if_output_above_percent=80
```

### [Logistics]

```ini
[Logistics]
auto_assign=true
max_moves_per_run=2
```

### [FuelLifeSupport]

```ini
[FuelLifeSupport]
o2h2_generators=G:Base Ice Generators
h2_tanks=G:Base Hydrogen Tanks
o2_tanks=G:Base Oxygen Tanks
include_ungrouped=false
```

### [Production]

```ini
[Production]
monitor_only=false
autocraft_components=true
sort_assembler_queue=true
sort_refinery_input=true
max_queue_per_run=2
max_queue_amount=500
assemblers=G:Base Assemblers
refineries=G:Base Refineries
enabled=true
show_machine_details=true
show_missing_resources=false
```

**Important:** `monitor_only=false` is required for autocrafting to queue items.

### [RefineryPriority]

```ini
[RefineryPriority]
Stone
Iron
Nickel
Cobalt
Silicon
Magnesium
Silver
Gold
Platinum
Uranium
```

### [AssemblerPriority] and Quotas

```ini
[AssemblerPriority]
SteelPlate
InteriorPlate
Construction
Computer
Motor
Display

AutoCrafting=Component
SteelPlate=70000
InteriorPlate=70000
Construction=70000
Computer=10000
Motor=15000
MetalGrid=10000
Girder=10000
SmallTube=10000
LargeTube=10000
Display=5000
BulletproofGlass=5000
PowerCell=5000
Reactor=10000
Thrust=12000
GravityGenerator=500
Superconductor=10000
```

---

## LCD Dashboard Setup

Add `[AGM-S]` to the LCD block name. Put one command in the LCD Custom Data.

### All Dashboard Commands

| Custom Data | Page |
|---|---|
| `CoreDashboard` | System overview |
| `AlertDashboard` | Alert status |
| `WarningDashboard` | Warning details |
| `PowerDashboard page=1` | Power overview |
| `ReactorRefuel` | Reactor uranium status |
| `BatteryControl` | Battery/reactor automation |
| `LogisticsDashboard` | Sorting status |
| `ProductionDashboard page=1` | Production overview |
| `ProductionDetails` | Assembler/refinery jobs |
| `ProductionWarnings` | Refinery details |
| `InventoryStock page=1` | All items |
| `OreStock page=1` | Ores |
| `IngotStock page=1` | Ingots |
| `ComponentStock page=1` | Components |
| `AmmoStock page=1` | Ammo |
| `ToolStock page=1` | Tools |
| `BottleStock page=1` | Bottles |
| `Autocrafting page=1` | Autocrafting quotas |
| `FuelLifeSupport` | H2/O2 and life support |
| `LifeSupport` | Life support only |

Multi-page: add separate LCDs with `page=1`, `page=2`, `page=3` etc.

---

## Cargo Container Tags

Put these in the **block name** to tell AGM where to sort items:

| Tag | Item type |
|---|---|
| `{Ore 1}` | Ores |
| `{Ingot 1}` | Ingots |
| `{Component 1}` | Components |
| `{Ammo 1}` | Ammo |
| `{Tool 1}` | Tools |
| `{Bottle 1}` | Bottles |

Lower number fills first. Number up for multiple containers of the same type: `{Ore 1}`, `{Ore 2}` etc.

If `auto_assign=true`, AGM will automatically name empty containers when it needs somewhere to put items.

### Protection Tags (block name)

| Tag | Effect |
|---|---|
| `[No Sorting]` | AGM ignores this grid |
| `{Locked}` | Container never used as sort destination |
| `{Manual}` | Assembler/refinery excluded from production management |
| `{Hidden}` | Block excluded from all AGM scanning |

---

## Alert Lights and Corner LCDs

Put `[AGM-LIGHT]` in the **Custom Data** of any light block or corner LCD:

```ini
[AGM-LIGHT]
watch=Battery
```

Valid `watch=` values: `Battery`, `Cargo`, `Hydrogen`, `Oxygen`, `Uranium`, `Production`, `Charging`, `Power OK`, or blank for overall alert.

Do NOT add `[AGM-S]` to these blocks — they are managed separately.

See `AGM_Alert_Light_Guide.md` for full details.

---

## Assembler Routing

AGM automatically detects Basic Assemblers by their SubtypeId and routes accordingly:

- **Basic components** (SteelPlate, InteriorPlate, Construction, SmallTube, LargeTube, Motor, Display, BulletproofGlass, Girder, MetalGrid) → Basic Assemblers first
- **Advanced components** → Advanced Assemblers first
- Work is spread across all idle master assemblers — not just one
- Cooperative mode assemblers are skipped in queuing — they pick up work from the master automatically
- Assembler Details page shows `[M]` for master assemblers, `COOP` for cooperative ones

---

## Troubleshooting

| Problem | Fix |
|---|---|
| LCD is blank | Confirm `[AGM-S]` in block name and valid command in Custom Data |
| AGM sees other-grid blocks | Use `G:Group Name` and `include_ungrouped=false` |
| Autocrafting not queuing | Set `monitor_only=false` in `[Production]` |
| Basic Assemblers idle | Check they are not all in cooperative mode — need at least one master |
| Alert light not working | Check `[AGM-LIGHT]` is in Custom Data not block name |
| CoreDashboard flickering on corner LCD | Remove `[AGM-S]` from that block — use `[AGM-LIGHT]` only |
