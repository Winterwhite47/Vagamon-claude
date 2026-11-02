#!/usr/bin/env python3
"""Hushlands static project checks. Runs without Unity. Exit 0 = green."""
import json
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
CODE = ROOT / "Assets/_Hushlands/Code"
errors: list[str] = []
checks = 0


def check(cond: bool, msg: str) -> None:
    global checks
    checks += 1
    if not cond:
        errors.append(msg)


# 1. JSON files parse.
for p in [ROOT / "Packages/manifest.json", *CODE.rglob("*.asmdef")]:
    try:
        json.loads(p.read_text())
        check(True, "")
    except json.JSONDecodeError as e:
        check(False, f"{p.relative_to(ROOT)}: bad JSON {e}")

# 2. Required packages.
manifest = json.loads((ROOT / "Packages/manifest.json").read_text())["dependencies"]
for pkg in ["com.unity.render-pipelines.high-definition", "com.unity.cinemachine",
            "com.unity.inputsystem", "com.unity.test-framework", "com.unity.splines"]:
    check(pkg in manifest, f"manifest missing {pkg}")

# 3. asmdef graph: references resolve to known names or Unity packages; Core is engine-free.
asmdefs = {json.loads(p.read_text())["name"]: (p, json.loads(p.read_text())) for p in CODE.rglob("*.asmdef")}
for name, (p, data) in asmdefs.items():
    for ref in data.get("references", []):
        check(ref in asmdefs or ref.startswith(("Unity.", "UnityEngine.", "UnityEditor.")),
              f"{name}: unknown reference {ref}")
check(asmdefs.get("Hushlands.Core", (None, {}))[1].get("noEngineReferences") is True,
      "Hushlands.Core must set noEngineReferences")
check(asmdefs["Hushlands.Core"][1].get("references") == [], "Hushlands.Core must have no references")

# 4. C# sanity: balanced braces, namespace present, Core never uses UnityEngine.
for cs in CODE.rglob("*.cs"):
    src = cs.read_text()
    stripped = re.sub(r'"(?:\\.|[^"\\])*"', '""', re.sub(r"//.*", "", src))
    check(stripped.count("{") == stripped.count("}"), f"{cs.relative_to(ROOT)}: unbalanced braces")
    check(stripped.count("(") == stripped.count(")"), f"{cs.relative_to(ROOT)}: unbalanced parens")
    check("namespace Hushlands" in src, f"{cs.relative_to(ROOT)}: missing Hushlands namespace")
    check(not re.search(r"\b(TODO|FIXME|HACK)\b", src), f"{cs.relative_to(ROOT)}: TODO/FIXME left in code")
    if CODE / "Core" in cs.parents:
        check("UnityEngine" not in stripped and "UnityEditor" not in stripped,
              f"{cs.relative_to(ROOT)}: Core must not use Unity APIs")

# 5. Canon interfaces declared.
interfaces = (CODE / "Core/Interfaces.cs").read_text()
for name in ["IInputSource", "ISlopeSpline", "ISurfaceProvider", "ISpeedModel", "IRider",
             "IChunkGenerator", "IFairnessChecker", "IBotValidator", "ICameraDirector",
             "IAudioMixDirector", "ICheckpointService", "IFailureService", "IAssistService",
             "IDucklingNestService", "IPickupEffect", "IScoreService", "ISaveService", "IFrameTimeProbe"]:
    check(re.search(rf"\binterface {name}\b", interfaces) is not None, f"interface {name} missing")

# 6. Duckling safety at type level: no Lost/Injured/Dead/Drowned states.
core_types = (CODE / "Core/CoreTypes.cs").read_text()
duck = re.search(r"enum DucklingGroupStatus\s*\{([^}]*)\}", core_types).group(1)
check(not re.search(r"\b(Lost|Injured|Dead|Drown\w*|Fall\w*)\b", duck), "DucklingGroupStatus has unsafe state")
cams = re.search(r"enum CameraState\s*\{([^}]*)\}", core_types).group(1)
check(len(re.findall(r"\w+\s*=", cams)) == 7, "CameraState must have exactly seven states")

# 7. Canon numbers live in config, not in Runtime behaviour code.
targets = (CODE / "Runtime/ProjectTargetsConfig.cs").read_text()
for token in ["fixedTimestep = 0.01f", "targetWidth = 3840", "targetHeight = 2160",
              "targetFrameRate = 60", "frameBudgetMs = 16.6f", "0x0B, 0x3B, 0x17",
              "0x8D, 0x3F, 0x2D", "0x4A, 0x6B, 0x82", "0xD4, 0xAF, 0x37"]:
    check(token in targets, f"ProjectTargetsConfig missing canon value {token}")
