# CLAUDE.md -- AutoGrid Manager

AI agent instructions for AGM Core.
Author: RevGamer

---

## Version: 1.5

## Source File
`F:\Space Engineers Script\My-Personal-Space-Engineer-Script\AGM\Scripts\AGM.cs`

## Minifier Command
```powershell
& "F:\Space Engineers Script\minifiedtool\IngameScriptMergeTool\IngameScriptMergeTool.exe" -s "F:\Space Engineers Script\My-Personal-Space-Engineer-Script\AGM\Minified_Tool\AGM_Minified.sln" -m -d "Autogrid Manager"
```
Current size: ~91.8KB / 100KB limit.
After minifying: Copy Scripts\AGM.cs to GitHub\AGM.cs

---

## Key Rules
- C# 6 only -- no inline out var, no string interpolation, no tuples
- No static fields
- Plain ASCII in string literals -- no em dashes, no smart quotes
- monitor_only=false required for autocrafting
- Always re-fetch source from disk before patching
- Validate brace balance before writing
- All borders on VP edge at 6f -- never on panel
- Corner LCD is BOTH IMyLightingBlock AND IMyTextSurfaceProvider -- never gate on if(light==null)
- FindBpFor uses CanUseBlueprint validation -- never TryParse guess
- _dockedGridIds is HashSet<IMyCubeGrid> -- both connector sides added, Me.CubeGrid always removed
- DrawAlertLcds per-entry try/catch -- bad entries auto-removed
- PrepSurf has null check and try/catch
- VP() has null and zero-size guard

---

## Dashboard Commands
CoreDashboard, AlertDashboard, WarningDashboard, PowerDashboard, ReactorRefuel, BatteryControl, LogisticsDashboard, ProductionDashboard, ProductionDetails, ProductionWarnings, InventoryStock, OreStock, IngotStock, ComponentStock, AmmoStock, ToolStock, BottleStock, FoodStock, SeedStock, IngredientStock, Autocrafting, FuelLifeSupport, LifeSupport

---

## Item Categories
Ore, Ingot, Component, Ammo, Tool, Bottle, Food, Seed, Ingredient

---

## Next
- v1.6: CoreDashboard AGM family PB scan
- AGM Defence Grid: future PB
