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

It receives UDP on port 9000 and drives the MG400:
- `core.py`: no sockets or threads. Clamps every move to the safe workspace (reach 170-420 mm, Z, R), filters jitter, follows only fresh poses, and handles `stop`. All the Teleop limits live here.
- `mg400_arm.py`: the MG400 adapter over the dashboard (29999) and motion (30003) ports, with the IK check and error recovery.
- `__main__.py`: the UDP listener, the 10 Hz control loop, and Robot Status (JSON, 4 times a second) to the last sender on UDP 9001 ([ADR-0024](../Docs/adr/0024-robot-panel-presets-and-robot-status.md)).

Robot Panel commands on 9000: `enable`, `disable`, `clear_error`, `resume`, `jog` (axis `x`/`y`/`z`, step ±1/10/50 mm) and `move_to` (x, y, z, r). After `stop`, every move is refused until `resume` or `engage`; after `disable`, until `enable`. Example messages for every command and Robot Status packets: [`contract/teleop_examples.json`](contract/teleop_examples.json), read by the Python tests (and meant for the Unity tests).

`Z_MIN_MM` must be calibrated on the real arm (#49) before running without `--dry-run`, and a person not wearing the headset holds the real E-stop during every real-arm test.

### `teleop/` (reference only)

The teammate's original scripts, kept as a reference and left out of ruff. Do not run `mg400_bridge.py`; run `python -m hub.teleop` instead.

- `mg400_bridge.py`, `mg400_control.py`: the original Teleop Bridge and MG400 client, now ported into `src/hub/teleop/`.
- `pc_sender.py`: sends the same messages as the headset, for testing without it. Still in use: `python teleop/pc_sender.py` against the new program.

Step-by-step test guide (Thai): [`Docs/teleop/teleop-test-guide-th.pdf`](../Docs/teleop/teleop-test-guide-th.pdf). It predates the move: where it says `python mg400_bridge.py`, run `python -m hub.teleop` from `Dobot/`.
- `unity/MG400Teleop.cs`: the Quest-side script, kept here as the Dobot-side owner's reference. It is not in the Unity project yet; copying it in, wiring it into a Teleop scene and bringing it up to the C# standard is a Unity ticket.

## LAN setup

Robot PC setup script (`scripts/setup-lan.ps1`) and SSH from an SSH Client: [`Docs/lan/LAN_SETUP.md`](../Docs/lan/LAN_SETUP.md).

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
