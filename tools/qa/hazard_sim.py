#!/usr/bin/env python3
"""
Reference model of T10 hazards (LandslideChaser, SquallDirector) with the kinematic bot over the 200 validation
stretches. Acceptance: the bot is never caught; reports the closest gap and shortest time-to-contact.
Also checks a stalled rider (held at minimum speed) is eventually caught, so the chase is real.
Usage: python3 tools/qa/hazard_sim.py [--log docs/logs/T10_hazard_sim.txt]
"""
import argparse
import math
import pathlib
import sys

sys.path.insert(0, str(pathlib.Path(__file__).parent))
import content_sim as cs  # noqa: E402


def load_h():
    return cs.scalars(cs.class_body((cs.CFG / "HazardSettings.cs").read_text(), "HazardSettings"))


class Slide:
    def __init__(self, H, x):
        self.H = H
        self.front, self.speed, self.delay = x - H["landslideStartGap"], 0.0, H["landslideStartDelaySeconds"]

    def step(self, rx, target, dt, foot):
        if self.delay > 0:
            self.delay -= dt
            return False
        H = self.H
        gap = rx - self.front
        v = H["landslidePaceRatio"] * target + H["landslideGapGain"] * (gap - H["landslidePreferredGap"])
        v = min(H["landslideMaxSpeed"], max(H["landslideMinSpeed"], v))
        self.speed = v
        self.front += v * dt
        return rx - self.front <= foot


class Squall:
    def __init__(self, H, seed):
        self.H, self.rng, self.t, self.next, self.cur, self.i = H, cs.Rng(seed), 0.0, H["squallFirstSeconds"], -math.inf, 0.0

    def step(self, dt):
        H = self.H
        self.t += dt
        if self.t >= self.next:
            self.cur = self.next
            self.next = self.cur + H["squallDurationSeconds"] + self.rng.range(H["squallIntervalMinSeconds"], H["squallIntervalMaxSeconds"])
        t = self.t - self.cur
        if t < 0 or t > H["squallDurationSeconds"]:
            self.i = 0.0
            return
        k = min(1.0, t / H["squallRampSeconds"], (H["squallDurationSeconds"] - t) / H["squallRampSeconds"])
        self.i = k * k * (3 - 2 * k)


DANGER = cs.scalars(cs.class_body((cs.CFG / "PresentationSettings.cs").read_text(), "CameraSettings"))["dangerLandslideSeconds"]


