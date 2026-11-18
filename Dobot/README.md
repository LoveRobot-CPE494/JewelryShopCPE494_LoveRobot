# Dobot (PC / Python)

The hub on the PC ([ADR-0001](../Docs/adr/0001-pc-orchestrator-hub.md)):
- Receives Orders from the Quest (WebSocket/MQTT, not decided yet: Q-02); Teleop goes to the separate Teleop Bridge over UDP (see below)
- Drives the MG400 over TCP/IP (port 29999 dashboard, 30003 motion, 30004 feedback), including suction on/off through DO ([ADR-0002](../Docs/adr/0002-suction-cup-end-effector.md))
- Drives the conveyor and reads the bag sensor through the MG400's I/O, and drives the Arduino UNO (Bag Closer servo) over Serial ([ADR-0016](../Docs/adr/0016-mg400-io-runs-conveyor-uno-only-bag-closer.md))

## Teleop (`teleop/`)

The working Teleop from a teammate ([ADR-0017](../Docs/adr/0017-teleop-udp-bridge-relative-mapping.md)), kept as-is and left out of ruff until it moves into `src/hub`:

- `mg400_bridge.py`: the Teleop Bridge. Receives UDP on port 9000 and drives the MG400. Run `python mg400_bridge.py --dry-run` first.
- `mg400_control.py`: MG400 client over TCP (29999 / 30003), with an IK check and error recovery.
- `pc_sender.py`: sends the same messages as the headset, for testing without it.

Step-by-step test guide (Thai): [`Docs/teleop/teleop-test-guide-th.pdf`](../Docs/teleop/teleop-test-guide-th.pdf).
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
