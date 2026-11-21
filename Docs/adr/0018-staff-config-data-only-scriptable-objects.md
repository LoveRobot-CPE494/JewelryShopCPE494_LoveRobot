---
status: accepted
---

# Staff Config is data-only ScriptableObject config, no event channels

The Staff Console's movement and camera tuning used to sit as constants and serialized fields inside `StaffConsoleMovement`. They move into **Staff Config** assets so a teammate can tune them without touching code, and so later tickets (gravity, jump, collision) read the same values.

Decided:

- **A Staff Config is a ScriptableObject that holds only data.** It has no methods that act, no events and no references to scene objects. Components read it; nothing writes to it at runtime.
- **No event channels.** A change to a Staff Config is picked up when the component starts, not pushed to listeners. The Staff Console is one scene with one avatar, so a channel would add moving parts and no value.
- **The rules live in plain C# in `JewelryShop.Core`** (`MovementSettings`, `CameraSettings`): the defaults and the validation. The ScriptableObject only holds a settings struct and hands out a validated copy.
- **Bad values are clamped with a warning, never thrown.** A missing config asset falls back to the defaults with a warning, so the scene still works before an asset is assigned.
- **Defaults equal the old constants**, so the preview walks and orbits the camera exactly as before.
- **One create-asset menu:** JewelryShop > Staff Console. Assets live in their own folders under `Assets/Settings/`.

## Considered Options

- **Keep serialized fields on the component:** no new assets, but the tuning cannot be shared or reused by the next tickets.
- **ScriptableObject event channels:** decouples listeners, but nothing here needs to listen.

## Consequences

- `StaffConsoleMovement` now reads gravity, step height, slope limit and capsule size for its CharacterController, plus `KillPlaneYMeters` for the respawn. `EdgeMarginMeters` is removed with the circular platform clamp.
- `StaffConsoleMovement` also reads `JumpHeightMeters`, `JumpTakeoffDelaySeconds` and `AirControl`.
  - Takeoff speed comes from jump height and the configured gravity.
  - The Jump pose follows takeoff and landing.
- `StaffConsoleMovement` reads the camera fields too.
  - The camera reads the collision radius, layer mask and relax speed. The default mask targets
    the `CameraBlocking` layer used by walls and ceilings.
  - `EyeHeightMeters` sets the orbit pivot and first-person height. `FirstPersonForwardMeters`
    and `FirstPersonMaxLookDownDegrees` set the first-person placement and pitch limit.
  - `EyeHeightMeters` replaces `PivotHeightMeters` (0.9 m, mid-body). An old asset reads it as 0
    and gets the 1.6 m default with a warning.
  - A wall squeezing the third-person camera raises it above the Staff Avatar, with the ceiling
    limiting how high it goes. The lift threshold keeps the serialized name
    `FirstPersonThresholdMeters`.
  - V cuts directly between third and first person. The Staff Avatar's head and hair stay visible.
  - The Jewelry shop camera collider uses the solid part of the shell mesh. The exterior glass
    stays in the avatar's collision mesh and the camera can pass through it for a wide view.
- The two assets must be created in the Editor and assigned on the movement component by a human.
