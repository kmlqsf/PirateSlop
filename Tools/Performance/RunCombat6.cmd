@echo off
setlocal
set "combatDir=%~dp0"
if not exist "%combatDir%PirateSlop.exe" set "combatDir=%~dp0..\..\Builds\WindowsRelease\"
if not exist "%combatDir%PirateSlop.exe" (
  echo PirateSlop.exe is missing. Build or extract the complete Release folder first.
  pause
  exit /b 2
)
tasklist /FI "IMAGENAME eq PirateSlop.exe" /NH | find /I "PirateSlop.exe" >nul
if not errorlevel 1 (
  echo Close the running PirateSlop game first.
  pause
  exit /b 2
)
set "combatResolution="
if /I "%~1"=="fullhd" set "combatResolution=-screen-width 1920 -screen-height 1080 -screen-fullscreen 0"
cd /d "%combatDir%"
echo Six ships, eighteen scripted participants. 30s preparation + 180s combat benchmark.
echo Keep the game focused. Do not resize the window or change graphics during recording.
start "" /wait "%combatDir%PirateSlop.exe" -performanceBenchmark -performanceCombat -performanceFleetShips 6 -performanceUncapped -performanceQuit -force-d3d11 %combatResolution% -logFile "%combatDir%Combat6-Player.log"
set "combatExit=%errorlevel%"
echo Game exit code: %combatExit%
echo Send the newest matching combat JSON and TXT reports, plus Combat6-Player.log.
if exist "%USERPROFILE%\AppData\LocalLow\DefaultCompany\PirateSlop\PerformanceReports" start "" explorer.exe "%USERPROFILE%\AppData\LocalLow\DefaultCompany\PirateSlop\PerformanceReports"
if not "%combatExit%"=="0" pause
exit /b %combatExit%