def ride(s, H, chunks, seed, stall=False):
    sp, r, F = s["SpeedSettings"], s["RiderSettings"], s["FairnessSettings"]
    heights, h = [], 0.0
    for c in chunks:
        heights.append(h)
        h -= c.length * math.tan(c.slope)
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

    def fric(d):
        c = chunks[idx(d)]
        loc = d - c.start
        return s["surfaces"][next((x[2] for x in c.surfaces if x[0] <= loc < x[0] + x[1]), c.surfaces[-1][2])][0]

    hz = sorted((c.start + f.start, c.start + f.end) for c in chunks for f in c.features if f.hazard)
    dt = r["physicsStep"]
    d, y, vy, v, run, air, nxt, jumped = 2.0, ground(2.0), 0.0, sp["startSpeed"], 0.0, False, 0, -1
    slide, squall = Slide(H, d), Squall(H, cs.Rng.mix(seed, 77))
    min_gap, min_ttc, steps, peak_squall, danger_steps = math.inf, math.inf, 0, 0.0, 0
    foot = F["riderFootprintMeters"]
    while d < total and steps < 400000:
        while nxt < len(hz) and d > hz[nxt][1]:
            nxt += 1
        tap = (not air) and (not stall) and nxt < len(hz) and jumped != nxt and hz[nxt][0] - d <= v * F["botTakeoffSeconds"]
        if tap:
            jumped = nxt
        run += dt
        squall.step(dt)
        peak_squall = max(peak_squall, squall.i)
        base = min(sp["maxSpeed"], sp["minSpeed"] + run / sp["rampIntervalSeconds"] * sp["rampAmount"])
        target = min(sp["maxSpeed"], max(sp["minSpeed"], base - H["squallSpeedPenalty"] * squall.i))
        ang = chunks[idx(d)].slope
        if stall:
            v = sp["minSpeed"]
            d += v * dt
        elif not air:
            acc = sp["gravity"] * math.sin(ang) - sp["gravity"] * math.cos(ang) * fric(d) - sp["baseDrag"] * v * v
            acc += (target - v) * sp["assistGain"]
            v = min(sp["maxSpeed"], max(sp["minSpeed"], v + acc * dt))
            d += v * dt
            y = ground(d)
            if tap:
                vy, air = r["jumpVelocity"] * s["jumpScale"], True
        else:
            d += v * dt
            vy -= sp["gravity"] * dt
            y += vy * dt
            if y <= ground(d):
                y, air = ground(d), False
        steps += 1
        caught = slide.step(d, min(target, v), dt, foot)  # RunSession: slower of target and real speed
        if slide.delay <= 0:
            gap = d - slide.front
            min_gap = min(min_gap, gap)
            closing = slide.speed - v
            ttc = 0.0 if gap <= H["landslideDangerGap"] else (math.inf if closing <= 1e-6 else gap / closing)
            min_ttc = min(min_ttc, ttc)
            if ttc < DANGER:
                danger_steps += 1
        if caught:
            return False, min_gap, min_ttc, steps * dt, peak_squall, danger_steps / max(1, steps)
    return True, min_gap, min_ttc, steps * dt, peak_squall, danger_steps / max(1, steps)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--log", default="Logs/hazard_sim.txt")
    a = ap.parse_args()
    s, H = cs.load(), load_h()
    C = s["ContentSettings"]
    lines, ok_all, worst_gap, worst_ttc, squalls, danger_total = [], True, math.inf, math.inf, 0, 0.0
    for i in range(C["validationStretchCount"]):
        seed, biome = 1 + i, cs.BIOMES[i % len(cs.BIOMES)]
        ch = cs.generate(s, seed, biome, C["chunksPerStretch"])
        ok, g, t, secs, pk, dfrac = ride(s, H, ch, seed)
        danger_total += dfrac
        ok_all &= ok
        worst_gap, worst_ttc = min(worst_gap, g), min(worst_ttc, t)
        squalls += pk > 0.99
        lines.append(f"{'PASS' if ok else 'CAUGHT'} {biome} seed={seed} time={secs:.0f}s closest_gap={g:.1f}m min_ttc={t:.2f}s squall_peak={pk:.2f} danger={dfrac * 100:.1f}%")
    # Full journeys (every biome on one track, ~5 km): the long run is where the target ramp reaches its cap.
    jn = 20
    for seed in range(1, jn + 1):
        ch = cs.generate_journey(s, seed)
        ok, g, t, secs, pk, dfrac = ride(s, H, ch, seed)
        ok_all &= ok
        worst_gap, worst_ttc = min(worst_gap, g), min(worst_ttc, t)
        lines.append(f"{'PASS' if ok else 'CAUGHT'} Journey seed={seed} time={secs:.0f}s closest_gap={g:.1f}m min_ttc={t:.2f}s danger={dfrac * 100:.1f}%")
    ch = cs.generate(s, 2, "MeadowSea", C["chunksPerStretch"])
    stall_ok, _, _, stall_t, _, _ = ride(s, H, ch, 2, stall=True)
    lines.append(f"stalled rider at minimum speed: {'caught after ' + format(stall_t, '.0f') + ' s' if not stall_ok else 'NOT caught'}")
    lines.append(f"summary: bot never caught in {sum(l.startswith('PASS') for l in lines)}/{C['validationStretchCount'] + jn} runs ({jn} full journeys), "
                 f"closest gap {worst_gap:.1f} m, shortest time-to-contact {worst_ttc:.2f} s (Danger below "
                 f"{cs.scalars(cs.class_body((cs.CFG / 'PresentationSettings.cs').read_text(), 'CameraSettings'))['dangerLandslideSeconds']} s), "
                 f"full squalls in {squalls} stretches, Danger camera {danger_total / C['validationStretchCount'] * 100:.1f}% of ride time")
    ok = ok_all and not stall_ok
    lines.append("RESULT " + ("PASS" if ok else "FAIL"))
    out = cs.ROOT / a.log
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text("\n".join(lines) + "\n")
    print("\n".join(lines[-4:]))
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
