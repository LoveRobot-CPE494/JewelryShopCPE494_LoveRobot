---
status: accepted
---

# Teleop controls v2: A toggles Engage, a two-mode pump, a Hand Menu on each hand, AR and Game Modes

Decided (grill, 2026-11-25). Holding Grip for the whole of a Teleop session tires the hand, and the real pump has two modes (suck and blow) that one on/off DO cannot express. The right Controller is remapped, the Robot Panel becomes a radial Hand Menu, and the headset switches between the Teleop scene and the Storefront from a menu.

Amends [ADR-0002](0002-suction-cup-end-effector.md), [ADR-0003](0003-quest-controllers-for-teleoperation.md), [ADR-0017](0017-teleop-udp-bridge-relative-mapping.md) and [ADR-0024](0024-robot-panel-presets-and-robot-status.md). ADR-0025 is reserved for the three-PC LAN (#228).

## Right Controller in AR Mode

| Input | Action |
|---|---|
| A | **Engage** on; press again to end it. Replaces the Grip-held Deadman |
| Hand position | Arm X/Y/Z, relative to where it was at Engage, 1:1 by default (unchanged from ADR-0017). Turning the wrist does nothing; R stays where it was |
| Trigger, held | Pump sucks (DO9). Let go: the pump stops and the Gem Piece stays held |
| Grip, held | Pump blows (DO10) and the Gem Piece drops. Let go: it stops |
| B | Software stop, unchanged. It does not touch the pump |
| Stick up/down | Nudge Z while Engaged, unchanged. Left/right is unused |
| Stick click | Show or hide the right Hand Menu |

On the left Controller, a stick click shows or hides the left Hand Menu in AR Mode, and warps in Game Mode (below).

- **Engage ends on its own** when the right Controller loses tracking, the headset is taken off, or the app loses focus. Opening either Hand Menu, pressing B and leaving AR Mode also end it. Opening a menu is a pause, not an emergency stop. Closing a menu never re-Engages; the operator presses A again.
- **The pump works only while Engaged.** Whenever Engage ends, leaving AR Mode included, the pump goes to hold: it stops and the Gem Piece stays on. Nothing ever blows on its own. The Teleop Bridge never drives DO9 and DO10 on together.
- **"Forward" is where the head faces at Engage.** The Engage message carries the head's yaw, and the Bridge turns the mapping by it, so the fixed `HEADSET_YAW_DEG` is no longer needed. The HUD reminds the operator to face the arm when pressing A.
- **Scale is 0.25×, 0.5× or 1×**, chosen in the Robot Panel, sent with Engage and shown on the HUD. 1× is the default and the Bridge never accepts more than 1×.

## Hand Menus

- A **radial menu at each Controller** (the mechanic VRChat uses, in our own visual style), toggled by clicking that hand's stick where that hand has a menu. Tilt past about 70 % to highlight a slot (it grows and lightens), hold it about 0.15 s, and let the stick return to centre to select. A flick through a slot selects nothing. **The bottom slot is always Back** (up one level); at the top level it closes the menu.
- **Right Hand Menu, both modes:** AR, Game, Home.
- **Left Hand Menu = the Robot Panel, AR Mode only:** Enable, Disable, Clear Error, Resume, Preset ▸, Speed ▸ (10/30/50 %, no 100 %), Scale ▸ (0.25×/0.5×/1×). Preset ▸ lists the Presets from Robot Status, with a rejected one greyed out and its reason shown (unchanged from ADR-0024). The wrist panel pressed by a ray, jog and typed X/Y/Z/R move-to from ADR-0024 are dropped. Robot Status shows on the HUD.
- In Game Mode the left stick click warps the Customer back to the Storefront's start point. While the right Hand Menu is open in Game Mode, the right stick picks slots instead of teleporting.
- Anyone wearing the headset can open the menus; there is no hidden unlock. The arm still moves only after Engage.

## AR and Game Modes

- **AR Mode** is the Teleop scene with the real room shown through the headset cameras (passthrough). **Game Mode** is the Storefront. They are separate scenes: entering AR Mode closes the Storefront and its Staff Link, and returning to Game Mode starts the Storefront afresh.
- Passthrough is a separate, lower-priority (P2) piece built in parallel with the rest. If it does not work over Quest Link in time, AR Mode is the Teleop scene without passthrough.

## Teleop Bridge messages

A new `pump` message with mode `suck`, `off` or `blow`. The old `grip` message stays (`1` = suck, `0` = off), so `pc_sender.py` and the existing tests keep working. Engage gains `yaw` and `scale`. The DO numbers live in config.

## HUD

An original sci-fi look of floating translucent panels, not a copy of any game's UI. It shows the mode, Engage state, Scale, pump state (sucking, holding, blowing), the Robot PC link, robot mode and error, and the Stop latch. The Controller gives a short haptic pulse whenever Engage starts or ends. Claude makes the rough layout and the human polishes it ([ADR-0014](0014-claude-rough-layout-via-unity-mcp.md)).

## Considered Options

- **Keep the Grip-held Deadman:** safer by construction, but it tires the hand over a session. The automatic end of Engage covers the common failures (a dropped Controller, the headset taken off).
- **Pump on/off on one DO:** the real pump has separate suck and blow outputs, and blowing is what makes a Gem Piece let go cleanly.
- **Trigger to select in the menus, as in VRChat:** the right Trigger already sucks. Flick-and-release with a dwell time keeps every button to one job.
- **Keep the ray-pressed wrist panel:** two interaction styles on one headset. One radial style on both hands is easier to learn.
- **Scale above 1×:** it magnifies hand tremor. Re-Engaging covers long reaches.

## Consequences

- #222 (the wrist Robot Panel) is replaced by the left Hand Menu ticket. #210 is unchanged and comes first for the Teleop input work.
- The Bridge's suction moves from DO1 to DO9 (suck) and DO10 (blow); ADR-0002 and the Preset suction step follow.
- `Z_MIN_MM` (#49), the real E-stop and the remote-run safety rule (#228) still apply unchanged.
