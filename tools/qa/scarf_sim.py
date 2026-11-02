#!/usr/bin/env python3
"""
Reference model of VerletScarfSolver + SledFlex (Assets/_Hushlands/Code/Core/Cloth). Reads ClothSettings defaults.
Scenario: rider accelerates 6 -> 18 m/s down a 12 deg slope, jumps every 4 s, a hard stop (retry Reset), and frame
hitches (16.6 / 33 / 100 ms). Checks stretch <= maxStretchRatio, finite, scarf trails behind, stays off the body.
Usage: python3 tools/qa/scarf_sim.py [--log docs/logs/T6_scarf_sim.txt]
"""
import argparse
import math
import pathlib
import re
import sys

sys.path.insert(0, str(pathlib.Path(__file__).parent))
import content_sim as cs  # noqa: E402


def load():
    src = (cs.CFG / "ClothSettings.cs").read_text()
    body = cs.class_body(src, "ClothSettings")
    s = cs.scalars(body)
    m = re.search(r"maxSubstepSeconds = 1f / (\d+)f", body)
    s["maxSubstepSeconds"] = 1.0 / float(m.group(1))
    return s


class Scarf:
    def __init__(self, s, g):
        self.s, self.g = s, g * s["gravityScale"]
        self.n = s["scarfSegments"] + 1
        self.seg = s["scarfLength"] / s["scarfSegments"]
        self.p = [[0.0, 0.0, 0.0] for _ in range(self.n)]
        self.q = [[0.0, 0.0, 0.0] for _ in range(self.n)]

    def reset(self, a, d):
        L = math.sqrt(sum(c * c for c in d)) or 1.0
        d = [c / L for c in d]
        for i in range(self.n):
            self.p[i] = [a[k] + d[k] * self.seg * i for k in range(3)]
            self.q[i] = list(self.p[i])

    def step(self, dt, a, body):
        s = self.s
        n = max(1, min(s["maxSubsteps"], math.ceil(dt / s["maxSubstepSeconds"])))
        covered = min(dt, s["maxSubstepSeconds"] * n)
        h = covered / n
        st = list(self.p[0])
        # Long frame: carry the whole scarf through the part of the anchor move we will not simulate.
        skip = 1.0 - covered / dt
        if skip > 0:
            off = [(a[j] - st[j]) * skip for j in range(3)]
            for i in range(self.n):
                self.p[i] = [self.p[i][k] + off[k] for k in range(3)]
                self.q[i] = [self.q[i][k] + off[k] for k in range(3)]
            st = list(self.p[0])
        for k in range(1, n + 1):
            t = k / n
            self.sub(h, [st[j] + (a[j] - st[j]) * t for j in range(3)], body)

    def sub(self, h, a, body):
        s, h2 = self.s, h * h
        wind = (s["windX"], s["windY"], s["windZ"])
        da = [a[k] - self.p[0][k] for k in range(3)]
        carry = s["anchorCarry"]
        for i in range(1, self.n):
            v = [(self.p[i][k] - self.q[i][k]) * s["damping"] for k in range(3)]
            self.q[i] = [self.p[i][k] + da[k] * carry for k in range(3)]
            world_v = [v[k] / h + da[k] * carry / h for k in range(3)]
            acc = [s["airDrag"] * (wind[k] - world_v[k]) for k in range(3)]
            acc[1] -= self.g
            self.p[i] = [self.p[i][k] + da[k] * carry + v[k] + acc[k] * h2 for k in range(3)]
        self.q[0] = list(self.p[0])
        self.p[0] = list(a)
        for _ in range(s["constraintIterations"]):
            for i in range(1, self.n):
                d = [self.p[i][k] - self.p[i - 1][k] for k in range(3)]
                dist = math.sqrt(sum(c * c for c in d))
                if dist < 1e-6:
                    continue
                diff = (dist - self.seg) / dist
                if i == 1:
                    self.p[i] = [self.p[i][k] - d[k] * diff for k in range(3)]
                else:
                    self.p[i - 1] = [self.p[i - 1][k] + d[k] * diff * 0.5 for k in range(3)]
                    self.p[i] = [self.p[i][k] - d[k] * diff * 0.5 for k in range(3)]
            r = s["bodyRadius"]
            for i in range(1, self.n):
                d = [self.p[i][k] - body[k] for k in range(3)]
                d2 = sum(c * c for c in d)
                if d2 < r * r and d2 > 1e-12:
                    dd = math.sqrt(d2)
                    self.p[i] = [self.p[i][k] + d[k] * (r - dd) / dd for k in range(3)]

    def stretch(self):
        w = 0.0
        for i in range(1, self.n):
            dist = math.sqrt(sum((self.p[i][k] - self.p[i - 1][k]) ** 2 for k in range(3)))
            w = max(w, abs(dist - self.seg) / self.seg)
        return w


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--log", default="Logs/scarf_sim.txt")
    a = ap.parse_args()
    s = load()
    g = cs.load()["SpeedSettings"]["gravity"]
    sc = Scarf(s, g)
    slope = math.radians(12)
    x, y, vy, v, air = 0.0, 0.0, 0.0, 6.0, False
    neck = 0.9
    body_off = -0.35
    sc.reset([x, y + neck, 0.0], [-1, 0, 0])
    t, worst, worst_t, trail_bad, inside, frames = 0.0, 0.0, 0.0, 0, 0, 0
    pattern = [1 / 60] * 50 + [1 / 30] * 5 + [1 / 60] * 50 + [0.1] + [1 / 60] * 30
    pi = 0
    reset_done = False
    lines = []
    while t < 30.0:
        dt = pattern[pi % len(pattern)]
        pi += 1
        t += dt
        if t >= 15.0 and not reset_done:
            reset_done = True  # retry: hard stop and teleport
            x, y, vy, v, air = 5.0, -5.0 * math.tan(slope), 0.0, 6.0, False
            sc.reset([x, y + neck, 0.0], [-1, 0, 0])
            lines.append(f"t={t:.2f} retry reset")
        v = min(18.0, v + 1.0 * dt)
        x += v * dt
        ground = -x * math.tan(slope)
        if not air and int(t / 4.0) != int((t - dt) / 4.0):
            vy, air = 6.5, True
        if air:
            vy -= g * dt
            y += vy * dt
            if y <= ground:
                y, air = ground, False
        else:
            y = ground
        anchor = [x, y + neck, 0.0]
        body = [x, y + neck + body_off, 0.0]
        sc.step(dt, anchor, body)
        frames += 1
        st = sc.stretch()
        if st > worst:
            worst, worst_t = st, t
        if v > 8 and sc.p[-1][0] > x:
            trail_bad += 1
        for i in range(1, sc.n):
            if math.dist(sc.p[i], body) < s["bodyRadius"] - 1e-4:
                inside += 1
        if any(math.isnan(c) or math.isinf(c) for p in sc.p for c in p):
            lines.append(f"NaN at t={t:.2f}")
            break
    # sled flex: landing at 6 m/s settles
    bend, vel, peak, settle = 0.0, 0.0, 0.0, None
    vel += 6.0 * s["flexImpulsePerMps"]
    for k in range(300):
        acc = -s["flexStiffness"] * bend - s["flexDamping"] * vel
        vel += acc * 0.01
        bend = max(-s["flexMaxDegrees"], min(s["flexMaxDegrees"], bend + vel * 0.01))
        peak = max(peak, abs(bend))
        if settle is None and k > 10 and abs(bend) < 0.1 and abs(vel) < 1:
            settle = k * 0.01
    ok = worst <= s["maxStretchRatio"] and trail_bad == 0 and inside == 0 and settle is not None and peak <= s["flexMaxDegrees"]
    lines += [f"frames={frames} sim={t:.1f}s (hitches 33 ms and 100 ms included)",
              f"worst stretch={worst * 100:.2f}% at t={worst_t:.2f} (limit {s['maxStretchRatio'] * 100:.1f}%)",
              f"tip ahead of rider at speed: {trail_bad} frames; particles inside body: {inside}",
              f"sled flex: peak {peak:.2f} deg (max {s['flexMaxDegrees']}), settles in {settle} s",
              "RESULT " + ("PASS" if ok else "FAIL")]
    out = cs.ROOT / a.log
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text("\n".join(lines) + "\n")
    print("\n".join(lines))
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
