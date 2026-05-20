# Inventory Monitor 2 — Space Engineers Programmable Block Script

Monitor item stock levels on your grid by tagging **any inventory block** — cargo containers, reactors, refineries, assemblers, or anything else that holds items. Each tagged block defines what to watch, the thresholds, and what action to take when stock drops low.

---

## Concept

Instead of tagging individual light blocks, you tag the **cargo container** (or any inventory block) that matters. The container's Custom Data holds the full configuration:

- **Which items** to monitor and at what thresholds (multiple items per container supported)
- **What to do** when stock is low — flash a named light block, or trigger a Timer Block on state change

The script reads the tag string from the Programmable Block's own Custom Data so you can change it without editing the script.

---

## Features

- Tag any inventory block (cargo containers, reactors, refineries, etc.) — configuration lives with the inventory that matters
- Multiple items per block — any one falling below threshold triggers the action
- Two action types: **light** (control a light block) or **timer** (trigger a Timer Block on state change)
- Light color and blink mode are configurable per block via named states (`green`, `blinkorange`, `red`, etc.)
- Light block name is specified per block — reuse the same light for multiple blocks, or use dedicated lights
- Timer action only fires on state **change** (ok → low, low → ok) to avoid triggering on every run
- Tag string is configurable via the Programmable Block's Custom Data
- Error state (red light) shown automatically when Custom Data is malformed
- Status output to the Programmable Block detail panel

---

## In-Game Setup

### Step 1 — Place a Programmable Block

Place a **Programmable Block** anywhere on your grid. Open it and paste the full contents of `InventoryMonitor2.cs` into the editor. Click **Check Code**, then **Remember & Exit**.

### Step 2 — Configure the tag (optional)

If you want to use a different tag than `[MONITOR]`, open the Programmable Block terminal, click **Custom Data**, and add:

```
MONITOR_TAG=[MY_TAG]
```

Leave Custom Data empty to use the default `[MONITOR]` tag.

### Step 3 — Tag your inventory blocks

Rename any inventory block you want to monitor so its name contains your tag. This can be a cargo container, reactor, refinery, assembler, or any other block that holds items. Examples:

```
[MONITOR] Ammo Storage
[MONITOR] Iron Supply
[MONITOR] HE-Large Reactor
```

The tag can appear anywhere in the name.

### Step 4 — Set the Custom Data on each tagged container

Open the container's terminal, click **Custom Data**, and fill in the configuration. The format uses INI-style sections.

#### Full example — light action

```
[items]
Ingot/Iron       = 2000
Ingot/Nickel     = 500
Component/Motor  = 100

[command]
action     = light
light_name = {Iron Alert Light}
light_ok   = green        ; optional — defaults to green if omitted
light_low  = blinkorange  ; optional — defaults to blinkorange if omitted
```

#### Full example — timer action

```
[items]
AmmoMagazine/NATO_5p56x45mm = 200
AmmoMagazine/Missile200mm   = 50

[command]
action    = timer
timer_low = {Ammo_Alert_Timer}
timer_ok  = {Ammo_OK_Timer}
```

`timer_ok` is optional. If omitted, nothing is triggered when stock recovers.

> **Block names:** Wrap block names in `{ }` braces. This avoids any ambiguity with spaces, underscores, or special characters in the name. Plain names without braces also work, but braces are recommended.

#### Format reference

| Section | Key | Description |
|---|---|---|
| `[items]` | `Type/Subtype = N` | Item to monitor and its threshold |
| `[command]` | `action` | `light` or `timer` |
| `[command]` | `light_name` | Name of the light block in `{ }` (required when `action=light`) |
| `[command]` | `light_ok` | Light state when stock is OK (optional, `action=light` only — see named states below) |
| `[command]` | `light_low` | Light state when stock is low (optional, `action=light` only — see named states below) |
| `[command]` | `timer_low` | Name of Timer Block to trigger when stock goes LOW, in `{ }` (required when `action=timer`) |
| `[command]` | `timer_ok` | Name of Timer Block to trigger when stock returns OK, in `{ }` (optional, `action=timer` only) |

Lines beginning with `;` or `#` are treated as comments and ignored.

### Step 5 — Place and name your light blocks (action=light only)

Place an **Interior Light** or any light block. Give it the exact name you used in `light_name`. No special tag is needed on the light itself.

**Default light states** (when `light_ok` / `light_low` are not set):

| State | Meaning |
|---|---|
| Green (solid) | All monitored items are at or above threshold |
| Orange (blinking, 2s / 50%) | At least one item is below threshold |
| Red (solid) | The block's Custom Data is missing or malformed |

**Named light states** (use with `light_ok` and `light_low`):

