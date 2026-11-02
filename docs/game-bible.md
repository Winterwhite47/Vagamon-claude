# Hushlands — Game Bible (canon v1)

> "Beyond the mist, someone is waiting for you to come home."

## Pitch
Slide an endless slope from the Windmouth ridge through meadow, pine, tea terrace and lake,
rescuing ducklings while a landslide chases. Calm, hyper-real, 3D world with a 2.5D gameplay plane.

## Platform and tech
- Unity 6.3 LTS, HDRP, Windows x64 (D3D12, IL2CPP). No mobile, consoles, multiplayer.
- Target 3840x2160 at 60 fps with upscaling (DLSS / FSR via HDRP dynamic resolution). Budget 16.6 ms (p95).

## Controls (single lane)
| Input | Ground | Air |
|---|---|---|
| Tap | Jump | — |
| Hold | Tuck (less drag) | Flip |

Rules (`RiderBrain`, T3): a tap is a press released within 0.18 s and jumps on release; coyote 0.08 s and jump
buffer 0.1 s apply. Hold past 0.18 s on ground = tuck. Hold past 0.18 s in air = flip, only on a press that began
in the air (a tuck carried off a lip never flips; taps never flip). Landing within 35° of the slope normal is safe.

## Physics
- PhysX, fixed step 0.01 s. Rider is a rigid body constrained to a spline slope plane.
- Per-surface friction and drag (`ISurfaceProvider`).
- Speed 6–18 m/s. Ramp +0.45 m/s per 10 s via config slope assist (`ISpeedModel.TargetSpeed`).
- Plane (T3): physics runs in the world XY plane (x = distance, y = height, z frozen). Speed is horizontal speed,
  identical in `KinematicRider`, `PhysxRider` and the content `JumpModel`. Visual world bending onto a spline is a
  presentation mapping (T7).

## Camera
Cinemachine, seven states: Cruise, Air, Boost, Vista, Danger, Shelter, FailureHold.
Per-biome profiles. Audio mix snapshots follow camera state and biome; mist low-passes audio.

Director (T4): signals → `CameraStateRules` → `CameraStateMachine` (priority FailureHold > Danger > Boost > Air >
Vista = Shelter > Cruise; higher priority takes over at once, lower waits 0.6 s dwell) → `CameraRig` (smoothstep blends,
retargets from current values, never cuts; X locked to rider, Y smoothed). One procedural `CinemachineCamera` applies
the pose. Framing contract: every state except FailureHold shows ≥ 40 m ahead at 16:10 on a 14° slope, rider ≥ −0.8 from
centre. Air needs 0.25 s airtime; Boost at ≥ 15 m/s or boost pickup; Danger when landslide < 2.5 s or collapse warning.
Reduced motion locks framing to Cruise (no zooms). Mix snapshots follow every camera state and biome change.

## Look
Hyper-real but calm. Volumetric mist, soft light.
Palette: emerald `#0B3B17`, terracotta `#8D3F2D`, mist blue `#4A6B82`, gold `#D4AF37`.

## Vertical slice (T7): Meadow Sea, golden hour
Built procedurally at play time by `SliceBootstrap` (scene `Slice_MeadowSea`, first in the build):
generated seed-2 stretch, PhysX track, terrain (corridor exactly on the slope line, ravines at gaps, front ground always
below the track, rolling backdrop hills), terracotta track strip, stone obstacles, nests, gold pickups, local mist
volumes per content mist bank, physically based sky, volumetric fog in mist blue, ACES.
Golden hour: sun 9° high from −38°, 3600 K, 32 klx, EV 12.6. Mist visual rule: a jump behind a mist bank at the
fairness threshold keeps ≥ 60% transmittance at the farthest camera (Meadow: 65%).
Audio: mist low-pass on the listener from `MixDirector`; snapshot bridge for an authored AudioMixer.
All art is original procedural placeholder in the canon palette; final hero assets are an approval item.

## UI
Motion graphics, smooth video-like transitions over the live 3D world. Near-zero HUD.
Continuous camera and mist transitions; no loading cuts. Reduced-motion option.

## Content
- Vehicles: wooden hill sled (default), tea-crate board, banana-leaf toboggan, bamboo sled, glider-skid, rain-umbrella slider.
- Pickups: spice coins, mist lantern, wind feather, tea flask, magnet bell, golden mistbell.
- Biomes v1: Windmouth Ridge, Meadow Sea, Pine Hollow, Terrace Country, Mirror Lake.

## Failure and retry (T5)
`RunFlow`: crash → FailureHold 0.9 s with a calm line and "Tap to ride again" (tap skips after 0.3 s) → Retrying
0.6 s mist wipe; the rider moves to the last checkpoint halfway through, ducklings restored, camera snapped → Riding.
Fastest retry 0.9 s. Crash causes: obstacle contact, landing > 35° off the slope, gap/pit, landslide.
Free assists switch on automatically after 3 consecutive failures (setting can require a prompt): cruise speed × 0.85
(gives ≥ 0.5 s extra reveal), landing tolerance +15°. They switch off after 2 clean checkpoints.
Lines live in `MessageSettings`; validation bans words like hurt, fall, lost, drown, crash, fail, bad.

