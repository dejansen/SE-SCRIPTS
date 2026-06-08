# RNB-Claude.md — AI Agent Instructions

Guidelines for AI coding agents working on `RNB.cs`.
Script: **RNB — Rev NanoBot Manager v1.0.0**
Author: RevGamer

---

## What This Script Is

A Space Engineers Programmable Block script pasted directly into the in-game editor and compiled by the game's Roslyn sandbox. No build system, no external tooling.

The game wraps all code in `public sealed class Program : MyGridProgram { }`. Never include `namespace`, `partial class`, or `using` directives.

---

## Architecture

```
Program()                Constructor — grabs PB surface, calls Initialise(), draws boot
Main(unused, src)        Entry point every ~167ms (Update10); no toolbar input used
  └─ Boot stage          Runs DrawBootScreen() for configured seconds, then sets Ready
  └─ Normal stage        State update → RefreshProjectors → CheckAssemblerQueues
                         → UpdateAlertLights → DrawDisplays → DrawCornerLcds → DrawPBScreen

Initialise()             Loads PB config, scans Custom Data roles/pages and name-tag fallbacks
TagToPage()              Maps block to PageKind via Custom Data Page= or LCD tag constants
RefreshProjectors()      Updates ProjectorInfo each tick
RefreshBaRData()         Reads BaR mod state once per tick
CheckAssemblerQueues()   Routes cached missing components to correct assembler pool
UpdateAlertLights()      Sets colour/blink on Role=Alert lights
DrawDisplays()           Calls DrawPageClean() for each DisplayEntry
DrawPageClean()          Header + footer frame, delegates to page draw method
DrawCornerLcds()         Large readable state-only display for command centres
DrawBootSurfaceClean()   Boot animation on PB surface and LCDs
DrawPBScreen()           Compact live status on PB surface 0 after boot
DrawStatusPage()         System overview
DrawMissingPage()        Missing components list
DrawListPage()           Shared weld/grind queue list
DrawWeldersPage()        Per-welder status detail
DrawAssemblersPage()     Per-assembler status detail
DrawProjectorsPage()     Per-projector build progress
```

---

## Custom Data And Name Tags

All block configuration is via Custom Data `[RNB]` section. Name tags are a fallback only.

```ini
[RNB]
Role=Assembler
```

```ini
[RNB]
Page=Status
```

PB Custom Data:

```ini
[RNB]
BootSeconds=6
RescanSeconds=10
AssemblerQueueSeconds=0.5
AutoOfflineSeconds=600
```

### Valid Roles

| Role | Block type | Notes |
|---|---|---|
| `NanoBot` | BaR welder | Explicit BaR selection; falls back to auto-detect if none tagged |
| `Assembler` | Assembler | Advanced assembler pool |
| `BasicAssembler` | Assembler | Basic component pool only |
| `Alert` | Light | State colour + blink |
| `Corner` | LCD | Large state-only display — see Corner LCD section |
| `Projector` | Projector | Build progress tracking |

### Valid Pages

`Status` `Missing` `Weld` `Grind` `Welders` `Assemblers` `Projectors`

### Name Tag Constants

| Constant | Value |
|---|---|
| `TAG_ASSEMBLER` | `[RNBAssembler]` |
| `TAG_BASIC_ASSEMBLER` | `[RNBBasicAssembler]` |
| `TAG_NANOBOT` | `[NanoBot]` |
| `TAG_ALERT` | `[RNBAlert]` |
| `TAG_CORNER_LCD` | `[RNBCorner]` |
| `TAG_PROJECTOR` | `[RNBProjector]` |
| `TAG_LCD_STATUS` | `[RNBStatus]` |
| `TAG_LCD_MISSING` | `[RNBMissing]` |
| `TAG_LCD_WELD` | `[RNBWeld]` |
| `TAG_LCD_GRIND` | `[RNBGrind]` |
| `TAG_LCD_WELDERS` | `[RNBWelders]` |
| `TAG_LCD_ASSEMBLERS` | `[RNBAssemblers]` |
| `TAG_LCD_PROJECTORS` | `[RNBProjectors]` |

`TagToPage()` checks longest tags first — `[RNBAssemblers]` before `[RNBAssembler]` etc.

---

## Corner LCD

`DrawCornerLcds()` renders a large at-a-glance state display for wide/small LCDs in a command centre. It does **not** render any page content — text only:

- Large centred state label (scale 1.6f): `WORKING` / `MISSING` / `OFFLINE` / `IDLE`
- Sub-line below: welder count, missing part count, or offline reason
- Coloured 3px border matching the alert light state colour
- Small `RNB` label top-left, elapsed idle timer top-right

**State to colour/subline mapping:**

