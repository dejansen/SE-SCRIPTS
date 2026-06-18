# Space Engineers Programmable Block — Scripting Rules

Reference compiled from official sources and verified against real production scripts:
- https://spaceengineers.wiki.gg/wiki/Scripting/Whitelist_Overview
- https://spaceengineers.wiki.gg/wiki/Scripting/The_Anatomy_of_a_Script
- https://spaceengineers.wiki.gg/wiki/Scripting/Do%27s_and_Don%27ts
- https://malforge.github.io/spaceengineers/pbapi (PB API reference)
- https://github.com/dorimanx/Isys-Inventory-Manager/blob/master/Script.cs (real production script)

Last verified: 2026-03-06

---

## 1. What you paste into the in-game editor

The game wraps your code in this structure automatically:

```csharp
public sealed class Program : MyGridProgram
{
    // YOUR CODE IS PASTED HERE
}
```

**You must NOT include:**
- `namespace IngameScript { ... }` — the game does not use this
- `partial class Program : MyGridProgram { ... }` — the game adds this
- Any `using` statements — the game injects a fixed list (see section 2)

If you paste a wrapper, the compiler sees a class nested inside a class, which is a compile error.

The default empty programmable block shows exactly this — class body members only:

```csharp
public Program()
{
}

public void Save()
{
}

public void Main(string argument, UpdateType updateSource)
{
}
```

---

## 2. Using statements — fixed, injected by the game

You cannot add your own `using` directives. The game injects exactly these:

```csharp
using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using VRage.Scripting.MemorySafeTypes;
using VRage;
using VRageMath;
using VRage.Game;
using Sandbox.ModAPI.Interfaces;
using Sandbox.ModAPI.Ingame;
using Sandbox.Game.EntityComponents;
using VRage.Game.Components;
using VRage.Collections;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using System.Collections.Immutable;
using SpaceEngineers.Game.ModAPI.Ingame;
using VRage.Game.GUI.TextPanel;
```

Everything in those namespaces is available by short name. Types outside this list must be referenced by fully qualified name (if whitelisted).

Examples of types that are **whitelisted but NOT injected** (must use full name):
- `System.Globalization.CultureInfo.InvariantCulture`
- `System.Globalization.NumberStyles.Float`
- `System.Text.RegularExpressions.Regex`

---

## 3. Whitelist — what .NET types are allowed

The game's Malicious Code Filter blocks most of the .NET framework. Only whitelisted types/members work.

### Allowed (selection of most useful):
| Type / Namespace | Notes |
|---|---|
| `bool`, `byte`, `char`, `int`, `float`, `double`, `string`, etc. | All primitive types, full access |
| `System.Math.*` | Full |
| `System.Random.*` | Full |
| `System.Convert.*` | Full |
| `System.DateTime.*` | Full |
| `System.TimeSpan.*` | Full |
| `System.Text.*` | Full namespace (StringBuilder, etc.) |
| `System.Collections.*` | Full namespace |
| `System.Collections.Generic.*` | Full namespace (List, Dictionary, HashSet, etc.) |
| `System.Globalization.*` | Whitelisted but **NOT in injected usings** — must use full name: `System.Globalization.CultureInfo.InvariantCulture` |
| `System.StringComparison.*` | Full |
| `System.StringSplitOptions.*` | Full |
| `System.Linq.*` | Full — LINQ works and is used in production scripts |
| `System.Text.RegularExpressions.Regex` | Available via full qualified name |
| `System.IO.BinaryReader`, `BinaryWriter`, `Path` | Specific members only — NO file I/O |
| `VRageMath.*` | Full namespace (Vector3, Color, MatrixD, etc.) |
| `VRage.Game.ModAPI.Ingame.*` | Full namespace |
| `Sandbox.ModAPI.Ingame.*` | Full namespace |
| `VRage.MyFixedPoint.*` | Full |
| `VRage.Game.GUI.TextPanel.*` | Full namespace (TextAlignment, ContentType, etc.) |

### NOT allowed:
- `System.IO.File`, `FileStream`, `StreamReader`, `StreamWriter` — no file I/O
- `System.Net.*` — no networking
- `System.Threading.*` — no threads
- `System.Reflection.*` — almost none (only specific `Type`/`MemberInfo` members)
- Any type not in the whitelist — compile error

---

## 4. Script entry points

The game calls three methods by convention:

