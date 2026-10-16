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
echo Keep the game focused. Resolution is taken from your game settings.
start "" /wait "%~dp0PirateSlop.exe" -performanceBenchmark -performanceIsolation -performanceFleetShips 4 -performanceUncapped -performanceQuit -force-d3d11 -logFile "%~dp0Fleet4-Player.log"
set "fleetExit=%errorlevel%"
echo Game exit code: %fleetExit%
echo Send the latest matching isolation JSON and TXT files, plus Fleet4-Player.log.
if exist "%USERPROFILE%\AppData\LocalLow\DefaultCompany\PirateSlop\PerformanceReports" start "" explorer.exe "%USERPROFILE%\AppData\LocalLow\DefaultCompany\PirateSlop\PerformanceReports"
if not "%fleetExit%"=="0" pause
exit /b %fleetExit%
