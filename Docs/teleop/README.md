# Teleop (Quest side)

The Quest script is `Unity/JewelryShop/Assets/Scripts/JewelryShop/Teleop/MG400Teleop.cs`
(moved from `Dobot/teleop/unity/`, 016-04). It sends UDP to the **Robot Address** on the Config's
Teleop port (9000): with Automatic IP in LAN on (the default), the address in the Teleop
Bridge's `robot` beacon; otherwise, or with no fresh beacon, the typed or default one. It
follows a change of the Robot Address without a restart. `HasSentRecently` says a packet left in the last second
(UDP has no acknowledgement). Controls and wire format are unchanged ([ADR-0017](../adr/0017-teleop-udp-bridge-relative-mapping.md)).

## Build setup

- Android **Internet Access** must be set to **Require** (Player Settings > Other Settings).
- The Robot PC firewall must allow **UDP 9000**.

## Editor tasks (human)

- Create the Teleop scene (XR Origin, Right Controller, a GameObject with `MG400Teleop`).
- Wire `Host Address Config` on `MG400Teleop` (and optionally Right Controller and Status Text).
- On-device check: #212.

Test guide (Thai): [teleop-test-guide-th.pdf](teleop-test-guide-th.pdf).