| State | Border | Sub-line |
|---|---|---|
| Working | COL_GREEN | Welders: X/Y |
| Missing | COL_RED | N part types needed |
| Offline | COL_AMBER | Idle timeout - welders off |
| Idle | COL_DIM | Welders: X/Y |

**Do not pass a `PageKind` to `DrawCornerLcds()`.** It determines all display content from `_state` directly. No page draws, no list draws, no progress bars.

---

## Key Fields

| Field | Type | Role |
|---|---|---|
| `_welders` | `BaRHandler` | All detected BaR welders |
| `_assemblerIds` | `List<long>` | All tagged assembler EntityIds |
| `_assemblers` | `List<IMyAssembler>` | Tagged assembler references |
| `_basicAssemblerIds` | `List<long>` | Basic assembler EntityIds |
| `_advancedAssemblerIds` | `List<long>` | Advanced assembler EntityIds |
| `_displays` | `List<DisplayEntry>` | All registered page LCD surfaces |
| `_cornerLcds` | `List<IMyTextSurface>` | All Role=Corner LCD surfaces |
| `_alertLights` | `List<IMyLightingBlock>` | All Role=Alert lights |
| `_projectors` | `List<ProjectorInfo>` | All Role=Projector projectors |
| `_pbSurface` | `IMyTextSurface` | PB surface 0 |
| `_state` | `RNBState` | Working / Idle / Offline / Missing |
| `_weldTargets` | `List<IMySlimBlock>` | Cached BaR weld queue |
| `_grindTargets` | `List<IMySlimBlock>` | Cached BaR grind queue |
| `_missing` | `Dictionary<MyDefinitionId, int>` | Cached missing components |
| `_weldPeak` | `int` | Peak queue count for progress bar latch |
| `_elapsed` | `double` | Accumulated seconds since start |
| `_lastActivityTime` | `double` | `_elapsed` at last tick with BaR activity |

---

## Assembler Routing Logic

```
for each missing component:
  basicCanMake = IsBasicComponent(subtype)
  if basicCanMake && _basicAssemblerIds.Count > 0 → _basicAssemblerIds
  else if _advancedAssemblerIds.Count > 0         → _advancedAssemblerIds
  else                                             → _assemblerIds (fallback)
  EnsureQueued(targets, componentId, amount)
```

Basic components: `SteelPlate InteriorPlate Construction SmallTube LargeTube Motor Display BulletproofGlass Girder`

---

## BaR Mod API Properties

All via `IMyShipWelder.GetValue<T>(string)` — always wrap in try/catch.

| Property | Type |
|---|---|
| `BuildAndRepair.ScriptControlled` | bool |
| `BuildAndRepair.CurrentTarget` | IMySlimBlock |
| `BuildAndRepair.CurrentGrindTarget` | IMySlimBlock |
| `BuildAndRepair.PossibleTargets` | List\<IMySlimBlock\> |
| `BuildAndRepair.PossibleGrindTargets` | List\<IMySlimBlock\> |
| `BuildAndRepair.PossibleCollectTargets` | List\<IMyEntity\> |
| `BuildAndRepair.MissingComponents` | Dictionary\<MyDefinitionId, int\> |
| `BuildAndRepair.ProductionBlock.EnsureQueued` | Func\<IEnumerable\<long\>, MyDefinitionId, int, int\> |

---

## SE Constraints

```csharp
$"hello {name}"         // no string interpolation → "hello " + name
(string a, int b) Foo() // no tuples → use a class
out var x               // no out var → explicit type
static int _field       // no static fields → memory leak
```

`GetBlocksOfType` — never use lambda predicate, filter in loop instead.
`CustomName` — only on `IMyTerminalBlock`, not `IMyCubeBlock`.
`MySprite` text field is `Data`, not `Id`.

---

## Adding a New Role or Page

**New Role:**
1. Add `TAG_MYROLE = "[RNBMyRole]"` constant
2. Add scan block in `Initialise()` using `HasRnbRole(tb, TAG_MYROLE, "MyRole")`
3. Add list field and clear it in `Initialise()`

**New Page:**
1. Add `TAG_LCD_MYPAGE = "[RNBMyPage]"` constant
2. Add `MyPage` to `enum PageKind`
3. Add to `TagToPage()` — before any tag it could substring-match
4. Add `case PageKind.MyPage:` in `DrawPageClean()` switch
5. Add `case PageKind.MyPage: return "MYPAGE";` in `PageLabel()`
6. Implement `DrawMyPagePage(MySpriteDrawFrame, float ox, float top, float W, float H)`
7. Update `RNB.md` and `RNB-Claude.md`

---

## File Map

```
RNB.cs          Paste into Programmable Block
RNB.md          User-facing setup and reference
RNB-Claude.md   This file — AI agent instructions
```
