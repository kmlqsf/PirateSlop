param(
    [switch]$Stress,
    [switch]$Isolation,
    [switch]$ShipIsolation,
    [switch]$LegacyWorldVisuals,
    [switch]$LegacyGuiLayout,
    [switch]$LegacyHull,
    [switch]$LegacyBodyPose,
    [switch]$Development,
    [int]$TimeoutSeconds = 660,
    [double]$StormScale = -1,
    [double]$SkyScale = -1,
    [ValidateRange(640, 7680)][int]$Width = 2560,
    [ValidateRange(480, 4320)][int]$Height = 1440,
    [ValidateSet('default', 'd3d11', 'd3d12')][string]$GraphicsApi = 'default',
    [ValidateSet(0, 1, 2, 4, 10)][int]$FleetShips = 0
)
$ErrorActionPreference = 'Stop'
if ($Stress -and ($FleetShips -gt 0 -or $Isolation -or $ShipIsolation)) { throw 'Stress uses the world route; do not combine it with FleetShips.' }
$taskProjectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskExecutable = Join-Path $taskProjectRoot $(if ($Development) { 'Builds/Windows/PirateSlop.exe' } else { 'Builds/WindowsRelease/PirateSlop.exe' })
if (!(Test-Path -LiteralPath $taskExecutable)) { throw "Benchmark executable is missing: $taskExecutable" }
if (Get-Process -Name PirateSlop -ErrorAction SilentlyContinue) { throw 'Another PirateSlop process is already running.' }
$taskStarted = [DateTime]::UtcNow
$taskStamp = $taskStarted.ToString('yyyyMMdd-HHmmss-fff')
$taskLog = Join-Path $taskProjectRoot "Temp/performance-autorun-$taskStamp.log"
$taskArguments = @('-performanceBenchmark', '-performanceQuit', '-performanceUncapped', '-screen-width', $Width.ToString(), '-screen-height', $Height.ToString(), '-screen-fullscreen', '1', '-logFile', $taskLog)
if ($GraphicsApi -ne 'default') { $taskArguments += "-force-$GraphicsApi" }
if ($Stress) { $taskArguments += '-performanceStress' }
if ($Isolation) { $taskArguments += '-performanceIsolation' }
if ($ShipIsolation) { $taskArguments += '-performanceShipIsolation' }
if ($LegacyWorldVisuals) { $taskArguments += '-performanceLegacyWorldVisuals' }
if ($LegacyGuiLayout) { $taskArguments += '-performanceLegacyGuiLayout' }
if ($LegacyHull) { $taskArguments += '-performanceLegacyHull' }
if ($LegacyBodyPose) { $taskArguments += '-performanceLegacyBodyPose' }
if ($FleetShips -gt 0) { $taskArguments += @('-performanceFleetShips', $FleetShips.ToString()) }
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
$taskReportPattern = if ($Isolation -or $ShipIsolation) { '*-isolation.json' } else { '*-benchmark.json' }
$taskReportFile = Get-ChildItem -LiteralPath $taskReportDirectory -Filter $taskReportPattern | Where-Object LastWriteTimeUtc -ge $taskStarted | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
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
    $taskManifest.actualWidth = $taskReport.metadata.width
    $taskManifest.actualHeight = $taskReport.metadata.height
    $taskManifest.resolutionMatches = $taskReport.metadata.width -eq $Width -and $taskReport.metadata.height -eq $Height
    $taskManifest.actualGraphicsApi = $taskReport.metadata.graphicsApi
    $taskExpectedGraphicsApi = if ($GraphicsApi -eq 'd3d12') { 'Direct3D12' } else { 'Direct3D11' }
    $taskManifest.graphicsApiMatches = $GraphicsApi -eq 'default' -or $taskReport.metadata.graphicsApi -eq $taskExpectedGraphicsApi
}
$taskManifest.isolation = $Isolation.IsPresent -or $ShipIsolation.IsPresent
$taskManifest.shipIsolation = $ShipIsolation.IsPresent
$taskManifest.legacyWorldVisuals = $LegacyWorldVisuals.IsPresent
$taskManifest.legacyGuiLayout = $LegacyGuiLayout.IsPresent
$taskManifest.fleetShips = $FleetShips
$taskManifest.development = $Development.IsPresent
$taskManifest.legacyBodyPose = $LegacyBodyPose.IsPresent
$taskManifest.requestedWidth = $Width
$taskManifest.requestedHeight = $Height
$taskManifest.requestedGraphicsApi = $GraphicsApi
$taskManifestPath = Join-Path $taskReportDirectory "autorun-$taskStamp.manifest.json"
$taskManifest | ConvertTo-Json | Set-Content -LiteralPath $taskManifestPath -Encoding utf8
$taskManifest | ConvertTo-Json
if ($taskTimedOut -or !$taskReportFile -or $taskManifest.status -ne 'COMPLETE' -or !$taskManifest.resolutionMatches -or !$taskManifest.graphicsApiMatches -or $taskProcess.ExitCode -ne 0) { exit 2 }