for cs in (CODE / "Runtime").glob("*.cs"):
    if cs.name.endswith("Config.cs"):
        continue
    src = re.sub(r"//.*", "", cs.read_text())
    check(not re.search(r"\b(0\.01f|3840|2160|16\.6)\b", src), f"{cs.name}: canon number hard-coded")

# 8. Git hygiene: LFS rules and .meta not ignored.
attrs = (ROOT / ".gitattributes").read_text()
for ext in ["*.png", "*.fbx", "*.wav", "*.exr", "*.psd"]:
    check(f"{ext} " in attrs and "filter=lfs" in attrs, f".gitattributes missing LFS rule {ext}")
check(".meta" not in (ROOT / ".gitignore").read_text().split("Keep .meta")[0], ".gitignore must not ignore .meta")

# 9. Docs present.
for doc in ["docs/game-bible.md", "docs/playbook.md", "docs/decisions.md"]:
    check((ROOT / doc).is_file(), f"missing {doc}")

# 10. T1: every settings block has a config asset wrapper, is linked in HushConfigSet and built by bootstrap.
settings_src = "".join(f.read_text() for f in (CODE / "Core/Config").glob("*.cs"))
settings = sorted(set(re.findall(r"class (\w+)Settings\s*:\s*ISettings", settings_src)))
check(len(settings) == 16, f"expected 16 settings blocks, found {len(settings)}")
config_set = (CODE / "Runtime/Config/HushConfigSet.cs").read_text()
builder = (CODE / "Editor/ConfigSetBuilder.cs").read_text()
for name in settings:
    f = CODE / f"Runtime/Config/{name}Config.cs"
    check(f.is_file(), f"missing wrapper {f.name}")
    if f.is_file():
        check(f"class {name}Config : SettingsAsset<{name}Settings>" in f.read_text(), f"{f.name}: class/file mismatch")
    check(f"public {name}Config " in config_set, f"HushConfigSet missing {name}Config")
    check(f"Make<{name}Config>" in builder, f"ConfigSetBuilder missing {name}Config")
    check(f"new {name}Settings()" in (CODE / "Tests/EditMode/SettingsTests.cs").read_text(), f"SettingsTests missing {name}")

# 11. Service stubs carry no tuning literals (only 0, 1, 360-degree constants).
for cs in [*(CODE / "Core/Services").glob("*.cs"), *(CODE / "Core/Content").glob("*.cs"), *(CODE / "Core/Gameplay").glob("*.cs")]:
    src = re.sub(r'"(?:\\.|[^"\\])*"', '""', re.sub(r"//.*", "", cs.read_text()))
    for lit in re.findall(r"(?<![\w.])(\d+\.\d+|\d+)f\b", src):
        check(lit in ("0", "1", "360"), f"{cs.name}: tuning literal {lit}f outside config")

# 12. Canon numbers in settings defaults.
for token in ["minSpeed = 6f", "maxSpeed = 18f", "rampAmount = 0.45f", "rampIntervalSeconds = 10f",
              "obstacleRevealSeconds = 1.5f", "collapseWarningSeconds = 1f", "assistAfterFailures = 3",
              "physicsStep = 0.01f", "validationStretchCount = 200"]:
    check(token in settings_src, f"settings missing canon default {token}")

# 13. T2: content pipeline present; Core content is engine-free; reference model in sync.
for name in ["Rng", "ChunkFormat", "ChunkSerializer", "JumpModel", "StretchSlope", "HazardZones",
             "ChunkGenerator", "FairnessChecker", "BotValidator", "StretchValidation"]:
    check((CODE / f"Core/Content/{name}.cs").is_file(), f"missing Core/Content/{name}.cs")
rules = re.findall(r'public const string Rule\w+ = "([\w.]+)"', (CODE / "Core/Content/FairnessChecker.cs").read_text())
sim = (ROOT / "tools/qa/content_sim.py").read_text()
for rule in rules:
    if rule in ("upgrade.required",):
        continue
    check(f'"{rule}"' in sim, f"content_sim.py does not mirror rule {rule}")
check(len(rules) >= 15, "fairness checker lost rules")

for e in errors:
    print("FAIL:", e)
print(f"validate_project: {checks - len(errors)}/{checks} checks passed, {len(errors)} failed")
sys.exit(1 if errors else 0)
