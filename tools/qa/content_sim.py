#!/usr/bin/env python3
"""
Reference model of the T2 content pipeline (generator, fairness checker, bot) mirroring
Assets/_Hushlands/Code/Core/Content/*.cs. Reads every number from the C# settings defaults so the
two cannot drift. Used to produce a validation log where Unity is not available.

Usage: python3 tools/qa/content_sim.py [--count 200] [--first-seed 1] [--log Logs/content_validation_py.txt]
"""
import argparse
import math
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parents[2]
CFG = ROOT / "Assets/_Hushlands/Code/Core/Config"
M64 = (1 << 64) - 1

SURFACES = ["Grass", "Snow", "Mud", "PineNeedles", "TeaTerrace", "Boardwalk", "Ice", "Stone"]
BIOMES = ["WindmouthRidge", "MeadowSea", "PineHollow", "TerraceCountry", "MirrorLake"]
PICKUPS = ["SpiceCoin", "MistLantern", "WindFeather", "TeaFlask", "MagnetBell", "GoldenMistbell"]
VEHICLES = ["WoodenHillSled", "TeaCrateBoard", "BananaLeafToboggan", "BambooSled", "GliderSkid", "RainUmbrellaSlider"]


# ---------- settings from C# ----------
def class_body(src: str, name: str) -> str:
    i = src.index(f"class {name}")
    depth, j = 0, src.index("{", i)
    for k in range(j, len(src)):
        depth += src[k] == "{"
        depth -= src[k] == "}"
        if depth == 0:
            return src[j:k]
    raise ValueError(name)


def scalars(body: str) -> dict:
    out = {}
    for m in re.finditer(r"public (float|int|bool) (\w+) = ([^;]+);", body):
        t, n, v = m.groups()
        v = v.strip()
        if t == "bool":
            out[n] = v == "true"
        elif t == "int":
            out[n] = int(v)
        elif re.fullmatch(r"[\d.]+f\s*/\s*[\d.]+f", v):
            num, den = (float(x.strip().rstrip("f")) for x in v.split("/"))
            out[n] = num / den
        else:
            out[n] = float(v.rstrip("f"))
    return out


def load():
    src = "".join(p.read_text() for p in CFG.glob("*.cs"))
    s = {n: scalars(class_body(src, n)) for n in
         ["SpeedSettings", "RiderSettings", "FairnessSettings", "ContentSettings"]}
    s["surfaces"] = {m[0]: (float(m[1].rstrip("f")), float(m[2].rstrip("f"))) for m in
                     re.findall(r"new SurfaceEntry\(SurfaceId\.(\w+), ([\d.]+f), ([\d.]+f)\)", src)}
    s["biomes"] = {m[0]: m[1] for m in
                   re.findall(r"new BiomeEntry\(BiomeId\.(\w+), [^)]*SurfaceId\.(\w+)\)", src)}
    veh = re.findall(r"new VehicleEntry\(VehicleId\.(\w+), ([\d.]+)f, ([\d.]+)f, ([\d.]+)f", src)
    s["defaultVehicle"] = re.search(r"defaultVehicle = VehicleId\.(\w+)", src).group(1)
    s["vehicles"] = {v[0]: {"friction": float(v[1]), "drag": float(v[2]), "jump": float(v[3])} for v in veh}
    # Biome content profiles (ContentSettings.biomeProfiles).
    s["profiles"] = {}
    for m in re.finditer(r"new BiomeContentProfile\s*\{(.*?)\n\s{12}\},", src, re.S):
        body = m.group(1)
        b = re.search(r"biome = BiomeId\.(\w+)", body).group(1)
        num = {k: float(v) for k, v in re.findall(r"(\w+) = ([\d.]+)f", body)}
        num.setdefault("shelterChance", 0.0)
        num.setdefault("vistaChance", 0.0)
        sec_m = re.search(r"secondarySurfaces = new\[\] \{([^}]*)\}", body)
        sec = re.findall(r"SurfaceId\.(\w+)", sec_m.group(1)) if sec_m else []
        s["profiles"][b] = dict(num, secondary=sec or list(SURFACES))
    # Content is generated and checked for the weakest jumper (JumpModel.ForAllVehicles).
    s["jumpScale"] = min(v["jump"] for v in s["vehicles"].values())
    return s


