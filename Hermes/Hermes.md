# HERMES — Intergrid Messaging Service

**Version 2.0**

A single-script intergrid alert system for Space Engineers. Buildings broadcast alerts to a central control room, where they appear on a sprite-rendered LCD dispatch board.

---

## Concept

Each building in your town runs this script in **sender** mode. When an Event Controller detects something (low hydrogen, low power, etc.), it triggers a Timer Block, which runs the Programmable Block with an alert message as the argument. The script broadcasts that message over an antenna.

The central control building runs the same script in **receiver** mode. It listens on the same channel and displays all incoming alerts on a tagged LCD panel — your office dispatch board.

One script, two roles. No extra files.

---

## Features

- **One script** — mode set via Custom Data (`sender`, `receiver`, `both`, or `local`)
- **Preprogrammed shortcodes** — type `HYDROGEN_LOW` and it broadcasts "Hydrogen tanks critically low"
- **Freeform messages** — any text not matching a shortcode is sent as-is
- **Antenna auto-management** — receiver keeps antenna on; sender turns it on to transmit and off again
- **Sprite-based dispatch board** — LCD renders with background, panel, dividers, and adaptive font sizing that works on any LCD type
- **Adaptive layout** — measures the actual surface pixel dimensions and scales font and layout to fit any LCD panel size
- **Per-message channel override** — prefix any argument with `@channel:` to route it to a different channel without changing config
- **Persistent log** — messages survive PB reboots via `Storage`
- **Auto-populate config** — missing Custom Data keys are written with defaults on first run
- **CLEAR commands** — dismiss one alert or all at once
- **Optional ACK mode** — sender retries until receiver confirms delivery
- **Alert light** — any lighting block with the tag in its name turns on while there are unread messages, off on CLEAR
- **Alert sound** — any sound block with the tag in its name plays once when a new message arrives
- **Broadcast Controller** — optionally announces incoming messages through a tagged Broadcast Controller block

---

## Setup — Sender (one per building)

1. Place a **Programmable Block** on the building.
2. Paste the content of `Hermes.cs` into it.
3. Open the PB's **Custom Data** and set:
   ```
   mode    = sender
   channel = HERMES
   ```
4. Click **Check Code** — it should compile with no errors.
5. Place a **Timer Block** on the same building. Set it to run the PB with your alert message (see "Triggering" below).
6. Wire your **Event Controller** to trigger that Timer Block.

> **Antenna:** The script automatically finds and uses any antenna on the same construct. If the antenna is off, it turns it on briefly to transmit and then off again. No antenna is needed for short-range IGC (buildings within physics range work without one).

---

## Setup — Receiver (central control building)

1. Place a **Programmable Block** on the control building.
2. Paste the content of `Hermes.cs` into it.
3. Open the PB's **Custom Data** and set:
   ```
   mode    = receiver
   channel = HERMES
   lcd_tag = [HERMES]
   ```
4. Click **Check Code**.
5. Name one or more LCD panels with the tag in their name (see "Naming the LCD" below).
6. The script runs automatically at ~1.67s intervals (`Update100`) and refreshes the LCD.

> **Antenna:** The receiver's antenna must be on at all times. The script enables it automatically on startup and re-enables it if it is ever turned off.

---

## Setup — Local (single building, no antenna needed)

Use `local` mode when you want a message board on one building without any intergrid networking. Other blocks on the same grid post messages by running the PB with an argument — there is no antenna, no channel, and no receiver elsewhere.

1. Place a **Programmable Block** on the building.
2. Paste the content of `Hermes.cs` into it.
3. Open the PB's **Custom Data** and set:
   ```
   mode    = local
   lcd_tag = [HERMES]
   ```
4. Click **Check Code**.
5. Name one or more LCD panels on the same grid with the tag (e.g. `[HERMES] Status Board`).
6. Wire your **Event Controller** → **Timer Block** → **Run** the PB with an argument (shortcode or freeform text).

