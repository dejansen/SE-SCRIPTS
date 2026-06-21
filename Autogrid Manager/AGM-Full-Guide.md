# AutoGrid Manager v3.0 - Complete Guide

## 1. Current Release

Use this paste-ready combined script:

```text
READY TO USE/AGM.cs
```

The editable unminified source is:

```text
Developer/AutoGrid-Manager-v3.0.cs
```

Regenerate `READY TO USE/AGM.cs` after future source changes instead of editing
the minified copy directly.

It combines the approved inventory-management features with the Power, Fuel,
Dock, Defence, Production, Logistics, and Alert systems in one programmable
block script.

AutoGrid Manager v3.0 does **not** perform autocrafting or automatic
disassembly. Those systems were intentionally removed. Assemblers are still
monitored and their completed output is moved to the correct tagged cargo.

## 2. Installation

1. Build one programmable block on the grid that AGM will manage.
2. Open `READY TO USE/AGM.cs` and paste the complete script into the
   programmable block editor.
3. Check the code, then run it once.
4. Wait for the AGM loading screen to reach 100 percent.
5. Add the required tags to cargo, machines, LCDs, connectors, and alert lights.

The programmable block automatically uses `Update1` as its timing clock. AGM
spreads heavy inventory work and LCD drawing over different ticks to reduce
instruction spikes and incomplete sprite frames.

## 3. Tag Rules

- Tags are case-insensitive.
- Most block-control and cargo tags work in either the block name or Custom Data.
- `[AGM-LCD]` must be in an LCD block's **name**.
- The LCD role must be a complete line in that LCD's Custom Data.
- Tags require square brackets where shown. Do not use `{` or `}`.
- Multiple cargo categories may be placed on one container.

Example cargo Custom Data:

```text
Food
Seeds
Ingredients
```

Example LCD setup:

```text
Block name: [AGM-LCD] Power Display

Custom Data:
page=0
Power
```

## 4. Inventory Cargo Categories

Add one or more category words to a cargo name or Custom Data:

| Tag | Receives |
| --- | --- |
| `Ore` | Ores, including stone, scrap ore, organic ore, and ice |
| `Ingot` | Refined ingots, gravel, scrap ingot, and prototech scrap |
| `Component` | Components |
| `Ammo` | Supported ammunition magazines |
| `Tools` | Hand tools, weapons, datapads, and similar items |
| `Bottles` | Oxygen and hydrogen bottles after refill processing |
| `Food` | Drinks and prepared meal packs |
| `Seeds` | `SeedItem` fruit, grain, mushroom, and vegetable seeds |
| `Ingredients` | Fruit, grain, mushrooms, vegetables, algae, and meat ingredients |

AGM treats ordinary untagged cargo as a sorting source. It removes recognized
items from that cargo and sends them to the matching tagged destination.

### Food and ingredient separation

`Food` receives drinks and prepared `MealPack_` items. `Ingredients` receives
farm outputs and raw/cooked ingredient subtypes. Fruit is an ingredient, not a
prepared food. Seed packets use the separate `SeedItem` type and go to `Seeds`.

Seeds and both ingredient item types are checked on every AGM sorter pass.
Assembler-produced seeds are also recognized by assembler output cleanup.

### Destination priority

Put a priority tag in the **cargo name** when several cargos share a category:

```text
[Pmax]     Highest priority
[P1]       Numeric priority
[P20]      Lower priority than P1
[Pmin]     Lowest priority
```

AGM fills the lowest numeric priority first. Untagged priorities use a stable
fallback order.

### Automatic cargo assignment

If a required category has no destination, AGM may assign an untagged local
cargo by adding category words to its name. Default grouping is:

- Ore and Ingot together
- Ammo, Tools, and Bottles together
- Food, Seeds, and Ingredients together
- Components separately

When more than one name-tagged cargo for a category is empty, AGM may remove an
extra category word after a delayed recheck. Category tags placed in Custom Data
are not removed by this name cleanup.

## 5. Inventory Control Tags

| Tag | Behaviour |
| --- | --- |
| `[no-sort]` | Excludes the block from normal AGM cargo sorting |
| `[locked]` | Leaves the inventory under manual control |
| `[no-pull]` | AGM will not use this block as a normal sorting source |
| `[hidden]` | Hides the block from normal inventory totals/source scanning |
| `[manual]` | AGM ignores supported machine automation for that block |
| `[NO-AGM]` | Excludes a tagged machine, weapon, or connected construct as applicable |
| `[Stock]` | Makes the inventory a stock-controlled container |

