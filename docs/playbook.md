# Hushlands — Playbook (process)

## Loop
1. One task at a time, in task order. Gates (G) stop for approval.
2. Task done = tests green, zero warnings, capture or log attached, report filed.
3. Report format: Task ID · what changed (3 lines) · files touched · tests and results · capture/log · open questions with default used · next task.

## Lanes and folder ownership
| Lane | Owns |
|---|---|
| lead / orchestrator | `docs/`, `tools/`, `.github/`, `Packages/`, `ProjectSettings/`, `Assets/_Hushlands/Code/Core/` |
| gameplay | `Code/Runtime/Gameplay/`, `Config/Gameplay/` |
| content tool | `Code/Core/Content/`, `Code/Editor/Content/`, `Content/`, `tools/qa/content_sim.py` |
| environment art | `Art/Environment/`, `Scenes/Biomes/` |
| character and cloth | `Art/Characters/`, `Art/Vehicles/`, `Code/Runtime/Cloth/` |
| tech art | `Config/Rendering/`, `Art/VFX/`, `Code/Runtime/Camera/` |
| audio | `Audio/`, `Code/Runtime/Audio/` |
| UI / motion | `UI/`, `Code/Runtime/UI/` |
| meta and data | `Code/Runtime/Meta/`, `Config/Meta/` |
| QA | `Code/Tests/`, `tools/qa/` |

Only one agent drives the Unity Editor at a time. Others edit text assets only.

## Definition of done
- `python3 tools/validate_project.py` green.
- Unity EditMode + PlayMode tests green (CI `unity-ci` job `test`).
- Windows build: zero errors, zero warnings (CI job `build-windows`).
- Visual tasks: 4K frame-time capture (`Hushlands.exe -hushCapture`) with `within_budget=True`, p95 ≤ 16.6 ms. Over budget = rework.

## Commands
```
# Setup (idempotent)
Unity -batchmode -quit -projectPath . -executeMethod Hushlands.Editor.ProjectBootstrap.Run
# Tests
Unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults Logs/editmode.xml
Unity -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults Logs/playmode.xml
# Build
Unity -batchmode -quit -projectPath . -executeMethod Hushlands.Editor.BuildScript.BuildWindows -logFile Logs/build.log
# Engine-free core: compile as Unity does + run its EditMode tests (no Unity needed)
dotnet build tools/dotnet/Core/Hushlands.Core.csproj -warnaserror
dotnet test tools/dotnet/CoreTests/Hushlands.CoreTests.csproj
# Compile-check Unity-bound code without Unity (Unity 6 refs + package stubs checked against Unity source)
export UNITY_REFS=$(tools/dotnet/fetch_unity_refs.sh /tmp/unityrefs)
dotnet build tools/dotnet/Editor/Hushlands.Editor.csproj -warnaserror        # also builds Runtime
dotnet build tools/dotnet/PlayModeTests/Hushlands.PlayModeTests.csproj -warnaserror
dotnet build tools/dotnet/EditModeUnityTests/Hushlands.EditModeUnityTests.csproj -warnaserror
# Content: 200 stretches (Unity) / reference model (no Unity)
Unity -batchmode -projectPath . -executeMethod Hushlands.Editor.ContentToolMenu.ValidateBatch
python3 tools/qa/content_sim.py && python3 tools/qa/content_sim.py --selftest
python3 tools/qa/content_sim.py --journey --count 40 --all-vehicles     # full runs, every biome on one track
python3 tools/qa/hazard_sim.py                                         # landslide/squall over stretches and journeys
python3 tools/validate_project.py                                      # static project checks
# Everything Unity-side on a Windows PC with Unity Hub (bootstrap, EditMode, PlayMode, build, 4K capture)
powershell -ExecutionPolicy Bypass -File tools/unity/run_local.ps1     # -> QA/local/summary.txt
# 4K capture (on Windows test GPU)
Builds/Windows/Hushlands.exe -screen-width 3840 -screen-height 2160 -hushCapture -hushAutopilot
# -> Captures/frametime_*.txt, Captures/frame_*.png  (opens Slice_Journey, the first build scene)
```

## Assets
Original assets only. Binary files go through Git LFS (`.gitattributes`). Commit `.meta` files.
