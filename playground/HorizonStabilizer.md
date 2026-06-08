# Horizon Stabilizer

Proof of concept. Keeps a ship aligned to the planetary horizon using gyroscope override. Useful for testing level flight — e.g. disable inertia dampeners and let the ship cruise while this holds it flat.

## Features

- ENABLE / DISABLE commands mapped to hotbar
- Proportional gyro controller — the further off-level, the harder it corrects
- Works with any ship controller (prefers the main cockpit)
- Controls all gyroscopes on the grid
- Echos alignment error each tick for diagnostics

## In-Game Setup

1. Place a Programmable Block on your ship.
2. Paste the script and click **Check Code**, then **OK**.
3. In the toolbar editor, add two actions for the Programmable Block:
   - Slot A: **Run** with argument `ENABLE`
   - Slot B: **Run** with argument `DISABLE`
4. Press the ENABLE hotkey to activate. The PB detail panel shows alignment error.
5. Press DISABLE to stop — gyros return to normal.

## Tuning

| Constant | Default | Effect |
|---|---|---|
| `GAIN` | `5.0` | How aggressively gyros correct. Increase if correction is sluggish; decrease if the ship oscillates. |

## Troubleshooting

| Symptom | Cause |
|---|---|
| "No ship controller found" | No cockpit, seat, or remote control on the grid |
| "No gyroscopes found" | No gyroscopes on the grid |
| Ship oscillates / shakes | `GAIN` is too high — lower it |
| Ship barely responds | `GAIN` is too low — raise it |
| Nothing happens after ENABLE | Check the PB detail panel for errors; make sure the argument is exactly `ENABLE` |
| Gyros stay locked after game reload | Run DISABLE once to clear override state |
