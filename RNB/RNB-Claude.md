# RNB-Claude.md — AI Agent Instructions

Guidelines for AI coding agents working on `RNB.cs`.  
Script: **RNB — Rev's Nanobot Bridge v1.0.0**  
Author: Simba 'Davy' Jones — 505th Expeditionary Force

---

## What This Script Is

A Space Engineers Programmable Block script pasted directly into the in-game editor and compiled by the game's Roslyn sandbox. No build system, no external tooling.

The game wraps all code in `public sealed class Program : MyGridProgram { }`. Never include `namespace`, `partial class`, or `using` directives.

---

## Always Read First

Read `SE-SCRIPTING-RULES.md` before any edit — authoritative on whitelisted APIs, C# 6 constraints, and known pitfalls.

---

## Architecture

```
Program()                Constructor — grabs PB surface, calls Initialise(), draws boot
Main(arg, src)           Entry point every ~167ms (Update10)
  └─ Boot stage          Runs DrawBootScreen() for BOOT_DURATION seconds, then sets Ready
  └─ Normal stage        State update → RefreshProjectors → CheckAssemblerQueues
                         → UpdateAlertLights → DrawDisplays → DrawPBScreen

Initialise()             Full block scan by tag, runs every REINIT_INTERVAL seconds
TagToPage()              Maps block name to PageKind via LCD tag constants
RefreshProjectors()      Updates ProjectorInfo each tick from TotalBlocks/RemainingBlocks
CheckAssemblerQueues()   Routes missing components to correct assembler pool
  └─ IsBasicComponent()  Returns true if subtype is in BASIC_COMPONENTS[]
EnsureAssemblyMode()     Fixes assemblers stuck in Disassembly mode
UpdateAlertLights()      Sets colour/blink on [RNBAlert] lights
DrawDisplays()           Calls DrawPage() for each DisplayEntry
DrawPage()               Header + footer frame, delegates to page method
DrawBootScreen()         Animated boot sequence on PB surface 0
DrawPBScreen()           Compact live status on PB surface 0 after boot
DrawStatusPage()         System overview page
DrawMissingPage()        Missing components list
DrawListPage()           Shared weld/grind queue list (title param differentiates)
DrawWeldersPage()        Per-welder status detail
DrawAssemblersPage()     Per-assembler status detail
DrawProjectorsPage()     Per-projector build progress
```

---

## Block Tags

| Constant | Value | Purpose |
|---|---|---|
| `TAG_ASSEMBLER` | `[RNBAssembler]` | Advanced assembler |
| `TAG_BASIC_ASSEMBLER` | `[RNBBasicAssembler]` | Basic assembler — explicit override |
| `TAG_ALERT` | `[RNBAlert]` | Light block |
| `TAG_PROJECTOR` | `[RNBProjector]` | Projector |
| `TAG_LCD_STATUS` | `[RNBStatus]` | Status page |
| `TAG_LCD_MISSING` | `[RNBMissing]` | Missing components page |
| `TAG_LCD_WELD` | `[RNBWeld]` | Weld queue page |
| `TAG_LCD_GRIND` | `[RNBGrind]` | Grind queue page |
| `TAG_LCD_WELDERS` | `[RNBWelders]` | Welder detail page |
| `TAG_LCD_ASSEMBLERS` | `[RNBAssemblers]` | Assembler detail page |
| `TAG_LCD_PROJECTORS` | `[RNBProjectors]` | Projector page |

`TagToPage()` checks longest/most-specific tags first to prevent substring false-matches — `[RNBAssemblers]` before `[RNBAssembler]`, `[RNBProjectors]` before `[RNBProjector]`, `[RNBWelders]` before `[RNBWeld]`.

`[RNBBasicAssembler]` takes priority over `[RNBAssembler]` — `hasBasicTag` is checked first, and `hasAdvancedTag = !hasBasicTag && n.Contains(TAG_ASSEMBLER)` prevents double-registration.

BaR welders are auto-detected — no tag. All welders on the same construct responding to `GetValueBool("BuildAndRepair.ScriptControlled")` are added to `_welders.Welders`.

---

## Key Fields

| Field | Type | Role |
|---|---|---|
| `_welders` | `BaRHandler` | Wraps all auto-detected BaR welders |
| `_assemblerIds` | `List<long>` | All tagged assembler EntityIds |
| `_basicAssemblerIds` | `List<long>` | Basic assembler EntityIds only |
| `_advancedAssemblerIds` | `List<long>` | Advanced assembler EntityIds only |
| `_displays` | `List<DisplayEntry>` | All registered LCD surfaces |
| `_alertLights` | `List<IMyLightingBlock>` | All `[RNBAlert]` lights |
| `_projectors` | `List<ProjectorInfo>` | All `[RNBProjector]` projectors |
| `_pbSurface` | `IMyTextSurface` | PB's own surface 0 — boot + live screen |
| `_bootStage` | `BootStage` | Booting / Ready enum |
| `_bootElapsed` | `double` | Seconds since script start for boot timer |
| `_bootProgress` | `float` | 0–1 boot bar fill |
| `_state` | `RNBState` | Working / Idle / Offline / Missing |
| `_isOffline` | `bool` | True when welders disabled |
| `_forcedOffline` | `bool` | Set by `offline` arg; only cleared by `online` |
| `_weldPeak` | `int` | Peak weld queue count for progress bar |
| `_weldPrev` | `int` | Previous tick queue count — detects new job |
| `_elapsed` | `double` | Accumulated seconds since script start |
| `_lastActivityTime` | `double` | `_elapsed` at last tick with any BaR targets |

