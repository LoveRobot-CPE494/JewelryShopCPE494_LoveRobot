---
status: accepted
---

# Teleoperate the arm with the Quest 3 controllers, not hand tracking

In remote maintenance mode (item 4b) the operator uses the controllers that ship with the Quest 3, because hand tracking is hard to use and unstable. Controller tracking is more accurate and has physical buttons for the deadman and the emergency stop. The controller position is still the operator's hand position, so it still meets the criterion "the pick position follows the hand position" (10 points).

| Controller | MG400 |
|---|---|
| Position (x, y, z) | End-effector position (after calibration) |
| Hold Grip | Enables the arm to follow; release and it stops (deadman) |
| Trigger | Suck / release |
| A / B | Home / emergency stop |
| Thumbstick | Nudge up and down in small steps |

## Consequences

- **Decided:** item 4a.i (storefront) says "the view changes with the movement of the buyer's **hand**". The team reads "hand" as the Quest 3 controller, so the Storefront also uses controllers.
- After a Dropped Gem is sucked up, the Maintenance Operator uses Teleop to put it back into the correct Stock Slot themselves. The system does not need to know that piece's Cut + Color (automatic put-back may be added later if time allows).
