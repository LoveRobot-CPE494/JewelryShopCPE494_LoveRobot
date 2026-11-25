---
status: accepted
---

# The Quest finds the Staff Console's PC by one Host Address: typed in the headset or found by UDP Host Discovery

Amended by [ADR-0024](0024-robot-panel-presets-and-robot-status.md) (2026-11-25): the MG400 is on its own Robot PC. Teleop reads a separate **Robot Address**, not the Host Address; Settings gains a Robot Address field and a Robot line showing whether Robot Status arrived (the Teleop Bridge now replies, which the option "Hub replies to Teleop" below left out); the Robot PC broadcasts its own beacon (role `robot`) and its firewall allows UDP 9000 in, 9001 out and the discovery port out. Where the text below says Teleop reads the Host Address, read Robot Address.

Decided (grill, 2026-11-25). The Staff Link ([ADR-0010](0010-staff-call-direct-netcode-link.md), Netcode, port 7777) and Teleop ([ADR-0017](0017-teleop-udp-bridge-relative-mapping.md), UDP 9000) each held their own PC address as an Inspector field (`StaffLinkBehaviour._serverAddress`, `MG400Teleop.pcIp`), so a new Wi-Fi meant two edits and a rebuild.

- **One PC is assumed.** The Staff Console, the Hub and the Teleop Bridge run on the same PC, which is the default in [ADR-0010](0010-staff-call-direct-netcode-link.md) and [ADR-0017](0017-teleop-udp-bridge-relative-mapping.md). If one of them moves to another PC, the shared Host Address no longer fits and this decision is revisited.
  **Amended (2026-11-25, [ADR-0024](0024-robot-panel-presets-and-robot-status.md)):** the MG400 is wired to its own **Robot PC**, which runs the Teleop Bridge. The Quest now holds two addresses, stored and typed the same way: the **Host Address** (Staff Console, Staff Link 7777) and the **Robot Address** (Robot PC, Teleop 9000 and the Robot Panel). The Teleop Bridge also broadcasts a beacon on the same discovery port, told apart by its role (`robot`, where the Staff Console's is `staff`). With Automatic IP in LAN ticked, each address is filled from the beacon of its role; the typed one stays the fallback.
- **One Host Address on the Quest.** The Staff Link and Teleop both read it. It is a data-only ScriptableObject ([ADR-0018](0018-staff-config-data-only-scriptable-objects.md)) holding the defaults, plus the Customer's saved choice in `PlayerPrefs`. The Order link (Q-02) can read it later; it is not built here.
- **Settings screen in the headset.** An IP field (VR numeric keypad) and an **Automatic IP in LAN** checkbox. Ticked: use the address Host Discovery found. Unticked: use the typed address. A typed address is always the fallback.
- **Host Discovery.** The Staff Console (the host, [ADR-0010](0010-staff-call-direct-netcode-link.md)) broadcasts a small UDP beacon in the LAN every second or so, carrying its IPv4 address and the Staff Link port. The Quest listens, and the latest valid beacon sets the discovered address. A beacon that is a few seconds old no longer counts, so a switched-off Staff Console is not remembered. With no fresh beacon, Settings says "Host not found" and the typed address stays in force.
- **The Staff Console still listens on `0.0.0.0`** and shows its own LAN IP on screen, so a person can read it out and type it in.
- **Settings shows the state.** Staff Link: connected or not (real state). Teleop: only "sent", because UDP gives no acknowledgement. A failure message points to the firewall (UDP 7777, 9000 and the discovery port).

## Considered Options

- **Typed IP only:** least code, but the Customer must know the PC's IP on every network.
- **Automatic means `127.0.0.1` on Quest Link only:** useless on a standalone headset, which is the real demo.
- **Hub replies to Teleop so Settings can confirm it:** left out for now; the Teleop Bridge stays as it is.

## Consequences

- New code on both ends: a beacon sender on the Staff Console, a listener on the Quest, the Settings scene and the shared config. Networking stays behind the Facade ([ADR-0008](0008-approved-pattern-menu.md)).
- Some networks block UDP broadcast, so the typed address is a permanent fallback, not a debug aid.
- Replaces the per-class address fields in the Staff Link and Teleop; [ADR-0010](0010-staff-call-direct-netcode-link.md) and [ADR-0017](0017-teleop-udp-bridge-relative-mapping.md) point here.
- The Quest build needs the Android multicast lock (and Internet Access: Require) for broadcast to arrive; the PC firewall must allow the discovery port.
- `MG400Teleop.cs` must first move into the Unity project (it is in `Dobot/teleop/unity/`) before it can read the Host Address.
