# CLAUDE.md -- AutoGrid Manager

AI agent instructions for AGM.
Author: RevGamer

---

## Version: 3.0

## Folder Role

This is the **release copy** tracked in the `SE-SCRIPTS` git repo:

```text
F:\Space Engineers Script\SE-SCRIPTS\Autogrid Manager\AGM.cs
```

It mirrors the paste-ready output from the canonical project at:

```text
F:\Space Engineers Script\My-Personal-Space-Engineer-Script\AGM\
```

**Do not edit `AGM.cs` in this folder as a development source.** It is a
generated, minified, paste-ready copy. Make code changes in the canonical
project's editable source, regenerate the minified copy there, then sync the
result into this folder.

## Canonical Source Layout

```text
My-Personal-Space-Engineer-Script\AGM\
|-- READY TO USE/
|   |-- AGM.cs              Minified paste-ready script (same role as this folder's AGM.cs)
|   |-- AGM-Full-Guide.md   Complete setup guide (same role as this folder's AGM-Full-Guide.md)
|   `-- README.md
|-- Developer/
|   |-- AutoGrid-Manager-v3.0.cs   Canonical EDITABLE source -- make changes here
|   |-- Minified_Tool/             AGM3_Minified.sln minification project
|   |-- Archive/                   Meaningful recovery points only
|   `-- README.md
|-- Guide/
|   `-- AGM-Full-Guide.md   Canonical copy of the guide; convenience copy lives in READY TO USE/
`-- Reference/
    |-- GOAT_Sorter_Reference.md
    |-- IIM_Sorter_Reference.md
    |-- SE_BlockTypeIds.md
    `-- SE_ItemTypeIds.md