| Value | Behaviour |
|---|---|
| `green` | Solid green |
| `orange` | Solid orange |
| `red` | Solid red |
| `blinkgreen` | Blinking green (2s / 50%) |
| `blinkorange` | Blinking orange (2s / 50%) |
| `blinkred` | Blinking red (2s / 50%) |
| `off` | Light disabled |

### Step 6 — Set up your Timer Blocks (action=timer only)

Place one or two **Timer Block(s)** and name them exactly as specified in `timer_low` and (optionally) `timer_ok`. Configure their actions as you would any other timer — the script calls `Trigger()` on them when the alert state changes.

- When stock goes **low**: `timer_low` is triggered once
- When stock returns **OK**: `timer_ok` is triggered once (if configured)

### Step 7 — Set up the Timer Block

Place a **Timer Block**. Configure it as follows:

| Setting | Value |
|---|---|
| **Trigger Delay** | `00:01:00` (1 minute recommended) |
| **Action 1** | `Run` on your Programmable Block |
| **Action 2** | `Start` on this Timer Block itself |

Leave the Run action's command field blank. Click **Trigger Now** once to start the loop.

### Step 8 — Verify

Open the Programmable Block terminal. The detail panel should show output like:

```
InventoryMonitor2 — checking 2 container(s)...
  [MONITOR] Iron Supply | Iron = 1840 / 2000 [LOW]
  [MONITOR] Iron Supply | Nickel = 620 / 500 [OK]
  [MONITOR] Ammo Storage | NATO_5p56x45mm = 340 / 200 [OK]
  [MONITOR] Ammo Storage | Missile200mm = 12 / 50 [LOW]
  Timer 'Ammo Alert Timer' triggered for '[MONITOR] Ammo Storage'
```

---

## Troubleshooting

| Symptom | Likely cause |
|---|---|
| "No containers tagged [MONITOR] found" | Container name does not contain the tag exactly |
| "WARNING: bad Custom Data on '...'" | `[items]` section is empty, `action` is missing, or required field for the action is absent |
| Light stays red | Config parse failed — re-check section headers, `=` separators, and Type/Subtype spelling |
| Light not found warning | `light_name` value does not exactly match the light block's in-game name |
| "WARNING: unknown light state" | `light_ok` or `light_low` value is misspelled — check against the named states table |
| Timer not found warning | `timer_low` or `timer_ok` does not match the Timer Block's exact in-game name — the warning shows the name length to help spot trailing spaces |
| Stock shows 0 but you have the item | Subtype name is wrong — check spelling and capitalisation |
| Timer fires every run | This should not happen — timer only fires on state change. If it does, the container's EntityId may have changed (e.g. grid merge/split) |
| Script never runs | The driving Timer Block is not started — click **Trigger Now** once |

---

## Supported Short Type Names

| Short name | Full type |
|---|---|
| `Ingot` | MyObjectBuilder_Ingot |
| `Ore` | MyObjectBuilder_Ore |
| `Component` | MyObjectBuilder_Component |
| `AmmoMagazine` | MyObjectBuilder_AmmoMagazine |
| `Tool` | MyObjectBuilder_PhysicalGunObject |
| `GasContainer` | MyObjectBuilder_GasContainerObject |

---

## Item Subtype Reference (common items)

### Ingots
`Iron`, `Nickel`, `Cobalt`, `Magnesium`, `Silicon`, `Silver`, `Gold`, `Platinum`, `Uranium`, `Stone`

### Ores
`Iron`, `Nickel`, `Cobalt`, `Magnesium`, `Silicon`, `Silver`, `Gold`, `Platinum`, `Uranium`, `Stone`, `Ice`

### Components
`SteelPlate`, `InteriorPlate`, `Construction`, `MetalGrid`, `SmallTube`, `LargeTube`,
`Motor`, `Display`, `BulletproofGlass`, `Superconductor`, `Computer`, `Reactor`,
`Thrust`, `GravityGenerator`, `Medical`, `RadioCommunication`, `Detector`,
`Explosives`, `SolarCell`, `PowerCell`, `Canvas`

### Ammo

| SubtypeId | In-game display name |
|---|---|
| `NATO_5p56x45mm` | S-20A Magazine |
| `AutocannonClip` | Autocannon Magazine |
| `NATO_25x184mm` | Gatling Ammo Box |
| `MediumCalibreAmmo` | Assault Cannon Shell |
| `LargeCalibreAmmo` | Artillery Shell |
| `Missile200mm` | Rocket |
| `LargeRailgunAmmo` | Large Railgun Sabot |
| `SmallRailgunAmmo` | Small Railgun Sabot |

---

## File Reference

| File | Description |
|---|---|
| `InventoryMonitor2.cs` | The Programmable Block script to paste in-game |
| `InventoryMonitor2.md` | This document |
| `InventoryMonitor.cs` | Original version (light-centric, single item per light) |
| `InventoryMonitor.md` | Original version documentation |
