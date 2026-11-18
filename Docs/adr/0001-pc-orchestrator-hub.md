---
status: proposed
---

# Use a PC (Python) as the hub between the Quest, MG400 and Arduino

The Quest 3 does not talk to the MG400 or the Arduino directly. It sends the Order (a list of Cut + Color) and Teleop commands to a Python program on a PC over Wi-Fi (WebSocket/MQTT). The PC then drives the MG400 over TCP/IP (Dobot SDK: port 29999 dashboard, 30003 motion, 30004 feedback) and the Arduino over Serial (USB). The reasons: Unity on Android/Quest cannot open a Serial connection, the Dobot SDK already exists for Python, and the Packing Sequence ([ADR-0005](0005-paper-gift-bag.md)) should live in one state machine rather than be spread across several machines.

## Considered Options

- **Quest connects to the MG400 directly over TCP:** possible, but the Quest still cannot reach the Arduino, and the safety logic would sit in a headset that can drop its connection.
- **Let the MG400 command the Arduino over DI/DO:** works for simple signals, but is harder to debug and carries little data.

## Consequences

- The PC must stay on during the demo, and every device must be on the same LAN (a router is required).
- The PC is the single place that enforces the arm's safety limits, such as workspace limits, minimum Z and a speed cap.
- **Amended (2026-11-18):** Teleop goes over UDP to a separate Teleop Bridge on the PC, not over the Order link ([ADR-0017](0017-teleop-udp-bridge-relative-mapping.md)). The conveyor and bag sensor run through the MG400's I/O, and the Arduino drives only the Bag Closer ([ADR-0016](0016-mg400-io-runs-conveyor-uno-only-bag-closer.md)).
- Data from port 30004 can later feed a Digital Twin in Unity (if the group has 6 members).
