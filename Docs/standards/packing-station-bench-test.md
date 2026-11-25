# Packing Station bench test

Checks for the [Packing Station](../../GLOSSARY.md) on the real hardware. Shared rules:
[CODING_STANDARDS.md](../../CODING_STANDARDS.md). Why the E-stop and Controller checks exist:
[ADR-0003](../adr/0003-quest-controllers-for-teleoperation.md),
[ADR-0016](../adr/0016-mg400-io-runs-conveyor-uno-only-bag-closer.md),
[ADR-0017](../adr/0017-teleop-udp-bridge-relative-mapping.md).

## When to run

- Before every demo.
- After any change to motion code (Dobot, Teleop mapping, safety limiter) or actuator code
  (conveyor and sensor on the MG400 I/O, UNO Bag Closer, Suction Cup).

Run every check in order. If one fails, do not run the demo. Fix it, then run the whole test
again.

## Record

- Date:
- Tester:
- Minimum Z recorded in check 3: `____ mm`
- Overall result: [ ] Pass  [ ] Fail

## Safety

- Clear the arm's work area of people and loose Gem Pieces before you start.
- Keep the physical E-stop within reach for the whole test.

## 1a. Physical E-stop cuts conveyor and Bag Closer power

- **Setup:** Start the conveyor with a Gift Bag on it. Start a Bag Closer close move.
- **Action:** Press the physical E-stop while the conveyor and the Bag Closer are moving.
- **Expected:** The conveyor and the Bag Closer stop at once. Their motor and servo power is
  cut, not only the software command. Nothing moves again until the E-stop is reset and the
  test is restarted on purpose.

- [ ] Pass
- [ ] Fail. Note: ____

## 1b. Controller B stops the MG400 mid-motion

- **Setup:** Start Teleop with the Maintenance Operator wearing the Quest. Hold Grip and move
  the arm slowly in an open area of the table, away from the Gem Stock.
- **Action:** While the arm is still moving, press B on the Controller.
- **Expected:** The MG400 stops in the middle of its path. It does not finish the move. It does
  not move again until the test is restarted.

- [ ] Pass
- [ ] Fail. Note: ____

## 2. Ending Engage stops Teleop motion

Until [ADR-0026](../adr/0026-teleop-controls-engage-toggle-pump-hand-menus.md) is built, Engage is
Grip held, and ending it is releasing Grip.

- **Setup:** Start Teleop. Press A to Engage and move the arm slowly in an open area.
- **Action:** Press A again while the arm is moving.
- **Expected:** The arm stops immediately. It does not keep following the Controller. Moving the
  Controller after Engage has ended does not move the arm.

- [ ] Pass
- [ ] Fail. Note: ____

## 3. Minimum Z is respected

- **Setup:** Record the configured minimum Z: `____ mm` (from the safety limiter config).
  Note: this value is pending calibration on the real Packing Station. Check it again after
  calibration.
- **Action:** In Teleop, hold Grip and push the thumbstick down in small steps, past the
  recorded height.
- **Expected:** The arm stops at or above the recorded value. It never touches the table or the
  Gem Stock.

- [ ] Pass
- [ ] Fail. Note: ____

## 4. Suction Cup picks exactly one Gem Piece from its Stock Slot

- **Setup:** Put several Gem Pieces of one Cut + Color in one Stock Slot. Check that its label
  matches them. Count the Gem Pieces in the Stock Slot: `____`. Keep any Dropped Gem away from
  the pick point.
- **Action:** In Teleop, move the Suction Cup over the Stock Slot. Press Trigger to suck, then
  lift. Count the Gem Pieces left in the Stock Slot.
- **Expected:** Exactly one Gem Piece is lifted. The count in the Stock Slot goes down by one.
  No second Gem Piece comes with it, and no Dropped Gem is created.
- **Clean up:** Release the Gem Piece back into the same Stock Slot.

- [ ] Pass
- [ ] Fail. Note: ____

## 5. Bag sensor detects a Gift Bag

- **Setup:** Place a Gift Bag at the start of the conveyor. Keep the arm's path clear.
- **Action:** Start the conveyor. Let the Gift Bag move toward the arm.
- **Expected:** The sensor detects the Gift Bag. The conveyor stops with the Gift Bag in front of
  the arm. The Gift Bag does not roll past the stop point.

- [ ] Pass
- [ ] Fail. Note: ____

## 6. Bag Closer closes the Gift Bag mouth

- **Setup:** Stand the Gift Bag still on the conveyor in front of the Bag Closer, with its mouth
  open. Keep fingers clear of the Bag Closer.
- **Action:** Run the Bag Closer close action. Then run the open action.
- **Expected:** The Bag Closer closes the mouth of the Gift Bag fully. The Gift Bag stays steady
  and does not move off the conveyor. The open action then opens the mouth again.

- [ ] Pass
- [ ] Fail. Note: ____
