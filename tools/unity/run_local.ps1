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
    [switch]$SkipCapture,
    [int]$CaptureTimeoutSeconds = 900
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
# Parenthesise the call: "Step a b -eq 0" would pass -eq and 0 to Step as extra arguments.
$ok = ((Step "bootstrap" ($proj + @("-quit", "-executeMethod", "Hushlands.Editor.ProjectBootstrap.Run"))) -eq 0) -and $ok
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
        if ([int]$run.failed -gt 0 -or [int]$run.total -eq 0) { $ok = $false }
    } else {
        Add-Content $summary "  $mode tests: no results file (compile error or crash; see $($mode.ToLower()).log)"
        $ok = $false
    }
    $ok = ($code -eq 0) -and $ok
}
if (-not $SkipBuild) {
    $ok = ((Step "build" ($proj + @("-quit", "-executeMethod", "Hushlands.Editor.BuildScript.BuildWindows"))) -eq 0) -and $ok
}
if (-not $SkipCapture -and -not $SkipBuild) {
    $exe = Join-Path $root "Builds\Windows\Hushlands.exe"
    if (-not (Test-Path $exe)) {
        Add-Content $summary "capture    MISSING $exe"
        $ok = $false
    } else {
        Write-Host "== capture (4K, autopilot, journey)"
        # The player writes Captures\ next to its _Data folder (Builds\Windows\Captures), not in the repo root.
        $caps = Join-Path (Split-Path $exe) "Captures"
        $before = @(Get-ChildItem $caps -Filter "frametime_*.txt" -ErrorAction SilentlyContinue | ForEach-Object { $_.FullName })
        $p = Start-Process -FilePath $exe -WorkingDirectory (Split-Path $exe) -PassThru -ArgumentList @(
            "-screen-width", "3840", "-screen-height", "2160", "-screen-fullscreen", "1", "-hushCapture", "-hushAutopilot",
            "-logFile", "`"$(Join-Path $out 'player.log')`"")
        $null = $p.Handle  # cache the handle so ExitCode is readable after exit
        if (-not $p.WaitForExit($CaptureTimeoutSeconds * 1000)) {
            $p.Kill()
            Add-Content $summary "capture    TIMEOUT after $CaptureTimeoutSeconds s"
            $ok = $false
        } else {
            # Exit 0 = within budget, 2 = over budget (FrameTimeCapture.QuitAfterCapture).
            Add-Content $summary ("capture    exit " + $p.ExitCode)
            $ok = ($p.ExitCode -eq 0) -and $ok
        }
        $new = @(Get-ChildItem $caps -Filter "frametime_*.txt" -ErrorAction SilentlyContinue | Where-Object { $before -notcontains $_.FullName })
        if ($new.Count -eq 0) {
            Add-Content $summary "capture    no new frametime_*.txt in $caps"
            $ok = $false
        }
        foreach ($f in $new) {
            Copy-Item $f.FullName $out
            $png = Join-Path $caps (($f.BaseName -replace "^frametime_", "frame_") + ".png")
            if (Test-Path $png) { Copy-Item $png $out }
            foreach ($kv in Get-Content $f.FullName | Where-Object { $_ -match "^(resolution|gpu|avg_ms|p\d+_ms|budget_ms|within_budget)=" }) {
                Add-Content $summary ("  " + $kv)
            }
        }
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
