@echo off
rem Stop the Teleop Bridge on the Robot PC (guide: Docs/lan/LAN_SETUP.md, section 11).
rem It sends "disable" to the Bridge first, so the motors are off, then ends the process on
rem UDP 9000. Ending it skips the Bridge's own exit step (suction off, disable): hence the disable.
rem The Bridge binds 0.0.0.0 on port 9000 (its default); a Bridge started with --port is not found.
setlocal
cd /d "%~dp0.."

set "PID="
for /f "tokens=1,2,4" %%a in ('netstat -ano -p udp ^| findstr ":9000 "') do (
    if "%%a"=="UDP" if "%%b"=="0.0.0.0:9000" set "PID=%%c"
)
if not defined PID (
    echo [stop] no Bridge is running ^(nothing on UDP 9000^).
    exit /b 0
)
tasklist /fi "PID eq %PID%" /nh | findstr /i "python.exe" >nul || (
    echo [stop] PID %PID% on UDP 9000 is not python.exe: left alone, check it with tasklist.
    exit /b 1
)

if exist ".venv\Scripts\python.exe" (
    echo [stop] disabling the arm
    ".venv\Scripts\python.exe" -c "import json, socket; s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM); [s.sendto(json.dumps({'type': 'disable'}).encode(), ('127.0.0.1', 9000)) for _ in range(3)]"
    rem one or two control ticks for DisableRobot(); ping, not timeout: timeout needs a console
    ping -n 3 127.0.0.1 >nul
) else (
    echo [warn] no venv: the arm stays enabled, disable it by hand.
)

echo [stop] stopping the Bridge, PID %PID%
taskkill /PID %PID% /F >nul
exit /b %errorlevel%
