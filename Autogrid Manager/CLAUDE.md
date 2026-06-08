# CLAUDE.md — AutoGrid Manager

AI agent instructions for working on AGM Core.
Author: RevGamer

---

## Project Layout

```
F:\Space Engineers Script\My-Personal-Space-Engineer-Script\
  AGM\
    Scripts\AGM.cs                         <- EDIT THIS — source file
    Minified_Tool\AGM_Minified.sln         <- minifier solution
    Docs\Guide\
    Docs\reference\

C:\Users\corra\AppData\Roaming\SpaceEngineers\IngameScripts\local\
  Autogrid Manager\Script.cs              <- AGM minified output (SE paste target)

F:\Space Engineers Script\minifiedtool\IngameScriptMergeTool\
  IngameScriptMergeTool.exe               <- shared minifier tool
```

---

## Minifier Command

```powershell
Remove-Item "F:\Space Engineers Script\My-Personal-Space-Engineer-Script\AGM\Minified_Tool\AGM_Minified\bin" -Recurse -Force -ErrorAction SilentlyContinue; Remove-Item "F:\Space Engineers Script\My-Personal-Space-Engineer-Script\AGM\Minified_Tool\AGM_Minified\obj" -Recurse -Force -ErrorAction SilentlyContinue; & "F:\Space Engineers Script\minifiedtool\IngameScriptMergeTool\IngameScriptMergeTool.exe" -s "F:\Space Engineers Script\My-Personal-Space-Engineer-Script\AGM\Minified_Tool\AGM_Minified.sln" -m -d "Autogrid Manager"
```

Always run the minifier after every source change. Never paste the raw .cs source into SE.

---

## AGM Core — v1.3+

### Source file
`F:\Space Engineers Script\My-Personal-Space-Engineer-Script\AGM\Scripts\AGM.cs`

### Current minified size
~81KB (limit is 100KB)

### LCD Tag
`[AGM-S]` in block name. AGM scans for this to find display screens.

IMPORTANT: Blocks with `[AGM-LIGHT]` in their Custom Data are excluded from _screens entirely.

### Alert Light / Corner LCD Tag
`[AGM-LIGHT]` in Custom Data of a light block or corner LCD.

```ini
[AGM-LIGHT]
watch=Battery
```

Valid watch= values: Battery, Cargo, Hydrogen, Oxygen, Uranium, Production, Charging, Power OK, or blank for overall alert.

Corner LCDs show topic name large (e.g. BATTERY) with status below and coloured border.
Drawn every tick via DrawAlertLcds() — never flickers.

### Dashboard Commands (in LCD Custom Data)

| Command | Page |
|---|---|
| CoreDashboard | System overview — full LCD only |
| AlertDashboard | Alert status |
| WarningDashboard | Warning details |
| PowerDashboard page=1 | Power overview |
| ReactorRefuel | Reactor uranium status |
| BatteryControl | Battery/reactor automation |
| LogisticsDashboard | Sorting status |
| ProductionDashboard page=1 | Production overview |
| ProductionDetails | Assembler/refinery jobs |
| ProductionWarnings | Bottleneck warnings |
| InventoryStock page=1 | All items |
| OreStock page=1 | Ores |
| IngotStock page=1 | Ingots |
| ComponentStock page=1 | Components |
| AmmoStock page=1 | Ammo |
| ToolStock page=1 | Tools |
| BottleStock page=1 | Bottles |
| Autocrafting page=1 | Autocrafting quotas |
| FuelLifeSupport | H2/O2 and life support |
| LifeSupport | Life support only |

### Cargo Container Tags (block name)

| Tag | Item type |
|---|---|
| {Ore 1} | Ores |
| {Ingot 1} | Ingots |
| {Component 1} | Components |
| {Ammo 1} | Ammo |
| {Tool 1} | Tools |
| {Bottle 1} | Bottles |

Lower number fills first. AGM auto-assigns if auto_assign=true.

### Key PB Custom Data

```ini
[Production]
monitor_only=false   <- MUST be false for autocrafting to queue items
autocraft_components=true
```

### Assembler Routing

- Basic Assemblers detected by SubtypeId containing "Basic" (BasicAssembler)
- Basic components routed to _basicAssemblers first
- Advanced components routed to _advAssemblers first
- QueueToAllMasters() queues to every idle non-coop master assembler
- Coop assemblers skipped in queuing (CooperativeMode=true)
- Assembler Details shows [M] for masters, COOP status for coop assemblers

### Responsive Layouts

- DrawPbScreen() — panel.Height < 200f = compact small grid PB layout
- DrawCoreDash() — panel.Width > panel.Height * 2.5f = wide LCD horizontal layout
- DrawAlertCornerLcd() — scales fonts to fit wide vs square

### Colour Theme

```
Background   new Color(1,8,13)
Panel        new Color(2,18,28)
Panel2/Rows  new Color(3,58,78)
Accent       new Color(38,239,255)
Accent2      new Color(112,247,255)
Text         new Color(126,246,255)
Dim          new Color(44,177,195)
OK           new Color(97,255,214)
Warning      new Color(255,202,34)
Error        new Color(255,79,66)
ProgBg       new Color(18,48,32)
ProgFill     new Color(255,204,36)
```

### Key Architecture Rules

- Edit Scripts/AGM.cs only — never the minified Script.cs
- static fields forbidden — only static methods allowed
- HasDashboardCmd() returns false for any block with [AGM-LIGHT] in Custom Data
- DrawAlertLcds() runs every tick — alert LCDs never flicker
- ScanBlocks() excludes [AGM-LIGHT] blocks from _screens
- monitor_only=false required in [Production] for autocrafting to work

### What Is Next

- AGM Core v1.4 — PB scan for AGM family status on CoreDashboard
- AGM Defence Grid — future separate PB (postponed)

### Common Mistakes To Avoid

- Never paste raw AGM.cs into SE — always use minified Script.cs
- Never add [AGM-LIGHT] blocks to _screens — causes flicker
- Never set ContentType every tick on corner LCDs — set once only
- Never use static fields
- Never use C# 7 — no inline out var, no string interpolation, no tuples
- monitor_only=false required in [Production] for autocrafting to work
