# RNB-Claude.md — AI Agent Instructions

Guidelines for AI coding agents working on `RNB.cs`.
Script: **RNB — Rev NanoBot Manager v2.0.0**
Author: RevGamer

---

## What This Script Is

A Space Engineers Programmable Block script for the SKO Nanobot Build and Repair System (Maintained).

`DEVELOPMENT/RNB.cs` is the readable source of truth. It is pasted into the in-game editor and compiled by the game's Roslyn sandbox. The game wraps this source in `public sealed class Program : MyGridProgram { }`, so never add a namespace, `partial class`, wrapper class, or `using` directives to that file.

`DEVELOPMENT/RNB.Project.cs` is the generated Visual Studio wrapper. The project and solution compile only this wrapper. Do not make independent feature edits in `RNB.Project.cs`; regenerate it from `RNB.cs` after changing the source of truth.

---

## Architecture

```
Program()                Constructor — grabs PB surface, calls Initialise(), draws boot
Main(unused, src)        Entry point every ~167ms (Update10); no toolbar input used
  └─ Boot stage          Runs DrawBootScreen() for configured seconds, then sets Ready
  └─ Normal stage        RefreshBaRData → RefreshProjectors → manual-enable guard
                         → projector/work wake → state and idle timer update
                         → CheckAssemblerQueues
                         → UpdateAlertLights → DrawDisplays → DrawCornerLcds → DrawPBScreen

Initialise()             Loads PB config, scans Custom Data roles/pages and name-tag fallbacks
TagToPage()              Maps block to PageKind via Custom Data Page= or LCD tag constants
RefreshProjectors()      Updates ProjectorInfo each tick
ProjectorActive()        Strict enabled/functional/working/projecting/incomplete test
RefreshBaRData()         Reads BaR mod state once per tick
CheckAssemblerQueues()   Routes cached missing components to correct assembler pool
UpdateAlertLights()      Sets colour/blink on Role=Alert lights
DrawDisplays()           Calls DrawPageClean() for each DisplayEntry
DrawPageClean()          Header + footer frame, delegates to page draw method
DrawCornerLcds()         Aspect-aware banner/centred state display
DrawBootSurfaceClean()   Boot animation on PB surface and LCDs
DrawPBScreen()           Four-line RNB identity screen on PB surface 0
DrawStatusPage()         Two-column system/build dashboard and alert line
DrawMissingPage()        Missing components list
DrawListPage()           Shared weld/grind queue list
DrawWeldersPage()        Per-welder status detail
DrawAssemblersPage()     Per-assembler status detail
DrawProjectorsPage()     Projector control/status rows and progress
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
WakeOnProjector=true
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

`DrawCornerLcds()` renders a state-only display. It does not receive a `PageKind` and must not call page draw methods.

Corner registration accepts every surface exposed by a tagged `IMyTextSurfaceProvider`. `Role=Corner` wins over page registration for the same surface.

The renderer chooses layout from the surface aspect ratio:

- Ratio `>= 2.2`: horizontal banner with header/timer above and state/context side-by-side.
- Ratio `< 2.2`: centred state and context layout.

Text uses `DrawTextFit()` and `MeasureStringInPixels`; do not restore fixed oversized scales. Damaged BaRs override normal `_state` and show a red `DAMAGED` display.

**State to colour/subline mapping:**

| State | Border | Sub-line |
|---|---|---|
| Damaged | COL_RED | N BaR need repair |
| Working | COL_GREEN | X/Y BaRs working |
| Missing | COL_RED | N part types needed |
| Offline | COL_AMBER | Idle timeout - welders off |
| Idle | COL_ACCENT | X/Y BaRs online |

**Do not pass a `PageKind` to `DrawCornerLcds()`.** It determines all display content from `_state` directly. No page draws, no list draws, no progress bars.

---

## LCD Registration And Text Fitting

- `AddCornerSurfaces()` and `AddDisplaySurfaces()` support direct `IMyTextSurface` blocks and every surface from `IMyTextSurfaceProvider` blocks.
- Prevent duplicate surface registration.
- Corner registration happens before page registration; a Corner surface must not also enter `_displays`.
- Use `Viewport()` to account for texture/surface offsets.
- Use `DrawTextFit()` for names, labels, state text, and variable-length values that can overflow.
- `_drawSurface` must be set before measured page/Corner/PB drawing and cleared afterward.
- `DrawPBScreen()` intentionally shows only four centred lines: `RNB`, `Rev Nanobot`, `Manager`, and the current version.

Detail-page state colours:

| State | Colour |
|---|---|
| Working | `COL_GREEN` |
| Idle | `COL_ACCENT` |
| Offline | `COL_AMBER` |
| Damaged | `COL_RED` plus red row background |

Assembler details are sorted by clean block name using `SortAssemblersByName()`.

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
| `_wakeOnProjector` | `bool` | Allows valid projector/BaR work to wake disabled BaRs |
| `_previousEnabledCount` | `int` | Detects a 0 to 1+ manual-enable transition |
| `_nextWake` | `double` | Five-second wake/Echo cooldown |
| `_drawSurface` | `IMyTextSurface` | Active surface used for measured text fitting |
| `_measureText` | `StringBuilder` | Reused buffer for `MeasureStringInPixels` |

---

## Idle, Manual Enable, And Wake

- Any BaR weld, grind, collect, or valid projector activity refreshes `_lastActivityTime`.
- When enabled BaRs transition from zero to one or more, treat it as deliberate manual activation and restart the full `AutoOfflineSeconds` period.
- Do not auto-disable a manually enabled BaR using an old idle timestamp.
- `BringOnline()` clears `_isOffline`, resets activity time, enables all managed BaRs, and starts the wake cooldown.
- Wake detection may use valid projector activity or cached BaR work queues.

## Projector State Model

`ProjectorInfo` caches `Enabled`, `Functional`, `Working`, `Projecting`, `Total`, `Remaining`, and `Progress`.

`ProjectorActive(info)` must require all of the following:

```text
Enabled
Functional
Working
Projecting
Total > 0
Remaining > 0
```

An OFF, unpowered, damaged, complete, or unloaded projector must not keep or wake BaRs online merely because it retains a blueprint with remaining blocks.

`ProjectorState()` values:

| State | Rule |
|---|---|
| `MISSING` | Block reference is null |
| `DAMAGED` | Not functional |
| `OFFLINE` | Disabled |
| `NO POWER` | Enabled but not working |
| `NO BLUEPRINT` | Not projecting or total is zero |
| `COMPLETE` | No remaining blocks |
| `BUILDING` | Strict active test passes |

Use `ProjectorActive()` everywhere active projector counts affect wake, idle, status, or alerts. Do not duplicate the old `Total > 0 && Remaining > 0` test.

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

## Development And Release Workflow

1. Edit `../DEVELOPMENT/RNB.cs` only.
2. Regenerate `../DEVELOPMENT/RNB.Project.cs` by wrapping the full source with the required `using` directives, namespace, and `Program : MyGridProgram` class.
3. Build `../DEVELOPMENT/RNB.sln`.
4. Compile-test the readable source using a temporary PB-style wrapper.
5. Regenerate `../READY TO USE/RNB-v2.0.0-PasteReady.cs` from `RNB.cs`:
   - remove blank lines;
   - remove comment-only lines;
   - remove indentation;
   - preserve statement line breaks and string contents.
6. Compile-test the exact minified ready-to-use file.
7. Update `../READY TO USE/GUIDE.md`, `RNB.md`, and this file for behavior or setup changes.
8. Remove generated `DEVELOPMENT/bin` and `DEVELOPMENT/obj` after verification. `.gitignore` prevents them from being tracked.

Do not manually maintain divergent behavior in readable, wrapped, and minified copies. The readable source is authoritative.

---

## File Map

```
READY TO USE/
  GUIDE.md                    Complete installation guide
  RNB-v2.0.0-PasteReady.cs   Minified release script
DEVELOPMENT/
  RNB.cs                      Full readable PB source (source of truth)
  RNB.Project.cs              Generated Visual Studio wrapper
  RNB.csproj                  Development project
  RNB.sln                     Visual Studio solution
DOCUMENTATION/
  RNB.md                      User-facing reference
  RNB-Claude.md               This file — AI agent instructions
  RNB.png                     Project image
```
