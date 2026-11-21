---
status: accepted
---

# The Staff Avatar's head and eyes follow a target, with a Staff toggle

Cheryll now shows a face with each gesture ([ADR-0019](0019-staff-avatar-gesture-expressions.md)), but her head and eyes stay fixed. A head that follows what the Staff Console camera faces reads as more alive. It also lets her glance at the Customer.

Decided:

- **Look-At** is a Staff Console toggle on **key 8**. It is **OFF by default** and **remembered between runs** (`PlayerPrefs`, behind a small seam so tests never touch the real store). A stored value always wins; with nothing stored, the default comes from the config (OFF). This differs on purpose from Idle Lock, which is OFF at every start.
- **Keys:** the top-row digits and letters, read through the Input System like the existing Staff Console keys (`Keyboard.current`). Each ticket that adds a key checks it clashes with no existing key.
- **What turns:** the head and the eyes. Look-At itself never turns the body outside a gesture; only a gesture turn (below) and Reset Look do.
- **Limits:** yaw ±45° and pitch ±25° from the body's forward. Past a limit the head stays at the limit. A target directly behind Cheryll takes the shortest signed angle and, at exactly 180°, the right-hand limit. The head eases toward the target over the smoothing time (config default 0.15 s). Turning Look-At OFF eases the head back to the animation pose over the same time.
- **Target priority**, highest first:
  1. **Focus** (key **F**, a toggle, OFF at every start, never remembered): the **Customer Avatar**, while it is within 3.5 m of Cheryll, measured horizontally between the two positions (config default). It becomes in range at 3.5 m and stays in range until it passes 4.0 m, so a Customer at the edge does not make the head flick. If Focus is switched ON while the Customer is between 3.5 m and 4.0 m, it starts out of range.
  2. **Interest Target**: an optional `Transform` field on the driver that a human fills in the Inspector. It is for testing and for pointing at a display.
  3. **Camera direction**: the direction the Staff Console camera faces. This is the default.
- **Finding the objects:** the driver gets two optional `Transform` fields, `Customer Avatar` and `Interest Target`, assigned in the Inspector. An empty or destroyed field counts as no object. Nothing is searched by name or tag.
- **Focus rules:** Focus works only while Look-At is ON; pressing F while Look-At is OFF does nothing and stores nothing. Turning Look-At OFF also turns Focus OFF. With no Customer Avatar, or one out of range, the target falls back to the next in the list, silently.
- **During a gesture:** if Look-At is ON, the whole body turns toward the target that is winning **when the gesture starts**, and keeps that target until the gesture ends, even if it moves or Focus changes. The body turn is yaw only: the target's height is ignored. It takes the gesture turn time (config default 0.25 s). The head's look-at weight eases to 0 over the smoothing time so the gesture animation owns the head, and eases back to 1 after the gesture. A gesture that starts before the weight has reached 1 eases it from where it is. The driver knows a gesture starts and ends from the same signal `StaffExpressionDriver` already uses. With Look-At OFF nothing changes.
- **After a gesture:** the body stays facing where it turned. Cheryll only turns on her own when the Staff walks, so she is not turned back automatically. Turning Look-At OFF in the middle of a gesture stops the body turn where it is, keeps the head weight at 0, and lets the gesture play to its end; the body then stays there.
- **Reset Look:** key **R** (a one-shot command, not a toggle, not remembered). It turns the body back to face the direction of the Staff Console camera, with the same yaw-only turn and turn time as a gesture turn. It works whether Look-At is ON or OFF, does nothing while a gesture plays, leaves the head and eyes to Look-At, and has no status text. If R is already bound to a gesture, the ticket picks the next free key, trying the digit 9 first and then letters that clash with nothing, and says so.
- **Status label:** one legacy uGUI `Text` named `Look-At Status`, found by name like `Idle Lock Status`. With Look-At OFF it reads `Look-At: OFF`. With it ON it reads `Look-At: ON · Focus: OFF|ON · Target: <name>`, where the name is `camera` (the Camera direction), `Customer` or the Interest Target's object name, and Focus ON adds `(no customer)` or `(out of range)` when it has no target. For example `Look-At: ON · Focus: OFF · Target: Display_01`. A missing label only logs a warning.
- **Where the values live:** a data-only `StaffLookAtConfig` asset ([ADR-0018](0018-staff-config-data-only-scriptable-objects.md)): default on/off, yaw and pitch limits, smoothing time, gesture turn time, Focus enter and exit distance. Bad values are corrected with a warning: no negative limits or times, and an exit distance never below the enter distance.
- **Where the rules live:** plain C# in `JewelryShop.Core` (limit clamp, smoothing, target priority, Focus hysteresis, gesture latch, status text), covered by EditMode tests. A thin MonoBehaviour only reads the scene and applies the result to the head, the eyes and the body yaw.
- **Eyes:** driven in the first version, through the VRM look-at of `Cheryll_3.vrm`, following the same target the head follows. The eye direction is worked out in the Core module with the head's. If the model has no usable eye bones or look-at expressions, the ticket reports it on the issue and ships head only.

## Considered Options

- **Head follows only the camera, no Interest Target or Focus:** simplest, but a Staff cannot glance at the Customer and there is nothing to point at while testing.
- **Look-At always on:** no toggle to build, but the head would swing with every camera orbit, which is distracting while the Staff is just looking around.
- **Focus as hold-to-glance:** needs no state, but the team preferred an on/off switch.
- **Clickable on-screen button:** the Staff Console has no UI system today; a key matches Idle Lock.
- **Look-At OFF at every start (like Idle Lock):** the team chose to remember it.
- **Turn the body back automatically after a gesture:** nothing else turns her when she stands still, so she would swing round on her own; the Reset Look key puts that choice with the Staff.
- **Find the Customer Avatar by name or tag:** breaks silently when someone renames an object; an Inspector field is also what the Editor pen already covers.

## Consequences

- One `area:unity` spec covers the config, the Core logic, the driver and the status label.
- A `StaffLookAtConfig` asset must be created, the driver added, `Customer Avatar` and `Interest Target` assigned and the scene saved by a human. Claude may place the `Look-At Status` Text and a test object roughly through Unity MCP ([ADR-0014](0014-claude-rough-layout-via-unity-mcp.md)).
- Look-At is the first Staff Console setting remembered between runs.
