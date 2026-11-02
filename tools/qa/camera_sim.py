#!/usr/bin/env python3
"""
Reference model of the T4 camera director (CameraStateRules, CameraStateMachine dwell, CameraRig blend),
driven by the kinematic bot on a generated stretch. Mirrors CameraTests.LiveRide_KeepsRevealFraming.
Reads numbers from the C# settings. Usage: python3 tools/qa/camera_sim.py [--log docs/logs/T4_camera_sim.txt]
"""
import argparse
import math
import pathlib
import re
import sys

sys.path.insert(0, str(pathlib.Path(__file__).parent))
import content_sim as cs  # noqa: E402

ROOT = cs.ROOT


def load_camera():
    src = (cs.CFG / "PresentationSettings.cs").read_text()
    body = cs.class_body(src, "CameraSettings")
    sc = cs.scalars(body)
    states = {}
    for m in re.finditer(r"new CameraStateProfile\(CameraState\.(\w+), ([\d.]+)f, ([\d.]+)f, ([\d.]+)f, ([\d.]+)f, ([\d.]+)f, (\d+), ([\d.]+)f, (true|false)\)", body):
        n, fov, dist, h, la, blend, prio, pitch, ex = m.groups()
        states[n] = dict(fov=float(fov), dist=float(dist), h=float(h), la=float(la), blend=float(blend), prio=int(prio), exempt=ex == "true")
    biomes = {m[0]: (float(m[1]), float(m[2]), float(m[3])) for m in
              re.findall(r"new BiomeCameraModifier\(BiomeId\.(\w+), (-?[\d.]+)f, ([\d.]+)f, (-?[\d.]+)f\)", body)}
    return sc, states, biomes


