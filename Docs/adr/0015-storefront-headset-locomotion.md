---
status: accepted
---

# Storefront locomotion on the headset: left stick walks, the head turns, fixed eye height

The first test on a Quest 3 over Link went wrong in two ways. The view did not follow the head, and the Customer's eyes were near the floor. Both came from the XR Interaction Simulator. It stays in the Storefront scene for testing without a headset, and its `Remove Other HMD Devices` removed the real headset, so a fake one with a fixed pose drove the camera. The XRI Starter Assets defaults also put teleport and snap turn on both thumbsticks, so neither stick walked.

Decided (grill, 2026-11-18):

- **The Simulator turns itself off when a real headset is found.** The check runs once at startup. With no real headset, the Simulator runs as before, so testing without a headset and `GemModelKeyRotator` still work. A headset connected after Play has started needs Play pressed again.
- **The left thumbstick walks smoothly**, relative to where the head faces, strafing included, at 1.5 m/s (amended below; first 2.5 m/s, the Starter Assets default).
- **Only the real head turns the view.** The right thumbstick does nothing for now: no snap turn, no smooth turn, no teleport.
- **Tracking Origin Mode is Device, with a fixed eye height of 1.6 m** (`Camera Y Offset`). The Customer stands at a standing adult's eye height whether they sit or stand in the real room. Real head movement still adds to it, measured from where the head was at startup.

## Considered Options

- **Turn the Simulator off by hand before each headset test:** easy to forget, and the failure (a frozen, low camera) doesn't look like a Simulator problem.
- **Floor tracking origin (the XRI default):** the eye height comes from the real room, so a team member testing seated at a desk is as short as a seated person in the Storefront.
- **Snap turn or smooth turn on the right stick:** rejected for now. The Customer turns their real head, and an unused stick can't throw them somewhere by accident. A right-stick action can be added later.
- **Teleport alongside walking:** rejected. The left stick already covers walking in a small shop.

## Consequences

- The Inspector values (Left Controller Smooth Motion on, Turn and Teleportation off, Device mode, `Camera Y Offset` 1.6) were set once through Unity MCP at the human's request, as a one-off exception to the Editor pen. ADR-0014 is unchanged.
- The Simulator's auto-off is code with a test, done as one `area:unity` ticket.

## Amended 2026-11-18: comfort and controller model

Walking with the stick on the headset made the tester dizzy, and the controller's ray seemed to leave the controller model downward and to the left.

- **Walking speed is 1.5 m/s**, close to real walking pace. 2.5 m/s was too fast for a small shop.
- **A Tunneling Vignette darkens the edge of the view while the stick walks.** It is the XRI Starter Assets `TunnelingVignette` under the Main Camera, driven only by the Continuous Move provider (aperture 0.7, ease-in 0.3 s), so turning the real head shows no vignette.
- **The controller model is turned to lie along the ray.** The ray follows the Quest's aim pose and is correct. The XRI generic controller model's body sits about 25° nose-up from that pose, so each `Controller Visual` is rotated about 25° nose-down (about 2° sideways) around the ray's start. The ray is unchanged. A real Quest Touch Plus model was considered and left for later: it needs a new asset.

These values were also set through Unity MCP at the human's request, as one-off exceptions to the Editor pen, after testing on the headset.

## Amended 2026-11-22

The right stick now teleports (Floor only, no snap turn), reversing "Teleport alongside walking: rejected" above. See [ADR-0021](0021-two-controllers-in-the-storefront.md).