Messages posted via run arguments appear on the tagged LCDs immediately. Alert lights and sound blocks work the same way as in receiver mode. CLEAR commands work normally.

---

## Broadcast Controller (optional)

When a new message arrives, Hermes can announce it through a **Broadcast Controller** block — the in-game block that lets you play pre-programmed voice lines or display scrolling text.

**Setup:**

1. Place a **Broadcast Controller** on the same grid as the receiver PB.
2. Include `[HERMES_BC]` in the block's name (or change the tag in Custom Data).
3. In the Broadcast Controller's settings, pre-program the message slot you want Hermes to use (default: slot 8).

The script writes the incoming message text to the configured slot and triggers it automatically. No extra wiring needed.

**Config keys:**

```
bc_tag     = [HERMES_BC]   ; name tag to find the Broadcast Controller block
bc_enabled = true           ; set to false to disable without removing the block
bc_index   = 8              ; which message slot to use (1–8)
```

---

## Alert Light and Sound Block

Place any lighting block or sound block on the same construct as the receiver PB and include the `lcd_tag` value in its name.

| Block | Behavior |
|---|---|
| Lighting block | Turns **on** while there are unread messages; turns **off** when you run CLEAR or FORWARD |
| Sound block | **Plays once** each time a new message arrives; stops on CLEAR |

The sound block plays whatever sound and at whatever volume you configure in-game — the script just calls `Play()` on it. A looping siren works fine; it will be stopped on CLEAR.

Multiple lights and sound blocks with the tag are all triggered simultaneously.

**Example names** (default tag `[HERMES]`):
```
[HERMES] Alert Light
[HERMES] Alarm
[HERMES] Siren
```

---

## Naming the Dispatch LCD

The script finds any block with your `lcd_tag` value in its name that has an LCD surface — this includes standalone LCD panels, cockpits, control stations, corner LCDs, and transparent LCDs.

**Rules:**
- The tag is **case-sensitive**. `[HERMES]` and `[hermes]` are different.
- The block must be on the **same construct** as the receiver PB (not a subgrid).
- Multiple tagged blocks all show the same content.
- For standalone LCD panels, surface index is always 0.
- For cockpits and control stations, use `lcd_surface` to pick which screen (0-based).

The script sets `ContentType = SCRIPT` on the surface automatically and adjusts font scale and layout to the actual pixel dimensions of the surface — no manual font or size setting needed.

**Valid name examples** (default tag `[HERMES]`):
```
[HERMES] Dispatch Board
Main Screen [HERMES]
[HERMES]
Alerts [HERMES] Panel
My Cockpit [HERMES]        ← cockpit, shows on the surface set by lcd_surface
```

---

### Using a cockpit or control station LCD

Cockpits and control stations expose multiple screens numbered from 0. To use one as the dispatch board:

1. Add the `lcd_tag` to the cockpit's name, e.g. `Fighter Cockpit [HERMES]`.
2. Set `lcd_surface` in Custom Data to the screen number you want (check in-game — surface 0 is usually the center screen).

```
mode        = receiver
channel     = HERMES
lcd_tag     = [HERMES]
lcd_surface = 1        ; second screen on the cockpit
```

If `lcd_surface` is out of range for a given block, that block is silently skipped.

---

## Triggering Alerts (Sender)

Event Controllers cannot run PBs directly. Use a **Timer Block** as the bridge:

1. Create a Timer Block on the building (e.g. `Hydrogen Alert Timer`).
2. In the Timer Block's toolbar, add an action: **Run** → select the HERMES PB → enter the argument.
3. In the Event Controller, set it to trigger that Timer Block.

**Using a shortcode:**
- Argument: `HYDROGEN_LOW`
- Broadcasts on the default channel: *"Hydrogen tanks critically low"*

**Using freeform text:**
- Argument: `East reactor offline`
- Broadcasts on the default channel: *"East reactor offline"*

**Routing to a specific channel (multi-channel):**
- Argument: `@PLAYERA:HYDROGEN_LOW`
- Broadcasts *"Hydrogen tanks critically low"* on channel `PLAYERA` instead of the default

