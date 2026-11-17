---
status: accepted
---

# The MG400 uses a suction cup, not a gripper

The MG400's end effector is a suction cup + air pump/solenoid valve, switched on and off directly through the MG400's DO. The reasons: the gems are small and slippery, and both the Round and Emerald cuts have a flat table face on top that can be sucked. The main advantage is that J4 does not need to rotate to line up with the Emerald's sides, which matters a lot when picking up a dropped gem (item 4b), where the piece's orientation is random.

## Considered Options

- **Electric gripper (e.g. DH-Robotics PGE):** accurate, but expensive, and the Emerald's orientation must be aligned before gripping.
- **3D-printed servo gripper wired to the Arduino:** cheap, but grips small slippery pieces unreliably and needs timing control across boards.

## Consequences

- The suction cup must be smaller than the table face of the smallest gem.
- A gem that lands upside down or on its side (table face not facing up) may not be suckable; this must be tested and written up in the report.