```

**Workflow for any code change:**
1. Read `Developer\AutoGrid-Manager-v3.0.cs` fresh from disk before editing.
2. Patch the editable source there.
3. Regenerate the minified copy via the `Minified_Tool` project (see Minifier Command below).
4. Copy the regenerated minified script into both `READY TO USE\AGM.cs` and
   this repo's `Autogrid Manager\AGM.cs`.
5. If guide content changed, update `Guide\AGM-Full-Guide.md` and sync to
   `READY TO USE\AGM-Full-Guide.md` and this folder's `AGM-Full-Guide.md`.

## Minifier Command

```powershell
& "F:\Space Engineers Script\My-Personal-Space-Engineer-Script\AGM\Developer\Minified_Tool\IngameScriptMergeTool.exe" -s "F:\Space Engineers Script\My-Personal-Space-Engineer-Script\AGM\Developer\Minified_Tool\AGM3_Minified.sln" -m -d "AutoGrid Manager"
```

Current size: ~86.9KB / 100KB limit (minified `AGM.cs` is 86,693 bytes / 308 lines).

---

## What AGM v3.0 Is

Confirmed directly from `Developer\AutoGrid-Manager-v3.0.cs` (read in full,
167 lines, header comment: "Combined inventory, logistics, monitoring,
control, and LCD release"). **v3.0 is one single PB script file that already
contains both Phase 1 and Phase 2 merged together** -- there is no separate
Phase-1-only or Phase-2-only source file in the Developer folder. "Phase 1"
and "Phase 2" below describe the two feature sets inside this one file, not
two different files or two different versions.

- **Phase 1 (inventory / logistics):** block scanning and classification
  (`S0_Scan`, `Classify`), item discovery (`S1_FindItems`), untagged-cargo
  auto-assignment (`S3_Assign`), `[Stock]` fill/limit/min/pinned logic
  (`S4_Stock`), category sorting (`S5_Sort`), optional same-category container
  balancing (`S6_Balance`), turret ammo resupply (`S7_Turrets`), ice/uranium
  balancing (`S8_Ice`, `S9_Uranium`), assembler output cleanup
  (`S12_AsmClean`), refinery ore balancing/sorting (`S15_OreBalance`,
  `S16_RefSort`), and per-container internal sort/fill-name display
  (`S17_InternalSort`).
- **Phase 2 (monitoring / control / LCD):** the `_p*` scanned-block lists
  (`P2Scan`), system metrics (`P2Metrics`), automatic H2-engine/sorter/turret
  control (`P2Control`, `SetSafe`, `SetSorters`, `BalanceTanks`), the alert
  engine (`P2Alert`, `AlertData`), and the dashboard renderers (`DrawPower`,
  `DrawFuel`, `DrawDock`, `DrawDef`, `DrawProd`, `DrawLog`, `DrawAlert`,
  `CDrawMain`, `CDrawPB`, the inventory-screen `CDrawInv`/`CDrawKnown` family).

One concrete trace of the historical merge: `EnsureConfig` auto-migrates an
old `@AGM-Phase2 START`/`END` Custom Data block (`OLD_CFG_START`/`OLD_CFG_END`)
into the current `@AGM-Configuration START`/`END` block (`CFG_START`/`CFG_END`)
on first boot if found. Phase 2 config used to live under its own tagged
section; it does not anymore, but AGM still upgrades old PB Custom Data in
place rather than breaking it.

Architecture: bracket-tag style (`[AGM-LCD]`, `[AGM-Dock]`, etc.), staged
25-step scheduler (`TOTAL_STEPS=25`) running on `Update1`, with a multi-phase
boot sequence before normal operation starts.

**v3.0 intentionally has no autocrafting and no automatic disassembly.**
Those systems were removed from earlier development branches. Assemblers are
still monitored (`S12_AsmClean`) and their finished output is sorted into the
correct tagged cargo, but AGM never queues new assembler jobs.

### Scheduler steps (in `Main`)
`S0_Scan -> S1_FindItems -> P2Control -> S3_Assign -> S4_Stock -> S5_Sort ->
S6_Balance -> S7_Turrets -> S8_Ice -> S9_Uranium -> S10_Amounts ->
S12_AsmClean -> S15_OreBalance -> S16_RefSort -> S17_InternalSort ->
CDrawPB -> CDrawMain -> DrawAlert -> DrawPower -> DrawFuel -> DrawDock ->
DrawDef -> DrawProd -> DrawLog -> DrawInventoryStep`

Live dashboards (`FastLiveDashboards` / `P2Display`) redraw on a faster
10-tick cycle separate from the heavy scheduler steps above.

### Commands (PB argument via `DoCmd`)
| Command | Action |
| --- | --- |
| `reboot` / `reset` | Restart staged boot, rescan, rebind LCDs |
| `safemode` | Persistently disable tagged `[AGM-Turret]` turrets |
| `combatmode` | Persistently enable tagged `[AGM-Turret]` turrets |
| `sorterson` | Enable AGM inventory routing + local conveyor sorters |
| `sortersoff` | Disable AGM inventory routing + local conveyor sorters |
| `sort` | Jump scheduler to the next sorting step |
| `asmstatus` | Log detected assembler working/idle status |
| `quotas` | Reload the display quota section from Custom Data |

There is no `phase2cfg`, `scanlcds`, `lcdreset`, or `page...` command, and no
per-dashboard PB-argument commands (`CoreDashboard` etc. do not exist in v3.0
-- that list is from an older pre-3.0 line, see Superseded Notes below).

### Item categories (cargo name/Custom Data tags)
`Ore, Ingot, Component, Ammo, Tools, Bottles, Food, Seeds, Ingredients`

### Core tags
`[AGM-LCD]` (LCD name tag), `[Stock]`, `[no-sort]`, `[no-agm]`, `[no-pull]`,
`[locked]`, `[hidden]`, `[manual]`, `[assembler]`, `[basic assembler]`,
`G:[tag]` (group prefix). Phase 2 system tags: `[AGM-Reactor]`,
`[AGM-H2Engine]`, `[AGM-Tank]`, `[AGM-Gen]`, `[AGM-Vent]`, `[AGM-Dock]`,
`[AGM-Turret]`, `[AGM-Alert]`, `[AGM-Refinery]`, `[AGM-Battery]`,
`[AGM-Solar]`, `[AGM-Wind]`.

LCD roles are assigned via a Custom Data line (not the tag itself): `Power`,
`Fuel`, `Dock`, `Defence`/`Defense`, `Production`, `Logistics`, `Main`,
`Autocraft`/`Crafting` (cleared, unused in v3.0), `OreStock`/`Ore`,
`IngotStock`/`Ingot`, `ComponentStock`/`Component`, `AmmoStock`/`Ammo`,
`ToolStock`/`Tools`, `FoodStock`/`Food`, `BottleStock`/`Bottles`,
`SeedStock`/`Seeds`, `IngredientStock`/`Ingredients`. Anything untagged
defaults to Main.

---

## Key Rules
- C# 6 only -- no inline `out var`, no string interpolation, no tuples
- No `static` fields
- Plain ASCII in string literals -- no em dashes, no smart quotes
- Always re-fetch source from disk before patching (read `Developer\AutoGrid-Manager-v3.0.cs`, never patch from memory or a stale copy)
- Validate brace balance before writing
- `[no-agm]` on a connector excludes the entire docked construct including subgrids via `IsSameConstructAs` -- `[locked]` alone does NOT exclude a dock grid
- All turrets (`IMyUserControllableGun`) are excluded from sort sources automatically
- Corner LCD is BOTH `IMyLightingBlock` AND `IMyTextSurfaceProvider` -- never gate registration on `if(light==null)`
- `_excl` is a `HashSet<IMyCubeGrid>`; `IsExcluded` also checks `_exclConn` so both connector sides of an excluded dock are covered
- `DrawAlert` auto-switches to a compact thin-banner layout on short, wide corner LCDs
- `SetupSurf` has a null/skip guard and is safe to call repeatedly without re-initialising an already-configured surface
- `XferType` matches on a TypeId substring, not an exact equality -- be careful introducing new TypeId constants that could substring-collide
- `[Stock]` blocks with non-empty Custom Data that contains neither an AGM nor GOAT stock section are intentionally ignored entirely (data-corruption guard) -- do not "fix" this by attempting to parse anyway
- `[Stock]` redistributes existing items -- it cannot create items from nothing
- `autoDisasm`/autocrafting do not exist in v3.0 -- do not add them without being told to; they were deliberately removed

---

## Dashboard Screens (LCD role, not PB command)
Main, Power, Fuel, Dock, Defence, Production, Logistics, Alert, plus per-category
inventory screens: OreStock, IngotStock, ComponentStock, AmmoStock, ToolStock,
BottleStock, FoodStock, SeedStock, IngredientStock.

## Item Categories
Ore, Ingot, Component, Ammo, Tools, Bottles, Food, Seeds, Ingredients

---

## Superseded Notes (do not follow)
An earlier v1.5 line of this same CLAUDE.md referenced PB-argument dashboard
commands (`CoreDashboard`, `AlertDashboard`, `WarningDashboard`, `PowerDashboard`,
`ReactorRefuel`, `BatteryControl`, `LogisticsDashboard`, `ProductionDashboard`,
`ProductionDetails`, `ProductionWarnings`, `InventoryStock`, `OreStock`,
`IngotStock`, `ComponentStock`, `AmmoStock`, `ToolStock`, `BottleStock`,
`FoodStock`, `SeedStock`, `IngredientStock`, `Autocrafting`, `FuelLifeSupport`,
`LifeSupport`) and a v1.6 roadmap item for "CoreDashboard AGM family PB scan."
None of that exists in the current v3.0 `DoCmd` or scheduler. Screens above are
LCD Custom Data roles, not PB commands. Do not reintroduce the old command set
or roadmap without being explicitly told to.

---

## Next
- v3.0 already includes the full Phase 1 + Phase 2 feature merge (see "What
  AGM v3.0 Is" above). No further feature work is scheduled on this line.
  Confirm with RevGamer before starting any new feature.
