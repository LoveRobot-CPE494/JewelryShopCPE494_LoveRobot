# UNO know-how (`UNO/`, Arduino UNO firmware)

The UNO owns the conveyor, the box sensor, the lid servo and the bow mechanism (ADR-0004). It obeys
the PC over Serial; it never talks to the MG400 directly.

- **Build check:** `arduino-cli compile --fqbn arduino:avr:uno UNO/<sketch>`. Uploading to the board
  is the robot pen: the human does it.
- **Testable core:** keep command parsing and the sequence logic in plain C++ (`.h`/`.cpp` with no
  Arduino calls) so it compiles and runs on the host with `g++` for tests; the `.ino` only wires
  pins to that core.
- **Serial protocol:** one line per command, one reply per command (`OK`/`ERR <reason>`), and a
  status line on every state change. Keep it in sync with the parser in `Dobot/`.
- **Non-blocking loop:** `millis()`-based timing, no long `delay()`, so the E-stop and sensor are
  read every loop.
- **Power and safety:** servos and the motor run from a separate supply (never the UNO 5V); the
  E-stop input wins over every command; a timeout with no command from the PC stops the conveyor.
- Pin assignments live in one header; wiring changes are a human task, reported with the new pins.
