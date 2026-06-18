# RNB v2.0.0 - Ready To Use Guide

RNB (Rev NanoBot Manager) is a Space Engineers Programmable Block script for the SKO Nanobot Build and Repair System (Maintained).

It monitors Build and Repair (BaR) blocks, queues missing components, tracks projectors, controls alert lights, manages idle shutdown and projector wake-up, and renders status pages on LCDs.

## Requirements

- Space Engineers Experimental Mode enabled.
- In-game scripts enabled in the world/server settings.
- SKO Nanobot Build and Repair System (Maintained) installed.
- One Programmable Block running RNB.
- Blocks must be available to the Programmable Block terminal system.

## Files In This Folder

| File | Purpose |
|---|---|
| `RNB-v2.0.0-PasteReady.cs` | Minified script to paste into the Programmable Block. |
| `GUIDE.md` | Complete setup and troubleshooting guide. |

## Step 1 - Install The Script

1. Place a Programmable Block.
2. Open the Programmable Block terminal.
3. Select **Edit**.
4. Remove the default code.
5. Open `RNB-v2.0.0-PasteReady.cs` and copy all its contents.
6. Paste it into the Programmable Block editor.
7. Select **Check Code**.
8. Confirm there are no errors.
9. Select **OK** to compile and start RNB.

The Programmable Block screen will show:

```text
RNB
Rev Nanobot
Manager
v2.0.0
```

## Step 2 - Configure The Programmable Block

Open the Programmable Block Custom Data and add:

```ini
[RNB]
BootSeconds=6
RescanSeconds=10
AssemblerQueueSeconds=0.5
AutoOfflineSeconds=600
WakeOnProjector=true
```

### Programmable Block Settings

| Setting | Recommended | Description |
|---|---:|---|
| `BootSeconds` | `6` | Boot screen duration. |
| `RescanSeconds` | `10` | How often RNB searches for configured blocks. |
| `AssemblerQueueSeconds` | `0.5` | Missing-component queue interval. |
| `AutoOfflineSeconds` | `600` | Idle time before RNB disables BaRs. |
| `WakeOnProjector` | `true` | Allows active projectors or detected BaR work to wake disabled BaRs. |

Manually switching a BaR on resets the full `AutoOfflineSeconds` countdown. RNB will not immediately switch it off using an old idle timer.

## Step 3 - Configure NanoBot/BaR Blocks

Add this to every BaR block that RNB should manage:

```ini
[RNB]
Role=NanoBot
```

Example block name fallback:

```text
[NanoBot] Station Repair BaR
```

Custom Data is recommended because it keeps names cleaner.

### Small-Grid Drones

Add the same role to each drone BaR:

```ini
[RNB]
Role=NanoBot
```

The drone must be visible to the Programmable Block terminal system. A connected connector normally provides access. Antenna communication alone may not provide direct Programmable Block terminal access.

## Step 4 - Configure Assemblers

### Advanced Assembler

```ini
[RNB]
Role=Assembler
```

### Basic Assembler

```ini
[RNB]
Role=BasicAssembler
```

RNB sends vanilla basic components to Basic Assemblers and sends advanced components to Advanced Assemblers.

Basic component routing includes:

```text
SteelPlate
InteriorPlate
Construction
SmallTube
LargeTube
Motor
Display
BulletproofGlass
Girder
```

Assembler Details are sorted alphabetically by clean block name.

## Step 5 - Configure Projectors

Add this to every projector that RNB should monitor:

```ini
[RNB]
Role=Projector
```

Name fallback:

```text
[RNBProjector] Station Repair Projector
```

### Projector Activity Rules

In v2.0.0, a projector only counts as active when all of these are true:

- Projector is enabled.
- Projector is functional.
- Projector is working/powered.
- Projector is actively projecting a blueprint.
- Blueprint has remaining blocks.

An OFF projector with an incomplete blueprint does not keep BaRs online.

Projector states shown by RNB:

| State | Meaning |
|---|---|
| `BUILDING` | Online, powered, projecting, and incomplete. |
| `OFFLINE` | Projector block is switched off. |
| `NO POWER` | Enabled but not working. |
| `DAMAGED` | Projector is not functional. |
| `NO BLUEPRINT` | No active projection is loaded. |
| `COMPLETE` | Projection has no remaining blocks. |

## Step 6 - Configure LCD Pages

Add one page section to each standard LCD.

### Main Status Dashboard

```ini
[RNB]
Page=Status
```

Shows BaR health, working/offline/damaged counts, assemblers, build queues, missing parts, projector totals, and alerts.

### Missing Components

```ini
[RNB]
Page=Missing
```

### Weld Queue

```ini
[RNB]
Page=Weld
```

### Grind Queue

```ini
[RNB]
Page=Grind
```

