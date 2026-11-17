---
status: accepted
---

# Stock Count is entered by the Staff, not sensed by the Packing Station

The Storefront refuses to put more of one Cut + Color into an Order than its Stock Count allows, but nothing on the Packing Station counts the Gem Pieces in a Stock Slot. The Stock Count is a number people enter. The Staff are the ones who put Gem Pieces on sale, so they are the ones who know and enter how many there are. For now, every Cut + Color starts from a fixed number stored with its Gem Details. After a successful Checkout the Stock Count drops by the number of Gems bought.

## Considered Options

- **The Hub tracks stock:** the Hub counts picks for each Stock Slot and reports to the Storefront. This was rejected: it adds a stock protocol to the Hub and a reset step after each refill, and its count would still be wrong after a Dropped Gem or a manual refill. A Stock Count entered by people has the same weakness, with far less code.

## Consequences

- A Stock Count is only as true as its last entry. If it says more than the Stock Slot really holds, the Packing Sequence finds an empty Stock Slot. How it handles that is outside this decision.
- Letting the Staff edit the Stock Count live from the Staff Console is an open question ([#83](https://github.com/LoveRobot-CPE494/JewelryShopCPE494_LoveRobot/issues/83)). It needs a Staff Console → Quest message, which [ADR-0010](0010-staff-call-direct-netcode-link.md)'s link carries none of yet.
