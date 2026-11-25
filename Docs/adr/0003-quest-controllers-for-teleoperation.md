---
status: accepted
---

# Teleoperate the arm with the Quest 3 controllers, not hand tracking

In remote maintenance mode (item 4b) the operator uses the controllers that ship with the Quest 3, because hand tracking is hard to use and unstable. Controller tracking is more accurate and has physical buttons for the deadman and the emergency stop. The controller position is still the operator's hand position, so it still meets the criterion "the pick position follows the hand position" (10 points).

| Controller | MG400 |
|---|---|
| Position (x, y, z) | End-effector position, moved by the same distance since Grip was pressed ([ADR-0017](0017-teleop-udp-bridge-relative-mapping.md)) |
| Hold Grip | Enables the arm to follow; release and it stops (deadman) |
| Trigger | Suck / release |
| A / B | Home / software stop (not a replacement for the real E-stop) |
| Thumbstick | Nudge up and down in small steps |

**Amended (2026-11-18):** the Position and A / B rows changed to match the working Teleop ([ADR-0017](0017-teleop-udp-bridge-relative-mapping.md)): the arm moves by the same distance as the Controller since Grip was pressed, with no calibration, and B is a software stop.

## Consequences

- **Decided:** item 4a.i (storefront) says "the view changes with the movement of the buyer's **hand**". The team reads "hand" as the Quest 3 controller, so the Storefront also uses controllers.
- After a Dropped Gem is sucked up, the Maintenance Operator uses Teleop to put it back into the correct Stock Slot themselves. The system does not need to know that piece's Cut + Color (automatic put-back may be added later if time allows).

**Amended (2026-11-22):** the Quest 3 has both Controllers, not only the right one ([ADR-0021](0021-two-controllers-in-the-storefront.md)). The table above is for the right Controller. The left Controller plays no part in Teleop. **Amended (2026-11-25, [ADR-0024](0024-robot-panel-presets-and-robot-status.md)):** the left Controller carries the Robot Panel in the Teleop scene (its menu button shows or hides it); it still does not drive the arm.
