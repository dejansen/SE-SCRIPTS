# Inventory Monitor — Space Engineers Programmable Block Script

Monitor item stock levels on your grid and get instant visual alerts via colored lights.

---

## Concept

The script scans all inventories on your grid (cargo containers, assemblers, refineries, etc.) and checks the total quantity of a configured item. Each alert light you place controls its own monitor:

- **Green (solid)** — stock is at or above your threshold
- **Orange (blinking)** — stock has dropped below your threshold
- **Red (solid)** — the light's Custom Data is missing or incorrectly formatted

The script is driven by a Timer Block you configure manually. It runs, checks all lights, updates colors, and exits — no background processing, no always-on load.

---

## Features

- Any number of monitored items — one light per item
- Supports ingots, ores, components, ammo, and tools
- Scans all inventories on the main grid (subgrids are intentionally excluded)
- Error state on misconfigured lights so you notice bad config immediately
- Status output to the Programmable Block's detail panel (visible in terminal)

---

## In-Game Setup

### Step 1 — Place a Programmable Block

Place a **Programmable Block** anywhere on your grid. Open it and paste the full contents of `InventoryMonitor.cs` into the editor. Click **Check Code**, then **Remember & Exit**.

> The script editor in Space Engineers expects only the content inside the `Program` class. Paste everything from `public Program()` onwards, or paste the full file — the game will accept it.

### Step 2 — Place and name your alert lights

Place one **Interior Light** (or any light block) per item you want to monitor. Rename each light so its name contains `[MONITOR]`. Examples:

```
[MONITOR] Magnesium Alert
[MONITOR] Iron Alert
[MONITOR] Stone Ore
```

The tag `[MONITOR]` can appear anywhere in the name.

### Step 3 — Set the Custom Data on each light

Open the light's terminal, click **Custom Data**, and enter exactly two lines:

```
Type/Subtype
threshold=N
```

**Examples:**

| Item | Custom Data |
|---|---|
| Magnesium ingots | `Ingot/Magnesium` / `threshold=500` |
| Iron ingots | `Ingot/Iron` / `threshold=2000` |
| Stone ore | `Ore/Stone` / `threshold=5000` |
| Steel plates | `Component/SteelPlate` / `threshold=1000` |
| NATO 5.56mm ammo | `AmmoMagazine/NATO_5p56x45mm` / `threshold=200` |

**Supported short type names (case-insensitive):**

| Short name | Full type |
|---|---|
| `Ingot` | MyObjectBuilder_Ingot |
| `Ore` | MyObjectBuilder_Ore |
| `Component` | MyObjectBuilder_Component |
| `AmmoMagazine` | MyObjectBuilder_AmmoMagazine |
| `Tool` | MyObjectBuilder_PhysicalGunObject |
| `GasContainer` | MyObjectBuilder_GasContainerObject |

### Step 4 — Set up the Timer Block

Place a **Timer Block** on your grid. Open it and configure:

| Setting | Value |
|---|---|
| **Trigger Delay** | `00:01:00` (1 minute recommended) |
| **Action 1** | `Run` on your Programmable Block |
| **Action 2** | `Start` on this Timer Block itself |

**The "Run" action command field must be left blank** — no argument is needed.

To start the loop: open the Timer Block and click **Trigger Now** once. It will run the script immediately and then restart itself every minute automatically.

> You can lower the delay (e.g. 10–30 seconds) for faster response, at a minor performance cost.

### Step 5 — Verify

Open the Programmable Block terminal and check the detail panel at the bottom. You should see output like:

```
Checking 2 monitor light(s)...
  [MONITOR] Magnesium Alert: Magnesium = 340 / 500 [LOW]
  [MONITOR] Iron Alert: Iron = 2140 / 2000 [OK]
```

If a light turns **red**, re-check its Custom Data for typos.

---

## Troubleshooting

| Symptom | Likely cause |
|---|---|
| Light stays red | Custom Data is empty, missing a line, or has a typo in Type/Subtype |
| Script says "No lights tagged [MONITOR] found" | Light name does not contain `[MONITOR]` exactly |
| Stock shows 0 but you have the item | Subtype name is wrong — check spelling and capitalisation |
| Script never runs | Timer Block is not started — click **Trigger Now** once |
| Subgrid containers not counted | By design — only the main connected grid is scanned |

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

---

## File Reference

| File | Description |
|---|---|
| `InventoryMonitor.cs` | The Programmable Block script to paste in-game |
| `InventoryMonitor.md` | This document |