```csharp
// Called once when script is compiled or game loaded
public Program()
{
    // One-time initialisation. Set Runtime.UpdateFrequency here.
}

// Called when game is saved (optional)
public void Save()
{
    // Persist data to the Storage string if needed.
}

// Called on each run (timer, toolbar button, sensor, automatic update)
public void Main(string argument, UpdateType updateSource)
{
    // Main logic
}
```

Both `Main(string argument, UpdateType updateSource)` and `Main(string argument)` are valid. `Main()` with no arguments also works.

---

## 5. Limits

| Limit | Value |
|---|---|
| Script size | 100 000 characters max |
| Instructions per run | 50 000 code junctions (method calls, loops, conditions) |
| Execution time | Shared with game frame (~16 ms for everything) |

---

## 6. Performance guidelines (Do's and Don'ts)

### DO:
- Cache block references as fields; refresh them in `Program()` or periodically (e.g. every 100 ticks)
- Reuse `List<T>` fields: call `.Clear()` before each use rather than `new List<T>()`
- Use `Runtime.UpdateFrequency` to drive the script automatically instead of a timer block
- Use `b.IsSameConstructAs(Me)` in block filters to exclude subgrids
- Call interface members directly (e.g. `light.Enabled = true`) rather than `ApplyAction("OnOff")`

### DON'T:
- Use `static` fields or properties — memory leak risk in the SE sandbox; `static` methods are fine
- Call `GetBlocksOfType` on every `Main()` tick for large grids — it is expensive
- Use Terminal Properties/Actions (`GetProperty`, `ApplyAction`) when a typed interface member exists
- Assume a typed interface method works just because it compiles — some methods exist in the API but are unreliable or no-ops at runtime (see section 7)

### C# version gotchas:
- **Inline `out` variable declarations are C# 7 — not supported.** Declare variables before the call:
  ```csharp
  // WRONG (C# 7):
  if (!TryGet(out string value)) { }

  // CORRECT (C# 6):
  string value;
  if (!TryGet(out value)) { }
  ```

### Notes on allocation:
- Allocating new lists inside methods is common in real scripts and works fine
- The official advice to reuse lists is a performance optimisation for hot paths, not a hard rule
- `foreach` on `List<T>` does not cause boxing — it is safe and widely used
- LINQ (`.Where`, `.OrderBy`, `.ToList`, etc.) is fully whitelisted and used in production scripts; avoid it only in extremely tight loops

### Error handling:
- `throw new Exception("message")` is the correct way to halt the PB on an unrecoverable error — the game will display the message and stop execution
- Use `try/catch` around unsafe operations (parsing, block access that may have become null)
- For recoverable errors, `Echo("WARNING: ...")` the message and continue or skip the affected item

---

## 7. Key API facts (confirmed from official API docs)

### MyItemType
- **No `TryParse` method.** Use the constructor or static helpers:
  ```csharp
  // Direct constructor — typeId must be the full MyObjectBuilder_ string:
  var t = new MyItemType("MyObjectBuilder_Ingot", "Iron");

  // Static helpers (cleaner when the type is known):
  var t = MyItemType.MakeIngot("Iron");
  var t = MyItemType.MakeOre("Iron");
  var t = MyItemType.MakeComponent("SteelPlate");
  var t = MyItemType.MakeAmmo("NATO_5p56x45mm");
  var t = MyItemType.MakeTool("AngleGrinderItem");
  ```
- `MyItemType.Parse(string)` accepts `"TypeId/SubtypeId"` — throws on bad input; wrap in try/catch.
- Common full type ID strings:

| Short name | Full MyObjectBuilder_ string |
|---|---|
| Ingot | `MyObjectBuilder_Ingot` |
| Ore | `MyObjectBuilder_Ore` |
| Component | `MyObjectBuilder_Component` |
| AmmoMagazine | `MyObjectBuilder_AmmoMagazine` |
| Tool | `MyObjectBuilder_PhysicalGunObject` |
| GasContainer | `MyObjectBuilder_GasContainerObject` |
| OxygenContainer | `MyObjectBuilder_OxygenContainerObject` |

### MyDefinitionId
- Has `TryParse`: `MyDefinitionId.TryParse(string, out MyDefinitionId)` — returns bool, safe to use
- Used for blueprint IDs and block definitions

