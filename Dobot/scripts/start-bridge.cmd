@echo off
rem Start the Teleop Bridge on the Robot PC, from any folder (guide: Docs/lan/LAN_SETUP.md, section 11).
rem   scripts\start-bridge.cmd                 real arm at 192.168.1.6, speed 10 %%
rem   scripts\start-bridge.cmd --speed 20      extra arguments go to python -m hub.teleop (last wins)
rem   scripts\start-bridge.cmd --dry-run       no arm
rem It first stops an old Bridge with stop-bridge.cmd (disable, then end it): a leftover one holds
rem the arm's link. Keep the default port: both scripts look for the Bridge on UDP 9000.
rem Over SSH, Ctrl+C does not stop it: run scripts\stop-bridge.cmd from a second window.
setlocal
cd /d "%~dp0.."

call "%~dp0stop-bridge.cmd" || exit /b 1
rem let Windows free the port; ping, not timeout: timeout needs a console
ping -n 2 127.0.0.1 >nul

echo [start] code: & git log -1 --oneline
git status --porcelain | findstr . >nul && echo [warn] local changes or copied-in files: run "git status"; git pull will refuse them.

if not exist ".venv\Scripts\activate.bat" (
    echo [error] no venv in %CD%\.venv: see Dobot\README.md, Development setup.
    exit /b 1
)
call ".venv\Scripts\activate.bat"
python -m hub.teleop --ip 192.168.1.6 --speed 10 %*
exit /b %errorlevel%