Blocks hidden with the terminal's `Show in Inventory` option are also excluded
from normal source scanning.

## 6. AGM Inventory Sorter

The AGM inventory sorter is script automation. It does not require conveyor
sorter blocks. It transfers items between connected inventories according to
the category and Stock rules above.

The Logistics LCD shows:

- AGM sorter online/offline state
- Overall fill level for every cargo category
- Recent transfers performed by AGM

Physical conveyor sorter blocks on the local construct follow the same
`sorterson` and `sortersoff` state, but AGM cannot observe transfers that a
physical sorter performs independently.

All transfers require a valid conveyor path, compatible inventory, available
space, and normal game ownership/access permissions.

## 7. Stock Containers

Add `[Stock]` to a block name or Custom Data.

### Custom Data guard

If the Stock block's Custom Data already contains **anything**, AGM does not
insert its own template. It leaves the content untouched. This allows an
existing GOAT template or any manually prepared Stock section to remain intact.

If Custom Data is completely empty, AGM may create an all-zero AGM template.

### AGM Stock format

```text
@AGM-Stock Definitions START
Ingot/Iron=0
Ingot/Nickel=0
Ingot/Cobalt=0
Ingot/Silicon=0
Ingot/Magnesium=0
Ingot/Silver=0
Ingot/Gold=0
Ingot/Platinum=0
Ingot/Uranium=0
Ingot/Stone=0
SteelPlate=1000
Construction=500M
Motor=200L
Computer=100P
NATO_25x184mm=all
Component/Display=50
@AGM-Stock Definitions END
```

New empty Stock containers receive every supported ingot as an explicit
`Ingot/Subtype=0` entry. This includes iron, nickel, cobalt, silicon,
magnesium, silver, gold, platinum, uranium, stone/gravel, prototech scrap, and
scrap. Change only the ingots required by that Stock container.

| Format | Meaning |
| --- | --- |
| `Item=100` | Fill to 100 and remove excess above 100 |
| `Item=100M` | Minimum 100: fill shortages and keep excess |
| `Item=100L` | Limit 100: remove excess and never add shortages |
| `Item=all` | Fill available space and keep excess |
| `Item=100P` | Exact 100, processed as a pinned priority rule |
| `Item=100MP` | Pinned minimum rule |
| `Item=100LP` | Pinned limiter rule |
| `Item=0` | Keep none under an exact rule |

Use `Type/Subtype` for ambiguous or modded items:

```text
Component/Display=50
ConsumableItem/Fruit=100
SeedItem/Fruit=20
PhysicalObject/Grain=100
```

### GOAT compatibility

AGM also reads sections bounded by:

```text
@GOAT-Stock Definitions START
@GOAT-Stock Definitions END
```

Common GOAT aliases are translated internally, including component names,
GOAT ammunition names, ore/ingot suffixes, tools, weapons, bottles, meals, and
seed names such as `FruitSeeds`, `GrainSeeds`, `MushroomSpores`, and
`VegetableSeeds`.

AGM does not replace a valid GOAT template with an AGM template.

## 8. Connector Isolation

Put `[NO-AGM]` on either connector involved in a connected dock when the docked
construct must be excluded from AGM inventory and system management.

AGM follows the connected pair and excludes the construct that is not the
programmable block's local construct. This prevents a docked ship's tagged or
untagged inventories from being treated as base storage.

`[NO-AGM]` isolation also prevents a docked `[Stock]` ship container from being
filled by the base AGM. Remove `[NO-AGM]` only when cross-dock management is
intended.

## 9. Assemblers and Production Output

Supported assembler tags:

```text
[Assembler]
[Basic Assembler]
```

These tags include the assembler in the Production dashboard. AGM reports
online, working, idle, damaged, and total queue state.

AGM also removes completed recognized output from managed local assemblers and
sends it to Component, Ammo, Tools, Bottles, Food, Seeds, or Ingredients cargo.

Use `[manual]` or `[NO-AGM]` to exclude an assembler from AGM management.

AutoGrid Manager v3.0 does not add jobs, remove jobs, reorder queues, change
assembly mode, or change disassembly mode.

## 10. Refinery Automation

Add this to refineries that should appear on the Production screen:

```text
[AGM-Refinery]
```

