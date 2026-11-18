#Requires -Version 7.0
[CmdletBinding()]
param(
    [ValidateSet('Compile', 'EditMode', 'PlayMode')]
    [string]$Mode = 'Compile',

    [string]$TestFilter,

    [string]$EditorPath = $env:UNITY_EDITOR
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

if ($Mode -eq 'Compile' -and $TestFilter) {
    throw 'Use -TestFilter with -Mode EditMode or -Mode PlayMode.'
}

$projectPath = Split-Path -Parent $PSScriptRoot
$versionFile = Join-Path $projectPath 'ProjectSettings/ProjectVersion.txt'
$versionLine = Get-Content -LiteralPath $versionFile -TotalCount 1
if ($versionLine -notmatch '^m_EditorVersion:\s*([\w.]+)\s*$') {
    throw "Could not read the Unity editor version from $versionFile."
}
$editorVersion = $Matches[1]

if (-not $EditorPath) {
    $EditorPath = Join-Path $env:ProgramFiles "Unity/Hub/Editor/$editorVersion/Editor/Unity.exe"
}

if (-not (Test-Path -LiteralPath $EditorPath -PathType Leaf)) {
    throw "Unity $editorVersion was not found at $EditorPath. Set UNITY_EDITOR or pass -EditorPath."
}
$EditorPath = (Resolve-Path -LiteralPath $EditorPath).Path

$runId = '{0:yyyyMMdd-HHmmss-fff}-{1}' -f (Get-Date), $PID
$outputPath = Join-Path $projectPath "Logs/Verification/$runId"
New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
$logPath = Join-Path $outputPath 'Editor.log'
$unityArguments = @('-batchmode', '-nographics', '-projectPath', $projectPath, '-logFile', $logPath)

if ($Mode -eq 'Compile') {
    $unityArguments += '-quit'
} else {
    $resultsPath = Join-Path $outputPath 'TestResults.xml'
    $unityArguments += @('-runTests', '-testPlatform', $Mode, '-testResults', $resultsPath)
    if ($TestFilter) {
        $unityArguments += @('-testFilter', $TestFilter)
    }
}

Write-Output "Running Unity $editorVersion ($Mode). Log at $logPath"
# Piping waits for the Windows GUI executable and keeps the console output short.
& $EditorPath @unityArguments | Out-Null
$unityExitCode = $LASTEXITCODE

$testRun = $null
if ($Mode -ne 'Compile' -and (Test-Path -LiteralPath $resultsPath -PathType Leaf)) {
    [xml]$results = Get-Content -LiteralPath $resultsPath -Raw
    $testRun = $results.'test-run'
    Write-Output "Tests passed $($testRun.passed), failed $($testRun.failed), skipped $($testRun.skipped). Results at $resultsPath"
}

if ($unityExitCode -ne 0) {
    Write-Output "Unity exited with code $unityExitCode. See $logPath"
    exit $unityExitCode
}

if ($Mode -ne 'Compile') {
    if (-not $testRun) {
        Write-Output "Unity wrote no test report. See $logPath"
        exit 1
    }

    if (([int]$testRun.passed + [int]$testRun.failed) -eq 0) {
        Write-Output "No tests executed. Check the test assemblies and filter. See $logPath"
        exit 1
    }

    if ($testRun.result -ne 'Passed') {
        Write-Output "The test run did not pass. See $resultsPath"
        exit 1
    }
}

Write-Output "Unity $Mode check passed."