## Scarf and sled flex (T6)
Scarf: 10-segment verlet rope (1.2 m), ≤ 4 substeps of 1/120 s, 6 constraint passes, 85% of anchor motion carried
(rider-relative frame) with drag on true world velocity, rider body sphere. Hitches longer than the substep budget carry
the rope instead of exploding. Quality bar: ≤ 6% stretch at 18 m/s through jumps and hitches.
Alternative: Unity Cloth strip (same length, root pinned). `ScarfBenchmark` measures both; `ScarfMethodSelector` takes
the cheaper per-scarf cost, ties (within 10%) to Verlet, flags rework if both exceed 0.05 ms.
Sled flex: damped spring kicked by landing speed (≤ 6°), settles in about 0.5 s.

## Duckling safety (absolute)
- Never shown injured, drowning or falling.
- Nest cover closes on crash.
- Checkpoint groups restored.
- Missed groups are "Waiting", never lost. (Enforced in `DucklingGroupStatus`: Ahead, Rescued, Waiting only.)

## Fairness
- 1.5 s obstacle reveal at current speed.
- Mist never hides a required jump.
- 1 s collapse warning.
- Safe landings exist for every required jump.
- Free assists after 3 consecutive failures.
- Never require locked upgrades (bot validates with default sled, no upgrades).

## Interfaces (code against these)
Defined in `Assets/_Hushlands/Code/Core/Interfaces.cs`, engine-free assembly `Hushlands.Core`.

| Interface | Owner lane | Purpose |
|---|---|---|
| `IInputSource` | gameplay | tap / hold state per fixed step |
| `ISlopeSpline` | gameplay | height, angle, surface by distance |
| `ISurfaceProvider` | gameplay | friction, drag per surface |
| `ISpeedModel` | gameplay | speed integration and ramp |
| `IRider` | gameplay | rider state machine on slope plane |
| `IChunk`, `IChunkGenerator` | content tool | deterministic stretches |
| `IFairnessChecker`, `IBotValidator` | content tool | fairness rules, bot clear |
| `ICameraDirector` | tech art / camera | seven states, biome profiles |
| `IAudioMixDirector` | audio | snapshots, mist low-pass |
| `ICheckpointService`, `IFailureService`, `IAssistService` | gameplay | crash, retry, assists |
| `IDucklingNestService` | gameplay | rescue loop, safety |
| `IPickupEffect`, `IScoreService` | meta and data | pickups, score |
| `ISaveService` | meta and data | persistence |
| `IFrameTimeProbe` | QA | frame-time stats |

## Content format (T2)
Chunk = constant downhill slope, surface spans, features (positions in metres from chunk start).
Text format `.chunk` (`ChunkSerializer`): `chunk id biome difficulty length slopeDeg`, `surface start length surface`,
`feature kind start length height density variant optional requiresUpgrade warningLead`, `end`.
Feature kinds: Obstacle, Gap, CollapseZone (required jumps), MistBank, DucklingNest, Pickup, Checkpoint, Ramp.

Fairness rules (`FairnessChecker`, checked at every speed 6–18 m/s, default sled, no upgrades):
`jump.clearable` (whole takeoff window 0.15–0.35 s clears), `landing.safe` + `zone.within.chunk` (clear runway to the
farthest touchdown + safe length), `hazard.spacing`, `mist.hides.jump` (no mist over the 1.5 s reveal window or the jump),
`camera.reveal` (camera shows ≥ 1.5 s × 18 m/s ahead), `collapse.warning` (≥ 1 s at max speed), `upgrade.required`,
`checkpoint.present` / `checkpoint.safe`, `nest.safe` (ducklings never near hazards), `pickup.on.hazard`,
plus bounds, contiguity and surface cover. The bot (`BotValidator`) must also ride every stretch clean.

## Config map (T1)
All numbers: `Assets/_Hushlands/Config/<Lane>/<Name>Config.asset`, defaults in `Code/Core/Config/*Settings.cs`.
Entry point: `HushConfigSet` (Resources). Gameplay: Speed, Rider, Surface, Failure, Assist, Duckling.
Content: Fairness, Content, Biome. Camera. Audio: AudioMix. UI: UiMotion. Meta: Pickup, Score, Vehicle.
Rendering: ProjectTargets, BootLook.

## Defaults (when a rule is unclear)
- Numbers: ScriptableObject config under `Assets/_Hushlands/Config`. Never literals in behaviour code.
- Out of scope (deferred): open world, hub town, multiplayer, mobile, consoles, accounts, shop, analytics SDKs, release / Steam.
