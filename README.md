# SE-SCRIPTS

A collection of C# scripts for Space Engineers Programmable Blocks. Scripts are pasted directly into the in-game editor — no build system, no external tooling. All compilation and testing happens inside the game.

---

## Scripts

### [InventoryMonitor](InventoryMonitor/InventoryMonitor.md)
Light-centric inventory monitor. Watches item counts and controls a lighting block to signal stock levels.

### [InventoryMonitor2](InventoryMonitor2/InventoryMonitor2.md)
Container-centric inventory monitor with support for multiple items per container and two action types: controlling a light block or triggering a timer block on state changes.

Includes a set of [in-game datapads](InventoryMonitor2/datapads/) for quick reference while playing.

---

## Reference

- [SE-SCRIPTING-RULES.md](SE-SCRIPTING-RULES.md) — Verified rules for PB scripting: whitelisted APIs, known pitfalls, Custom Data conventions, and a ready-to-paste script skeleton.
