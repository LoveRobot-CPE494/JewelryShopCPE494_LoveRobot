---
status: accepted
---

# Checkout is Approved only after the Hub accepts the Order

The Payment Card has no credit limit and asks for no PIN, so nothing about the card itself can fail a Checkout. The only thing that can is the Order failing to reach the Packing Station. The Card Terminal therefore shows Approved only after the Hub has accepted the Order. If the Hub cannot be reached, the Card Terminal shows a decline ("terminal offline, try again"; wording to revisit with [#75](https://github.com/LoveRobot-CPE494/JewelryShopCPE494_LoveRobot/issues/75)), the Order stays in the Order Tray, and the Stock Count is untouched. Approved always means the Hub has accepted this Order for packing.

## Considered Options

- **Approve at once and queue the Order for the Hub:** a Checkout that never fails in front of the Customer, but if the Hub is down the Customer sees Approved and a Receipt for an Order that is never packed, which is worse in a graded demo than an honest decline.
- **A credit limit on the Payment Card that can decline an Order:** more like a real card, but it shows a failure path that has nothing to do with our system and creates a dead end during the demo.

## Consequences

- Checkout waits on a reply from the Hub, so the Card Terminal's "Processing…" step covers that wait as well as the animation.
- The Storefront ↔ Hub integration ([Q-02, #75](https://github.com/LoveRobot-CPE494/JewelryShopCPE494_LoveRobot/issues/75)) must give the Quest an acceptance reply for each Order, not fire-and-forget.
- Until that integration is built, Checkout hands the Order to an order submitter whose local stand-in accepts every Order, so the Storefront can be built and demoed without the Hub.
- The Stock Count drops ([ADR-0011](0011-staff-entered-stock-count.md)) only after an Approved Checkout.
