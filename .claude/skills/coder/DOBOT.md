# Dobot know-how (`Dobot/`, Python on the PC)

The PC is the hub (ADR-0001): Quest → PC → MG400 (TCP/IP) and PC → UNO (Serial).

- **MG400 ports:** 29999 dashboard (enable, clear error, DO for suction per ADR-0002),
  30003 motion, 30004 feedback (joint angles and pose; also feeds a Digital Twin). Use the official
  Dobot TCP/IP Python SDK rather than a hand-rolled protocol.
- **The robot pen stays human.** Tests never open a socket to the real controller IP. Hide the
  robot and the serial port behind small interfaces and test against fakes (a local fake TCP server,
  a fake serial object); `pytest` is the runner.
- **Limits live here, in one module:** workspace bounds, minimum Z above the table, speed caps,
  and the tele-op deadman (motion only while Grip is held, ADR-0003). Every motion command passes
  through it; out-of-bounds targets are clamped or rejected and logged.
- **The packing sequence is one state machine:** order received → conveyor runs → box detected →
  stop → pick (shape + colour → slot) → place → close lid → bow → convey out. Every state has a
  timeout and an error state that stops the conveyor and the arm.
- **Calibration** (Quest frame → MG400 frame) is data, loaded from a file the human produces at the
  station; code applies it, never hard-codes it.
