[CmdletBinding()]
param(
    [Parameter(Position = 0)][string]$Topic = 'overview',
    [string]$ProjectRoot,
    [switch]$List,
    [switch]$Json,
    [switch]$Validate
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (-not $ProjectRoot) { $ProjectRoot = Join-Path $PSScriptRoot '../..' }
$root = (Resolve-Path -LiteralPath $ProjectRoot).Path
foreach ($marker in @('AGENTS.md', 'ProjectSettings/ProjectVersion.txt', 'Packages/manifest.json')) {
    if (-not (Test-Path -LiteralPath (Join-Path $root $marker) -PathType Leaf)) {
        throw "Not a supported project root: $root (missing $marker)"
    }
}
$catalog = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'topics.json') -Raw -Encoding UTF8 | ConvertFrom-Json

function Get-ProjectPath([string]$RelativePath) {
    $resolved = [IO.Path]::GetFullPath((Join-Path $root $RelativePath))
    $prefix = $root.TrimEnd([char[]]'\/') + [IO.Path]::DirectorySeparatorChar
    if (-not $resolved.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Catalog path escapes project: $RelativePath"
    }
    return $resolved
}

function Read-ProjectFile([string]$RelativePath) {
    $path = Get-ProjectPath $RelativePath
    if (Test-Path -LiteralPath $path -PathType Leaf) {
        return Get-Content -LiteralPath $path -Raw -Encoding UTF8
    }
    return ''
}

function Read-Scalar([string]$Content, [string]$Name) {
    $match = [regex]::Match($Content, '(?m)^\s*' + [regex]::Escape($Name) + ':\s*([^\r\n]+)')
    if ($match.Success) { return $match.Groups[1].Value.Trim() }
    return 'unavailable'
}

if ($List) {
    $items = @($catalog.topics | ForEach-Object {
        [pscustomobject]@{ topic = $_.id; title = $_.title; aliases = $_.aliases }
    })
    if ($Json) { ConvertTo-Json -InputObject $items -Depth 5 }
    else { $items | ForEach-Object { '{0}: {1} ({2})' -f $_.topic, $_.title, ($_.aliases -join ', ') } }
    return
}

if ($Validate) {
    $issues = @()
    $names = @{}
    foreach ($entry in $catalog.topics) {
        foreach ($name in @($entry.id) + @($entry.aliases)) {
            if ($names.ContainsKey($name)) { $issues += "Duplicate topic/alias: $name" }
            $names[$name] = $true
        }
        foreach ($path in @($entry.files) + @($entry.docs)) {
            if (-not (Test-Path -LiteralPath (Get-ProjectPath $path) -PathType Leaf)) {
                $issues += "$($entry.id): missing $path"
            }
        }
    }
    foreach ($path in $catalog.liveSources) {
        if (-not (Test-Path -LiteralPath (Get-ProjectPath $path) -PathType Leaf)) {
            $issues += "Missing live source: $path"
        }
    }
    if ($issues.Count) { throw ($issues -join [Environment]::NewLine) }
    if ($Json) { [pscustomobject]@{ valid = $true; topics = @($catalog.topics).Count } | ConvertTo-Json }
    else { "Catalog valid: $(@($catalog.topics).Count) topics; all mapped files exist." }
    return
}

$key = $Topic.Trim().ToLowerInvariant()
$entries = @($catalog.topics | Where-Object { $_.id -eq $key -or $_.aliases -contains $key })
if ($entries.Count -ne 1) { throw "Unknown or ambiguous topic '$Topic'. Run with -List to see supported topics." }
$entry = $entries[0]
$session = Read-ProjectFile 'Assets/Settings/Networking/SessionConfig.asset'
$manifest = Read-ProjectFile 'Packages/manifest.json' | ConvertFrom-Json
$live = [ordered]@{
    Unity = Read-Scalar (Read-ProjectFile 'ProjectSettings/ProjectVersion.txt') 'm_EditorVersion'
}
foreach ($package in @('com.unity.render-pipelines.universal', 'com.firstgeargames.fishnet', 'com.firstgeargames.fishysteamworks')) {
    $property = $manifest.dependencies.PSObject.Properties[$package]
    $live[$package] = if ($null -ne $property) { $property.Value } else { 'unavailable' }
}
foreach ($field in @('ProtocolVersion', 'Port', 'MaxPlayers', 'ClusteredTestSpawns', 'GameScene')) {
    $live[$field] = Read-Scalar $session $field
}
$scenes = @([regex]::Matches((Read-ProjectFile 'ProjectSettings/EditorBuildSettings.asset'), '(?m)^\s*- enabled: 1\r?\n\s*path: ([^\r\n]+)') | ForEach-Object { $_.Groups[1].Value.Trim() })
$live['EnabledBuildScenes'] = $scenes
$missingSources = @($catalog.liveSources | Where-Object { -not (Test-Path -LiteralPath (Get-ProjectPath $_) -PathType Leaf) })
$files = @($entry.files | ForEach-Object {
    [pscustomobject]@{ path = $_; exists = (Test-Path -LiteralPath (Get-ProjectPath $_) -PathType Leaf) }
})
$docs = @($entry.docs | ForEach-Object {
    [pscustomobject]@{ path = $_; exists = (Test-Path -LiteralPath (Get-ProjectPath $_) -PathType Leaf) }
})
$result = [ordered]@{
    root = $root
    topic = $entry.id
    title = $entry.title
    scope = 'Read-only file context; not a live Editor inspection or gameplay verification.'
    live = $live
    liveSources = $catalog.liveSources
    missingSources = $missingSources
    rules = $catalog.rules
    notes = $entry.notes
    files = $files
    docs = $docs
}
if ($Json) { $result | ConvertTo-Json -Depth 8; return }
"# $($entry.title) [$($entry.id)]"
"Root: $root"
$result.scope
''
'Current file values:'
foreach ($item in $live.GetEnumerator()) { '- {0}: {1}' -f $item.Key, ($item.Value -join ', ') }
'Sources: ' + ($catalog.liveSources -join '; ')
foreach ($source in $missingSources) { "MISSING SOURCE: $source" }
''
'Working rules:'
$catalog.rules | ForEach-Object { "- $_" }
''
'Topic notes (verify against current implementation):'
$entry.notes | ForEach-Object { "- $_" }
''
'Read only the files needed for this task:'
$files | ForEach-Object { if ($_.exists) { "- $($_.path)" } else { "- MISSING: $($_.path)" } }
''
'References only when needed; historical claims are not current settings:'
$docs | ForEach-Object { if ($_.exists) { "- $($_.path)" } else { "- MISSING: $($_.path)" } }