def resolve(states, biomes, state, biome):
    p = dict(states[state])
    fo, ds, ho = biomes[biome]
    p["fov"] += fo
    p["dist"] *= ds
    p["h"] += ho
    return p


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--log", default="Logs/camera_sim.txt")
    a = ap.parse_args()
    s = cs.load()
    sc, states, biomes = load_camera()
    F, sp, r = s["FairnessSettings"], s["SpeedSettings"], s["RiderSettings"]
    biome = "MeadowSea"
    chunks = cs.generate(s, 2, biome, s["ContentSettings"]["chunksPerStretch"])

    # kinematic rider + bot (same as content_sim.bot, inlined to expose state per step)
    heights, hh = [], 0.0
    for c in chunks:
        heights.append(hh)
        hh -= c.length * math.tan(c.slope)
    total = chunks[-1].start + chunks[-1].length

    def idx(d):
        lo, hi = 0, len(chunks) - 1
        while lo < hi:
            mid = (lo + hi + 1) // 2
            lo, hi = (mid, hi) if chunks[mid].start <= d else (lo, mid - 1)
        return lo

    def ground(d):
        i = idx(d)
        return heights[i] - (d - chunks[i].start) * math.tan(chunks[i].slope)

    def friction(d):
        c = chunks[idx(d)]
        loc = d - c.start
        sf = next((x[2] for x in c.surfaces if x[0] <= loc < x[0] + x[1]), c.surfaces[-1][2])
        return s["surfaces"][sf][0]

    hz = sorted((c.start + f.start, c.start + f.end) for c in chunks for f in c.features if f.hazard)
    dt = 0.01
    d, y, vy, v, run, air, airt = 0.0, ground(0.0), 0.0, sp["startSpeed"], 0.0, False, 0.0
    nxt, jumped = 0, -1

    # camera
    cur, tin = "Cruise", 0.0
    p0 = resolve(states, biomes, cur, biome)
    now = {k: p0[k] for k in ("fov", "dist", "h", "la")}
    frm, to, t, dur = dict(now), dict(now), 0.0, 0.0
    camy = None
    seen, worst_ahead, worst_rx, worst_ry, steps, fails = set(), 1e9, 0.0, 0.0, 0, []
    stop = total - states["Vista"]["la"]
    while d < stop and steps < 200000:
        while nxt < len(hz) and d > hz[nxt][1]:
            nxt += 1
        tap = (not air) and nxt < len(hz) and jumped != nxt and hz[nxt][0] - d <= v * F["botTakeoffSeconds"]
        if tap:
            jumped = nxt
        run += dt
        ang = chunks[idx(d)].slope
        if not air:
            acc = sp["gravity"] * math.sin(ang) - sp["gravity"] * math.cos(ang) * friction(d) - sp["baseDrag"] * v * v
            target = min(sp["maxSpeed"], sp["minSpeed"] + run / sp["rampIntervalSeconds"] * sp["rampAmount"])
            acc += (target - v) * sp["assistGain"]
            v = min(sp["maxSpeed"], max(sp["minSpeed"], v + acc * dt))
            d += v * dt
            y = ground(d)
            if tap:
                vy, air = r["jumpVelocity"] * s["jumpScale"], True
        else:
            d += v * dt
            vy -= sp["gravity"] * r["airGravityScale"] * dt
            y += vy * dt
            if y <= ground(d):
                y, vy, air = ground(d), 0.0, False
        airt = airt + dt if air else 0.0

        landslide = 1.0 if steps % 900 < 150 else math.inf
        boost = steps % 1300 > 1200
        req = ["Cruise"]
        if landslide < sc["dangerLandslideSeconds"]:
            req.append("Danger")
        if boost or v >= sc["boostSpeedThreshold"]:
            req.append("Boost")
        if air and airt >= sc["airDelaySeconds"]:
            req.append("Air")
        win = max(req, key=lambda q: states[q]["prio"])
        tin += dt
        if win != cur and (states[win]["prio"] > states[cur]["prio"] or tin >= sc["minDwellSeconds"]):
            cur, tin = win, 0.0
            tgt = resolve(states, biomes, cur, biome)
            frm, to, t, dur = dict(now), {k: tgt[k] for k in now}, 0.0, states[cur]["blend"]
        if t < dur:
            t = min(dur, t + dt)
            u = t / dur
            k = u * u * (3 - 2 * u)
            now = {q: frm[q] + (to[q] - frm[q]) * k for q in now}
        cx = d + now["la"]
        ty = ground(cx) + now["h"]
        camy = ty if camy is None else camy + (ty - camy) * (1 - math.exp(-dt / sc["verticalSmoothSeconds"]))
        steps += 1
        seen.add(cur)
        if states[cur]["exempt"]:
            continue
        halfH = now["dist"] * math.tan(math.radians(now["fov"] / 2))
        halfW = halfH * sc["minAspect"]
        ahead = halfW + now["la"]
        rx = (d - cx) / halfW
        ry = (y - camy) / halfH
        worst_ahead, worst_rx, worst_ry = min(worst_ahead, ahead), min(worst_rx, rx), max(worst_ry, abs(ry))
        if ahead < F["visibleAheadMeters"] or rx < -sc["maxRiderBehindCenter"] or abs(ry) > sc["safeScreenExtent"]:
            fails.append(f"step {steps} {cur} ahead={ahead:.1f} rx={rx:.2f} ry={ry:.2f}")
    lines = [f"steps={steps} distance={d:.1f} states={sorted(seen)}",
             f"worst ahead={worst_ahead:.2f} m (need {F['visibleAheadMeters']}), worst riderX={worst_rx:.3f} "
             f"(limit {-sc['maxRiderBehindCenter']}), worst |riderY|={worst_ry:.3f} (limit {sc['safeScreenExtent']})",
             f"violations={len(fails)}"] + fails[:10]
    ok = not fails and {"Air", "Danger", "Boost"} <= seen
    lines.append("RESULT " + ("PASS" if ok else "FAIL"))
    out = ROOT / a.log
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text("\n".join(lines) + "\n")
    print("\n".join(lines))
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
