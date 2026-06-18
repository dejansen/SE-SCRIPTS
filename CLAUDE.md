# AGENTS.md — Space Engineers Scripts

Guidelines for AI coding agents working in this repository.

---

## Working Directory Rule

**Always stay inside this repository directory.** Never access, read, or write files outside of it. Do not attempt to navigate to parent directories or other projects. If you are unsure of the working directory, read it from the environment context before doing anything.

The working directory is `F:\Space Engineers Script\SE-SCRIPTS\`


---

## Project Overview

This repository contains **Space Engineers Programmable Block scripts** written in C#. Scripts are pasted directly into the in-game Programmable Block editor and run inside the Space Engineers sandbox. There is no build system, no compiler invocation, no test runner, and no package manager — all compilation happens inside the game.

---

## No Build / Lint / Test Commands

There are no CLI build, lint, or test commands. The "build" is the game's in-game script compiler. To validate a script:

1. Open Space Engineers
2. Open a Programmable Block
3. Paste the script content
4. Click **Check Code** — the game reports compile errors in the editor

There is no way to run a single test from the command line. All testing is done in-game.

---

## File Structure

```
/
├── CLAUDE.md                      # This file
├── SE-SCRIPTING-RULES.md          # Verified scripting rules and API reference
├── SE_BlockTypeIds.md             # Reference: block TypeId/SubtypeId pairs
├── SE_ItemTypeIds.md              # Reference: item TypeId/SubtypeId pairs
├── README.md                      # Repository overview
├── Autogrid Manager/              # AGM -- ready-to-use release folder (v1.5 line)
│   ├── AGM.cs                     # Minified, paste-ready PB script (generated -- do not edit directly)
│   ├── AGM-Full-Guide.md          # Full setup guide: tagging cargo, machines, LCDs, alerts, docks
│   ├── CLAUDE.md                  # AGM-specific agent instructions (source path, minifier command, key rules)
│   └── README.md                  # Install steps; states dev source lives outside this repo (path currently stale)
├── InventoryMonitor/
│   ├── InventoryMonitor.cs        # Light-centric inventory monitor (v1)
│   └── InventoryMonitor.md
├── InventoryMonitor2/
│   ├── InventoryMonitor2.cs       # Container-centric monitor with multi-item and timer/light actions (v2)
│   ├── InventoryMonitor2.md
│   └── datapads/                  # In-game datapad text for setup, format, actions, troubleshooting, item types
├── RedAlert/
│   ├── RedAlert.cs                # Saves and restores a lighting group state; forces solid red on alert
│   └── RedAlert.md
├── R.O.S/                         # Rev Operating System -- base station + miner fleet
│   ├── ROS-Main.cs                # Base station: dock monitor, proximity scanner, fleet tracker
│   ├── ROS-Main.md
│   ├── ROS-FleetBroadcast.cs      # Miner companion: broadcasts telemetry over IGC
│   ├── ROS-FleetBroadcast.md
│   └── Claude_ROS.md              # R.O.S-specific agent instructions
├── Hermes/
│   ├── Hermes.cs                  # Intergrid messaging service: sender + receiver in one script
│   └── Hermes.md
├── MULE/
│   ├── MULE.cs                    # Autonomous planetary cargo drone: pickup -> dropoff round-trip
│   └── MULE.md
├── PTA/                           # Planetary Travel Assistant
│   ├── PTA.cs                     # Autopilot: horizon stabilizer, cruise/ascend/descend, teach-and-replay autodock
│   └── PTA.md
├── RNB/                           # Rev NanoBot Manager (SKO Nanobot mod integration)
│   ├── DEVELOPMENT/               # RNB.cs, RNB.Project.cs, .sln/.csproj -- formatted dev source
│   ├── DOCUMENTATION/             # RNB.md, RNB-Claude.md (agent instructions), RNB.png
│   └── READY TO USE/              # RNB-v2.0.0-PasteReady.cs, GUIDE.md
└── playground/                    # Proofs of concept, not production scripts
    ├── HorizonStabilizer.cs       # POC: gyro-based horizon leveling
    └── HorizonStabilizer.md
