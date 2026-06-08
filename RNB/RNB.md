# RNB - Rev NanoBot Manager

Author: RevGamer
Version: v1.0.0

RNB is a Space Engineers Programmable Block script for the **SKO Nanobot Build and Repair System (Maintained)** mod. It monitors BaR/NanoBot welders, queues missing parts into assemblers, tracks projectors, drives LCD pages, and shows a PB boot/live screen automatically.

## Quick Setup

1. Place a Programmable Block on the same construct as the BaR welders.
2. Paste `RNB.cs` into the PB and click **Check Code**.
3. Add RNB roles/pages via Custom Data (recommended) or block name tags.
4. Recompile or wait for the automatic rescan.

No toolbar arguments are used. The script is fully automatic.

## Custom Data Setup (recommended — keeps block names clean)

### Programmable Block

```ini
[RNB]
BootSeconds=6
RescanSeconds=10
AssemblerQueueSeconds=0.5
AutoOfflineSeconds=600
```

| Setting | Default | Notes |
|---|---:|---|
| `BootSeconds` | `6` | Boot screen duration. Min `0.5`, max `60`. |
| `RescanSeconds` | `10` | How often block roles/pages are rescanned. |
| `AssemblerQueueSeconds` | `0.5` | How often missing parts are pushed to assemblers. |
| `AutoOfflineSeconds` | `600` | Idle time before BaR welders are disabled (600 = 10 min). |

### Functional blocks

| Role value | Block type | What it does |
|---|---|---|
| `Role=NanoBot` | BaR welder | Explicit BaR/NanoBot selection. |
| `Role=Assembler` | Assembler | Advanced assembler pool. |
| `Role=BasicAssembler` | Basic Assembler | Basic component pool. |
| `Role=Alert` | Light | State colour and blink. |
| `Role=Corner` | LCD panel | Large readable state display for command centre. |
| `Role=Projector` | Projector | Projector progress tracking. |

```ini
[RNB]
Role=NanoBot
```

### LCD pages

```ini
[RNB]
Page=Status
```

Valid pages: `Status` `Missing` `Weld` `Grind` `Welders` `Assemblers` `Projectors`

## Name Tag Fallback

Name tags still work if preferred. Custom Data takes priority.

| Name tag | Block type |
|---|---|
| `[NanoBot]` | BaR welder |
| `[RNBAssembler]` | Advanced assembler |
| `[RNBBasicAssembler]` | Basic assembler |
| `[RNBAlert]` | Light |
| `[RNBCorner]` | Corner LCD |
| `[RNBProjector]` | Projector |
| `[RNBStatus]` | Status LCD |
| `[RNBMissing]` | Missing parts LCD |
| `[RNBWeld]` | Weld queue LCD |
| `[RNBGrind]` | Grind queue LCD |
| `[RNBWelders]` | Welder detail LCD |
| `[RNBAssemblers]` | Assembler detail LCD |
| `[RNBProjectors]` | Projector progress LCD |

## Page Reference

| Page | Shows |
|---|---|
| `Status` | Welders, assemblers, current work, queue counts, missing count, projectors. |
| `Missing` | Missing component types and amounts. |
| `Weld` | Weld queue and latched progress bar. |
| `Grind` | Grind queue. |
| `Welders` | Per-welder state, mode, reason, and target status. |
| `Assemblers` | Per-assembler mode, enabled state, output count, repeat, and coop. |
| `Projectors` | Per-projector build progress and remaining blocks. |

## Corner LCD

`Role=Corner` turns any LCD into a large at-a-glance state display for use in a command centre or at a distance. It shows:

- Large centred state label: **WORKING** / **MISSING** / **OFFLINE** / **IDLE**
- One line of context below (welder count, missing part count, etc.)
- Border colour matches the alert light state
- Tiny `RNB` label top-left and idle timer top-right

Works on any LCD size. Font scales automatically for small vs large panels.

## Display Style

- PB surface 0 shows a boot screen on compile, then a compact live overview.
- Standard LCDs show fixed pages with a dark navy panel, cyan frame, and monospace text.
- Corner LCD shows large state text only — designed to be readable from across a room.

## Assembler Routing

Basic assemblers receive only vanilla basic components:

```text
SteelPlate, InteriorPlate, Construction, SmallTube, LargeTube,
Motor, Display, BulletproofGlass, Girder
```

Advanced assemblers receive everything else. If a block is tagged `BasicAssembler`, that wins over subtype detection.

## Auto-Offline

RNB disables BaR welders after `AutoOfflineSeconds` of no weld, grind, collect, or active projector work. Manually re-enabling a welder clears the offline state automatically.

## Troubleshooting

| Symptom | Check |
|---|---|
| No BaR welders found | BaR mod installed, maintained SKO fork, same construct, or `Role=NanoBot`. |
| LCD black | Recompile PB, confirm Custom Data has `[RNB]` section and valid `Page=` or `Role=`. |
| Wrong LCD page | Custom Data `Page=` overrides name tags. Check spelling. |
| No assembler queue | Add `Role=Assembler` or `Role=BasicAssembler`, confirm assembler is functional. |
| Corner LCD garbled | Use `Role=Corner` not a page tag — corner LCD ignores `Page=`. |
| Projector idle | Blueprint complete or no blueprint loaded. |
| Welders disabled | Auto-offline fired; re-enable a BaR welder or raise `AutoOfflineSeconds`. |

## Compatibility

- Requires SKO maintained BaR properties: `BuildAndRepair.MissingComponents`, `BuildAndRepair.PossibleTargets`, `BuildAndRepair.ProductionBlock.EnsureQueued`.
- Uses only SE programmable block safe APIs.
