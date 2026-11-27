# LAN setup: three PCs on one network

Three PCs talk over one LAN ([ADR-0027](../adr/0027-three-pcs-quest-staff-robot.md)):

| PC | Runs | Network role |
|---|---|---|
| **Quest PC** (PC1) | The Storefront (Unity), with the Meta Quest 3 on Quest Link | Client: joins the Staff Link, sends Teleop and Orders |
| **Staff PC** (PC2) | The Staff Console (Unity) | **Host** of the Staff Link; its address is the Host Address |
| **Robot PC** (PC3) | The Hub and the Teleop Bridge, cabled to the MG400 and the Arduino UNO | Waits for Teleop and Orders and sends replies back; its address is the Robot Address |

Teammates reach the Robot PC by SSH from any **SSH Client** (a laptop such as the Mac, or one of the
other two PCs). This guide covers the Robot PC in section 4, SSH in section 5, the Quest PC and the
Staff PC in section 8, the two Quest modes in section 9, undo in section 10 and troubleshooting in
section 7.

Decisions: [ADR-0023](../adr/0023-lan-host-address-and-udp-discovery.md),
[ADR-0024](../adr/0024-robot-panel-presets-and-robot-status.md),
[ADR-0017](../adr/0017-teleop-udp-bridge-relative-mapping.md).

## 1. Remote-run safety rule (read first)

Before a remote session runs anything that can move the MG400 (arm, suction, conveyor, bag closer),
the person at the keyboard announces it (team chat or out loud) and waits for an OK from a person
who stands within reach of the E-stop for the whole run.

Exempt: `--dry-run`, reading logs, and `git pull`.

No Preset runs on the real arm without `--dry-run` until #49 (Z_MIN calibration) is done.

## 2. Network check

All three PCs must reach each other. From an SSH Client, ping the Robot PC once:

```
ping <robot-ip>
```

Replies mean the network is fine. No replies: check both PCs are on the same Wi-Fi or switch, and
that the Robot PC ran the setup script (it allows ping). If the venue network blocks device-to-device
traffic, fall back to a phone hotspot or our own router, and connect every PC to it.

## 3. Fixed addresses

Addresses (example: Staff PC 10.35.120.17, Robot PC 10.35.120.20) come from DHCP and may change.

- Best: a DHCP reservation for the Robot PC and the Staff PC on the router.
- Else: at the start of every day run `ipconfig` on each PC, read the IPv4 address, and use that.
  The setup script also prints it at the end.

## 4. Robot PC setup

On the Robot PC, open PowerShell **as administrator** at the repo root:

```powershell
# Look first: prints each step and whether it is done; changes nothing, needs no admin.
powershell -ExecutionPolicy Bypass -File Dobot\scripts\setup-lan.ps1 -Role robot -DryRun
# Do it:
powershell -ExecutionPolicy Bypass -File Dobot\scripts\setup-lan.ps1 -Role robot
```

It installs OpenSSH Server if missing, starts it and sets it to start with Windows, and allows
inbound TCP 22 (SSH), UDP 9000 (Teleop) and ping, on every network profile. The script opens no
other port itself.
Installing OpenSSH Server also makes Windows add its own `OpenSSH-Server-In-TCP` rule (also port 22).
Run it again any time: done steps are reported and skipped. A failing step is reported, the rest still
run, and the exit code is 1.

It also lists any enabled inbound **Block** rule for Python (those can silently stop UDP 9000). It
never removes one: delete it yourself in Windows Defender Firewall if it is wrong.

Rules it creates (display names are fixed):

- `LoveRobot - SSH TCP 22`
- `LoveRobot - Teleop UDP 9000`
- `LoveRobot - ICMP echo`

To undo them, see section 10.

Human task (ticket 018-03): run the script on the real Robot PC and check it with the hand-check list
there. The script has no automated test.

## 5. SSH Client setup

Replace `<user>` with the Robot PC's Windows user and `<robot-ip>` with its address.

