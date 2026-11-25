---
status: accepted
---

# Staff Call travels directly between the Quest and the Staff Console over Netcode for GameObjects, not through the Hub

Amended by [ADR-0022](0022-cheryll-exists-from-link-connect.md): the link relays Cheryll for as long as it is connected, not only during a call.

Amended by [ADR-0023](0023-lan-host-address-and-udp-discovery.md): the Quest takes the Staff Console's address from one shared Host Address (typed or found by Host Discovery), not from its own Inspector field.

[ADR-0001](0001-pc-orchestrator-hub.md) routes everything the Quest sends through the Python Hub. Staff Call is the exception: the Quest and the Staff Console talk directly over the LAN with Unity Netcode for GameObjects, with the Staff Console as host (running on the Hub PC by default) and the Quest connecting to its IP. Both ends are Unity builds of the same project ([ADR-0009](0009-live-staff-via-staff-console.md)), so the Staff Avatar's and Customer Avatar's positions and animations stay in sync with almost no code of our own, and live voice avoids an extra hop through Python. The Hub keeps sole charge of the Packing Station.

## Considered Options

- **Relay everything through the Hub** (keep ADR-0001 pure): one network path, but the Hub would need its own relay for avatar state and voice, which is more code and more latency. This stays the fallback if the direct link fails in the demo room.

## Consequences

- The Quest now holds two links: one to the Hub and one to the Staff Console.
- [ADR-0008](0008-approved-pattern-menu.md)'s networking Facade owns both links, so no other class references Netcode.
- Live voice rides on this link, built in-house (Opus via Concentus) rather than a cloud or paid voice service; see [#64](https://github.com/LoveRobot-CPE494/JewelryShopCPE494_LoveRobot/issues/64).
