# Runs the Unity side of Hushlands QA on this Windows PC with the Editor that Unity Hub installed:
# bootstrap, EditMode tests, PlayMode tests, Windows build, then the 4K autopilot capture.
# Results land in QA/local/ (committed so the cloud session can read them).
#
# Usage (PowerShell, from the repo root, with the Unity Editor closed for this project):
#   powershell -ExecutionPolicy Bypass -File tools/unity/run_local.ps1
#   powershell -ExecutionPolicy Bypass -File tools/unity/run_local.ps1 -SkipCapture
param(
    [string]$UnityExe = "",
    [switch]$SkipBuild,
    [switch]$SkipCapture
)

$ErrorActionPreference = "Stop"
$root = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$version = (Select-String -Path (Join-Path $root "ProjectSettings\ProjectVersion.txt") -Pattern "^m_EditorVersion: (.+)$").Matches[0].Groups[1].Value.Trim()
if (-not $UnityExe) {
    $candidates = @(
        "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe",
        "$env:ProgramFiles\Unity\Hub\Editor\$version\Editor\Unity.exe"
    )
    $UnityExe = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
}
if (-not $UnityExe -or -not (Test-Path $UnityExe)) {
    Write-Error "Unity $version not found. Install it from Unity Hub, or pass -UnityExe <path to Unity.exe>."
}

$out = Join-Path $root "QA\local"
New-Item -ItemType Directory -Force -Path $out | Out-Null
$summary = Join-Path $out "summary.txt"
"Hushlands local Unity QA  $(Get-Date -Format s)  Unity $version" | Set-Content $summary

function Step([string]$name, [string[]]$unityArgs) {
    $log = Join-Path $out "$name.log"
    Write-Host "== $name"
    $p = Start-Process -FilePath $UnityExe -ArgumentList ($unityArgs + @("-logFile", "`"$log`"")) -Wait -PassThru -NoNewWindow
    $line = "{0,-10} exit {1}" -f $name, $p.ExitCode
    Write-Host $line
    Add-Content $summary $line
    return $p.ExitCode
}

$proj = @("-batchmode", "-projectPath", "`"$root`"")
$ok = $true
$ok = (Step "bootstrap" ($proj + @("-quit", "-executeMethod", "Hushlands.Editor.ProjectBootstrap.Run")) -eq 0) -and $ok
foreach ($mode in @("EditMode", "PlayMode")) {
    $xml = Join-Path $out "$($mode.ToLower()).xml"
    $code = Step $mode.ToLower() ($proj + @("-runTests", "-testPlatform", $mode, "-testResults", "`"$xml`""))
    if (Test-Path $xml) {
        [xml]$r = Get-Content $xml
        $run = $r.'test-run'
        $line = "  $mode tests: total $($run.total) passed $($run.passed) failed $($run.failed) skipped $($run.skipped)"
        Add-Content $summary $line
        foreach ($t in $r.SelectNodes("//test-case[@result='Failed']")) {
            Add-Content $summary ("  FAILED " + $t.fullname + " :: " + ($t.failure.message.'#cdata-section' -replace "\s+", " "))
        }
    }
    $ok = ($code -eq 0) -and $ok
}
if (-not $SkipBuild) {
    $ok = (Step "build" ($proj + @("-quit", "-executeMethod", "Hushlands.Editor.BuildScript.BuildWindows")) -eq 0) -and $ok
}
if (-not $SkipCapture -and -not $SkipBuild) {
    $exe = Join-Path $root "Builds\Windows\Hushlands.exe"
    if (Test-Path $exe) {
        Write-Host "== capture (4K, autopilot, journey)"
        $p = Start-Process -FilePath $exe -WorkingDirectory $root -Wait -PassThru -ArgumentList @(
            "-screen-width", "3840", "-screen-height", "2160", "-hushCapture", "-hushAutopilot",
            "-logFile", "`"$(Join-Path $out 'player.log')`"")
        Add-Content $summary ("capture    exit " + $p.ExitCode)
        $caps = Join-Path $root "Captures"
        if (Test-Path $caps) { Copy-Item (Join-Path $caps "frametime_*.txt") $out -ErrorAction SilentlyContinue }
    }
}

# Errors and warnings from every Unity log, so the summary alone shows what went wrong.
foreach ($log in Get-ChildItem $out -Filter *.log) {
    $hits = Select-String -Path $log.FullName -Pattern "error CS\d+|warning CS\d+|Exception:|\[Hushlands\].*(error|Config)" | Select-Object -First 40
    foreach ($h in $hits) { Add-Content $summary ("  " + $log.BaseName + ": " + $h.Line.Trim()) }
}
Add-Content $summary ("RESULT " + $(if ($ok) { "PASS" } else { "FAIL" }))
Get-Content $summary
Write-Host ""
Write-Host "Share results: git add QA/local && git commit -m 'Local Unity QA results' && git push"