# ---------- core ----------
class Rng:
    def __init__(self, seed: int):
        self.s = seed & M64

    @staticmethod
    def mix(seed: int, salt: int) -> int:
        v = (seed * 1000003 + salt) & M64
        return v - (1 << 64) if v >= (1 << 63) else v

    def next_ulong(self) -> int:
        self.s = (self.s + 0x9E3779B97F4A7C15) & M64
        z = self.s
        z = ((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9) & M64
        z = ((z ^ (z >> 27)) * 0x94D049BB133111EB) & M64
        return z ^ (z >> 31)

    def next_double(self) -> float:
        return (self.next_ulong() >> 11) * (1.0 / 9007199254740992.0)

    def range(self, a, b):
        return a + (b - a) * self.next_double()

    def range_incl(self, a, b):
        v = a + int(self.next_double() * (b - a + 1))
        return min(v, b)


class Feature:
    __slots__ = ("kind", "start", "length", "height", "density", "variant", "warning")

    def __init__(self, kind, start, length):
        self.kind, self.start, self.length = kind, start, length
        self.height = self.density = self.warning = 0.0
        self.variant = 0

    @property
    def end(self):
        return self.start + self.length

    @property
    def hazard(self):
        return self.kind in ("Obstacle", "Gap", "CollapseZone")


class Chunk:
    def __init__(self, cid, biome, diff, length, slope):
        self.id, self.biome, self.diff, self.length, self.slope = cid, biome, diff, length, slope
        self.start = 0.0
        self.features: list[Feature] = []
        self.surfaces: list[tuple] = []


class Jump:
    def __init__(self, s):
        r, sp = s["RiderSettings"], s["SpeedSettings"]
        self.vy = r["jumpVelocity"] * s["jumpScale"]
        self.g = sp["gravity"] * r["airGravityScale"]
        self.vmin, self.vmax = sp["minSpeed"], sp["maxSpeed"]

    def rise(self, v, tan): return self.vy + v * tan
    def h(self, t, v, tan): return self.rise(v, tan) * t - 0.5 * self.g * t * t
    def air(self, v, tan): return 2.0 * self.rise(v, tan) / self.g

    def sample(self, i, n):
        return self.vmin if n < 2 else self.vmin + (self.vmax - self.vmin) * i / (n - 1)

    def clears(self, f, v, tan, lead, clear, margin, foot):
        t1, t2 = max(0.0, lead - foot / v), lead + (f.length + foot) / v
        if t2 + margin > self.air(v, tan):
            return False
        if f.kind != "Obstacle":
            return True
        need = f.height + clear
        return self.h(t1, v, tan) >= need and self.h(t2, v, tan) >= need

    def clears_window(self, f, tan, F):
        lo, hi, clear, margin, n, foot = (F["takeoffWindowMinSeconds"], F["takeoffWindowMaxSeconds"],
                                          F["obstacleClearanceMeters"], F["landingMarginSeconds"],
                                          F["speedSamples"], F["riderFootprintMeters"])
        for i in range(n):
            v = self.sample(i, n)
            if not self.clears(f, v, tan, lo, clear, margin, foot) or not self.clears(f, v, tan, hi, clear, margin, foot):
                return False
        return True

    def farthest(self, tan, lead_min): return self.vmax * (self.air(self.vmax, tan) - lead_min)
    def takeoff_len(self, lead_max): return self.vmax * lead_max


def f32(x):  # float cast as in C# (float) for slope radians, heights, densities
    import struct
    return struct.unpack("f", struct.pack("f", x))[0]


class Zone:
    def __init__(self, c, f, jump, F):
        tan = math.tan(c.slope)
        s = c.start + f.start
        self.c, self.f = c, f
        self.body = (s, s + f.length)
        self.takeoff = (s - jump.takeoff_len(F["takeoffWindowMaxSeconds"]), s)
        far = max(s + jump.farthest(tan, F["takeoffWindowMinSeconds"]), self.body[1])
        self.landing = (self.body[1], far + F["minSafeLandingLength"] + F["riderFootprintMeters"])
        self.whole = (self.takeoff[0], self.landing[1])
        self.reveal = (s - F["obstacleRevealSeconds"] * jump.vmax, self.body[1])


def overlaps(a, b): return a[0] < b[1] and b[0] < a[1]
def contains(a, d): return a[0] <= d <= a[1]


def zones_of(chunks, jump, F):
    z = [Zone(c, f, jump, F) for c in chunks for f in c.features if f.hazard]
    z.sort(key=lambda q: q.body[0])
    return z


# ---------- generator ----------
def generate(s, seed, biome, count):
    C, F, jump = s["ContentSettings"], s["FairnessSettings"], Jump(s)
    rng = Rng(Rng.mix(seed, BIOMES.index(biome)))
    chunks, nest = [], [0]
    for i in range(count):
        chunks.append(build_chunk(s, rng, biome, seed, i, nest, jump))
    cur = 0.0
    for c in chunks:
        c.start, cur = cur, cur + c.length
    safe = f32(F["maxMistOverRequiredJump"] * C["mistSafeFactor"])
    zs = zones_of(chunks, jump, F)
    for c in chunks:
        for m in c.features:
            if m.kind == "MistBank" and m.density > safe:
                a = (c.start + m.start, c.start + m.end)
                if any(overlaps(z.reveal, a) for z in zs):
                    m.density = safe
    return chunks


def build_chunk(s, rng, biome, seed, idx, nest, jump):
    C, F = s["ContentSettings"], s["FairnessSettings"]
    length = rng.range(C["minChunkLength"], C["maxChunkLength"])
    diff = rng.range_incl(C["minDifficulty"], C["maxDifficulty"])
    K = s["profiles"].get(biome, {"minSlopeDegrees": C["minSlopeDegrees"], "maxSlopeDegrees": C["maxSlopeDegrees"],
                                  "obstacleWeight": C["obstacleWeight"], "gapWeight": C["gapWeight"],
                                  "mistBankChance": C["mistBankChance"], "secondary": list(SURFACES),
                                  "shelterChance": 0.0, "vistaChance": 0.0})
    slope_deg = rng.range(K["minSlopeDegrees"], K["maxSlopeDegrees"])
    c = Chunk(f"{biome}_{seed}_{idx}", biome, diff, length, f32(slope_deg * math.pi / 180.0))
    tan = math.tan(c.slope)
    primary = s["biomes"][biome]
    if rng.next_double() < C["secondarySurfaceChance"] and length - C["tailOutMeters"] > C["leadInMeters"]:
        split = rng.range(C["leadInMeters"], length - C["tailOutMeters"])
        sec = K["secondary"][rng.range_incl(0, len(K["secondary"]) - 1)]
        c.surfaces = [(0.0, split, primary), (split, length - split, sec)]
    else:
        c.surfaces = [(0.0, length, primary)]
    c.features.append(Feature("Checkpoint", C["checkpointOffsetMeters"], 0.0))

    blocked = []
    max_h = math.floor(C["hazardsBase"] + C["hazardsPerDifficulty"] * diff)
    dr = C["maxDifficulty"] - C["minDifficulty"]
    dn = (diff - C["minDifficulty"]) / dr if dr > 0 else 0.0
    cursor = C["leadInMeters"]
    takeoff = jump.takeoff_len(F["takeoffWindowMaxSeconds"])
    far = jump.farthest(tan, F["takeoffWindowMinSeconds"])
    lerp = lambda a, b, t: a + (b - a) * t
    for _ in range(int(max_h)):
        roll, size_t, jitter = rng.next_double(), dn * rng.next_double(), rng.range(0.0, C["hazardSpacingJitterMeters"])
        if roll < K["obstacleWeight"]:
            kind = "Obstacle"
        elif roll < K["obstacleWeight"] + K["gapWeight"] or diff < C["collapseMinDifficulty"]:
            kind = "Gap"
        else:
            kind = "CollapseZone"
        lead = takeoff
        if kind == "Obstacle":
            f = Feature(kind, 0.0, lerp(C["obstacleMinLength"], C["obstacleMaxLength"], size_t))
            f.height = f32(lerp(C["obstacleMinHeight"], C["obstacleMaxHeight"], size_t))
        elif kind == "Gap":
            f = Feature(kind, 0.0, lerp(C["gapMinLength"], C["gapMaxLength"], size_t))
        else:
            f = Feature(kind, 0.0, lerp(C["collapseMinLength"], C["collapseMaxLength"], size_t))
            f.warning = F["collapseWarningSeconds"] * jump.vmax * C["collapseWarningSlack"]
            lead = max(takeoff, f.warning)
        ok = False
        for _t in range(C["sizeShrinkTries"] + 1):
            if jump.clears_window(f, tan, F):
                ok = True
                break
            f.length *= C["sizeShrinkFactor"]
            f.height = f32(f.height * C["sizeShrinkFactor"])
        if not ok:
            continue
        f.start = cursor + lead
        zone_end = f.start + max(f.length, far) + F["minSafeLandingLength"] + F["riderFootprintMeters"]
        if zone_end > length - C["tailOutMeters"]:
            break
        c.features.append(f)
        blocked.append((f.start - lead, zone_end))
        cursor = zone_end + jitter

    if rng.next_double() < K["mistBankChance"]:
        ml = min(rng.range(C["mistMinLength"], C["mistMaxLength"]), length)
        ms = rng.range(0.0, length - ml)
        m = Feature("MistBank", ms, ml)
        m.density = f32(rng.range(0.0, C["mistMaxDensity"]))
        c.features.append(m)

    if K["shelterChance"] > 0 and rng.next_double() < K["shelterChance"]:
        room = length - C["leadInMeters"] - C["tailOutMeters"]
        sl = min(rng.range(K["shelterMinLength"], K["shelterMaxLength"]), room)
        ss = rng.range(C["leadInMeters"], C["leadInMeters"] + max(0.0, room - sl))
        if sl > 0:
            c.features.append(Feature("Shelter", ss, sl))
    if K["vistaChance"] > 0 and rng.next_double() < K["vistaChance"]:
        room = length - C["leadInMeters"] - C["tailOutMeters"]
        vl = min(rng.range(K["vistaMinLength"], K["vistaMaxLength"]), room)
        vs = rng.range(C["leadInMeters"], C["leadInMeters"] + max(0.0, room - vl))
        if vl > 0:
            c.features.append(Feature("Vista", vs, vl))

    def place(spacing):
        for _ in range(C["placementTries"]):
            cand = rng.range(C["leadInMeters"], length - C["tailOutMeters"])
            if all(not contains((b[0] - spacing, b[1] + spacing), cand) for b in blocked):
                return cand
        return None

    for _ in range(C["pickupsPerChunk"]):
        rare_roll = rng.next_double()
        rare_idx = rng.range_incl(1, len(PICKUPS) - 1)
        pos = place(0.0)
        if pos is None:
            continue
        p = Feature("Pickup", pos, 0.0)
        p.variant = rare_idx if rare_roll < C["rarePickupChance"] else 0
        c.features.append(p)
    if rng.next_double() < C["nestChance"]:
        pos = place(F["nestHazardSpacing"])
        if pos is not None:
            n = Feature("DucklingNest", pos, 0.0)
            n.variant = nest[0]
            nest[0] += 1
            c.features.append(n)
    return c


# ---------- fairness ----------
def check(s, chunks):
    C, F, jump = s["ContentSettings"], s["FairnessSettings"], Jump(s)
    eps, v = 1e-6, []
    if F["visibleAheadMeters"] + eps < F["obstacleRevealSeconds"] * jump.vmax:
        v.append("camera.reveal")
    expected = 0.0
    for c in chunks:
        if not (C["minChunkLength"] - eps <= c.length <= C["maxChunkLength"] + eps) or not (
                C["minDifficulty"] <= c.diff <= C["maxDifficulty"]):
            v.append("chunk.bounds")
        if abs(c.start - expected) > eps:
            v.append("stretch.contiguous")
        expected = c.start + c.length
        cover, cont = 0.0, True
        for st, ln, _ in c.surfaces:
            cont &= abs(st - cover) <= eps and ln > 0
            cover = st + ln
        if not cont or abs(cover - c.length) > eps:
            v.append("surface.cover")
        if not any(f.kind == "Checkpoint" for f in c.features):
            v.append("checkpoint.present")
        for f in c.features:
            if f.start < -eps or f.end > c.length + eps:
                v.append("feature.bounds")
    zs = zones_of(chunks, jump, F)
    mist = [((c.start + f.start, c.start + f.end), f.density) for c in chunks for f in c.features if f.kind == "MistBank"]
    for i, z in enumerate(zs):
        tan = math.tan(z.c.slope)
        if not jump.clears_window(z.f, tan, F):
            v.append("jump.clearable")
        if z.whole[0] < z.c.start - eps or z.whole[1] > z.c.start + z.c.length + eps:
            v.append("zone.within.chunk")
        if z.f.kind == "CollapseZone" and z.f.warning + eps < F["collapseWarningSeconds"] * jump.vmax + jump.takeoff_len(F["takeoffWindowMaxSeconds"]):
            v.append("collapse.warning")
        if any(overlaps(m[0], z.reveal) and m[1] > F["maxMistOverRequiredJump"] + eps for m in mist):
            v.append("mist.hides.jump")
        if i + 1 < len(zs):
            n = zs[i + 1]
            if z.landing[1] > n.takeoff[0] + eps:
                v.append("landing.safe")
            if n.body[0] - z.body[1] < F["minGapBetweenObstacles"] - eps:
                v.append("hazard.spacing")
    lead = F["checkpointLeadSeconds"] * jump.vmin
    for c in chunks:
        for f in c.features:
            d = c.start + f.start
            if f.kind == "Checkpoint" and any(contains(z.whole, d) or (z.body[0] > d and z.body[0] - d < lead - eps) for z in zs):
                v.append("checkpoint.safe")
            if f.kind == "DucklingNest" and any(
                    contains((z.whole[0] - F["nestHazardSpacing"] + eps, z.whole[1] + F["nestHazardSpacing"] - eps), d) for z in zs):
                v.append("nest.safe")
            if f.kind == "Pickup" and any(contains(z.body, d) for z in zs):
                v.append("pickup.on.hazard")
    return v


# ---------- bot (KinematicRider + SpeedModel mirror) ----------
def bot(s, chunks, vehicle=None):
    sp, r, F = s["SpeedSettings"], s["RiderSettings"], s["FairnessSettings"]
    veh = s["vehicles"][vehicle or s["defaultVehicle"]]
    vy0 = r["jumpVelocity"] * veh["jump"]
    heights, h = [], 0.0
    for c in chunks:
        heights.append(h)
        h -= c.length * math.tan(c.slope)
    total = chunks[-1].start + chunks[-1].length

    def idx(d):
        lo, hi = 0, len(chunks) - 1
        while lo < hi:
            mid = (lo + hi + 1) // 2
            if chunks[mid].start <= d:
                lo = mid
            else:
                hi = mid - 1
        return lo

    def ground(d):
        i = idx(d)
        return heights[i] - (d - chunks[i].start) * math.tan(chunks[i].slope)

    def surface(d):
        c = chunks[idx(d)]
        loc = d - c.start
        for st, ln, sf in c.surfaces:
            if st <= loc < st + ln:
                return sf
        return c.surfaces[-1][2]

    hz = sorted(((c.start + f.start, c.start + f.end, f) for c in chunks for f in c.features if f.hazard), key=lambda q: q[0])
    dt = r["physicsStep"]
    d, height, vvert, speed, run = 0.0, ground(0.0), 0.0, sp["startSpeed"], 0.0
    air, nxt, jumped, steps, jumps = False, 0, -1, 0, 0
    vmin_seen, vmax_seen = 1e9, 0.0
    max_steps = math.ceil(total / sp["minSpeed"] / dt) * 2
    fail = None
    while fail is None and d < total and steps < max_steps:
        while nxt < len(hz) and d > hz[nxt][1]:
            nxt += 1
        tap = (not air) and nxt < len(hz) and jumped != nxt and hz[nxt][0] - d <= speed * F["botTakeoffSeconds"]
        if tap:
            jumped, jumps = nxt, jumps + 1
        run += dt
        ang = chunks[idx(d)].slope
        fric = s["surfaces"][surface(d)][0] * veh["friction"]
        if not air:
            drag = sp["baseDrag"] * veh["drag"]
            acc = sp["gravity"] * math.sin(ang) - sp["gravity"] * math.cos(ang) * fric - drag * speed * speed
            target = min(sp["maxSpeed"], sp["minSpeed"] + max(0.0, run) / sp["rampIntervalSeconds"] * sp["rampAmount"])
            acc += (target - speed) * sp["assistGain"]
            speed = min(sp["maxSpeed"], max(sp["minSpeed"], speed + acc * dt))
            d += speed * dt
            height = ground(d)
            if tap:
                vvert, air = vy0, True
        else:
            d += speed * dt
            vvert -= sp["gravity"] * r["airGravityScale"] * dt
            height += vvert * dt
            g = ground(d)
            if height <= g:
                height, vvert, air = g, 0.0, False
        steps += 1
        vmin_seen, vmax_seen = min(vmin_seen, speed), max(vmax_seen, speed)
        foot = F["riderFootprintMeters"]
        for st, en, f in hz[max(0, nxt - 1):]:
            if st - foot > d:
                break
            if f.kind == "Obstacle":
                if st - foot <= d <= en + foot and height - ground(d) < f.height:
                    fail = f"hit obstacle at {d:.1f}"
            elif not air and st <= d <= en:
                fail = f"grounded over {f.kind} at {d:.1f}"
    if fail is None and d < total:
        fail = "timeout"
    rep = f"chunks={len(chunks)} length={total:.1f} jumps={jumps}/{len(hz)} steps={steps} speed={vmin_seen:.1f}..{vmax_seen:.1f}"
    return fail is None, rep + (" ok" if fail is None else f" FAIL {fail}")


def selftest(s):
    """Mutations that must be caught. Returns list of (name, caught_by_checker, caught_by_bot)."""
    import copy
    results = []
    base = generate(s, 2, "MeadowSea", s["ContentSettings"]["chunksPerStretch"])
    assert not check(s, base) and bot(s, base)[0], "base stretch must pass"

    def first(chunks, kind):
        for c in chunks:
            for f in c.features:
                if f.kind == kind:
                    return c, f
        raise LookupError(kind)

    def mutate(name, fn, expect_bot):
        ch = copy.deepcopy(base)
        fn(ch)
        v = check(s, ch)
        ok_bot, rep = bot(s, ch)
        results.append((name, bool(v), not ok_bot, expect_bot, v[:3], rep))

    mutate("obstacle too tall", lambda ch: setattr(first(ch, "Obstacle")[1], "height", 3.0), True)
    mutate("gap too long", lambda ch: setattr(first(ch, "Gap")[1], "length", 14.0), True)
    def dense_mist(ch):
        c, f = first(ch, "Gap")
        m = Feature("MistBank", max(0.0, f.start - 10), 20.0)
        m.density = 1.0
        c.features.append(m)
    mutate("mist over jump", dense_mist, False)
    def no_checkpoint(ch):
        ch[0].features = [f for f in ch[0].features if f.kind != "Checkpoint"]
    mutate("missing checkpoint", no_checkpoint, False)
    def crowd(ch):
        c, f = first(ch, "Gap")
        g = Feature("Obstacle", f.end + 3.0, 1.0)
        g.height = 0.5
        c.features.append(g)
    mutate("hazard in landing zone", crowd, False)  # bot may clear it at its actual speed; rule guards max speed
    mutate("short collapse warning", lambda ch: setattr(first(ch, "CollapseZone")[1], "warning", 2.0), False)
    def nest_on_gap(ch):
        c, f = first(ch, "Gap")
        n = Feature("DucklingNest", f.start + 0.5, 0.0)
        c.features.append(n)
    mutate("nest on hazard", nest_on_gap, False)
    return results


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--count", type=int)
    ap.add_argument("--first-seed", type=int, default=1)
    ap.add_argument("--log", default="Logs/content_validation_py.txt")
    ap.add_argument("--selftest", action="store_true")
    ap.add_argument("--all-vehicles", action="store_true")
    ap.add_argument("--biome", help="validate only this biome (otherwise biomes cycle)")
    a = ap.parse_args()
    s = load()
    if a.selftest:
        res = selftest(s)
        bad = 0
        for name, by_check, by_bot, expect_bot, rules, rep in res:
            good = by_check and (by_bot or not expect_bot)
            bad += not good
            print(f"{'CAUGHT' if good else 'MISSED'} {name}: checker={rules} bot={'fail' if by_bot else 'pass'}")
        print(f"selftest: {len(res) - bad}/{len(res)} mutations caught")
        return 1 if bad else 0
    count = a.count or s["ContentSettings"]["validationStretchCount"]
    lines, passed, hazards, kinds, vehicle_runs = [], 0, 0, {}, {}
    for i in range(count):
        seed, biome = a.first_seed + i, (a.biome or BIOMES[i % len(BIOMES)])
        chunks = generate(s, seed, biome, s["ContentSettings"]["chunksPerStretch"])
        for c in chunks:
            for f in c.features:
                if f.hazard:
                    hazards += 1
                kinds[f.kind] = kinds.get(f.kind, 0) + 1
        viol = check(s, chunks)
        ok_bot, rep = bot(s, chunks)
        if a.all_vehicles:
            for vname in s["vehicles"]:
                vok, vrep = bot(s, chunks, vname)
                vehicle_runs[vname] = vehicle_runs.get(vname, 0) + vok
                if not vok:
                    ok_bot, rep = False, f"{vname}: {vrep}"
        ok = ok_bot and not viol
        passed += ok
        lines.append(f"{'PASS' if ok else 'FAIL'} {biome} seed={seed} {rep}" + "".join(f" [{x}]" for x in viol))
    summary = f"summary: {passed}/{count} passed, hazards={hazards}, features={dict(sorted(kinds.items()))}"
    if a.all_vehicles:
        summary += " vehicles=" + ", ".join(f"{k} {v}/{count}" for k, v in vehicle_runs.items())
    lines.append(summary)
    out = ROOT / a.log
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text("\n".join(lines) + "\n")
    print("\n".join(lines[:5]))
    print("...")
    print(summary)
    print(f"log: {out}")
    return 0 if passed == count else 1


if __name__ == "__main__":
    sys.exit(main())
