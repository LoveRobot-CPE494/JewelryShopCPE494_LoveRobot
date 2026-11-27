---
status: accepted
---

# The MG400 I/O runs the conveyor and bag sensor; the UNO drives only the Bag Closer

Amended (2026-11-25, [ADR-0027](0027-three-pcs-quest-staff-robot.md) and [ADR-0026](0026-teleop-controls-engage-toggle-pump-hand-menus.md)): the wiring is conveyor DO1 and Bag Sensor DI2 (ADR-0027); the pump is wired to DO9 (suck) and DO10 (blow) (ADR-0026), not DO1. The Teleop code still drives DO1 until ADR-0026 is built.

Supersedes [ADR-0004](0004-arduino-owns-conveyor-and-packaging.md).

The real wiring (2026-11-18) is different from ADR-0004. The duties are now split like this:

- **MG400:** moves the arm, switches the suction cup (DO1, [ADR-0002](0002-suction-cup-end-effector.md)), switches the conveyor motor through one of its DOs, and reads the sensor that stops the Gift Bag in front of the arm through one of its DIs.
- **Arduino UNO:** drives only the Bag Closer servo ([ADR-0005](0005-paper-gift-bag.md)).

Python on the PC sends every command, as in [ADR-0001](0001-pc-orchestrator-hub.md). The MG400 and the UNO still never command each other.

## Considered Options

- **Keep ADR-0004 (the UNO runs the conveyor and the sensor):** the conveyor and sensor are already wired to the MG400 controller, and its DI/DO are reachable over the same TCP link the Hub already uses for the arm.

## Consequences

- Conveyor and sensor code belongs in `Dobot/`, not `UNO/`. The UNO firmware only needs a Serial command for the servo.
- The Packing Sequence uses one TCP link for the conveyor, the sensor and the arm, so there is no cross-board timing between them.
- The servo still needs its own power supply; do not draw 5V from the UNO. The real E-stop must still cut power to the conveyor and the servo.
