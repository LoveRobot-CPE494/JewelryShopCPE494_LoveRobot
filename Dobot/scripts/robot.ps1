<#
.SYNOPSIS
    One command from the SSH Client to the Robot PC (guide: Docs/lan/LAN_SETUP.md, section 11).
.DESCRIPTION
    Finds the Robot PC's address, then:
      (no switch)  SSH in and land in the Dobot folder with the venv on.
      -Bridge      SSH in and start the Teleop Bridge (scripts\start-bridge.cmd); extra -BridgeArgs.
      -Stop        SSH in and stop it (scripts\stop-bridge.cmd): disable, then end the process.
      -Sender      run teleop\pc_sender.py here, pointed at the Robot PC.
      -Find        only print the address.
    Finding the address: -Ip if given; else the Robot beacon on UDP 7778 (heard only while a
    Bridge runs); else the last address that answered, saved in scripts\.robot-ip (git-ignored);
    else it asks. A found address is checked on TCP 22 and saved.
    Use one switch at a time. -BridgeArgs takes no double quotes (Windows PowerShell 5.1
    mangles them on the way to ssh).
    Remote side: Windows OpenSSH with cmd as the shell. Over SSH, Ctrl+C does not stop the
    Bridge: use -Stop from a second window.
    Exit code: the ssh or pc_sender exit code; 1 the Robot PC does not answer on TCP 22;
    2 refused before connecting (no address, two switches, quotes in -BridgeArgs).
.EXAMPLE
    powershell -ExecutionPolicy Bypass -File Dobot\scripts\robot.ps1
.EXAMPLE
    powershell -ExecutionPolicy Bypass -File Dobot\scripts\robot.ps1 -Bridge -BridgeArgs '--speed 20'
#>
param(
    [switch]$Bridge,
    [switch]$Stop,
    [switch]$Sender,
    [switch]$Find,
    [string]$Ip,
    [string]$BridgeArgs = '',
    [string]$User = 'cpe kmutt',
    # Unquoted on purpose: cmd's cd takes spaces, and Windows PowerShell 5.1 mangles quotes
    # inside an argument to a native program.
    [string]$RemoteDobot = '%USERPROFILE%\Desktop\FinalProject\JewelryShopCPE494_LoveRobot\Dobot'
)

$ErrorActionPreference = 'Stop'
$DobotDir = Split-Path -Parent $PSScriptRoot
$IpFile = Join-Path $PSScriptRoot '.robot-ip'
$DiscoveryPort = 7778  # Robot beacon: "JSRB", version, IPv4, port (Dobot/src/hub/teleop/beacon.py)

function Read-RobotBeacon([int]$TimeoutMs) {
    $udp = New-Object System.Net.Sockets.UdpClient
    try {
        # Shared: the Storefront may listen on the same port.
        $udp.ExclusiveAddressUse = $false
        $udp.Client.SetSocketOption('Socket', 'ReuseAddress', $true)
        $udp.Client.Bind((New-Object System.Net.IPEndPoint([System.Net.IPAddress]::Any, $DiscoveryPort)))
        $udp.Client.ReceiveTimeout = 300
        $deadline = (Get-Date).AddMilliseconds($TimeoutMs)
        while ((Get-Date) -lt $deadline) {
            $from = New-Object System.Net.IPEndPoint([System.Net.IPAddress]::Any, 0)
            try { $data = $udp.Receive([ref]$from) } catch { continue }  # timeout: try again
            $isRobot = [System.Text.Encoding]::ASCII.GetString($data, 0, [Math]::Min(4, $data.Length)) -eq 'JSRB'
            if ($data.Length -ge 11 -and $isRobot -and $data[4] -eq 1) {
                return '{0}.{1}.{2}.{3}' -f $data[5], $data[6], $data[7], $data[8]
            }
        }
    } catch {
        Write-Host "[robot] cannot listen on UDP ${DiscoveryPort}: $($_.Exception.Message)"
    } finally {
        $udp.Close()
    }
    return $null
}

function Test-Ssh([string]$Address) {
    $client = New-Object System.Net.Sockets.TcpClient
    try {
        $attempt = $client.BeginConnect($Address, 22, $null, $null)
        return ($attempt.AsyncWaitHandle.WaitOne(1500) -and $client.Connected)
    } catch {
        return $false
    } finally {
        $client.Close()
    }
}

function Find-RobotIp {
    if ($Ip) { return $Ip }
    $heard = Read-RobotBeacon 2500
    if ($heard -and (Test-Ssh $heard)) { Write-Host "[robot] beacon: $heard"; return $heard }
    if ($heard) { Write-Host "[robot] beacon $heard does not answer on TCP 22." }
    if (Test-Path $IpFile) {
        $last = "$(Get-Content $IpFile -TotalCount 1)".Trim()
        if ($last -and (Test-Ssh $last)) { Write-Host "[robot] last address: $last"; return $last }
        Write-Host "[robot] last address $last does not answer on TCP 22."
    }
    try {
        return "$(Read-Host '[robot] Robot PC address (ipconfig on it, the IPv4 that is not 192.168.1.x)')".Trim()
    } catch {
        return $null  # no console to ask on
    }
}

if (@($Bridge, $Stop, $Sender, $Find | Where-Object { $_ }).Count -gt 1) {
    Write-Host '[robot] use one of -Bridge, -Stop, -Sender, -Find at a time.'
    exit 2
}
if ($BridgeArgs.Contains('"')) {
    Write-Host '[robot] -BridgeArgs takes no double quotes.'
    exit 2
}

$address = Find-RobotIp
if (-not $address) { Write-Host '[robot] no address.'; exit 2 }
if (-not (Test-Ssh $address)) {
    Write-Host "[robot] $address does not answer on TCP 22: same Wi-Fi? Robot PC awake? (guide, section 7)"
    exit 1
}
Set-Content -Path $IpFile -Value $address -Encoding ascii
if ($Find) { $address; exit 0 }

$target = "$User@$address"
if ($Sender) {
    & (Join-Path $DobotDir '.venv\Scripts\python.exe') (Join-Path $DobotDir 'teleop\pc_sender.py') --host $address
} elseif ($Stop) {
    ssh $target "cd /d $RemoteDobot && scripts\stop-bridge.cmd"
} elseif ($Bridge) {
    Write-Host '[robot] Ctrl+C will not stop the Bridge over SSH: run robot.ps1 -Stop in a second window.'
    ssh -t $target "cd /d $RemoteDobot && scripts\start-bridge.cmd $BridgeArgs"
} else {
    ssh -t $target "cd /d $RemoteDobot && .venv\Scripts\activate.bat && cmd /k"
}
exit $LASTEXITCODE