Local refineries are fed from Ore cargo. AGM:

1. Removes refinery output to Ingot cargo.
2. Selects a processable ore whose matching ingot stock is lowest.
3. Excludes ice from refinery feeding.
4. Adds ore when refinery input is below approximately 45 percent full.
5. Balances refinery input across multiple available refineries.
6. Moves the lowest-stock ore type toward the front of a refinery inventory.

This is why stored cobalt ore can be sent to an idle refinery when cobalt ingot
stock is comparatively low.

## 11. Turret Ammunition and Defence

AGM automatically tops up supported local weapon inventories from Ammo cargo.
Weapons tagged `[manual]` or `[NO-AGM]` are ignored by automatic loading.

Default weapon targets are defined in `TURRET_AMMO_TARGETS` near the top of the
script. The current supported turret ammunition includes gatling, missile,
autocannon, artillery/assault cannon, railgun, and interior-turret magazines
under their real in-game subtype IDs.

Add this tag to turrets that belong on the Defence screen and respond to safe
mode:

```text
[AGM-Turret]
```

Defence states:

- Green: enabled, functional, and carrying ammunition
- Amber: disabled or no ammunition
- Red: damaged/non-functional
- Cyan alert: Safe Mode or all tagged turrets offline
- Amber fast alert: a tagged large turret has an actual target

`safemode` disables all tagged turrets. `combatmode` enables them again. Safe
mode is stored and survives a programmable block or server reload.

## 12. Power System

### Required tags

```text
[AGM-Battery]
[AGM-Reactor]
[AGM-H2Engine]
[AGM-Solar]
[AGM-Wind]
```

Only tagged blocks appear in the Power dashboard. Untagged solar panels and
wind turbines are hidden from that screen.

The Power screen shows one state square per tagged reactor and hydrogen engine,
plus live combined bars for:

- Battery charge
- Battery output
- Solar output, when tagged solar panels exist
- Wind output, when tagged wind turbines exist

### Hydrogen fallback automation

AGM leaves the user's existing vanilla reactor battery automation intact.
Tagged hydrogen engines are enabled only when all of these are true:

- At least one tagged reactor exists
- At least one tagged battery exists
- Tagged reactors contain no uranium
- Tagged battery charge is below `BatteryEngineOn`

Hydrogen engines turn off when uranium becomes available or battery charge
reaches `BatteryEngineOff`.

### Reactor uranium distribution

Only `[AGM-Reactor]` reactors receive uranium. Untagged reactors, including a
docked ship's reactor, are not supplied by this feature.

Default targets:

- Large reactor: 200 kg
- Large-grid small reactor: 50 kg
- Small-grid reactor: 25 kg

## 13. Fuel and Life Support

### Required tags

```text
[AGM-Tank]
[AGM-Gen]
[AGM-Vent]
```

The Fuel and Life Support screen shows:

- One state square per tagged hydrogen tank
- One state square per tagged oxygen tank
- Combined H2 and O2 fill percentages
- Tagged O2/H2 generator ice supply
- Tagged pressurizing vent count and room pressure health
- Names of up to three leaking or low-pressure vents

### Tank balancing

AGM compares tagged tanks of the same gas. A tank more than roughly two percent
below the group average is set to Stockpile until the group becomes balanced.
Only `[AGM-Tank]` tanks participate.

### Generator ice and bottle refill

AGM distributes ice to `[AGM-Gen]` generators and enables their Auto-Refill
setting. Oxygen and hydrogen bottles are moved into tagged generators first.
After refill processing, they are returned to Bottles cargo.

### Air vents

Only `[AGM-Vent]` vents are monitored. No extra room key is required: AGM uses
the vent block's Custom Name when reporting a leak.

Vents set to Depressurize are ignored because they are treated as airlock or
intentional depressurization vents. A monitored pressurizing vent warns when it
is disabled, cannot pressurize, is damaged, or reports less than 95 percent
oxygen.

## 14. Dock Monitor

Add this to each dock connector's name or Custom Data:

```text
[AGM-Dock]
```

Optional friendly label in connector Custom Data:

```text
dock=Hangar 1
```

Without `dock=`, AGM displays the connector's block name.

Each occupied dock row reports the entire connected ship construct:

- Ship/grid name
- Battery percentage and battery count
- Hydrogen percentage and tank count
- Oxygen percentage and tank count
- Overall inventory/cargo percentage and inventory count

