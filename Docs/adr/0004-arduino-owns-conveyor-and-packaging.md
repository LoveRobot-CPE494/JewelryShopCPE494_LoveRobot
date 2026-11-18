---
status: superseded by ADR-0016
---

# The Arduino UNO runs the conveyor and packaging; the MG400 only picks and places

Hardware duties are split as follows:
- **MG400:** moves the arm + suck/release (through its own DO)
- **Arduino UNO:** the conveyor motor, the sensor that stops the bag in front of the arm, and the Bag Closer (a servo that closes the bag mouth, see [ADR-0005](0005-paper-gift-bag.md))

The two never command each other; everything goes through the PC per [ADR-0001](0001-pc-orchestrator-hub.md). The reasons: the parts can be tested one system at a time (meets the criterion "Working of individual system"), and suction does not have to wait on timing across boards.

## Consequences

- The servo and motor need a separate power supply; do not draw 5V from the UNO. With several servos, consider a PCA9685.
- There must be an E-stop button that really cuts power to the conveyor and servo (item 2, safety).