**Posting a local-only message (no broadcast):**
- Argument: `@local:Generator restarted`
- Injects the message directly into the local log and LCD without transmitting over IGC
- Works in any mode — in `local` mode the `@local:` prefix is optional but still accepted
- Shortcodes work normally after `@local:`

The `@channel:` and `@local:` prefixes are per-message. The configured `channel` in Custom Data remains the default for all plain messages. Shortcodes work normally after the `:`.

> **Important:** the channel name after `@` must match the receiver's `channel` config exactly — same capitalisation, same characters, no extra brackets unless the receiver is also configured with them.

### Shortcode reference

| Argument | Broadcast text |
|---|---|
| `HYDROGEN_LOW` | Hydrogen tanks critically low |
| `OXYGEN_LOW` | Oxygen supply critically low |
| `POWER_LOW` | Battery power critically low |
| `GAS_LOW` | Gas supply critically low |
| `AMMO_LOW` | Ammunition stockpile low |
| `ICE_LOW` | Ice supply low |
| `ORE_LOW` | Ore stockpile low |
| `COMPONENT_LOW` | Component stockpile low |
| `OFFLINE` | System offline or unresponsive |
| `MAINTENANCE` | Maintenance required |
| `FUEL_LOW` | Fuel reserves low |
| `WATER_LOW` | Water supply low |

Shortcodes are **case-insensitive**: `hydrogen_low`, `Hydrogen_Low`, and `HYDROGEN_LOW` all work.

---

## Using a Grid as a Message Relay

A grid that is always online (e.g. an airport control tower) can act as a relay: it stores messages for a player while they are away, then delivers them when the player lands.

**How to wire it:**

1. Configure the relay PB with `mode = both` and `channel = PLAYERA`.
2. Buildings send player-addressed messages using `@PLAYERA:HYDROGEN_LOW`.
   The relay receives and stores them like a normal receiver.
3. Place a **Sensor Block** or use a **Connector** event on the landing pad to detect arrival.
4. Wire the arrival event to a **Timer Block** → **Run** the relay PB with argument `FORWARD`.
5. `FORWARD` re-broadcasts all stored messages on `PLAYERA`.
   Player A's ship receiver (also on `PLAYERA`) picks them up.

**With `ack = true` on the relay:**
`FORWARD` enqueues the messages instead of fire-and-forget. The relay's retry loop keeps re-broadcasting every `retry_seconds` until Player A's receiver ACKs. Useful if their PB is slow to come online after landing.

**The relay's own dispatch LCD** still shows what's queued — so the tower operator can see pending messages before the player arrives.

---

## Clearing Alerts

Run the **receiver** PB from the terminal with one of these arguments:

| Argument | Action |
|---|---|
| `CLEAR` | Remove all messages |
| `CLEAR ALL` | Same as `CLEAR` |
| `CLEAR 1` | Remove message #1 (the newest) |
| `CLEAR 3` | Remove message #3 |
| `FORWARD` | Re-broadcast all stored messages on the configured channel, then clear the log (relay use case — requires `mode = both`) |

The number matches the `#N` shown on the dispatch board. After clearing, the board updates immediately.

---

## Custom Data — Full Reference

All keys are written automatically with their defaults on first run. You only need to set the values you want to change.

```
mode                 = receiver    ; sender | receiver | both | local
channel              = HERMES      ; Must match on all senders and the receiver
lcd_tag              = [HERMES]    ; Any LCD/cockpit screen with this in its name shows the dispatch board
lcd_surface          = 0           ; Surface index for cockpits/control stations (0 = first screen)
lcd_mode             = dispatch    ; dispatch = all messages | carousel = one at a time, cycling
lcd_carousel_seconds = 5           ; Seconds each message is shown before advancing (carousel only)
max_messages         = 20          ; How many alerts to keep (oldest are dropped)
ack                  = false       ; true = enable delivery confirmation + retry queue
retry_seconds        = 30          ; Seconds between retransmission attempts (ack mode only)
max_retries          = 0           ; Max retries before dropping (0 = retry forever)
bc_tag               = [HERMES_BC] ; Name tag to find the Broadcast Controller block
bc_enabled           = true        ; Set to false to disable broadcast announcements
bc_index             = 8           ; Message slot used on the Broadcast Controller (1–8)
```

