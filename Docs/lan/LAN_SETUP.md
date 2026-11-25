# LAN setup: three PCs on one network

The **Host PC** (Staff Console, Hub), the **Robot PC** (cabled to the MG400, runs the Teleop Bridge)
and the **Client PCs** (the Mac, the Host PC when it logs in elsewhere) talk over one LAN. This guide
is filled in role by role. The `host` role section arrives with ticket 018-02.

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

All three kinds of PC must reach each other. From a Client PC, ping the Robot PC once:

```
ping <robot-ip>
```

Replies mean the network is fine. No replies: check both PCs are on the same Wi-Fi or switch, and
that the Robot PC ran the setup script (it allows ping). If the venue network blocks device-to-device
traffic, fall back to a phone hotspot or our own router, and connect every PC to it.

## 3. Fixed addresses

Addresses (example: Host PC 10.35.120.17, Robot PC 10.35.120.20) come from DHCP and may change.

- Best: a DHCP reservation for the Robot PC and Host PC on the router.
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

**Undo** (administrator PowerShell; removes exactly those rules):

```powershell
Remove-NetFirewallRule -DisplayName 'LoveRobot - SSH TCP 22','LoveRobot - Teleop UDP 9000','LoveRobot - ICMP echo'
```

To also turn SSH off (this also closes Windows' own port 22 rule, which is not ours):
```powershell
Stop-Service sshd; Set-Service sshd -StartupType Disabled
Disable-NetFirewallRule -Name 'OpenSSH-Server-In-TCP' -ErrorAction SilentlyContinue
```

Human task (ticket 018-03): run the script on the real Robot PC and check it with the hand-check list
there. The script has no automated test.

## 5. Client PC setup

Replace `<user>` with the Robot PC's Windows user and `<robot-ip>` with its address.

1. **Generate a key** (once per Client PC): `ssh-keygen -t ed25519`
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
   - One key per Client PC, one line each. From a Mac you can first send it with
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
  address with `ipconfig`.
- Key refused for an admin user: the `administrators_authorized_keys` pitfall in section 5.
- Teleop packets do not arrive: look for an inbound Block rule on Python (the script lists them).