The screen fits five dock rows per page. Empty slots are shown as clear. Dock
data is prioritized in the live dashboard rotation for faster connect/disconnect
updates.

## 15. Production Monitor

The Production LCD monitors tagged `[Assembler]`, `[Basic Assembler]`, and
`[AGM-Refinery]` blocks.

It displays:

- One colored state square per machine
- Assembler online/working count
- Total assembler queue amount
- Refinery online/working count
- Combined refinery input fill

Green means working, amber means disabled or idle, and red means damaged.

## 16. LCD Setup

Every standard AGM LCD must contain this in the block name:

```text
[AGM-LCD]
```

Put exactly one role on its own Custom Data line:

```text
Main
Power
Fuel
Dock
Defence
Production
Logistics
OreStock
IngotStock
ComponentStock
AmmoStock
ToolStock
FoodStock
BottleStock
SeedStock
IngredientStock
```

`Defense` is accepted as an alternative spelling for `Defence`.

Old `Autocraft` and `Crafting` LCD roles are cleared and ignored.

### Multiple pages

Use one LCD for each required page and put a zero-based page number in Custom
Data:

```text
page=0
ComponentStock
```

```text
page=1
ComponentStock
```

Continue with `page=2`, `page=3`, and so on. AGM automatically resets an invalid
page to page 0. Inventory item screens fit up to 14 rows per page and preserve
item sprite icons.

### Automatic LCD detection and boot refresh

AGM watches the LCD set, names, Custom Data, and working state. When the layout
changes, it automatically starts a staged loading sequence and rebinds all
screens. Manual `scanlcds`, `lcdreset`, and page commands are not required and
are not supported.

The boot sequence deliberately draws across multiple ticks. It does not call
multiple conflicting sprite frames for the same dashboard in one update.

## 17. Display Quotas

AGM creates this section in programmable block Custom Data:

```text
@AGM-Display Quotas START
Ore/Iron=100000
Ingot/Iron=100000
Component/SteelPlate=10000
AmmoMagazine/NATO_25x184mm=100
ConsumableItem/ClangCola=100
ConsumableItem/Fruit=500
PhysicalObject/Grain=500
GasContainerObject/HydrogenBottle=20
OxygenContainerObject/OxygenBottle=20
SeedItem/Fruit=100
@AGM-Display Quotas END
```

Inventory-screen bars compare the current grid amount with the quota. A
configured item remains visible when its current amount is zero.

Use `Type/Subtype` whenever two item types share a subtype. Run the `quotas`
argument after editing this section to reload it immediately.

Display quotas do not order crafting. They are visual targets only.

## 18. AGM Configuration

AGM creates this section in programmable block Custom Data:

```text
@AGM-Configuration START
BatteryEngineOn=25
BatteryEngineOff=100
H2Warn=30
H2Crit=10
O2Warn=30
O2Crit=10
IceWarn=10000
IceCrit=1000
CargoWarn=85
CargoCrit=98
UraniumWarn=10
LargeReactorUranium=200
SmallReactorUranium=50
SmallGridReactorUranium=25
@AGM-Configuration END
```

AGM reloads these values automatically during control cycles. Existing legacy
configuration values are migrated to the new section name during boot.

| Setting | Purpose |
| --- | --- |
| `BatteryEngineOn` | Battery percentage that permits hydrogen fallback |
| `BatteryEngineOff` | Battery percentage that stops hydrogen fallback |
| `H2Warn` / `H2Crit` | Hydrogen alert thresholds |
| `O2Warn` / `O2Crit` | Oxygen alert thresholds |
| `IceWarn` / `IceCrit` | Generator ice alert thresholds in kg |
| `CargoWarn` / `CargoCrit` | Overall cargo alert percentages |
| `UraniumWarn` | Tagged-reactor uranium warning amount |
| Reactor uranium settings | Per-reactor uranium targets in kg |

## 19. Alert LCDs and Lights

Add `[AGM-Alert]` to each corner LCD and its matching light block. Put one role
on its own Custom Data line on both blocks:

```text
Battery
Uranium
Hydrogen
Oxygen
Cargo
Defence
```

The alert renderer automatically uses a compact thin-banner layout on short,
wide corner LCDs.

### General colors

| Color | Meaning |
| --- | --- |
| Green | Healthy, ready, plentiful, or full |
| Amber slow blink | Warning or approaching a limit |
| Red fast blink | Critical, empty, depleted, full cargo, or damaged |
| Cyan | Charging, refilling, Safe Mode, or intentionally offline |

