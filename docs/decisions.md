# Decisions and open questions

Format: ID · date · question · default used · status.

## D-001 · 2026-11-02 · No Unity Editor in agent container
- Context: cloud container is Linux, no Unity install, no license, no GPU. Installing external SDKs blocked by policy.
- Default: author project as text (manifest, asmdefs, C#, editor bootstrap). Unity generates `ProjectSettings/*` and `.meta` on first open. Verification via `tools/validate_project.py` locally and GameCI in `.github/workflows/unity-ci.yml`.
- Needs from owner: repo secrets `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`; or run the playbook commands on a Windows machine.
- Status: open.

## D-002 · 2026-11-02 · Exact Unity 6.3 LTS patch and package versions
- Default: `6000.3.0f1`; HDRP 17.3.0, Cinemachine 3.1.4, Input System 1.14.2, Splines 2.8.1, Test Framework 1.6.0. Editor resolves/upgrades on first open; commit the resolved `packages-lock.json`.
- Status: open (confirm on first open).

## D-003 · 2026-11-02 · Core interfaces engine-free
- Default: `Hushlands.Core` uses `noEngineReferences`; 2.5D plane is `SlopePoint(distance, height)`. Engine adapters live in `Hushlands.Runtime`. Enables fast pure-C# tests and content-tool reuse.
- Status: decided.

## D-004 · 2026-11-02 · Graphics API and backend
- Default: D3D12 only, IL2CPP. Needed for DLSS and HDRP volumetrics at 4K.
- Status: decided.

## D-005 · 2026-11-02 · Frame budget percentile
- Bible says "budget 16.6 ms" without percentile. Default: p95 ≤ 16.6 ms in config (`budgetPercentile`).
- Status: open.

## D-006 · 2026-11-02 · Sub-agents
- Default: lead runs T0 inline; lanes spawn from T2 on, when parallel text-only work exists. One Editor driver rule still applies.
- Status: decided.