### `mode`
- `sender` — responds to run arguments only; no polling loop
- `receiver` — polls IGC and updates the LCD; handles CLEAR commands
- `both` — same PB acts as sender and receiver (e.g. a relay building)
- `local` — no antenna or IGC at all; run arguments are posted directly to the on-grid LCD board

### `ack` — delivery confirmation (optional)

When `ack = true`:
- The sender queues each message and retransmits every `retry_seconds` until the receiver confirms.
- The receiver sends a unicast acknowledgment back to the sender's IGC address.
- Once confirmed, the message is removed from the retry queue.
- The queue survives PB reboots via `Storage`.

---

## `lcd_mode` — Dispatch vs Carousel

| Value | Behaviour |
|---|---|
| `dispatch` (default) | All stored messages displayed at once, newest first. |
| `carousel` | One message fills the screen. Cycles to the next every `lcd_carousel_seconds` seconds. Resets to the newest message whenever a new alert arrives. |

---

## Dispatch Board Layout

The dispatch board is rendered as sprites and adapts its font size and spacing to whatever LCD panel it is on — wide, narrow, tall, or square. No manual font configuration required.

**Dispatch mode:**
```
HERMES DISPATCH                    HH:mm
────────────────────────────────────────
#1  [HH:mm]  GridName
  Message text
#2  [HH:mm]  GridName
  Message text
────────────────────────────────────────
         Intergrid Messaging  v2.0
```

**Carousel mode** (`lcd_mode = carousel`):
```
────────────────────────────────────────

  Message text

────────────────────────────────────────
GridName  •  HH:mm  •  1 / 3
```

---

## PB Surface Display

The small LCD on the Programmable Block's face shows live status:

```
══════════════════
   * H E R M E S *
══════════════════
 Intergrid Comms
      v2.0
══════════════════
Mode: RECEIVER
Chan: HERMES
Msgs: 3 / 20
 Ant: ON
```

---

## Known Limitations

| Issue | Notes |
|---|---|
| **Two PBs on the same grid (sender + receiver)** | If a building runs a dedicated sender PB and a dedicated receiver PB, the receiver will log the sender's own broadcasts. The self-filter (`msg.Source == IGC.Me`) only works when both roles are on the same PB (`mode = both`). Fix: use `mode = both` on one PB. |
| **Sender grid unloaded** | If nobody is near the sending building, the retry queue pauses. Messages resume when the grid loads again. |
| **Storage cleared on recompile** | SE clears `Storage` when a script is recompiled from the editor. Messages and queues are lost. This is an SE limitation. |

---

## Troubleshooting

| Symptom | Likely cause |
|---|---|
| LCD shows nothing | Block name doesn't contain the `lcd_tag` value (check case and brackets) |
| Cockpit screen stays blank | `lcd_surface` index is out of range — check how many screens the cockpit has |
| LCD on wrong construct | Block is on a subgrid — move it to the same grid as the receiver PB |
| Sender Echo shows "Cannot send" | PB `mode` is set to `receiver` — change to `sender` or `both` |
| Messages not arriving | Sender and receiver `channel` values don't match exactly |
| Antenna warning on receiver | Enable an antenna on the control building; the script will keep it on |
| Shortcode not expanding | Check spelling; shortcodes are case-insensitive but must match exactly (no extra spaces) |
| Messages lost when PB recompiled | Normal — Storage is cleared on recompile. This is an SE limitation. |
| CLEAR N does nothing | Index must be between 1 and the number shown on the board |
| Broadcast Controller silent | Check block name contains `bc_tag`, slot is pre-programmed, and `bc_enabled = true` |
