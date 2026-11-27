---
status: accepted
---

# Pack into the paper bag the instructor provided, not a folded box

Brief item 5 says "a pre-folded paper box" and "close the box lid", but the container the instructor actually provided is a paper bag. The system is therefore designed around the bag: place the bag on the conveyor → the MG400 puts every Gem Piece of the Order into the bag → the Bag Closer (a servo hand) closes the bag mouth → send it to the end of the conveyor. This is recorded because someone reading only the brief would expect a box with a lid.

## Consequences

- There is no lid-pushing mechanism; the Bag Closer is used instead.
- The bag mouth must be held open while the arm places items, and the bag must stand steady on the conveyor; this must be tested.
- **The ribbon is attached by hand beforehand** (Amended 2026-11-27, [Q-01 #15](https://github.com/LoveRobot-CPE494/JewelryShopCPE494_LoveRobot/issues/15)): brief item 5 requires "attach a ribbon", and the instructor allows a person to attach it to the Gift Bag before the bag goes onto the conveyor. The robot attaches no ribbon, so the Packing Sequence has no ribbon step and no ribbon mechanism is designed.