---

## Assembler Routing Logic

```
CheckAssemblerQueues()
  for each missing component:
    basicCanMake = IsBasicComponent(subtype)
    if basicCanMake && _basicAssemblerIds.Count > 0:
      targets = _basicAssemblerIds
    else if _advancedAssemblerIds.Count > 0:
      targets = _advancedAssemblerIds
    else:
      targets = _assemblerIds  // fallback
    EnsureQueued(targets, componentId, amount)
```

`BASIC_COMPONENTS[]` — vanilla subtypes a basic assembler can produce:
`SteelPlate, InteriorPlate, Construction, SmallTube, LargeTube, Motor, Display, BulletproofGlass, Girder`

---

## BaR Mod API Properties

All via `IMyShipWelder.GetValue<T>(string)` — always wrap in try/catch.

| Property | Type | Notes |
|---|---|---|
| `BuildAndRepair.ScriptControlled` | bool | Detection probe |
| `BuildAndRepair.CurrentTarget` | IMySlimBlock | Block being welded |
| `BuildAndRepair.CurrentGrindTarget` | IMySlimBlock | Block being ground |
| `BuildAndRepair.PossibleTargets` | List\<IMySlimBlock\> | Weldable blocks in range |
| `BuildAndRepair.PossibleGrindTargets` | List\<IMySlimBlock\> | Grindable blocks in range |
| `BuildAndRepair.PossibleCollectTargets` | List\<IMyEntity\> | Floating items in range |
| `BuildAndRepair.MissingComponents` | Dictionary\<MyDefinitionId, int\> | Missing amounts |
| `BuildAndRepair.ProductionBlock.EnsureQueued` | Func\<IEnumerable\<long\>, MyDefinitionId, int, int\> | Queue into assembler |

---

## Whitelisted API Facts

### Projector
- `TotalBlocks` ✅ `RemainingBlocks` ✅ `RemainingArmorBlocks` ✅
- `BuildProgress` ❌ `IsProjecting` ❌

### Assembler
- `Mode` ✅ `CooperativeMode` ✅ `Repeating` ✅ `OutputInventory` ✅
- `MyResourceSinkComponent` ❌ `MyResourceDistributorComponent` ❌

### Power
- `IMyBatteryBlock` ✅ — `CurrentStoredPower`, `MaxStoredPower`, `ChargeMode`
- Power draw via `ResourceSink` ❌ — not whitelisted

---

## GetBlocksOfType Rule

Never use a predicate — returns `MemorySafeList<T>` which cannot assign to `List<T>`. Filter in the loop:

```csharp
// CORRECT
GridTerminalSystem.GetBlocksOfType<IMyShipWelder>(_wBuf);
for (int i = 0; i < _wBuf.Count; i++)
{
    if (!_wBuf[i].IsSameConstructAs(Me)) continue;
    // work
}

// WRONG — compile error
GridTerminalSystem.GetBlocksOfType<IMyShipWelder>(_wBuf, b => b.IsSameConstructAs(Me));
```

---

## CustomName Rule

`IMyCubeBlock` does not have `CustomName`. Always cast to `IMyTerminalBlock` first:

```csharp
var tb = block as IMyTerminalBlock;
string name = tb != null ? tb.CustomName : "fallback";
```

`_tBuf` is `List<IMyTerminalBlock>` — safe to access `.CustomName` directly there.

---

## Drawing Rules

- `PrepSurface()` sets `ContentType.SCRIPT` + `ScriptBackgroundColor` — call on every new surface.
- Fill background first with a full-size `SquareSimple` rect.
- Use `using (var frame = s.DrawFrame()) { }` — Dispose called automatically.
- `MySprite` field for both texture name and text is `Data` — **`Id` does not exist**.
- All drawing goes through `DrawRect()`, `DrawText()`, `DrawRow()`, `DrawProgressBar()`.

---

## C# 6 Constraints

```csharp
$"hello {name}"         // ❌ no string interpolation → use "hello " + name
(string a, int b) Foo() // ❌ no tuple returns → use a class
out var x               // ❌ no out var → use explicit type
static int _field       // ❌ no static fields → memory leak in SE sandbox
```

---

## Adding a New Page

1. Add `TAG_LCD_MYPAGE = "[RNBMyPage]"` constant
2. Add `MyPage` to `enum PageKind`
3. Add to `TagToPage()` — before any tag it could substring-match
4. Add `case PageKind.MyPage:` in `DrawPage()` switch
5. Add `case PageKind.MyPage: return "MYPAGE";` in `PageLabel()`
6. Implement `DrawMyPagePage(MySpriteDrawFrame frame, float ox, float top, float W, float H)`
7. Update `RNB.md`

---

## File Map

```
RNB.cs          Paste into Programmable Block
RNB.md          User-facing setup and reference
RNB-Claude.md   This file — AI agent instructions
SE-SCRIPTING-RULES.md   Authoritative SE PB scripting rules
```
