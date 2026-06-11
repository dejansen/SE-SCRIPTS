# AutoGrid Manager v1.5 -- Full Guide

**Author:** RevGamer
**LCD Tag:** `[AGM-S]`

---

## What AGM Does

One PB handles inventory sorting, automated production, power monitoring, reactor automation, fuel/life support, stock dashboards, and alerts.

---

## Quick Start

1. Paste `AGM.cs` into a Programmable Block
2. Compile -- default Custom Data written automatically
3. Edit PB Custom Data to point at your block groups
4. Add `[AGM-S]` to LCD block names, put one dashboard command in each LCD Custom Data
5. Recompile

---

## Block Groups

```
Base Batteries
Base Reactors
Base Assemblers
Base Refineries
Base Ice Generators
Base Hydrogen Tanks
Base Oxygen Tanks
```

Use `G:` prefix in config:

```ini
batteries=G:Base Batteries
```

---

## PB Custom Data -- Full Reference

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
ingot_low_percent=20
component_low_percent=20
ammo_low_percent=20
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
enabled=true
monitor_only=false
autocraft_components=true
auto_disassemble=false
sort_assembler_queue=true
sort_refinery_input=true
max_queue_per_run=2
max_queue_amount=500
assemblers=G:Base Assemblers
refineries=G:Base Refineries
show_machine_details=true
show_missing_resources=false
show_blocked_assemblers=true
show_blocked_refineries=true
```

**`monitor_only=false` is required for autocrafting to queue items.**

**`auto_disassemble=false` by default.** When enabled, disassembly will not fight autocrafting -- it skips any component with assembly queued (v1.5).

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
SolarCell=1000
Detector=1000
RadioCommunication=1000
Medical=200
Reactor=10000
Thrust=12000
GravityGenerator=500
Superconductor=10000
Explosives=500
Canvas=200
ShieldComponent=2000
```

---

## LCD Dashboard Commands

Add `[AGM-S]` to LCD block name. Put one command in Custom Data.

| Custom Data | Page |
|-------------|------|
| `CoreDashboard` | System overview |
| `AlertDashboard` | Alert status |
| `WarningDashboard` | Warning details |
| `PowerDashboard page=1` | Power overview |
| `ReactorRefuel` | Reactor uranium |
| `BatteryControl` | Battery/reactor automation |
| `LogisticsDashboard` | Sorting status |
| `ProductionDashboard page=1` | Production overview |
| `ProductionDetails` | Assembler details |
| `ProductionWarnings` | Refinery details |
| `InventoryStock page=1` | All items |
| `OreStock page=1` | Ores |
| `IngotStock page=1` | Ingots |
| `ComponentStock page=1` | Components |
| `AmmoStock page=1` | Ammo |
| `ToolStock page=1` | Tools |
| `BottleStock page=1` | Bottles |
| `FoodStock page=1` | Foods (v1.5) |
| `SeedStock page=1` | Seeds (v1.5) |
| `IngredientStock page=1` | Ingredients (v1.5) |
| `Autocrafting page=1` | Autocrafting quotas |
| `FuelLifeSupport` | H2/O2 and life support |

Multi-page: separate LCDs with `page=1`, `page=2` etc.

---

## Cargo Container Tags

Put in block **name** OR **Custom Data** (v1.5 supports both):

| Tag | Item type |
|-----|-----------|
| `{Ore 1}` | Ores |
| `{Ingot 1}` | Ingots |
| `{Component 1}` | Components |
| `{Ammo 1}` | Ammo |
| `{Tools 1}` | Tools |
| `{Bottle 1}` | Bottles |
| `{Food 1}` | Foods (v1.5) |
| `{Seed 1}` | Seeds (v1.5) |
| `{Ingredient 1}` | Ingredients (v1.5) |

Lower number fills first. `{Ore 1}`, `{Ore 2}` etc. for multiple containers.

### Protection Tags

| Tag | Effect |
|-----|--------|
| `[No Sorting]` | Connector: excludes entire docked grid |
| `{Locked}` | Container never used as sort destination |
| `{Manual}` | Block excluded from production management |
| `{Hidden}` | Block excluded from all scanning |

---

## Alert Lights and Corner LCDs

Put `[AGM-LIGHT]` in the **Custom Data** of any light or corner LCD:

```ini
[AGM-LIGHT]
watch=Battery
```

Valid `watch=`: Battery, Cargo, Hydrogen, Oxygen, Uranium, Production, Charging, Power OK, blank (overall).

Do NOT add `[AGM-S]` to these blocks.

---

## Troubleshooting

| Problem | Fix |
|---------|-----|
| LCD blank | Check `[AGM-S]` in block name, valid command in Custom Data |
| AGM pulls from docked ship | Put `[No Sorting]` in connector Custom Data |
| Autocrafting not queuing | Set `monitor_only=false` in [Production] |
| Assembler stuck after disassembly run | Fixed in v1.5 -- update script |
| Alert light flickering | Remove `[AGM-S]` from that block |
| Instruction limit | Lower `max_moves_per_run` and `max_queue_per_run` |
