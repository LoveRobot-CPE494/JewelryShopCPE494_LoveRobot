---
status: accepted
---

# Robot Panel: button commands and Preset Actions over the Teleop link, with Robot Status sent back

Amended (2026-11-27): reach narrowed to the measured 225–365 mm (`probe_reach`, #49).

Amended (2026-11-25, [ADR-0027](0027-three-pcs-quest-staff-robot.md)): the Robot PC also runs the Hub, and the Staff Console runs on its own Staff PC, apart from the Quest PC that runs the Storefront.

Amended by [ADR-0026](0026-teleop-controls-engage-toggle-pump-hand-menus.md): the Robot Panel is the left Hand Menu (radial, picked with the left stick) instead of a ray-pressed wrist panel. Jog and typed move-to are dropped, Scale is added, and Robot Status shows on the HUD.

Decided (grill, 2026-11-25). The Maintenance Operator can drive the arm only with the right Controller (Teleop, [ADR-0017](0017-teleop-udp-bridge-relative-mapping.md)). Checking the arm before a demo, clearing an error, or trying a short move needs buttons, and the Quest cannot tell whether the arm is enabled, in error or where it is, because UDP 9000 is one-way.

- **The Robot PC is not the Staff Console's PC.** The MG400 is wired to its own PC on the same LAN, running the Teleop Bridge. The Quest reaches it by a separate **Robot Address** ([ADR-0023](0023-lan-host-address-and-udp-discovery.md), amended).
- **The Robot Panel lives in the Quest's Teleop scene**, on the left Controller like a wrist panel. The left Controller's menu button shows or hides it; the right hand presses its buttons with a ray, only while Grip is not held. Unity never talks to the MG400 directly: every command goes to the Teleop Bridge, which stays the only safety point ([ADR-0001](0001-pc-orchestrator-hub.md)).
- **Commands ride the Teleop link (UDP 9000, one JSON message per packet).** The existing messages keep their exact shape, so controller Teleop is unchanged. New commands: Enable, Disable, Clear Error, Resume, jog ±X/Y/Z by a step (1, 10 or 50 mm), move to a typed X/Y/Z/R, and run a Preset Action by id. Home, Stop, suction and speed reuse the existing messages.
- **Stop latches until Resume.** After `stop`, every motion (pose, jog, move-to, home, joint, calibrate, Preset) is ignored until Resume or a new Grip engage. This closes the known gap in ADR-0017 where `joint` and `calibrate` still moved the arm.
- **Robot Status comes back over UDP 9001**, a few times a second, to the address of the last sender: robot mode (enabled, disabled, error), error id, X/Y/Z/R pose, suction state, whether Stop is latched, the result of the last command, the Preset list (id and name, and why a rejected one was rejected) and the running Preset's progress (step n of m). The Robot Panel treats the link as alive when a status packet arrived in the last 2 seconds.
- **Preset Actions live in a file on the Robot PC**, not in the APK, so a teammate edits one and tries it without a rebuild. A Preset is an ordered list of steps: move to X/Y/Z/R (mm), suction on/off, wait (ms), home. The Quest only sends the id.
- **Presets are checked on load and rejected whole.** If any move point is outside the safe workspace (reach 225–365 mm, Z from `Z_MIN_MM` to 150 mm), the whole Preset is rejected, never clamped, because a clamped Preset goes somewhere other than what the file says. Rejected Presets show greyed out with the reason.
- **Presets run only when safe.** A Preset starts only when the arm is enabled, not in error, Stop is not latched and Teleop is not engaged. One runs at a time; a second start is ignored. Stop or a Grip engage cancels it at once. Preset moves are capped at 30 % speed whatever the speed setting. `--dry-run` runs a Preset without moving the arm.
- **Start with three placeholder Presets** (small square at a safe height, suction test, Home), up to nine buttons. Real positions (Stock Slots, Packing Station) wait for the `Z_MIN_MM` calibration (#49).
- **Preset ids are meant to be callable later by the Order link.** Picking a Gem Piece may become "run the Preset for this Stock Slot". This is not decided here; it belongs to Q-02 (#75).
- **The Teleop Bridge moves into `Dobot/src/hub`** with tests for every safety rule (clamp, `Z_MIN_MM`, Stop latch and Resume, Preset checks and run conditions) against the fake MG400, under ruff and CI. `Dobot/teleop/` stays as a reference until the real arm passes the same checks.
- **Rough layout by Claude, wiring by the human** ([ADR-0014](0014-claude-rough-layout-via-unity-mcp.md), not widened). The panel's buttons are bound in code and the Preset buttons are built at runtime from Robot Status, so the human only sets a few references, checks the layout and saves the scene. A polished panel is later work.

## Considered Options

- **Panel on the Staff Console, or a Python GUI on the Robot PC:** easier to test, but the Maintenance Operator wears the headset and Teleop already lives there.
- **The Quest connects to the MG400 by its own IP:** skips the Teleop Bridge's clamp and `Z_MIN_MM`.
- **Keep the link one-way:** the panel could not show an error or the arm's pose, which is most of its use.
- **Presets defined in Unity, sent step by step:** a Wi-Fi drop leaves a sequence half-sent, safety would be checked in two places, and every change needs a rebuild.
- **Extend `teleop/mg400_bridge.py` in place:** less work, but new safety rules on a real arm with no tests.

## Consequences

- The Robot PC's firewall must allow UDP 9000 in, 9001 out and the discovery port out, and the Quest's must accept 9001.
- Most likely only one program can command the MG400 at a time, so the Robot Panel and controller Teleop share one Teleop Bridge process.
- Button B and Stop remain software stops over Wi-Fi. The real E-stop is still held by a person not wearing the headset ([ADR-0017](0017-teleop-udp-bridge-relative-mapping.md)).
- No Preset runs on the real arm without `--dry-run` until `Z_MIN_MM` is calibrated (#49).
