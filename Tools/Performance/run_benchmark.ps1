param(
    [switch]$Stress,
    [switch]$LegacyHull,
    [int]$TimeoutSeconds = 660,
    [double]$StormScale = -1,
    [double]$SkyScale = -1
)
$ErrorActionPreference = 'Stop'
$taskProjectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskExecutable = Join-Path $taskProjectRoot 'Builds/WindowsRelease/PirateSlop.exe'
if (!(Test-Path -LiteralPath $taskExecutable)) { throw "Release executable is missing: $taskExecutable" }
if (Get-Process -Name PirateSlop -ErrorAction SilentlyContinue) { throw 'Another PirateSlop process is already running.' }
$taskStarted = [DateTime]::UtcNow
$taskStamp = $taskStarted.ToString('yyyyMMdd-HHmmss-fff')
$taskLog = Join-Path $taskProjectRoot "Temp/performance-autorun-$taskStamp.log"
$taskArguments = @('-performanceBenchmark', '-performanceQuit', '-performanceUncapped', '-screen-width', '2560', '-screen-height', '1440', '-screen-fullscreen', '1', '-logFile', $taskLog)
if ($Stress) { $taskArguments += '-performanceStress' }
if ($LegacyHull) { $taskArguments += '-performanceLegacyHull' }
if ($StormScale -gt 0) { $taskArguments += @('-performanceStormScale', $StormScale.ToString('0.000', [Globalization.CultureInfo]::InvariantCulture)) }
if ($SkyScale -gt 0) { $taskArguments += @('-performanceSkyScale', $SkyScale.ToString('0.000', [Globalization.CultureInfo]::InvariantCulture)) }
$taskProcess = Start-Process -FilePath $taskExecutable -ArgumentList $taskArguments -WorkingDirectory (Split-Path $taskExecutable) -PassThru
Write-Output "RUNNING pid=$($taskProcess.Id) log=$taskLog"
$taskTimedOut = $false
while (!$taskProcess.WaitForExit(1000)) {
    if (([DateTime]::UtcNow - $taskStarted).TotalSeconds -gt $TimeoutSeconds) {
        $taskTimedOut = $true
        $null = $taskProcess.CloseMainWindow()
        if (!$taskProcess.WaitForExit(5000)) { $taskProcess.Kill(); $taskProcess.WaitForExit() }
        break
    }
}
$taskProcess.Refresh()
$taskReportDirectory = Join-Path $env:USERPROFILE 'AppData/LocalLow/DefaultCompany/PirateSlop/PerformanceReports'
$taskReportFile = Get-ChildItem -LiteralPath $taskReportDirectory -Filter '*-benchmark.json' | Where-Object LastWriteTimeUtc -ge $taskStarted | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
$taskManifest = [ordered]@{ startedUtc = $taskStarted.ToString('O'); finishedUtc = [DateTime]::UtcNow.ToString('O'); pid = $taskProcess.Id; exitCode = $taskProcess.ExitCode; timedOut = $taskTimedOut; stress = $Stress.IsPresent; legacyHull = $LegacyHull.IsPresent; stormScale = $StormScale; skyScale = $SkyScale; log = $taskLog; report = $null }
if ($taskReportFile) {
    $taskReport = Get-Content -LiteralPath $taskReportFile.FullName -Raw | ConvertFrom-Json
    $taskManifest.report = $taskReportFile.FullName
    $taskManifest.status = $taskReport.status
    $taskManifest.reason = $taskReport.reason
    $taskManifest.averageFps = $taskReport.total.averageFps
    $taskManifest.p99Ms = $taskReport.total.p99Ms
    $taskManifest.route = $taskReport.metadata.routeVersion
    $taskManifest.instrumentation = $taskReport.metadata.instrumentationVersion
}
$taskManifestPath = Join-Path $taskReportDirectory "autorun-$taskStamp.manifest.json"
$taskManifest | ConvertTo-Json | Set-Content -LiteralPath $taskManifestPath -Encoding utf8
$taskManifest | ConvertTo-Json
if ($taskTimedOut -or !$taskReportFile -or $taskManifest.status -ne 'COMPLETE' -or $taskProcess.ExitCode -ne 0) { exit 2 }
