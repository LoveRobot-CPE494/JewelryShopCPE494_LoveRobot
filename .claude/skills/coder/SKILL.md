---
name: coder
description: Standing rules for writing or fixing code in this monorepo's `Unity/`, `Dobot/` or `UNO/` folders, with per-folder know-how. Use when a dispatched sub-agent edits code, or before any code change in those folders.
---

# coder

The rules every code change in this repo runs under. The dispatch prompt adds the ticket, the
definition of done, and the report format.

**Canary:** begin your final report with the line `coder: loaded (<area>)`. The dispatcher reads
a report without it as a run where these rules never arrived.

## 1. Pick your area

Your **area** is the one folder the ticket lives in. Read its know-how file before writing code:

| Area | Folder | Know-how |
|---|---|---|
| `unity` | `Unity/JewelryShop/` (Meta Quest 3, C#) | [UNITY.md](UNITY.md) |
| `dobot` | `Dobot/` (PC orchestrator, Python, MG400) | [DOBOT.md](DOBOT.md) |
| `uno` | `UNO/` (Arduino UNO firmware) | [UNO.md](UNO.md) |

Stay inside your area. When the ticket needs a change in a second area (e.g. a new message in the
Quest ↔ PC protocol), finish your side, then report the other side as a follow-up ticket.

## 2. Standing rules

- **Read the decisions first:** every ADR in `Docs/adr/` that touches your area. Respect them; when
  one blocks the ticket, stop and report the conflict.
- **Git is read-only.** `status`, `diff`, `log` only: the human commits, branches, and opens PRs.
- **The human holds three pens**, and you hand each back as an explicit human task:
  - **GitHub pen:** commit, push, PR, issues.
  - **Editor pen:** Inspector wiring, placing or connecting scene/prefab elements. You author the
    seam (a `[SerializeField]`, a named event) with a safe fallback when unwired.
  - **Robot pen:** running code against the real MG400 or a real Arduino. You test against a
    **mock** (fake TCP server, fake serial port, host-compiled logic); the human runs it on hardware.
- **Loud fallbacks.** An unwired seam or a missing device logs a warning naming what is missing.
  Catch blocks log with context or rethrow.
- **Safety is a feature** (brief §2, graded). Motion and actuator code carries its limits in code:
  workspace bounds, a minimum Z, speed caps, a deadman, an E-stop path. A change that removes or
  loosens a limit is reported as such.
- **Review every change.** Run `code-review` on your diff before you report, whatever its size,
  and fix its findings with tests green. A report without review findings is not done.
- Any agent you start passes `effort: medium`.