1. **Generate a key** (once per SSH Client): `ssh-keygen -t ed25519`
2. **Copy it to the Robot PC.**
   - If `<user>` is a normal (non-admin) user, on the Robot PC append the public key to
     `C:\Users\<user>\.ssh\authorized_keys`.
   - **Pitfall:** if `<user>` is in the Administrators group (the usual case), Windows ignores that
     file. Put the key in `C:\ProgramData\ssh\administrators_authorized_keys` and fix its ACL, in
     an administrator PowerShell on the Robot PC:
     ```powershell
     Add-Content C:\ProgramData\ssh\administrators_authorized_keys '<paste the .pub line>'
     icacls C:\ProgramData\ssh\administrators_authorized_keys /inheritance:r /grant 'Administrators:F' /grant 'SYSTEM:F'
     ```
   - One key per SSH Client, one line each. From a Mac you can first send it with
     `scp ~/.ssh/id_ed25519.pub <user>@<robot-ip>:` (password login) and then paste it as above.
3. **Plain ssh:** `ssh <user>@<robot-ip>`
4. **VS Code Remote-SSH:** install the "Remote - SSH" extension, run "Remote-SSH: Connect to Host",
   enter `<user>@<robot-ip>`, open the repo folder on the Robot PC.
5. **Fallback:** password login stays on. If the key is refused, `ssh` asks for the Windows password.
   A Windows account with an empty password cannot log in over the network: set a password
   (`net user "<user>" *`) or use a key. Keys do not need a password.
   Quote a user name with a space: `ssh "CPE KMUTT@<robot-ip>"`.

## 6. First remote run

In the SSH session on the Robot PC (venv set up as in the [Dobot README](../../Dobot/README.md)):

```
cd <repo>\Dobot
.venv\Scripts\Activate.ps1
python -m hub.teleop --dry-run
```

The log shows in your own terminal. `--dry-run` logs each command and never touches the arm, so the
safety rule exempts it. Stop with Ctrl+C. Running without `--dry-run` falls under section 1.

## 7. Troubleshooting (short)

- The script says Windows needs a restart, or "OpenSSH Server" is missing from Optional features:
  the install is pending. Restart the Robot PC (stop the arm first), then run the script again.
  The sshd steps may also say FAILED in that run: one cause, run again after the restart.
  Check with `Get-WindowsCapability -Online -Name 'OpenSSH.Server*'` (want `Installed`).
- `ssh` asks for a password you do not have: copy your key as in section 5 step 2.
- `ssh` times out: ping first (section 2); run the setup script `-DryRun` on the Robot PC; check the
  address with `ipconfig`. SSH is on the Robot PC only: the host role does not install it, so `ssh`
  to the Quest PC or the Staff PC will not work.
- Key refused for an admin user: the `administrators_authorized_keys` pitfall in section 5.
- Teleop packets do not arrive: look for an inbound Block rule on Python on the Robot PC (the script
  lists them).
- No Robot Status on the Quest PC: run `setup-lan.ps1 -Role host -DryRun` on the Quest PC (section 8)
  and check the three rules show `[done]`. Check the Robot PC is running the Teleop Bridge and that
  the Quest's Robot Address (section 8) is the Robot PC's current address. Check that no inbound
  Block rule for Unity is listed (next bullet).
- The script listed a Block rule for Unity: an inbound Block rule can stop UDP 7777, 9001 or 7778
  on the Quest PC or the Staff PC. The script lists it and never removes it. If it is wrong, delete it yourself in
  Windows Defender Firewall, then run the host role again.
- A PC's address changed: a DHCP address can change after a restart or a new network. Run `ipconfig`
  on that PC, then type its new address in the headset's Settings: the Robot Address for the Robot
  PC, the Host Address for the Staff PC if Automatic IP in LAN is not ticked. With Automatic IP in
  LAN ticked, the Host Address is found again from the Staff PC's beacon. To stop this happening, set
  a DHCP reservation (section 3).

