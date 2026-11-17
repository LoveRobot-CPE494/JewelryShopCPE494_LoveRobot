# Dobot (PC / Python)

ตัวกลางสั่งงานบน PC ([ADR-0001](../Docs/adr/0001-pc-orchestrator-hub.md)):
- รับออเดอร์และคำสั่งบังคับทางไกลจาก Quest (WebSocket/MQTT)
- สั่ง MG400 ผ่าน TCP/IP (พอร์ต 29999 dashboard, 30003 motion, 30004 feedback) รวมถึงดูด/ปล่อย suction ผ่าน DO ([ADR-0002](../Docs/adr/0002-suction-cup-end-effector.md))
- สั่ง Arduino UNO ผ่าน Serial ([ADR-0004](../Docs/adr/0004-arduino-owns-conveyor-and-packaging.md))

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
