# UNO (Arduino firmware)

Runs the conveyor, the bag-stop sensor and the Bag Closer (a servo that closes the bag mouth). Takes commands from the PC over Serial ([ADR-0004](../Docs/adr/0004-arduino-owns-conveyor-and-packaging.md), [ADR-0005](../Docs/adr/0005-paper-gift-bag.md))

## Board

We use an **i-Duino R3B**, an UNO-compatible board (ATmega328P, 16 MHz), so the board target is
`arduino:avr:uno` ("Arduino Uno" in the Arduino IDE). No extra board package is needed.

- Upload from Arduino IDE (1.8.x): open `UnoFirmware/UnoFirmware.ino`, pick **Arduino Uno** and the
  COM port, Upload. Or: `arduino-cli upload -p COMx --fqbn arduino:avr:uno UnoFirmware`.
- No COM port? Install the CH340 USB-serial driver.
- Upload fails with `stk500_getsync() ... not in sync`? Try board **Arduino Nano → ATmega328P (Old
  Bootloader)**; the compiled code is the same.

## Checks (run from `UNO/`)

Same three steps as CI (`.github/workflows/uno.yml`). Tool versions: `clang-format` 23.1.5
(CI installs it with `pip install clang-format==23.1.5`; use the same version locally or the
formatting can differ), `g++` with C++17, `arduino-cli` with the `arduino:avr` core.

```sh
clang-format --dry-run --Werror UnoFirmware/UnoFirmware.ino UnoFirmware/src/* test/*.cpp
g++ -std=c++17 -Wall -Wextra -o uno_test test/command_parser_test.cpp UnoFirmware/src/CommandParser.cpp && ./uno_test
arduino-cli compile --fqbn arduino:avr:uno UnoFirmware
```

Fix formatting with `clang-format -i` on the same files. Logic goes in `UnoFirmware/src/` as plain
C++ (no Arduino calls) so the host test can build it; `test/vendor/doctest.h` is vendored (v2.4.12)
and is not formatted.
