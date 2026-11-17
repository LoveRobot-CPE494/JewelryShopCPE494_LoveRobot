---
status: accepted
---

# The Gem Details Panel is the extra feature, opened with A on a held Gem Model

Brief item 4a.iv awards 5 points, all or nothing, for one extra feature that increases sales or improves the Customer's experience. Ours is the Gem Details Panel: while holding a Gem Model, the Customer presses A on the Controller to open it beside the Gem, and presses A again or lets go of the Gem Model to close it. It opens with a "wow" effect and a one-time hint, so the examiners notice it during the 9 Dec demo; the details belong to the 004 spec.

## Considered Options

- **Staff Avatar packing animation after Checkout** ([Q-05, #86](https://github.com/LoveRobot-CPE494/JewelryShopCPE494_LoveRobot/issues/86)): more spectacular, but it depends on the Staff Call work and the Packing Sequence timing.
- **Open the Gem Details Panel automatically on grab:** nothing for the examiners to miss, but it covers the Gem while the Customer rotates it for 4a.i, and it stops reading as a separate feature.
- **Point and click from a distance:** needs a ray interactor on the one-Controller rig for the same 5 points. Grab and trigger are already used by the Order Tray ([#78](https://github.com/LoveRobot-CPE494/JewelryShopCPE494_LoveRobot/issues/78)), so A is the free input.
- **Staff Avatar gestures:** these belong to 4a.iii (Staff Call), already ticketed as [#70](https://github.com/LoveRobot-CPE494/JewelryShopCPE494_LoveRobot/issues/70), so they cannot count again as 4a.iv.

## Consequences

- The Gem Details Panel reads the catalog entries from [#76](https://github.com/LoveRobot-CPE494/JewelryShopCPE494_LoveRobot/issues/76) and adds no data of its own.
- Q-05 (#86) is no longer a candidate for 4a.iv. If it is built, it is polish.
- The demo script must tell the examiners to pick up a Gem Model and press A, and must name the Gem Details Panel as the 4a.iv feature.
