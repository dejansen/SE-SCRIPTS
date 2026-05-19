# SE-SCRIPTS

A collection of C# scripts for Space Engineers Programmable Blocks. Scripts are pasted directly into the in-game editor — no build system, no external tooling. All compilation and testing happens inside the game.

---

## Scripts

### [InventoryMonitor](InventoryMonitor/InventoryMonitor.md)
Light-centric inventory monitor. Watches item counts and controls a lighting block to signal stock levels.

### [InventoryMonitor2](InventoryMonitor2/InventoryMonitor2.md)
Container-centric inventory monitor with support for multiple items per container and two action types: controlling a light block or triggering a timer block on state changes.

Includes a set of [in-game datapads](InventoryMonitor2/datapads/) for quick reference while playing.

### [RedAlert](RedAlert/RedAlert.md)
Saves all settings of a named lighting group and forces every light to solid red on `activate`. Restores each light to its original colour, intensity, radius, and blink settings on `deactivate`. State persists across game saves and PB reboots.

### R.O.S — Rev Operating System

Base station management system driving three LCD panels simultaneously: dock connector status, a proximity/approach scanner with runway lights, and a live miner fleet tracker.

- [R.O.S — Dock Control / Proximity Scanner / Fleet Ops](R.O.S/ROS-DockingMonitor.md) — main base station script
- [R.O.S — Fleet Broadcast](R.O.S/ROS-MinerBroadcast.md) — companion script installed on each miner ship; broadcasts telemetry over IGC

---

## Reference

- [SE-SCRIPTING-RULES.md](SE-SCRIPTING-RULES.md) — Verified rules for PB scripting: whitelisted APIs, known pitfalls, Custom Data conventions, and a ready-to-paste script skeleton.
