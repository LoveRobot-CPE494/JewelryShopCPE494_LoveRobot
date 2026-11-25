<#
.SYNOPSIS
    LAN setup for one PC of the three-PC setup (guide: Docs/lan/LAN_SETUP.md).
.DESCRIPTION
    -Role robot: OpenSSH Server on, firewall allows TCP 22, UDP 9000 and ICMP echo in on every
    network profile. Lists enabled inbound Block rules for Python.
    -Role host: firewall allows UDP 7777 (Staff Link), 9001 (Robot Status) and 7778 (discovery) in
    on every network profile. Does not install OpenSSH Server and adds no ICMP rule. Lists enabled
    inbound Block rules for Unity.
    Safe to run again: done steps are reported and skipped. It replaces only its own named rules
    and never removes a Block rule.
    -DryRun prints every step and whether it is done; it changes nothing and needs no admin.
    Exit code: 0 all fine, 1 a step failed, 2 refused before changing anything.
.EXAMPLE
    powershell -ExecutionPolicy Bypass -File Dobot\scripts\setup-lan.ps1 -Role robot -DryRun
.EXAMPLE
    powershell -ExecutionPolicy Bypass -File Dobot\scripts\setup-lan.ps1 -Role host -DryRun
#>
param(
    [Parameter(Mandatory)][string]$Role,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
$Prefix = 'LoveRobot - '
# Per role: the firewall rules it allows (ports: ADR-0017, ADR-0023, ADR-0024) and the program whose
# enabled inbound Block rules it lists. Report only: the script never removes a Block rule.
$RoleConfig = @{
    robot = @{
        Rules = @(
            @{ Name = "${Prefix}SSH TCP 22"; Protocol = 'TCP'; Port = 22 },
            @{ Name = "${Prefix}Teleop UDP 9000"; Protocol = 'UDP'; Port = 9000 },
            @{ Name = "${Prefix}ICMP echo"; Protocol = 'ICMPv4'; IcmpType = 8 }
        )
        BlockLabel = 'Python'
        BlockProgram = '*python*'
        BlockNote = 'they may block UDP 9000'
    }
    host = @{
        Rules = @(
            @{ Name = "${Prefix}Staff Link UDP 7777"; Protocol = 'UDP'; Port = 7777 },
            @{ Name = "${Prefix}Robot Status UDP 9001"; Protocol = 'UDP'; Port = 9001 },
            @{ Name = "${Prefix}Discovery UDP 7778"; Protocol = 'UDP'; Port = 7778 }
        )
        BlockLabel = 'Unity'
        BlockProgram = '*Unity*'
        BlockNote = 'they may block UDP 7777, 9001 or 7778'
    }
}

if ($Role -notin 'robot', 'host') {
    Write-Host "Unknown role '$Role'. Known roles: robot, host."
    Write-Host 'Nothing was changed.'
    exit 2
}
$cfg = $RoleConfig[$Role]

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $DryRun -and -not $isAdmin) {
    Write-Host 'This script must run as administrator (right-click PowerShell, Run as admin).'
    Write-Host 'Nothing was changed. Use -DryRun to only look.'
    exit 2
}

$failed = 0

function Test-Rule {
    param($Rule)
    $r = Get-NetFirewallRule -DisplayName $Rule.Name -ErrorAction SilentlyContinue
    if (-not $r -or @($r).Count -ne 1) { return $false }
    if ($r.Enabled -ne 'True' -or $r.Direction -ne 'Inbound' -or $r.Action -ne 'Allow' -or
        "$($r.Profile)" -ne 'Any') { return $false }
    $p = $r | Get-NetFirewallPortFilter
    if ($p.Protocol -ne $Rule.Protocol) { return $false }
    if ($Rule.Port) { return "$($p.LocalPort)" -eq "$($Rule.Port)" }
    return "$($p.IcmpType)" -eq "$($Rule.IcmpType)"
}

