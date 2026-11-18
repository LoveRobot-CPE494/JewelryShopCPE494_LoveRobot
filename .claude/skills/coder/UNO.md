# UNO know-how (`UNO/`, Arduino UNO firmware)

Language page: [C++ / UNO standards](../../../Docs/standards/cpp-uno.md).

The UNO drives only the Bag Closer servo (ADR-0016, ADR-0005); the conveyor and the bag sensor
run through the MG400 I/O from `Dobot/`. It obeys
the PC over Serial; it never talks to the MG400 directly.

- **Board:** the team's board is an i-Duino R3B, UNO-compatible, so the target stays
  `arduino:avr:uno`. Upload notes (CH340 driver, bootloader fallback) are in `UNO/README.md`.
- **Build check:** `arduino-cli compile --fqbn arduino:avr:uno UNO/<sketch>`. Uploading to the board
  is the robot pen: the human does it.
- **Testable core:** keep command parsing and the sequence logic in plain C++ (`.h`/`.cpp` with no
  Arduino calls) so it compiles and runs on the host with `g++` for tests; the `.ino` only wires
  pins to that core.
- **Serial protocol:** one line per command, one reply per command (`OK`/`ERR <reason>`), and a
  status line on every state change. Keep it in sync with the parser in `Dobot/`.
- **Non-blocking loop:** `millis()`-based timing, no long `delay()`, so the E-stop is read every
  loop.
- **Power and safety:** the servo runs from a separate supply (never the UNO 5V); the E-stop input
  wins over every command; a timeout with no command from the PC stops the servo.
- Pin assignments live in one header; wiring changes are a human task, reported with the new pins.
