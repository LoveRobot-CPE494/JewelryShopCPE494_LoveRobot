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
- `core.py`: no sockets or threads. Clamps every move to the safe workspace (reach 225-365 mm, Z, R), filters jitter, follows only fresh poses, and handles `stop`. All the Teleop limits live here.
- `mg400_arm.py`: the MG400 adapter over the dashboard (29999) and motion (30003) ports, with the IK check and error recovery.
- `__main__.py`: the UDP listener, the 10 Hz control loop, and Robot Status (JSON, 4 times a second) to the last sender on UDP 9001 ([ADR-0024](../Docs/adr/0024-robot-panel-presets-and-robot-status.md)).

Robot beacon (`beacon.py`, [ADR-0023](../Docs/adr/0023-lan-host-address-and-udp-discovery.md)): once a second, `--dry-run` included, the Bridge broadcasts an 11-byte beacon (role `robot`, magic `JSRB`) with its LAN IPv4 and the Teleop port to UDP **7778** (the discovery port), so the Quest fills in the Robot Address with Automatic IP in LAN on. With two network cards it announces the one the MG400 (`--ip`) is not on; override with `--beacon-ip`. Firewall: outbound UDP 7778 is allowed by default on Windows; if outbound is blocked, allow it. `setup-lan.ps1 -Role robot` adds no rule for it.

Robot Panel commands on 9000: `enable`, `disable`, `clear_error`, `resume`, `jog` (axis `x`/`y`/`z`, step ±1/10/50 mm) and `move_to` (x, y, z, r). After `stop`, every move is refused until `resume` or `engage`; after `disable`, until `enable`. Example messages for every command and Robot Status packets: [`contract/teleop_examples.json`](contract/teleop_examples.json), read by the Python tests (and meant for the Unity tests).

### Preset Actions (`presets.json`)

The Robot Panel's Preset buttons run a Preset from [`presets.json`](presets.json), read when the Bridge starts (`--presets <file>` to use another; a missing or unreadable file means no Presets and a logged reason). Edit it and restart the Bridge, no rebuild. The Robot Panel sends `{"type": "preset", "id": 1}`; the Bridge runs it one step per control tick and reports progress in Robot Status.

```json
{"presets": [{"id": 1, "name": "Square", "steps": [
  {"type": "move", "x": 250, "y": -25, "z": 50, "r": 0},
  {"type": "suction", "on": true},
  {"type": "wait", "ms": 1000},
  {"type": "home"}
]}]}
```

- `id` is 1-9 and unique; `name` is the button label. Steps: `move` (x, y, z in mm, r in deg), `suction` (`on` true or false), `wait` (`ms`, 0-60000) and `home` (all joints to 0).
- Checked on load: every `move` must be inside reach 225-365 mm (distance of x, y from the base), Z from `Z_MIN_MM` to 150 mm and R within +/-170. One bad step rejects the **whole** Preset (never clamped); it shows greyed out with the reason. A repeated id rejects that id (neither copy runs); an id outside 1-9 is skipped and logged; the other Presets still load.
- A Preset starts only when the arm is enabled and not in error, Stop is not latched and Teleop is not engaged. One runs at a time. `stop` or `engage` cancels it at once, and other moves are refused while it runs. Preset moves run at most 30 % speed whatever `--speed` says; the setting comes back when it ends.
- `--dry-run` steps through a Preset and logs each call without moving the arm. The shipped Presets are placeholders; real positions wait for the `Z_MIN_MM` calibration (#49).

Reach probe: `python -m hub.teleop.probe_reach --ip 192.168.1.6` (stop the Bridge first; nothing moves) asks the arm's `InverseSolution()` over Z_MIN_MM..Z_MAX_MM and prints the reach band per height, to set `MIN_REACH_MM` and `MAX_REACH_MM` (and the reach above) from data. Measured 2026-11-27 on y = 0: 215-375 mm at every height. The old copies in `teleop/` keep 170-420 and 440 mm; do not reuse them.

`Z_MIN_MM` (provisionally 0 mm, a guess, not a measured value) must be calibrated on the real arm (#49) before running without `--dry-run`, and a person not wearing the headset holds the real E-stop during every real-arm test.

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
