---
status: accepted
---

# Claude may make a rough layout through Unity MCP; humans finish it and save the scene

The Editor pen in `CLAUDE.md` gave all scene placement to humans. Laying out the Storefront (Display Cases, the Checkout Counter, other furniture) starts with a rough layout, and that first pass is mostly numbers: heights, spacing, walkway widths. So the Editor pen gets one exception. Claude, in the main session only, may create, place and move objects through Unity MCP to make that rough layout. Humans then adjust it to the final positions, check it in the Editor or the headset, and save the scene themselves. Claude never saves the scene.

## Considered Options

- **Keep placement human-only:** the Editor pen stays simple, but each rough layout is hand work that Claude could do from a short description.
- **Claude may place objects at any stage, still without wiring:** Claude works from numbers and screenshots and can't judge how the Storefront feels to reach and walk through in VR, so humans would end up redoing the final positions anyway.
- **Claude may also wire the Inspector:** a missing or wrong reference doesn't show in a screenshot, and the seams are designed for humans to wire.
- **Claude saves the scene itself:** a `.unity` diff is hard to review, so changes nobody has looked at could land in a commit.
- **Role sub-agents may place objects too:** they run unattended, with no human watching the Editor.

## Consequences

- Inspector wiring, prefab connections and final placement remain the Editor pen.
- Role sub-agents dispatched by `grab-ticket` stay code-only. The `coder` skill keeps its Editor pen rule and points at this exception for the main session.
- Repeated, precise placement (a row of Gem Models in a Display Case) stays code: a component Claude writes and tests, not MCP moves.