### NanoBot/BaR Details

```ini
[RNB]
Page=Welders
```

### Assembler Details

```ini
[RNB]
Page=Assemblers
```

### Projector Control

```ini
[RNB]
Page=Projectors
```

## Step 7 - Configure Corner LCDs

Corner LCDs use a simplified state display:

```ini
[RNB]
Role=Corner
```

Do not add `Page=Status` to the same Corner LCD. `Role=Corner` is its own display mode and takes priority.

RNB automatically chooses:

- Horizontal banner layout for wide, short LCDs.
- Centered layout for normal or tall LCDs.

Corner states:

| Color | State |
|---|---|
| Green | Working |
| Cyan | Idle |
| Yellow | Offline |
| Red | Missing parts or damaged BaR |

Multiple Corner LCDs and blocks with multiple text surfaces are supported.

## Step 8 - Configure Alert Lights

Add this to a light:

```ini
[RNB]
Role=Alert
```

Alert light behavior:

| State | Color | Blink |
|---|---|---|
| Working | Green | No |
| Idle | Dim cyan | No |
| Missing | Red | Yes |
| Offline | Yellow | Yes |

## Recommended Station Setup

Example arrangement:

```text
1 Programmable Block running RNB
2-8 BaR blocks with Role=NanoBot
1+ Advanced Assemblers with Role=Assembler
Optional Basic Assemblers with Role=BasicAssembler
1 Status LCD
1 NanoBot Details LCD
1 Assembler Details LCD
1 Projector Control LCD
1 Missing Components LCD
Optional Corner LCDs and alert lights
```

## Recommended Ship Printer Setup

```text
Projector Custom Data:
[RNB]
Role=Projector

BaR Custom Data:
[RNB]
Role=NanoBot

PB Custom Data:
[RNB]
AutoOfflineSeconds=600
WakeOnProjector=true
```

Workflow:

1. Load and enable the projection.
2. RNB detects an enabled, powered, incomplete projector.
3. RNB wakes disabled BaRs.
4. BaRs build the projection.
5. When the projection completes and no other work exists, the idle timer begins.
6. RNB disables the BaRs after `AutoOfflineSeconds`.

## Name Tag Fallbacks

| Tag | Purpose |
|---|---|
| `[NanoBot]` | BaR block |
| `[RNBAssembler]` | Advanced Assembler |
| `[RNBBasicAssembler]` | Basic Assembler |
| `[RNBAlert]` | Alert light |
| `[RNBCorner]` | Corner LCD |
| `[RNBProjector]` | Projector |
| `[RNBStatus]` | Status page |
| `[RNBMissing]` | Missing page |
| `[RNBWeld]` | Weld queue page |
| `[RNBGrind]` | Grind queue page |
| `[RNBWelders]` | NanoBot Details page |
| `[RNBAssemblers]` | Assembler Details page |
| `[RNBProjectors]` | Projector Control page |

## Troubleshooting

### RNB Finds No BaRs

- Confirm SKO Build and Repair System is installed.
- Add `Role=NanoBot` to BaR Custom Data.
- Recompile the Programmable Block or wait for the rescan timer.
- Confirm the BaR is accessible through the same terminal system.

### BaR Switches Off Immediately

- Install the latest v2.0.0 script.
- Manually enabling a BaR should reset the full idle timer.
- Check `AutoOfflineSeconds` in Programmable Block Custom Data.

### BaR Stays Online Because Of A Projector

- Check the Projector Control page.
- Only `BUILDING` should keep BaRs online.
- An `OFFLINE`, `NO POWER`, `NO BLUEPRINT`, or `COMPLETE` projector does not count as active.

### Projector Does Not Wake BaRs

- Confirm `WakeOnProjector=true`.
- Confirm the projector has `Role=Projector`.
- Confirm it is enabled, powered, projecting, and incomplete.

### LCD Is Blank

- Confirm the LCD is powered.
- Confirm it has a valid `Page=` or `Role=Corner`.
- Recompile RNB.
- Wait for the configured rescan interval.

### Corner LCD Text Overlaps

- Use only `Role=Corner` on that surface.
- Remove any page role/tag from the same LCD.
- Confirm the latest v2.0.0 script is installed.

### Missing Radio Or Detector Components

- Confirm an Advanced Assembler is registered.
- Confirm the complete conveyor route uses ports capable of moving those components.
- A large connector does not help if another part of the conveyor route uses restricted small ports.

## Updating RNB

1. Open the current Programmable Block script editor.
2. Replace all code with the new paste-ready script.
3. Select **Check Code**.
4. Select **OK**.
5. Existing block Custom Data can remain unchanged.

Keep your block role/page configuration in Custom Data. Replacing the script does not remove Custom Data from blocks.