## 8. Quest PC and Staff PC setup

Run the host role on **both** the Quest PC and the Staff PC. The Quest PC needs UDP 9001 (Robot
Status) and 7778 (Host Discovery); the Staff PC needs UDP 7777 (Staff Link). The role opens all
three on each; the unused port on each PC is harmless. (`host` is only the script's role name: the Quest PC is still a client on the network, and the Staff PC is the host.) On each of the two PCs, open PowerShell
**as administrator** at the repo root:

```powershell
# Look first: prints each step and whether it is done; changes nothing, needs no admin.
powershell -ExecutionPolicy Bypass -File Dobot\scripts\setup-lan.ps1 -Role host -DryRun
# Do it:
powershell -ExecutionPolicy Bypass -File Dobot\scripts\setup-lan.ps1 -Role host
```

It allows inbound UDP 7777 (Staff Link), UDP 9001 (Robot Status) and UDP 7778 (Host Discovery) on
every network profile. It opens no other port. It does not install OpenSSH Server and adds no ping
rule: neither PC is an SSH target. Run it again any time: done steps are reported and skipped,
and a failing step is reported while the rest still run (exit code 1).

It also lists any enabled inbound **Block** rule for Unity (those can silently stop the three UDP
ports). It never removes one: delete it yourself in Windows Defender Firewall if it is wrong.

Rules it creates (display names are fixed):

- `LoveRobot - Staff Link UDP 7777`
- `LoveRobot - Robot Status UDP 9001`
- `LoveRobot - Discovery UDP 7778`

Then in the headset's Settings, type the Robot PC's address (section 3) as the **Robot Address**
(ticket #217). The Host Address is the Staff PC's address; with **Automatic IP in LAN** ticked it is
found from the Staff PC's beacon, otherwise type the Staff PC's address too.

The script prints the PC's LAN address at the end: on the Staff PC, use it when you type the Host
Address.

Human task (ticket 018-03): run the script on the real Quest PC and Staff PC and check each with the
hand-check list there. The script has no automated test.

## 9. Quest modes

- **Quest Link on the Quest PC (the demo mode).** The headset is tethered to the Quest PC, so the
  Quest PC takes the headset's place on the network: Robot Status and beacons reach the Quest PC,
  and the host role (section 8) must be run there.
- **Standalone headset over Wi-Fi.** This mode stays supported (tickets 016 and 017) when the network
  lets the Quest connect to the PCs. If the venue network blocks device-to-device traffic, it cannot
  reach them: use the phone hotspot or our own router (section 2), or use Quest Link.

## 10. Undo

Undo needs an administrator PowerShell. Each command removes only the project's rules: they all start
with the prefix `LoveRobot - `. Run the first line on the PC whose rules you want gone; it removes
the robot and host rules alike.

```powershell
# Remove every rule the project created (by prefix), on this PC.
Get-NetFirewallRule -DisplayName 'LoveRobot - *' -ErrorAction SilentlyContinue | Remove-NetFirewallRule
```

Or remove the named rules one by one. The robot role uses:

```powershell
Remove-NetFirewallRule -DisplayName 'LoveRobot - SSH TCP 22','LoveRobot - Teleop UDP 9000','LoveRobot - ICMP echo'
```

The host role uses:

```powershell
Remove-NetFirewallRule -DisplayName 'LoveRobot - Staff Link UDP 7777','LoveRobot - Robot Status UDP 9001','LoveRobot - Discovery UDP 7778'
```

To also turn SSH off on a Robot PC (this also closes Windows' own port 22 rule, which is not ours):

```powershell
Stop-Service sshd; Set-Service sshd -StartupType Disabled
Disable-NetFirewallRule -Name 'OpenSSH-Server-In-TCP' -ErrorAction SilentlyContinue
```

The script never removes Block rules, so undo does not touch them. Delete one in Windows Defender
Firewall yourself if it is wrong.