### IMyInventory
- `GetItemAmount(MyItemType)` — returns `MyFixedPoint` total for that item type. Simplest way to check stock.
- `GetItems(List<MyInventoryItem>, Func<MyInventoryItem,bool>)` — fills a list; reuse the list across calls
- `ContainItems(MyFixedPoint amount, MyItemType)` — quick boolean stock check
- `TransferItemTo(IMyInventory, ...)` — move items between inventories

### IMyLightingBlock — property ranges
| Property | Type | Range | Notes |
|---|---|---|---|
| `BlinkIntervalSeconds` | float | ≥ 0 | 0 = solid (no blink) |
| `BlinkLength` | float | **0 – 100** | Percentage of cycle light is ON. 50 = 50 %. Despite what the API docs suggest, the in-game range is 0–100, not 0–1. |
| `BlinkOffset` | float | 0.0 – 1.0 | Phase offset |
| `Intensity` | float | 0 – 10 | |
| `Radius` | float | 0 – 500 | |
| `Color` | Color | — | `VRageMath.Color` |
| `Enabled` | bool | — | Inherited from `IMyFunctionalBlock` |

### IMyTimerBlock
- Lives in `SpaceEngineers.Game.ModAPI.Ingame` — whitelisted and available
- **`Trigger()` is unreliable at runtime** — it compiles and casts succeed, but the timer may not fire
- **Use `ApplyAction("TriggerNow")` instead** — this is the confirmed-working call, equivalent to clicking "Trigger Now" in the terminal:
  ```csharp
  IMyTimerBlock timer = GridTerminalSystem.GetBlockWithName(name) as IMyTimerBlock;
  if (timer != null)
      timer.ApplyAction("TriggerNow");
  ```
- `StartCountdown()` starts the timer's countdown (same as the "Start" toolbar action)

### IMyBroadcastController
- Exists in the whitelist (`SpaceEngineers.Game.ModAPI.Ingame`); cast from `GridTerminalSystem.GetBlocksOfType<IMyBroadcastController>(...)`
- **Has no own scripting members.** The interface is entirely inherited from `IMyFunctionalBlock` / `IMyTerminalBlock`.
- Block types: `MyObjectBuilder_BroadcastController/LargeBlockBroadcastController` and `.../SmallBlockBroadcastController`
- Supports up to **8 messages**, zero-indexed as properties, one-indexed in actions:

| What | How | Notes |
|---|---|---|
| Read message text | `GetProperty("Message0").As<StringBuilder>().GetValue(bc)` | Index 0–7 |
| Write message text | `GetProperty("Message0").As<StringBuilder>().SetValue(bc, new StringBuilder("text"))` | Index 0–7 |
| Transmit a message | `bc.ApplyAction("Transmit Message 1")` | Index 1–8 (one-indexed!) |
| Transmit random | `bc.ApplyAction("TransmitRandomMessage")` | |
| Enable/disable | `bc.Enabled = true/false` | Inherited from `IMyFunctionalBlock` |

- **Property vs action index offset:** `Message0` is triggered by `"Transmit Message 1"` — properties are 0-based, actions are 1-based.
- **StringBuilder `SetValue` gotcha:** Do NOT use the property object (`GetProperty().As<StringBuilder>().SetValue()`). The sandbox type substitution causes a `MemorySafeStringBuilder` / `System.Text.StringBuilder` mismatch that can't be resolved cleanly. Use `IMyTerminalBlock.SetValue<StringBuilder>` directly with an explicit type parameter instead:
  ```csharp
  var sb = new StringBuilder();
  sb.Append("text");
  block.SetValue<StringBuilder>("Message0", sb);
  ```
  The explicit `<StringBuilder>` generic parameter bypasses the implicit type substitution. `MemorySafeStringBuilder` has an implicit cast to `StringBuilder` so this call succeeds. (Keen bug report: fixed in 1.207, but the direct `SetValue<StringBuilder>` pattern remains the safe approach.)
- **Practical pattern:** user pre-configures messages in the terminal; script calls `ApplyAction("Transmit Message N")` for the appropriate event. Alternatively, the script can write message content dynamically via `SetValue` before transmitting.

### IMyEventControllerBlock
- **Does NOT exist in the scripting whitelist.** There is no scriptable Event Controller interface.
- If you need to trigger automation on a state change, use a **Timer Block** (`IMyTimerBlock`) and call `ApplyAction("TriggerNow")` on it.