function New-Rule {
    param($Rule)
    Get-NetFirewallRule -DisplayName $Rule.Name -ErrorAction SilentlyContinue |
        Remove-NetFirewallRule
    $a = @{
        DisplayName = $Rule.Name; Direction = 'Inbound'; Action = 'Allow'; Profile = 'Any'
        Protocol = $Rule.Protocol
    }
    if ($Rule.Port) { $a.LocalPort = $Rule.Port } else { $a.IcmpType = $Rule.IcmpType }
    New-NetFirewallRule @a | Out-Null
}

# Each step: a name, a check (true = already done) and an apply.
$steps = @()
if ($Role -eq 'robot') {
    $steps += @(
        @{ Name = 'OpenSSH Server installed'
           Check = { [bool](Get-Service sshd -ErrorAction SilentlyContinue) }
           Apply = {
               $r = Add-WindowsCapability -Online -Name 'OpenSSH.Server~~~~0.0.1.0'
               if ($r.RestartNeeded) { throw 'Windows needs a restart to finish; restart, run again' }
           } },
        @{ Name = 'sshd starts with Windows'
           Check = { (Get-Service sshd -ErrorAction SilentlyContinue).StartType -eq 'Automatic' }
           Apply = { Set-Service sshd -StartupType Automatic } },
        @{ Name = 'sshd running'
           Check = { (Get-Service sshd -ErrorAction SilentlyContinue).Status -eq 'Running' }
           Apply = { Start-Service sshd } }
    )
}
foreach ($rule in $cfg.Rules) {
    $steps += @{ Name = "Firewall rule '$($rule.Name)' (all profiles)"
                 Check = { Test-Rule $rule }.GetNewClosure()
                 Apply = { New-Rule $rule }.GetNewClosure() }
}

foreach ($s in $steps) {
    try {
        $done = [bool](& $s.Check)
        if ($done) { Write-Host "[done]    $($s.Name)"; continue }
        if ($DryRun) { Write-Host "[todo]    $($s.Name)"; continue }
        & $s.Apply
        if (-not [bool](& $s.Check)) { throw 'still not in place after applying' }
        Write-Host "[applied] $($s.Name)"
    } catch {
        $failed++
        Write-Host "[FAILED]  $($s.Name): $($_.Exception.Message)"
    }
}

# Report only: an enabled inbound Block rule for the role's program beats our Allow rules.
# Never removed here (ADR-0023/0024 ports; ADR-0017 bridge).
try {
    $blocks = Get-NetFirewallRule -Direction Inbound -Action Block -Enabled True |
        Where-Object { ($_ | Get-NetFirewallApplicationFilter).Program -like $cfg.BlockProgram }
    if ($blocks) {
        Write-Host "Inbound Block rules for $($cfg.BlockLabel) ($($cfg.BlockNote))."
        Write-Host 'Remove them yourself if wrong:'
        $blocks | ForEach-Object { Write-Host "  $($_.DisplayName)  [$($_.Name)]" }
    } else {
        Write-Host "No enabled inbound Block rule for $($cfg.BlockLabel)."
    }
} catch {
    $failed++
    Write-Host "[FAILED]  $($cfg.BlockLabel) Block rule scan: $($_.Exception.Message)"
}

try {
    $ip = Get-NetIPAddress -AddressFamily IPv4 |
        Where-Object { $_.IPAddress -notlike '169.254.*' -and $_.IPAddress -ne '127.0.0.1' } |
        Sort-Object InterfaceMetric | Select-Object -ExpandProperty IPAddress
    Write-Host "LAN IPv4 address: $($ip -join ', ')"
} catch {
    $failed++
    Write-Host "[FAILED]  LAN address: $($_.Exception.Message)"
}

if ($DryRun) { Write-Host 'Dry run: nothing was changed.' }
if ($failed) { Write-Host "$failed step(s) failed."; exit 1 }
exit 0
