@echo off
setlocal
cd /d "%~dp0"
if not exist "PirateSlop.exe" (
  echo PirateSlop.exe is missing. Extract the complete game archive first.
  pause
  exit /b 2
)
tasklist /FI "IMAGENAME eq PirateSlop.exe" /NH | find /I "PirateSlop.exe" >nul
if not errorlevel 1 (
  echo Close the running PirateSlop game before starting this test.
  pause
  exit /b 2
)
echo Four ships, twelve characters: loading + 20s warmup + 60s recording.
echo Keep the game focused. Forced 1920x1080 windowed mode. Do not maximize or resize the game window.
start "" /wait "%~dp0PirateSlop.exe" -performanceBenchmark -performanceIsolation -performanceFleetShips 4 -performanceUncapped -performanceQuit -force-d3d11 -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 -logFile "%~dp0Fleet4-FullHD-Player.log"
set "fleetExit=%errorlevel%"
echo Game exit code: %fleetExit%
echo Send the latest matching isolation JSON and TXT files, plus Fleet4-FullHD-Player.log.
if exist "%USERPROFILE%\AppData\LocalLow\DefaultCompany\PirateSlop\PerformanceReports" start "" explorer.exe "%USERPROFILE%\AppData\LocalLow\DefaultCompany\PirateSlop\PerformanceReports"
if not "%fleetExit%"=="0" pause
exit /b %fleetExit%