### IMyProjector
- `TotalBlocks` / `RemainingBlocks` / `RemainingArmorBlocks` / `BuildableBlocksCount` — block **counts** only
- `RemainingBlocksPerType` — `Dictionary<MyDefinitionBase, int>` of block definition → count still to weld. On an unbuilt, projecting blueprint this equals the full blueprint, so it is the BOM source. Iterate with `entry.Key.Id.SubtypeName` for the block subtype.
- **No API exposes a block's component recipe.** The projector does not expose the projected grid's slim blocks either, so `GetMissingComponents` is not reachable. To convert blocks → components (Steel Plate counts etc.) you must carry a **hardcoded block-subtype → component table** (the approach used by Projector2LCD / Projector2Assembler, and by `ProjectorBOM` in this repo).
- `LoadBlueprint(string)` / `LoadRandomBlueprint(string)` exist but cannot pick an arbitrary stored blueprint by path; offset/rotation are settable (`ProjectionOffset`, `ProjectionRotation`, then `UpdateOffsetAndRotation()`).
- Large- and small-grid blocks have **distinct SubtypeIds** (`LargeBlockArmorBlock` vs `SmallBlockArmorBlock`), so a subtype-keyed cost table distinguishes grid size automatically.

### IMyIntergridCommunicationSystem (IGC)

Accessed via `IGC` in any `MyGridProgram`. Handles all inter-grid messaging.

#### TransmissionDistance enum

| Value | Behaviour |
|---|---|
| `TransmissionDistance.AntennaRelay` | Default. Broadcast reaches all listeners within the sender's antenna range. |
| `TransmissionDistance.TransmissionDistanceMax` | Broadcast at maximum antenna range — functionally the same as `AntennaRelay` but ensures the sender's antenna is used at full power. **Does NOT bypass antenna range or become server-wide.** |
| `TransmissionDistance.CurrentConstruct` | Only listeners on the same construct (same PB grid) receive the broadcast. |
| `TransmissionDistance.ConnectedConstructs` | Only listeners on physically connected constructs (via connectors, etc.) receive it. |

#### Antenna range — what actually matters

- Broadcasts are physically range-limited. Both grids need to be within the sender's antenna range.
- The receiver's antenna **transmit** range is irrelevant for receiving — only the sender's range matters.
- The receiver's antenna must have **Enable Broadcast ON** or it will not receive any IGC messages.
- If no antenna is present, IGC still works within physics range (~150–300m, i.e. both grids must be loaded near each other).
- The IGC system automatically uses whatever connection is available (radio antenna, laser antenna, physical connector) — you do not need to specify which.

#### Message buffering — verified in-game behaviour

When a broadcast arrives and the receiving PB has a registered listener (`IGC.RegisterBroadcastListener`), the game queues the message in an internal buffer. Key findings:

- **PB disabled (grid still loaded and in range):** messages queue up in the IGC buffer. When the PB is re-enabled and registers its listener, it drains the entire queue at once — potentially receiving all retry transmissions simultaneously.
- **Grid unloaded (nobody nearby):** no active listener, no buffer. Messages sent during this period are dropped. The sender's retry queue (if `ack = true`) handles eventual delivery once the grid loads again.
- **Practical consequence for ACK retry scripts:** if the receiver PB was off while the grid was loaded, it may receive multiple copies of the same payload (one per retry) when it comes back online. Deduplicate by `gridName + text` before inserting into the message log, and always send the ACK even for duplicates so the sender stops retrying.

#### Unicast

- `IGC.SendUnicastMessage(long address, string tag, T data)` — sends directly to a single endpoint by IGC address.
- The sender's IGC address is `IGC.Me` (a `long`). Include it in broadcast payloads when ACK replies are needed.
- Unicast goes through the `IGC.UnicastListener`; check `UnicastListener.HasPendingMessage` and `UnicastListener.AcceptMessage()`.
- Unicast is also range-limited by the same antenna rules as broadcast.

#### Checking reachability

- `IGC.IsEndpointReachable(long address)` — returns true if the target is currently reachable. Use before unicast when delivery confirmation matters.

#### SetMessageCallback

- `listener.SetMessageCallback()` — causes `Main()` to be called with `UpdateType.IGC` when a message arrives. Pass an optional string argument to receive in `Main`'s argument parameter.
- Without this, messages only arrive when `Main` is called by another trigger (timer, Update100, etc.) and `HasPendingMessage` is polled manually.