### Battery alert

- Green: OK or full
- Cyan: charging while below full
- Amber slow blink: within 10 percent above the low threshold
- Red fast blink: at or below `BatteryEngineOn`

### Uranium alert

- Green: plenty
- Amber slow blink: below `UraniumWarn`
- Red fast blink: depleted

### Hydrogen and oxygen alerts

- Green: at least 90 percent/full
- Cyan: refilling when generators and ice are available
- Amber slow blink: below 50 percent but above critical
- Red fast blink: critical or empty

### Cargo alert

- Green: below 50 percent
- Amber slow blink: 50 percent or more
- Red fast blink: at or above `CargoCrit`

### Defence alert

- Green: online
- Amber fast blink: a tagged large turret has an actual target
- Cyan: Safe Mode or offline
- Red fast blink: damaged

## 20. Commands

Run these as programmable block arguments:

| Command | Action |
| --- | --- |
| `reboot` | Restart AGM's staged boot, rescan, and LCD binding |
| `reset` | Alias for `reboot` |
| `safemode` | Persistently disable tagged `[AGM-Turret]` turrets |
| `combatmode` | Persistently enable tagged `[AGM-Turret]` turrets |
| `sorterson` | Enable AGM inventory routing and local conveyor sorters |
| `sortersoff` | Disable AGM inventory routing and local conveyor sorters |
| `sort` | Move the scheduler to its next inventory sorting step |
| `asmstatus` | Write detected assembler status to AGM's recent log |
| `quotas` | Reload the display quota section |

There is no `phase2cfg`, `scanlcds`, `lcdreset`, or `page...` command.

## 21. Main and Programmable Block Screens

The Main LCD shows Systems and Storage summaries for later expansion. The
programmable block's own screen displays `AGM` and `AutoGrid Manager v3.0` only.
All release branding and dashboard footers use `AutoGrid Manager v3.0`.

## 22. Update and Refresh Behaviour

- `Update1` is the script timing clock.
- Live dashboard drawing runs every 10 game ticks, approximately six draw
  opportunities per second under normal simulation speed.
- Heavy inventory work runs on a separate offset tick.
- Only one dashboard from the live rotation is drawn per opportunity.
- Dock is included three times in the eight-slot live rotation.
- Inventory categories and large multi-page sets are spread across scheduler
  cycles to control instruction use.
- LCD layout changes trigger automatic staged rebinding.

This design reduces stale, missing, overlapping, and partially committed sprite
frames. A game or graphics-mod rendering fault can still require leaving and
re-entering the area or reloading the world; AGM cannot directly reset the game
client's renderer.

## 23. Troubleshooting

### Items do not move

Check all of the following:

1. Run `sorterson`.
2. Confirm the destination category tag is present in its name or Custom Data.
3. Confirm the source is not `[no-sort]`, `[locked]`, `[no-pull]`, `[hidden]`,
   or hidden with `Show in Inventory` disabled.
4. Confirm the destination has free volume.
5. Confirm a working conveyor path exists.
6. Confirm ownership and access permit inventory transfers.
7. Confirm `[NO-AGM]` is not excluding the connected construct.
8. Check the Logistics screen's recent AGM transfers.

### A Stock block is ignored

If its Custom Data is non-empty but contains neither an AGM nor GOAT Stock
section, the Custom Data guard intentionally ignores it completely. Add a valid
section manually or empty Custom Data and allow AGM to create its template.

### Docked ship is being sorted

Add `[NO-AGM]` to either connector in that connected pair.

### Vent reports a leak

Check the reported vent block name. The vent must be enabled, functional, set to
pressurize, able to pressurize, and reading at least 95 percent oxygen.

### Screen does not update after editing tags

Wait for automatic LCD detection and the staged AGM loading screen. Use `reboot`
only when an immediate complete rescan is needed.

### Assembler remains idle

AGM v3.0 monitors assemblers and clears completed output, but it does not create
assembler jobs. Another script or manual queue must provide production orders.

## 24. Backup and Recovery

Recovery sources are stored under:

```text
Developer/Archive/
```

It contains the exact Phase 1 stable source, the last source before automatic
crafting/disassembly removal, and the immediate pre-combined development source.

Do not overwrite those files when creating later minified or experimental
versions.
