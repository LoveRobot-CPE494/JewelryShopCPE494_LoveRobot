---
status: accepted
---

# The Staff Avatar shows a facial Expression with every gesture

The team wants Cheryll to show a face with each gesture, not only a body animation. `Cheryll_3.vrm` (VRM 1.0) already carries the presets `happy`, `relaxed`, `angry`, `sad`, `surprised`, `blink`, `aa` and `ee`. No script drove any of them before this decision. Settles issue #131.

Decided:

- **Gesture → Expression** (weights are first guesses, tuned in the config asset):

  | Gesture | Expression |
  |---|---|
  | Wave | `happy` |
  | Bow | `relaxed` |
  | Point | `happy`, light |
  | Nod / Yes | `happy` |
  | Shake / No | `sad`, low weight (never `angry`: it must not look like anger at the Customer) |
  | Shy | `happy` + `relaxed` |

- **Hold, then fade.** An Expression stays 0.5 s after its gesture ends, then fades out over 0.3 s. Both values live in the config.
- **Default smile.** Every state without a gesture (Idle, Walk, Jump, Idle Lock) shows a light `happy` (about 0.3).
- **Gestures overlap by crossfade.** A new gesture fades the old Expression out while the new one fades in, over the same fade time.
- **Emotion presets and blink are driven** (`happy`, `relaxed`, `angry`, `sad`, `blink`). `aa`, `ee` and `surprised` are left alone for other systems.
- **Blink (amended, spec #163).** This driver owns `blink`: a separate channel layered on the emotions, firing after a random 2 to 6 s interval and lasting about 0.15 s (fast close, slower open). A started blink always finishes, even if a gesture begins. While `happy` is above a threshold the blink is scaled down (about 0.6) so a squinting smile does not look doubled. The values live in the same config; the Standby variations get no Expression of their own (default smile plus blink).
- **No blush.** VRoid has no blush blendshape; Shy uses the preset mix. A texture or material blush can be a later ticket if the team wants one.
- **Where the values live:** a new data-only `StaffExpressionConfig` asset ([ADR-0018](0018-staff-config-data-only-scriptable-objects.md)), separate from `StaffGestureMapConfig` (keys vs. faces).
- **Where the rules live:** the weight-at-time calculation is plain C# in `JewelryShop.Core`, covered by EditMode tests. A thin MonoBehaviour only writes the weights to `Vrm10Instance.Runtime.Expression`.

## Considered Options

- **Put the Expression table in `StaffGestureMapConfig`:** one asset fewer, but it mixes key bindings with face tuning.
- **`angry` for Shake / No:** reads as the avatar being cross with the Customer.
- **A real blush blendshape or material trick:** needs art work the team does not have time for.

## Consequences

- One `area:unity` ticket covers the config, the Core logic, the MonoBehaviour and tests.
- The `StaffExpressionConfig` asset must be created, the component added and wired in the Editor by a human.
