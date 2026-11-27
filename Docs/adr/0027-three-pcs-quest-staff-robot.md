---
status: accepted
---

# Three PCs on one LAN: the Quest PC, the Staff PC (host) and the Robot PC

Amends [ADR-0001](0001-pc-orchestrator-hub.md), [ADR-0023](0023-lan-host-address-and-udp-discovery.md), [ADR-0024](0024-robot-panel-presets-and-robot-status.md), [ADR-0010](0010-staff-call-direct-netcode-link.md) and [ADR-0016](0016-mg400-io-runs-conveyor-uno-only-bag-closer.md).

Amended (2026-11-27): first lab day (#231) findings added to Consequences; no new decision.

Decided (team, 2026-11-25). Earlier docs put the Storefront and the Staff Console on one "Host PC". The real demo setup has three PCs on one LAN, each with one job:

| PC | Runs | Network role |
|---|---|---|
| **Quest PC** (PC1) | The Storefront, with the Meta Quest 3 tethered over Quest Link | Client: joins the Staff Link, sends Teleop and Orders, receives Robot Status and Host Beacons |
| **Staff PC** (PC2) | The Staff Console | **Host**: the Netcode host of the Staff Link (UDP 7777) and the sender of the `staff` Host Beacon |
| **Robot PC** (PC3) | The Hub and the Teleop Bridge, cabled to the MG400 and the Arduino UNO | Waits for data: receives Teleop (UDP 9000) and Orders, and sends Robot Status (UDP 9001) and each Order's accept or reject back |

- **The Host Address is the Staff PC's address**, and the **Robot Address is the Robot PC's address**. The Quest PC holds both ([ADR-0023](0023-lan-host-address-and-udp-discovery.md)).
- **The Hub runs on the Robot PC**, next to the Teleop Bridge, because the MG400 and the UNO are cabled there. The Order link (Q-02, [#75](https://github.com/LoveRobot-CPE494/JewelryShopCPE494_LoveRobot/issues/75)) therefore sends to the Robot Address, not the Host Address. It is not built yet.
- **All three PCs share one LAN.** Teammates reach the Robot PC by SSH from their own machine (an **SSH Client**) to run and watch the Python side ([LAN_SETUP](../lan/LAN_SETUP.md)). Before a remote run that can move the arm, follow the [remote-run safety rule](../lan/LAN_SETUP.md#1-remote-run-safety-rule-read-first); the [daily start](../lan/LAN_SETUP.md#11-daily-start-after-the-setup-is-done) scripts automate the start-of-day address check (`Dobot/scripts/robot.ps1` finds the Robot PC by its beacon, then the last address that answered, then asks).
- **MG400 I/O map** (the real wiring):

  | I/O | Use |
  |---|---|
  | DO1 | Conveyor motor |
  | DO9 | Pump sucks; stopping it keeps the Gem Piece held |
  | DO10 | Pump blows (pressure) to release the Gem Piece; see [ADR-0026](0026-teleop-controls-engage-toggle-pump-hand-menus.md) |
  | DI2 | Bag Sensor: a Gift Bag is in front of the arm |

## Considered Options

- **Keep one Host PC for the Storefront and the Staff Console:** fewer machines, but it does not match the setup the team actually runs, where the Staff Console and the headset are on separate PCs.

## Consequences

- `setup-lan.ps1 -Role host` opens UDP 7777, 9001 and 7778. Run it on **both** the Quest PC (needs 9001 and 7778) and the Staff PC (needs 7777); the one unused port on each is harmless. Splitting the role is later work.
- The Teleop code still switches suction on DO1 (`SUCTION_DO_INDEX = 1` in `Dobot/src/hub/teleop/core.py` and `Dobot/teleop/mg400_bridge.py`). On the real wiring DO1 is the conveyor, so Teleop suction runs the conveyor instead until the move to DO9/DO10 in [ADR-0026](0026-teleop-controls-engage-toggle-pump-hand-menus.md) is built.
- First lab day (2026-11-27, #231): the Robot PC's DHCP address changes from day to day (10.35.120.20, then 10.35.120.17). The university network allows no router reservation, so `Dobot/scripts/robot.ps1` finds it each day.
- Ctrl+C does not stop the Teleop Bridge over SSH. The stop path is `Dobot/scripts/stop-bridge.cmd` (sends `disable`, then ends the process), run by `robot.ps1 -Stop` or the VS Code task "Robot: Stop".
- A leftover Bridge keeps UDP 9000 and the arm's link, so a new Bridge's jog silently did nothing. `start-bridge.cmd` therefore stops the old Bridge first.
