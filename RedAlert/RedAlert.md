# Red Alert

## Concept

Activates a red alert state on a named group of lighting blocks. On activation, the script saves all current light settings to persistent storage, then forces every light in the group to solid red. On deactivation, each light is restored exactly to its saved state — colour, intensity, radius, blink settings, and on/off state included.

If the game saves or the PB reboots mid-alert, the saved state survives in `Storage` and deactivate will still restore correctly.

---

## Features

- Saves and restores: colour, intensity, radius, blink interval, blink length, blink offset, and enabled state
- Persistent across game saves and PB reboots (uses `Storage`)
- Guards against double-activating or deactivating without a prior activation
- Tolerates lights being removed from the group between activate and deactivate (skips missing lights)

---

## In-Game Setup

1. Create a block group named exactly `RedAlertLights` and add all alert lights to it.
2. Create a Programmable Block.
3. Open the PB, click **Edit**, paste the contents of `RedAlert.cs`, and click **Check Code**.
4. Set up two toolbar buttons or Timer Blocks, each running the PB with the respective argument:
   - Argument: `activate`
   - Argument: `deactivate`

---

## Commands

| Argument     | Effect                                                        |
|--------------|---------------------------------------------------------------|
| `activate`   | Saves all light states and sets every light in the group to solid red |
| `deactivate` | Restores all lights to their saved state                      |

Commands are case-insensitive.

---

## Troubleshooting

| Symptom | Cause |
|---|---|
| `ERROR: Group 'RedAlertLights' not found` | Group name does not match exactly — check capitalisation and spaces |
| `WARNING: No lights found in group 'RedAlertLights'` | Group exists but contains no lighting blocks |
| Lights not restored after PB recompile | Storage was cleared; always deactivate before recompiling |
| Some lights stay red after deactivate | Those lights were removed from the group or the grid after activation — they must be restored manually |
| `Red Alert already active` | Activate was called twice; run deactivate first |