```

Each script gets:
- A `.cs` file containing the script code
- A `.md` file containing the concept documentation and in-game setup instructions

Some subfolders (AGM, R.O.S, RNB) also carry their own `CLAUDE.md` / `*-Claude.md` with project-specific agent instructions (source file paths, minifier commands, key rules). **Read the subfolder's own CLAUDE.md before working on that script** -- it takes precedence over this top-level file for anything specific to that project.

---

## Scripting Rules Reference

**Always read `SE-SCRIPTING-RULES.md` before writing or editing any script.** It contains verified rules on what to paste, which APIs exist, and known runtime pitfalls. When a script produces in-game errors, consult that file first.

---

## C# Code Style

### Target environment
- Scripts run inside Space Engineers' **Malicious Code Filter** sandbox
- Available: a restricted subset of .NET — no `System.IO`, no `System.Net`, no reflection
- Targeting roughly C# 6 feature set (the game uses an older Roslyn version)
- All code is **pasted directly as class body members** — the game provides the class wrapper automatically

### What to paste — class body only
The game wraps pasted code in `public sealed class Program : MyGridProgram { }` automatically.
**Do NOT include** `namespace`, `partial class`, or `using` statements — they cause compile errors.

Correct structure of what gets pasted:
```csharp
// constants and fields
private const string MY_TAG = "[MONITOR]";
private readonly List<IMyLightingBlock> lightsBuffer = new List<IMyLightingBlock>();

// constructor
public Program()
{
}

// entry point
public void Main(string argument, UpdateType updateSource)
{
}

// helper methods
private void DoSomething()
{
}
```

### Naming conventions
| Element | Convention | Example |
|---|---|---|
| Constants | `UPPER_SNAKE_CASE` | `MONITOR_TAG` |
| Private fields | `camelCase` | `allInventories` |
| Private methods | `PascalCase` | `ParseMonitoredLights()` |
| Local variables | `camelCase` | `totalAmount` |
| Parameters | `camelCase` | `shortType` |
| Structs / types | `PascalCase` | `MonitorConfig` |

### Formatting
- Indent with **4 spaces** (no tabs)
- Opening brace on the **same line** for methods and control flow
- Align related assignments with extra spaces for readability:
  ```csharp
  light.Color     = COLOR_OK;
  light.Intensity = LIGHT_INTENSITY;
  light.Radius    = LIGHT_RADIUS;
  ```
- Separate logical sections with comment banners:
  ```csharp
  // -------------------------------------------------------------------------
  // Section name
  // -------------------------------------------------------------------------
  ```

### Imports
- **Do NOT include any `using` statements** — the game injects a fixed set automatically
- All commonly needed namespaces (`Sandbox.ModAPI.Ingame`, `VRageMath`, `SpaceEngineers.Game.ModAPI.Ingame`, etc.) are already available
- Types not covered by the injected usings (e.g. `System.Globalization`) must be referenced by their fully qualified name

### Types
- Prefer `float` over `double` for SE quantities (the API uses `VRage.MyFixedPoint` which casts cleanly to `double` for accumulation, then back to `float`)
- Use `out` parameters for `TryParse`-style methods that return a parsed struct
- Use `default(T)` to initialise structs before an `out` assignment
- Use `var` only when the type is obvious from the right-hand side

### Error handling
- `throw new Exception("message")` is the correct way to halt the PB on an unrecoverable error — the game displays the message and stops execution cleanly
- Use `TryParse` / boolean return patterns for recoverable errors (bad config, missing blocks)
- On configuration errors, apply a visible error state to the affected block (e.g. solid red light) and write a warning via `Echo()`
- Always validate Custom Data before use; never assume it is well-formed

### Performance (SE scripting constraints)
- Avoid allocating collections inside loops — declare lists outside and call `.Clear()`
- Collect block lists once per `Main()` call, not repeatedly inside loops
- Use `IsSameConstructAs(Me)` to exclude subgrids when that is the intended scope

### Echo / logging
- Use `Echo()` to report status to the Programmable Block detail panel
- Keep Echo output concise — one summary line, then one line per monitored item
- Prefix warnings clearly: `Echo("WARNING: ...")`

---

## Documentation Style

Each script must have a companion `.md` file covering:
1. **Concept** — what the script does and why
2. **Features** — bullet list of capabilities
3. **In-Game Setup** — numbered step-by-step instructions
4. **Troubleshooting** — table of symptoms and causes
5. **Reference tables** — item names, type strings, etc.

Use standard Markdown. Tables are preferred over prose for reference data. Keep instructions concrete and literal (exact block names, exact Custom Data text).

---

## Adding New Scripts

1. Create `ScriptName.cs` with the script code
2. Create `ScriptName.md` with the documentation
3. Follow the code style and documentation style above
4. Update this `AGENTS.md` file structure section if needed