### GridTerminalSystem
- `GetBlocksOfType<T>(List<T>, Func<T, bool>)` — fills existing list with optional filter
- `GetBlocksOfType<T>(List<T>)` — fills list with no filter (gets all blocks of type T)
- Passing `null` as the list and a predicate as the second argument is also valid for side-effect-only iteration
- `GetBlockWithName(string)` — exact, **case-sensitive** match on the block's full in-game name; returns `null` if not found

### Runtime
- `Runtime.UpdateFrequency` — set in `Program()` to drive automatic updates:
  ```csharp
  Runtime.UpdateFrequency = UpdateFrequency.Update10;   // every 10 ticks (~167ms)
  Runtime.UpdateFrequency = UpdateFrequency.Update100;  // every 100 ticks (~1.67s)
  Runtime.UpdateFrequency = UpdateFrequency.Update1;    // every tick (~16ms) — heavy
  // Combine with |= to add without removing existing flags
  Runtime.UpdateFrequency |= UpdateFrequency.Update100;
  ```

---

## 8. Custom Data parsing conventions

Custom Data is a free-form string. There is no built-in parser — scripts must parse it manually. Follow these conventions for robust, user-friendly config:

### INI-style sections
Use `[section]` headers to group related keys:
```
[items]
Ingot/Iron = 2000

[command]
action = light
light_name = {Iron Alert Light}
```

### Block name quoting with `{ }`
When a Custom Data value is a block name, wrap it in `{ }` braces. This convention (used by mMaster's LCD scripts and others) makes the name boundary unambiguous regardless of spaces, underscores, brackets, or other characters in the name:
```
light_name = {Iron Alert Light}
timer_low  = {Ammo_Alert_Timer}
```
Strip the braces before passing the value to `GetBlockWithName`. Plain values without braces should also be supported as a fallback.

### Comment lines
Treat lines starting with `;` or `#` as comments and skip them. Strip inline comments (`; text after semicolon`) from values **before** splitting on `=`, but be careful not to strip a `;` that appears inside a `{ }` block name.

### Diagnostic output for name lookup failures
When `GetBlockWithName` returns null, Echo the name **and its character length**. This immediately reveals trailing spaces or invisible characters from copy-paste:
```csharp
Echo("WARNING: block '" + name + "' (len=" + name.Length + ") not found");
```

### Section header collision with tags
If your script uses a tag like `[MONITOR]` in block names, and you parse the block's own Custom Data with `[section]` headers, guard against the tag being mistaken for a section. One reliable heuristic: only treat `[text]` as a section header if the inner text contains no spaces:
```csharp
string inner = trimmed.Substring(1, trimmed.Length - 2);
if (inner.IndexOf(' ') < 0)
    section = inner.ToLower();
```

---

## 9. Correct script skeleton (ready to paste)

```csharp
// --- Fields (allocated once) ---
private readonly List<IMyLightingBlock> _lightsBuffer = new List<IMyLightingBlock>();

// --- Constructor ---
public Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update100;
}

// --- Save (optional) ---
public void Save()
{
    // Storage = someString;
}

// --- Main ---
public void Main(string argument, UpdateType updateSource)
{
    _lightsBuffer.Clear();
    GridTerminalSystem.GetBlocksOfType(_lightsBuffer, b => b.IsSameConstructAs(Me));

    foreach (var light in _lightsBuffer)
    {
        // work with light
    }
}

// --- Helper methods ---
private void DoSomething()
{
}
```

**Key rules at a glance:**
- No `namespace`, no `class` wrapper, no `using` statements
- Field lists: reuse with `.Clear()`, especially in hot paths
- `static` fields are forbidden; `static` methods are fine
- `throw new Exception(...)` to halt on unrecoverable error
- `foreach`, LINQ, `string.ToLower()` — all fine
- `BlinkLength` is 0.0–1.0, **not** 0–100
- `MyItemType` has no `TryParse` — use `new MyItemType(typeId, subtypeId)` or the `Make*` helpers
- `IMyTimerBlock.Trigger()` is unreliable — use `ApplyAction("TriggerNow")` instead
- `IMyEventControllerBlock` does not exist in the whitelist — use Timer Blocks for automation triggers
- `GetBlockWithName` is case-sensitive and exact — log `name.Length` when debugging lookup failures
- Wrap block names in `{ }` in Custom Data to handle any character unambiguously
