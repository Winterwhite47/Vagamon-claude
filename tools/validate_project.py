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
check(len(settings) == 22, f"expected 22 settings blocks, found {len(settings)}")
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
for cs in [*(CODE / "Core/Services").glob("*.cs"), *(CODE / "Core/Content").glob("*.cs"), *(CODE / "Core/Gameplay").glob("*.cs"), *(CODE / "Core/Flow").glob("*.cs")]:
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

# 14. T4: camera director core present, seven profiles, contract wired into config validation.
for name in ["FramingMath", "CameraRules", "CameraRig", "CameraDirectorCore"]:
    check((CODE / f"Core/Camera/{name}.cs").is_file(), f"missing Core/Camera/{name}.cs")
pres = (CODE / "Core/Config/PresentationSettings.cs").read_text()
check(len(re.findall(r"new CameraStateProfile\(CameraState\.", pres)) == 7, "need seven camera profiles")
check("FramingMath.CheckContract" in (CODE / "Runtime/Config/HushConfigSet.cs").read_text(), "framing contract not validated")
check((ROOT / "tools/qa/camera_sim.py").is_file(), "missing camera reference model")
for cs in (CODE / "Core/Camera").glob("*.cs"):
    src = re.sub(r"//.*", "", cs.read_text())
    check("UnityEngine" not in src, f"{cs.name}: Core camera must be engine-free")

# 15. T5: failure lines are calm and duckling-safe (mirrors MessageSettings.IsClean).
gp = (CODE / "Core/Config/GameplaySettings.cs").read_text()
msg_body = gp[gp.index("class MessageSettings"):gp.index("public string[] For(")]
banned = re.findall(r'"([^"]+)"', msg_body[msg_body.index("bannedWords"):])
lines = re.findall(r'"([^"]+)"', msg_body[:msg_body.index("public int maxLength")])
check(len(lines) >= 10 and len(banned) >= 10, "message catalogue too small")
for line in lines:
    low = line.lower()
    check(len(line) <= 60 and not any(b in low for b in banned), f"failure line not clean: {line!r}")
check((CODE / "Core/Flow/RunFlow.cs").is_file(), "missing RunFlow")

# 16. T6: both scarf methods, benchmark and selector exist; reference model present.
for f in ["Core/Cloth/VerletScarfSolver.cs", "Runtime/Cloth/VerletScarf.cs", "Runtime/Cloth/UnityClothScarf.cs",
          "Runtime/Cloth/ScarfBenchmark.cs", "Runtime/Cloth/SledFlexDriver.cs", "Editor/Tech/ScarfBenchmarkMenu.cs"]:
    check((CODE / f).is_file(), f"missing {f}")
check((ROOT / "tools/qa/scarf_sim.py").is_file(), "missing scarf reference model")
check("UnityEngine" not in re.sub(r"//.*", "", (CODE / "Core/Cloth/VerletScarfSolver.cs").read_text()), "solver must be engine-free")

# 17. T7: slice pieces present; Meadow Sea look uses canon palette; mist visibility rule wired.
for f in ["Core/Config/LookSettings.cs", "Core/World/TerrainSampler.cs", "Runtime/World/SliceBuilder.cs",
          "Runtime/World/SliceBootstrap.cs", "Runtime/World/TerrainMeshBuilder.cs", "Runtime/World/RetryOverlay.cs",
          "Runtime/World/MistLowPass.cs", "Runtime/World/AudioMixBridge.cs", "Editor/World/SliceMenu.cs"]:
    check((CODE / f).is_file(), f"missing {f}")
look_src = (CODE / "Core/Config/LookSettings.cs").read_text()
meadow = look_src[look_src.index("biome = BiomeId.MeadowSea"):]
meadow = meadow[:meadow.index("},")]
for token in ["fogAlbedo = MistBlue", "grass = Emerald", "soil = Terracotta", "accent = Gold", "tuned = true"]:
    check(token in meadow, f"Meadow Sea look missing {token}")
check("MistVisibility.Check" in (CODE / "Runtime/Config/HushConfigSet.cs").read_text(), "mist visibility not validated")

# 18. T8: pickup system, session, presenter; every PickupId has an effect path; boost capped by speed model.
for f in ["Core/Meta/PickupSystem.cs", "Core/Meta/RunSession.cs", "Runtime/World/PickupPresenter.cs", "Runtime/World/ScoreHud.cs"]:
    check((CODE / f).is_file(), f"missing {f}")
ps_src = (CODE / "Core/Meta/PickupSystem.cs").read_text()
for name in ["SpiceCoin", "GoldenMistbell", "WindFeather", "MagnetBell", "MistLantern", "TeaFlask"]:
    check(f"PickupId.{name}" in ps_src, f"PickupSystem does not handle {name}")
check("if (t > _s.maxSpeed) t = _s.maxSpeed;" in (CODE / "Core/Services/GameplayServices.cs").read_text(), "boost must be capped at maxSpeed")

# 19. T9: duckling loop, audit and presenter; covers reopen only when riding resumes; ducklings have no physics.
for f in ["Core/Ducklings/DucklingLoop.cs", "Runtime/World/DucklingPresenter.cs"]:
    check((CODE / f).is_file(), f"missing {f}")
