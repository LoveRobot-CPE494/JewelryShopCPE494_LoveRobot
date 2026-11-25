---
status: accepted
---

# Teleop: the Quest sends UDP to a Teleop Bridge on the PC, and the arm follows relative moves

Amended by [ADR-0023](0023-lan-host-address-and-udp-discovery.md): Teleop sends to the shared Host Address instead of its own PC IP field.

Amended by [ADR-0024](0024-robot-panel-presets-and-robot-status.md): the Teleop Bridge runs on its own Robot PC and Teleop sends to the Robot Address; the bridge also takes Robot Panel commands, sends Robot Status back on UDP 9001, and latches Stop for every motion (closing the `joint`/`calibrate` gap below); it moves into `Dobot/src/hub`.

Amended by [ADR-0026](0026-teleop-controls-engage-toggle-pump-hand-menus.md): A toggles Engage instead of the Grip-held Deadman, forward follows the head's yaw at Engage, Scale (up to 1×) is sent with Engage, and suction becomes a two-mode `pump` message that works only while Engaged.

A teammate's working Teleop (2026-11-18) is now in the repo. On the Quest, `MG400Teleop.cs` (kept in `Dobot/teleop/unity/` until it is brought into the Unity project with its own Teleop scene) reads the right Controller. It sends the Controller's position and buttons as one JSON message per UDP packet to port 9000 on the PC. `mg400_bridge.py` (`Dobot/teleop/`) maps the position to MG400 coordinates, clamps it to the safe workspace and drives the arm over TCP through `mg400_control.py`. `pc_sender.py` sends the same messages from the PC, so the bridge can be tested without the headset. The test guide is `Docs/teleop/teleop-test-guide-th.pdf`.

Decided:

- **UDP, not WebSocket/MQTT, for Teleop.** Only the newest position matters, so a lost packet does no harm. The bridge keeps only the latest pose and ignores one older than 0.5 s, so it sends no new move when packets stop (the move already sent still finishes). `stop` is sent three times and runs at once, even while the arm is moving. The Order link (Q-02) is still open; this ADR covers only Teleop.
- **Relative mapping, no calibration (this answers the calibration part of Q-04).** Each time Grip is pressed, the Controller's position right then is taken as the arm's position right then (`engage`), so the arm never jumps. After that, 1 cm of Controller movement is 1 cm of arm movement in X, Y and Z. The head angle R stays where it was.
- **The bridge is the only safety point** ([ADR-0001](0001-pc-orchestrator-hub.md)). It clamps reach to 170–420 mm and Z to `Z_MIN_MM`–150 mm, filters jitter (3 mm deadband, smoothing 0.5), sends at most 10 moves per second, and ignores poses and `home` after `stop` until Grip is pressed again. Known gap: the test-only `joint` and `calibrate` messages still move the arm after `stop`.
- **Teleop runs in its own scene, not the Storefront scene.** The A button is Home in Teleop and opens the Gem Details Panel in the Storefront ([ADR-0013](0013-gem-details-extra-feature.md)).

## Considered Options

- **Send Teleop through the Hub's Order link (WebSocket/MQTT):** reliable delivery queues stale poses, and that link is not decided yet (Q-02).
- **Absolute mapping after calibrating the headset space to the table (a marker on the table):** harder to set up, and the arm jumps when the mapping is off.

## Consequences

- The Teleop Bridge runs as its own program on the PC, next to the Hub. Merging it into `src/hub` (and up to the Python standard) is later work, so `Dobot/teleop/` is left out of ruff for now.
- `Z_MIN_MM` must be calibrated on the real arm (#49) before the bridge runs without `--dry-run`.
- Button B is a software stop over Wi-Fi. It does not replace the real E-stop, which a person who is not wearing the headset must hold during every real-arm test.
- The teammate's code does not follow [ADR-0008](0008-approved-pattern-menu.md) yet: `MG400Teleop.cs` builds JSON strings by hand instead of one typed message per command, and the reach limit is set twice (420 mm in the bridge, 440 mm in `mg400_control.py`). It has no tests for the safety rules. All of this is part of the move into `src/hub`.
- To build for the headset, Android Internet Access must be set to Require, and the PC firewall must allow UDP 9000.
