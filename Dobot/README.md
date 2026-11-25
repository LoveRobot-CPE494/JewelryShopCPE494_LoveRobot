# Dobot (PC / Python)

The hub on the PC ([ADR-0001](../Docs/adr/0001-pc-orchestrator-hub.md)):
- Receives Orders from the Quest (WebSocket/MQTT, not decided yet: Q-02); Teleop goes to the separate Teleop Bridge over UDP (see below)
- Drives the MG400 over TCP/IP (port 29999 dashboard, 30003 motion, 30004 feedback), including suction on/off through DO ([ADR-0002](../Docs/adr/0002-suction-cup-end-effector.md))
- Drives the conveyor and reads the bag sensor through the MG400's I/O, and drives the Arduino UNO (Bag Closer servo) over Serial ([ADR-0016](../Docs/adr/0016-mg400-io-runs-conveyor-uno-only-bag-closer.md))

## Teleop Bridge (`src/hub/teleop/`)

The Teleop Bridge ([ADR-0017](../Docs/adr/0017-teleop-udp-bridge-relative-mapping.md)) is the program to run. From `Dobot/` with the venv active:

```
python -m hub.teleop --dry-run          # no arm: logs each command instead
python -m hub.teleop --ip 192.168.1.6   # the real arm (--port 9000, --speed 30 by default)
```

It receives UDP on port 9000 (Teleop and the Robot Panel's commands, [ADR-0024](../Docs/adr/0024-robot-panel-presets-and-robot-status.md)), drives the MG400, and sends Robot Status as JSON 4 times a second to UDP port 9001 of whoever sent last:
- `core.py`: no sockets or threads. Clamps every move (Teleop poses, `jog`, `move_to`) to the safe workspace (reach 170-420 mm, Z, R), filters jitter, follows only fresh poses, and latches `stop`: every motion is refused until `resume` or `engage`. All the Teleop limits live here.
- `mg400_arm.py`: the MG400 adapter over the dashboard (29999) and motion (30003) ports, with the IK check and error recovery.
- `__main__.py`: the UDP listener, the 10 Hz control loop and the Robot Status sender.
- `sender.py`: sends the commands from the keyboard and prints the Robot Status it gets back, to test without the headset: `python -m hub.teleop.sender [--host <bridge ip>]`.

Every message and a few Robot Status packets are in [`protocol/teleop_examples.json`](protocol/teleop_examples.json); the Python tests read it, and the Unity tests will too.

`Z_MIN_MM` must be calibrated on the real arm (#49) before running without `--dry-run`, and a person not wearing the headset holds the real E-stop during every real-arm test.

### `teleop/` (reference only)

The teammate's original scripts, kept as a reference and left out of ruff. Do not run `mg400_bridge.py`; run `python -m hub.teleop` instead.

- `mg400_bridge.py`, `mg400_control.py`: the original Teleop Bridge and MG400 client, now ported into `src/hub/teleop/`.
- `pc_sender.py`: sends the same messages as the headset, for testing without it. Its Controller paths (`ctrl`, `up`) still work against the new program; for the Robot Panel's commands use `python -m hub.teleop.sender`.

Step-by-step test guide (Thai): [`Docs/teleop/teleop-test-guide-th.pdf`](../Docs/teleop/teleop-test-guide-th.pdf). It predates the move: where it says `python mg400_bridge.py`, run `python -m hub.teleop` from `Dobot/`.
- `unity/MG400Teleop.cs`: the Quest-side script, kept here as the Dobot-side owner's reference. It is not in the Unity project yet; copying it in, wiring it into a Teleop scene and bringing it up to the C# standard is a Unity ticket.

## Development setup

Python 3.12 or newer. From the `Dobot/` folder:

```powershell
# Windows PowerShell
py -3.12 -m venv .venv
.venv\Scripts\Activate.ps1
```

```bash
# POSIX
python3.12 -m venv .venv
source .venv/bin/activate
```

```
pip install -e ".[dev]"
ruff check .
ruff format --check .
pytest
```

CI (`.github/workflows/dobot.yml`) runs the same three commands. Tests use a fake MG400
(`tests/fakes/`) and never connect to a real controller.
