---
status: accepted
---

# Staff Call is live: a real Staff drives the Staff Avatar from a Staff Console in the same Unity project

Amended by [ADR-0022](0022-cheryll-exists-from-link-connect.md): the Staff Avatar is visible from link connect, not only after Answer.

Supersedes [ADR-0006](0006-prerecorded-staff-clip.md). Brief item 4a.iii awards 5 points for "actually talking with the staff", and a pre-recorded clip risks losing them, so a real Staff now serves the Customer live from a Staff Console. The Staff Console is a second scene in `Unity/JewelryShop/` with its own Windows build. It is not a separate Unity project, because the Staff Console and the Quest build both need the Staff Avatar, the Gem Models and the messages exchanged between them.

## Considered Options

- **Separate Unity project** (`Unity/StaffConsole/`): a cleaner split, but the shared assets and message definitions would exist twice and drift apart.
- **Keep the pre-recorded clip** (ADR-0006): the least work, but the 4a.iii points depend on live talk.

## Consequences

- The Quest build must leave the Staff Console scene out, and the Windows build must leave out the XR rig.
- The voice technology and the transport are open for [#13](https://github.com/LoveRobot-CPE494/JewelryShopCPE494_LoveRobot/issues/13), as is whether the Staff Console uses ADR-0008's networking Facade. A direct Quest ↔ Staff Console link would be a second path next to the Hub, so it would partly depart from ADR-0001. Settled in [ADR-0010](0010-staff-call-direct-netcode-link.md).
