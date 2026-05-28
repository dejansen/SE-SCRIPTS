# RNB - Rev NanoBot Manager

Author: RevGamer
Version: v1.0.0

RNB is a Space Engineers Programmable Block script for the **SKO Nanobot Build and Repair System (Maintained)** mod. It monitors BaR/NanoBot welders, queues missing parts into assemblers, tracks projectors, drives LCD pages, and shows a PB boot/live screen automatically.

## Quick Setup

1. Place a Programmable Block on the same construct as the BaR welders.
2. Paste `RNB.cs` into the PB and click **Check Code**.
3. Add RNB roles/pages with either block names or Custom Data.
4. Recompile or wait for the automatic rescan.

No toolbar arguments are used. The script is automatic.

## Recommended Custom Data

Custom Data is the cleanest setup because it keeps block names readable. Name tags still work as a fallback.

### PB Custom Data

Put this in the Programmable Block Custom Data only if you want to override defaults:

```ini
[RNB]
BootSeconds=6
RescanSeconds=10
AssemblerQueueSeconds=0.5
AutoOfflineSeconds=600
```

| Setting | Default | Notes |
|---|---:|---|
| `BootSeconds` | `6` | Boot screen duration. Minimum `0.5`, maximum `60`. |
| `RescanSeconds` | `10` | How often block roles/pages are rescanned. |
| `AssemblerQueueSeconds` | `0.5` | How often missing parts are pushed to assemblers. |
| `AutoOfflineSeconds` | `600` | Idle time before BaR welders are disabled. |

### LCD Custom Data

Put this in any LCD Custom Data:

```ini
[RNB]
Page=Missing
```

Valid pages:

```text
Status
Missing
Weld
Grind
Welders
Assemblers
Projectors
```

### Functional Block Custom Data

Put this in assembler, welder, light, or projector Custom Data:

```ini
[RNB]
Role=Assembler
```

Valid roles:

```text
Assembler
BasicAssembler
NanoBot
Alert
Projector
```

Examples:

```ini
[RNB]
Role=BasicAssembler
```

```ini
[RNB]
Role=NanoBot
```

```ini
[RNB]
Role=Projector
```

## Name Tag Fallback

If you prefer block-name tags, these still work.

### Functional Blocks

| Name tag | Block type | What it does |
|---|---|---|
| `[RNBAssembler]` | Assembler | Advanced assembler pool. |
| `[RNBBasicAssembler]` | Basic Assembler | Basic component pool. |
| `[NanoBot]` | BaR welder | Explicit BaR/NanoBot selection. |
| `[RNBAlert]` | Light | State colour and blink. |
| `[RNBProjector]` | Projector | Projector progress tracking. |

### LCD Pages

| Name tag | Page |
|---|---|
| `[RNBStatus]` | System overview |
| `[RNBMissing]` | Missing components |
| `[RNBWeld]` | Weld queue |
| `[RNBGrind]` | Grind queue |
| `[RNBWelders]` | Welder details |
| `[RNBAssemblers]` | Assembler details |
| `[RNBProjectors]` | Projector progress |

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

## Display Style

- PB surface 0 shows a centred boot screen first, then a compact live overview.
- Tagged/configured LCDs show fixed pages with a dark navy panel, cyan frame, and monospace text.
- The boot loading bar uses a separate centred bar with no internal divider line.

## Assembler Routing

Basic assemblers receive only vanilla basic components:

```text
SteelPlate, InteriorPlate, Construction, SmallTube, LargeTube,
Motor, Display, BulletproofGlass, Girder
```

Advanced assemblers receive everything else.

If a block is marked `BasicAssembler`, that wins. Otherwise RNB can infer basic vs advanced from the assembler subtype.

## Auto-Offline

RNB disables BaR welders after `AutoOfflineSeconds` with no weld, grind, collect, or active projector work. If you manually re-enable a welder later, RNB clears the offline state automatically.

## Troubleshooting

| Symptom | Check |
|---|---|
| No BaR welders found | BaR mod installed, maintained SKO fork, same construct, or `Role=NanoBot`. |
| LCD black | Recompile PB, or confirm LCD Custom Data has `[RNB]` and valid `Page=`. |
| Wrong LCD page | Custom Data `Page=` overrides name tags. Check spelling. |
| No assembler queue | Add `Role=Assembler` or `Role=BasicAssembler`, and confirm assembler is functional. |
| Projector idle | Blueprint complete or no blueprint loaded. |
| Welders disabled | Auto-offline fired; manually re-enable a BaR welder or raise `AutoOfflineSeconds`. |

## Compatibility Notes

- Requires SKO maintained BaR properties such as `BuildAndRepair.MissingComponents`, `BuildAndRepair.PossibleTargets`, and `BuildAndRepair.ProductionBlock.EnsureQueued`.
- Uses only Space Engineers programmable block safe APIs.
- No pasted RNB image/logo Custom Data is supported; the logo is drawn natively as text for reliability.