flow_src = (CODE / "Core/Flow/RunFlow.cs").read_text()
check("_ducklings.OpenCovers();" in flow_src, "covers must reopen in RunFlow when riding resumes")
nest_src = (CODE / "Core/Services/FlowServices.cs").read_text()
restore = nest_src[nest_src.index("public void RestoreToCheckpoint"):nest_src.index("public void OpenCovers")]
check("NestCoversClosed = false" not in restore, "RestoreToCheckpoint must not open covers")
pres = (CODE / "Runtime/World/DucklingPresenter.cs").read_text()
check("Rigidbody" not in pres and "DucklingSafetyAudit.Check" in pres, "ducklings must have no physics and be audited")

# 20. T10: hazards present, landslide floor above rider floor (chase is real), squall fog in mist check.
for f in ["Core/Hazards/Hazards.cs", "Core/Config/HazardSettings.cs", "Runtime/World/HazardPresenter.cs", "Runtime/Config/HazardConfig.cs"]:
    check((CODE / f).is_file(), f"missing {f}")
hz = cs_scalars = None
import importlib.util
spec = importlib.util.spec_from_file_location("content_sim", ROOT / "tools/qa/content_sim.py")
mod = importlib.util.module_from_spec(spec); spec.loader.exec_module(mod)
H = mod.scalars(mod.class_body((CODE / "Core/Config/HazardSettings.cs").read_text(), "HazardSettings"))
SP = mod.scalars(mod.class_body((CODE / "Core/Config/GameplaySettings.cs").read_text(), "SpeedSettings"))
check(H["landslideMinSpeed"] > SP["minSpeed"], "landslide floor must exceed rider minimum speed")
check(H["landslideMaxSpeed"] < SP["maxSpeed"], "landslide must stay slower than max rider speed")
check("squallFogScale" in (CODE / "Runtime/Config/HushConfigSet.cs").read_text(), "squall fog not in mist fairness check")
check((ROOT / "tools/qa/hazard_sim.py").is_file(), "missing hazard reference model")

# 21. T11: fairness against the weakest vehicle; garage unlocks by earned coins (no shop); loadout per vehicle.
check("ForDefaultVehicle" not in "".join(f.read_text() for f in CODE.rglob("*.cs")), "fairness must use ForAllVehicles")
veh_src = (CODE / "Core/Meta/Vehicles.cs").read_text()
for bad in ["Buy", "Purchase", "Spend", "Price"]:
    check(bad not in veh_src, f"garage must not have a shop ({bad})")
check("--all-vehicles" in (ROOT / "tools/qa/content_sim.py").read_text(), "content mirror must ride every vehicle")

# 22. Unity serialization: each MonoBehaviour / ScriptableObject lives in a file with its own name.
for cs in CODE.rglob("*.cs"):
    src = re.sub(r"//.*", "", cs.read_text())
    for m in re.finditer(r"public (?:sealed |abstract )?class (\w+)(?:<[^>]*>)?\s*:\s*(MonoBehaviour|ScriptableObject|SettingsAsset<)", src):
        check(m.group(1) == cs.stem, f"{cs.relative_to(ROOT)}: {m.group(1)} must be in {m.group(1)}.cs")

# 23. T12: save is versioned + checksummed + atomic; progression wired; goals clean; tests never touch the player save.
fs = (CODE / "Core/Meta/FileSaveService.cs").read_text()
for token in ["public const int Version", "Crc32", ".bak", ".tmp", "ReadOnlyNewerVersion"]:
    check(token in fs, f"save missing {token}")
for t in ["Tests/PlayMode/SliceTests.cs", "Tests/PlayMode/DucklingSafetyTests.cs"]:
    check("SaveProvider.UseMemory = true" in (CODE / t).read_text(), f"{t} must isolate the player save")
check("AttachProgression" in (CODE / "Runtime/World/SliceBootstrap.cs").read_text(), "slice must report runs to progression")

# 24. T13: UI core engine-free; pause never uses timeScale; all UI text from config; contrast enforced.
for f in ["Core/UI/UiModel.cs", "Core/Config/UiSettings.cs", "Runtime/UI/UiDirector.cs", "Runtime/UI/UiInput.cs"]:
    check((CODE / f).is_file(), f"missing {f}")
all_rt = "".join(f.read_text() for f in (CODE / "Runtime").rglob("*.cs"))
check("Time.timeScale" not in re.sub(r"//.*", "", all_rt), "pause must not use Time.timeScale (input runs in FixedUpdate)")
ui_src = re.sub(r"//.*", "", (CODE / "Runtime/UI/UiDirector.cs").read_text())
literals = [m for m in re.findall(r'"([^"]*)"', ui_src) if re.search(r"[a-z]{3,} [a-z]{3,}", m)]
check(not literals, f"UI prose must come from UiSettings, found {literals[:3]}")
check("Contrast.Ratio" in (CODE / "Core/Config/UiSettings.cs").read_text(), "UI contrast must be validated")

# 25. T14: tuned biomes each have a slice scene path, a fair content log, and scatter that respects the corridor.
look_src = (CODE / "Core/Config/LookSettings.cs").read_text()
tuned = re.findall(r"biome = BiomeId\.(\w+), tuned = true", look_src)
check("MeadowSea" in tuned and "WindmouthRidge" in tuned, f"tuned biomes: {tuned}")
check("SliceSceneFor" in (CODE / "Editor/World/SliceMenu.cs").read_text(), "slice scenes must be per biome")
check((ROOT / "docs/logs/T14_windmouth_content.txt").is_file(), "missing Windmouth content log")
check("--biome" in (ROOT / "tools/qa/content_sim.py").read_text(), "content mirror needs per-biome mode")

for e in errors:
    print("FAIL:", e)
print(f"validate_project: {checks - len(errors)}/{checks} checks passed, {len(errors)} failed")
sys.exit(1 if errors else 0)
