---
status: accepted
---

# Cheryll exists from link connect; Answer only warps her to the Staff Door

Decided (grill, 2026-11-22), part of [#64](https://github.com/LoveRobot-CPE494/JewelryShopCPE494_LoveRobot/issues/64).

- **The Customer can see Cheryll before the call.** She stays in the Staff Room, and the Customer can see in from the Storefront (a window or opening; placing it is Editor work).
- **Her networked copy spawns when the Customer's link to the Staff Console connects and despawns only when the link drops.** No Staff Console connected means no Cheryll.
- **Answer no longer spawns her.** It warps her (the host-side Staff Cheryll and her copy) to the Staff Door with the existing `Warp` helper. Walking from the door to the Customer is not built yet (#68); until then Answer only warps her.
- **Hang-up warps her back to her Staff Room spot** instead of despawning her.
- **Before Answer** the Customer sees her live movement, gestures and face through `CheryllNetworkAvatar`, as today; no new relay logic.
- `StaffCallSession.IsCheryllVisible` still means "Cheryll is serving me" (StaffComing / InCall); nothing used it to hide her renderer.

## Consequences

- Changes [ADR-0009](0009-live-staff-via-staff-console.md): the Staff Avatar is no longer an on-demand appearance; the Staff Console serves a Customer who can already see her.
- Changes [ADR-0010](0010-staff-call-direct-netcode-link.md): the link carries Cheryll's relay (movement, gestures, face) for as long as it is connected, not only during a call.
- `StaffLinkBehaviour` (host) spawns and despawns the copy on the link's connection event; `StaffCallSession` only calls `WarpToDoor` (Answer) and `WarpToRoom` (call ended).
- The `GLOSSARY.md` **Staff Room** entry no longer says "out of the Customer's sight".
