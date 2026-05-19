# HERMES — Intergrid Messaging Service

**Version 1.1** — ⚠️ TESTING PHASE — Not yet verified in-game. Expect bugs.

A single-script intergrid alert system for Space Engineers. Buildings broadcast alerts to a central control room, where they appear on a large LCD dispatch board.

---

## Concept

Each building in your town runs this script in **sender** mode. When an Event Controller detects something (low hydrogen, low power, etc.), it triggers a Timer Block, which runs the Programmable Block with an alert message as the argument. The script broadcasts that message over an antenna.

The central control building runs the same script in **receiver** mode. It listens on the same channel and displays all incoming alerts on a tagged LCD panel — your office dispatch board.

One script, two roles. No extra files.

---

## Features

- **One script** — mode set via Custom Data (`sender`, `receiver`, or `both`)
- **Preprogrammed shortcodes** — type `HYDROGEN_LOW` and it broadcasts "Hydrogen tanks critically low"
- **Freeform messages** — any text not matching a shortcode is sent as-is
- **Antenna auto-management** — receiver keeps antenna on; sender turns it on to transmit and off again
- **Timestamped dispatch board** — LCD shows all alerts with time received, newest first
- **Per-message channel override** — prefix any argument with `@channel:` to route it to a different channel without changing config
- **Persistent log** — messages survive PB reboots via `Storage`
- **CLEAR commands** — dismiss one alert or all at once
- **Optional ACK mode** — sender retries until receiver confirms delivery

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

## Naming the Dispatch LCD

The script finds LCDs by looking for your `lcd_tag` value anywhere in the block's name.

**Rules:**
- The tag is **case-sensitive**. `[HERMES]` and `[hermes]` are different.
- The LCD must be on the **same construct** as the receiver PB (not a subgrid).
- Multiple LCDs with the tag all show the same content.

**Valid name examples** (default tag `[HERMES]`):
```
[HERMES] Dispatch Board
Main Screen [HERMES]
[HERMES]
Alerts [HERMES] Panel
```

**Invalid** (wrong tag, wrong case):
```
[hermes] Dispatch     ← lowercase, won't match
HERMES Board          ← no brackets, won't match
[HERMES_BOARD]        ← extra characters inside brackets
```

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
- Argument: `@[PLAYERA]:HYDROGEN_LOW`
- Broadcasts *"Hydrogen tanks critically low"* on channel `[PLAYERA]` instead of the default

The `@channel:` prefix is optional and per-message. The configured `channel` in Custom Data remains the default for all messages without a prefix. Shortcodes work normally after the `:`.

This lets one sender PB reach multiple receivers on different channels — for example, town-wide alerts on `HERMES` and direct messages to a player's ship on `[PLAYERA]` — all from the same Timer Block setup, just with different arguments per event.

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

1. Configure the relay PB with `mode = both` and `channel = [PLAYERA]`.
2. Buildings send player-addressed messages using `@[PLAYERA]:HYDROGEN_LOW`.  
   The relay receives and stores them like a normal receiver.
3. Place a **Sensor Block** or use a **Connector** event on the landing pad to detect arrival.
4. Wire the arrival event to a **Timer Block** → **Run** the relay PB with argument `FORWARD`.
5. `FORWARD` re-broadcasts all stored messages on `[PLAYERA]`.  
   Player A's ship receiver (also on `[PLAYERA]`) picks them up.

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

```
; HERMES Configuration
; Lines starting with ; are comments and are ignored.

mode          = receiver    ; sender | receiver | both
channel       = HERMES      ; Must match on all senders and the receiver
lcd_tag       = [HERMES]    ; Any LCD with this in its name shows the dispatch board
max_messages  = 20          ; How many alerts to keep (oldest are dropped)
ack           = false       ; true = enable delivery confirmation + retry queue
retry_seconds = 30          ; Seconds between retransmission attempts (ack mode only)
max_retries   = 0           ; Max retries before dropping (0 = retry forever)
```

### `mode`
- `sender` — responds to run arguments only; no polling loop
- `receiver` — polls IGC and updates the LCD; handles CLEAR commands
- `both` — same PB acts as sender and receiver (e.g. a relay building)

### `ack` — delivery confirmation (optional)

When `ack = true`:
- The sender queues each message and retransmits every `retry_seconds` until the receiver confirms.
- The receiver sends a unicast acknowledgment back to the sender's IGC address.
- Once confirmed, the message is removed from the retry queue.
- The queue survives PB reboots via `Storage`.

This is useful when buildings or the control room may be unloaded or offline. With `ack = false` (default), a message sent while the receiver is off is lost.

---

## Dispatch Board Layout

```
 HERMES DISPATCH
 2026-05-19  14:32
══════════════════════════════════════════════════
 # 1  [14:32]  Hydro Plant      Hydrogen tanks critically low
 # 2  [14:15]  Power Station    Battery power critically low
 # 3  [09:44]  Refinery         Ice supply low
══════════════════════════════════════════════════
 Run with CLEAR or CLEAR N to dismiss
```

Set the LCD to **Monospace** font for proper column alignment. The script sets `ContentType = TEXT_AND_IMAGE` automatically; it does not change your font size setting.

---

## PB Surface Display

The small LCD on the Programmable Block's face shows live status:

```
══════════════════
   * H E R M E S *
══════════════════
 Intergrid Comms
      v1.0
══════════════════
Mode: RECEIVER
Chan: HERMES
Msgs: 3 / 20
 Ant: ON
```

---

## Troubleshooting

| Symptom | Likely cause |
|---|---|
| LCD shows nothing | LCD name doesn't contain the `lcd_tag` value (check case and brackets) |
| LCD on wrong construct | LCD is on a subgrid — move it to the same grid as the receiver PB |
| Sender Echo shows "Cannot send" | PB `mode` is set to `receiver` — change to `sender` or `both` |
| Messages not arriving | Sender and receiver `channel` values don't match exactly |
| Antenna warning on receiver | Enable an antenna on the control building; the script will keep it on |
| Shortcode not expanding | Check spelling; shortcodes are case-insensitive but must match exactly (no extra spaces) |
| Messages lost when PB recompiled | Normal — Storage is cleared on recompile. This is an SE limitation. |
| CLEAR N does nothing | Index must be between 1 and the number shown on the board |
