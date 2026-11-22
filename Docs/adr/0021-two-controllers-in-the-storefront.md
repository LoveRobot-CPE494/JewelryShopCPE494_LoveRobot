---
status: accepted
---

# The Storefront has two Controllers; the right stick teleports

The team believed the Quest 3 had only the right Controller (#30). On a real headset test (Wednesday 2026-11-18) it turned out the headset has both. The "no left Controller" premise in `GLOSSARY.md` and #30 no longer holds.

Decided (grill, 2026-11-22):

- **Both Controllers work as hands.** Either hand grabs a Gem Model and presses the Staff Call button. A Customer holds one Gem Model at a time; whoever grabs first holds it.
- **Left stick walks** (unchanged, [ADR-0015](0015-storefront-headset-locomotion.md): smooth, 1.5 m/s, with the Tunneling Vignette).
- **Right stick teleports**: push forward to aim, release to move. It lands only on the Floor (the `TeleportationArea` from #30). This reverses ADR-0015's "Teleport alongside walking: rejected".
- **Still no snap turn or smooth turn.** The Customer turns their real head (ADR-0015). Teleport has no Tunneling Vignette.
- **Teleop is untouched.** The right Controller alone drives the arm ([ADR-0003](0003-quest-controllers-for-teleoperation.md), [ADR-0017](0017-teleop-udp-bridge-relative-mapping.md)); the left Controller does nothing in Teleop.
- **The Simulator simulates both hands**, so a layout can be tested without a headset. The "left Controller untracked" setting from #30 is removed. `GemModelKeyRotator` turns the Gem Model in whichever hand holds one.
- **The Customer Avatar on the Staff Console still follows the right hand only** (`CustomerPose`). A Gem taken with the left hand is not shown. Tracked as a Q-NN question issue, not fixed here.
- **The ray that does not match the real Controller** (#122) is a separate question: it happens only on the real headset and the cause is unknown.
- **Motion sickness** is tuned on the headset and tracked as a Q-NN question issue.

## Consequences

- #30's "single right Controller" decision is superseded by this ADR. Its Floor `TeleportationArea` stays and is now used.
- The Inspector wiring (Right Controller teleport on, left hand grab and Staff Call, removing the Simulator override) is Editor pen work for the human.
