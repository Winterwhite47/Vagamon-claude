using System;

namespace Hushlands.Core
{
    /// <summary>
    /// Allocation-free verlet rope for the scarf. Particle 0 is pinned to the anchor; distance constraints keep
    /// segment length; drag against relative air (wind minus particle velocity) makes it trail; a sphere keeps it
    /// off the rider. Arrays are allocated once in the constructor.
    /// </summary>
    public sealed class VerletScarfSolver
    {
        private readonly ClothSettings _s;
        private readonly float _gravity;
        private readonly float _segment;
        private readonly float[] _x;
        private readonly float[] _y;
        private readonly float[] _z;
        private readonly float[] _px;
        private readonly float[] _py;
        private readonly float[] _pz;
        private float _windX;
        private float _windY;
        private float _windZ;

        public VerletScarfSolver(ClothSettings settings, float gravity)
        {
            _windX = settings.windX;
            _windY = settings.windY;
            _windZ = settings.windZ;
            _s = settings ?? throw new ArgumentNullException(nameof(settings));
            _gravity = gravity * settings.gravityScale;
            Count = settings.scarfSegments + 1;
            _segment = settings.scarfLength / settings.scarfSegments;
            _x = new float[Count];
            _y = new float[Count];
            _z = new float[Count];
            _px = new float[Count];
            _py = new float[Count];
            _pz = new float[Count];
        }

        public int Count { get; }

        /// <summary>Weather override (squall gusts); defaults to the ClothSettings wind.</summary>
        public void SetWind(float x, float y, float z)
        {
            _windX = x;
            _windY = y;
            _windZ = z;
        }
        public float SegmentLength => _segment;
        public float X(int i) => _x[i];
        public float Y(int i) => _y[i];
        public float Z(int i) => _z[i];

        /// <summary>Lays the scarf straight from the anchor along (dx, dy, dz) at rest (after spawn or retry).</summary>
        public void Reset(float ax, float ay, float az, float dx, float dy, float dz)
        {
            float len = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (len < 1e-6f)
            {
                dx = -1f;
                dy = 0f;
                dz = 0f;
                len = 1f;
            }
            dx /= len;
            dy /= len;
            dz /= len;
            for (int i = 0; i < Count; i++)
            {
                _x[i] = _px[i] = ax + dx * _segment * i;
                _y[i] = _py[i] = ay + dy * _segment * i;
                _z[i] = _pz[i] = az + dz * _segment * i;
            }
        }

        /// <summary>
        /// Advances by <paramref name="dt"/> using up to maxSubsteps of at most maxSubstepSeconds (longer frames are
        /// clamped rather than exploding). Anchor moves linearly from its previous to new position across substeps.
        /// </summary>
        public void Step(float dt, float ax, float ay, float az, float bodyX, float bodyY, float bodyZ)
        {
            if (!(dt > 0f)) return;
            int n = (int)Math.Ceiling(dt / _s.maxSubstepSeconds);
            if (n < 1) n = 1;
            if (n > _s.maxSubsteps) n = _s.maxSubsteps;
            float covered = Math.Min(dt, _s.maxSubstepSeconds * n);
            float h = covered / n;
            float sx = _x[0];
            float sy = _y[0];
            float sz = _z[0];

            // Long frame (hitch): carry the whole scarf through the part of the anchor move that is not simulated.
            float skip = 1f - covered / dt;
            if (skip > 0f)
            {
                Translate((ax - sx) * skip, (ay - sy) * skip, (az - sz) * skip);
                sx = _x[0];
                sy = _y[0];
                sz = _z[0];
            }
            for (int k = 1; k <= n; k++)
            {
                float t = (float)k / n;
                Substep(h, sx + (ax - sx) * t, sy + (ay - sy) * t, sz + (az - sz) * t, bodyX, bodyY, bodyZ);
            }
        }

        private void Translate(float dx, float dy, float dz)
        {
            for (int i = 0; i < Count; i++)
            {
                _x[i] += dx;
                _y[i] += dy;
                _z[i] += dz;
                _px[i] += dx;
                _py[i] += dy;
                _pz[i] += dz;
            }
        }

        private void Substep(float h, float ax, float ay, float az, float bx, float by, float bz)
        {
            float h2 = h * h;
            float keep = _s.damping;
            float drag = _s.airDrag;
            float carry = _s.anchorCarry;
            // Anchor displacement this substep; every particle is carried by a fraction of it (rider-relative frame),
            // while drag acts on the particle's true world velocity so the scarf still streams in the airflow.
            float cx = (ax - _x[0]) * carry;
            float cy = (ay - _y[0]) * carry;
            float cz = (az - _z[0]) * carry;
            for (int i = 1; i < Count; i++)
            {
                float vx = (_x[i] - _px[i]) * keep;
                float vy = (_y[i] - _py[i]) * keep;
                float vz = (_z[i] - _pz[i]) * keep;
                _px[i] = _x[i] + cx;
                _py[i] = _y[i] + cy;
                _pz[i] = _z[i] + cz;
                float axx = drag * (_windX - (vx + cx) / h);
                float ayy = drag * (_windY - (vy + cy) / h) - _gravity;
                float azz = drag * (_windZ - (vz + cz) / h);
                _x[i] += cx + vx + axx * h2;
                _y[i] += cy + vy + ayy * h2;
                _z[i] += cz + vz + azz * h2;
            }

            _px[0] = _x[0];
            _py[0] = _y[0];
            _pz[0] = _z[0];
            _x[0] = ax;
            _y[0] = ay;
            _z[0] = az;

            for (int it = 0; it < _s.constraintIterations; it++)
            {
                for (int i = 1; i < Count; i++)
                {
                    float dx = _x[i] - _x[i - 1];
                    float dy = _y[i] - _y[i - 1];
                    float dz = _z[i] - _z[i - 1];
                    float d = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
                    if (d < 1e-6f) continue;
                    float diff = (d - _segment) / d;
                    if (i == 1)
                    {
                        _x[i] -= dx * diff;
                        _y[i] -= dy * diff;
                        _z[i] -= dz * diff;
                    }
                    else
                    {
                        float half = diff * 0.5f;
                        _x[i - 1] += dx * half;
                        _y[i - 1] += dy * half;
                        _z[i - 1] += dz * half;
                        _x[i] -= dx * half;
                        _y[i] -= dy * half;
                        _z[i] -= dz * half;
                    }
                }
                PushOutOfBody(bx, by, bz);
            }
        }

        private void PushOutOfBody(float bx, float by, float bz)
        {
            float r = _s.bodyRadius;
            if (r <= 0f) return;
            for (int i = 1; i < Count; i++)
            {
                float dx = _x[i] - bx;
                float dy = _y[i] - by;
                float dz = _z[i] - bz;
                float d2 = dx * dx + dy * dy + dz * dz;
                if (d2 >= r * r || d2 < 1e-12f) continue;
                float d = (float)Math.Sqrt(d2);
                float push = (r - d) / d;
                _x[i] += dx * push;
                _y[i] += dy * push;
                _z[i] += dz * push;
            }
        }

        /// <summary>Worst |segment - rest| / rest.</summary>
        public float MaxStretch()
        {
            float worst = 0f;
            for (int i = 1; i < Count; i++)
            {
                float dx = _x[i] - _x[i - 1];
                float dy = _y[i] - _y[i - 1];
                float dz = _z[i] - _z[i - 1];
                float d = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
                float s = Math.Abs(d - _segment) / _segment;
                if (s > worst) worst = s;
            }
            return worst;
        }

        public bool AllFinite()
        {
            for (int i = 0; i < Count; i++)
            {
                if (float.IsNaN(_x[i]) || float.IsNaN(_y[i]) || float.IsNaN(_z[i]) ||
                    float.IsInfinity(_x[i]) || float.IsInfinity(_y[i]) || float.IsInfinity(_z[i])) return false;
            }
            return true;
        }
    }

    /// <summary>Damped spring for sled runner bend: kicked on landing, settles back to straight.</summary>
    public sealed class SledFlex
    {
        private readonly ClothSettings _s;

        public SledFlex(ClothSettings settings)
        {
            _s = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public float BendDegrees { get; private set; }
        public float Velocity { get; private set; }

        public void Land(float verticalSpeed)
        {
            Velocity += Math.Abs(verticalSpeed) * _s.flexImpulsePerMps;
        }

        public void Step(float dt)
        {
            // Semi-implicit Euler is stable for these stiffness/damping values at 100 Hz.
            float acc = -_s.flexStiffness * BendDegrees - _s.flexDamping * Velocity;
            Velocity += acc * dt;
            BendDegrees += Velocity * dt;
            if (BendDegrees > _s.flexMaxDegrees)
            {
                BendDegrees = _s.flexMaxDegrees;
                if (Velocity > 0f) Velocity = 0f;
            }
            else if (BendDegrees < -_s.flexMaxDegrees)
            {
                BendDegrees = -_s.flexMaxDegrees;
                if (Velocity < 0f) Velocity = 0f;
            }
        }

        public void Reset()
        {
            BendDegrees = 0f;
            Velocity = 0f;
        }
    }

    public readonly struct ScarfChoice
    {
        public readonly ScarfMethod Method;
        public readonly bool WithinBudget;
        public readonly string Reason;

        public ScarfChoice(ScarfMethod method, bool withinBudget, string reason)
        {
            Method = method;
            WithinBudget = withinBudget;
            Reason = reason;
        }
    }

    /// <summary>T6 rule: choose by measured per-scarf cost; ties (within tieToleranceRatio) go to Verlet.</summary>
    public static class ScarfMethodSelector
    {
        /// <summary>Reads the "choice=" line of a benchmark log (Logs/scarf_benchmark.txt).</summary>
        public static ScarfMethod ParseLogChoice(string log)
        {
            foreach (string line in (log ?? string.Empty).Split('\n'))
            {
                string l = line.Trim();
                if (l.StartsWith("choice=", StringComparison.Ordinal) &&
                    Enum.TryParse(l.Substring("choice=".Length), out ScarfMethod m) && Enum.IsDefined(typeof(ScarfMethod), m)) return m;
            }
            throw new FormatException("no choice= line in benchmark log");
        }

        public static ScarfChoice Choose(double verletMs, double clothMs, ClothSettings s)
        {
            if (double.IsNaN(verletMs) || double.IsNaN(clothMs) || verletMs < 0d || clothMs < 0d)
                return new ScarfChoice(ScarfMethod.Verlet, false, "invalid measurement; default Verlet");
            bool verletOk = verletMs <= s.perScarfBudgetMs;
            bool clothOk = clothMs <= s.perScarfBudgetMs;
            double hi = Math.Max(verletMs, clothMs);
            bool tie = hi <= 0d || Math.Abs(verletMs - clothMs) <= s.tieToleranceRatio * hi;
            if (verletOk && !clothOk) return new ScarfChoice(ScarfMethod.Verlet, true, "only Verlet within budget");
            if (clothOk && !verletOk) return new ScarfChoice(ScarfMethod.UnityCloth, true, "only Unity Cloth within budget");
            bool ok = verletOk && clothOk;
            if (tie) return new ScarfChoice(ScarfMethod.Verlet, ok, "tie; Verlet preferred");
            return verletMs < clothMs
                ? new ScarfChoice(ScarfMethod.Verlet, ok, "Verlet cheaper")
                : new ScarfChoice(ScarfMethod.UnityCloth, ok, "Unity Cloth cheaper");
        }
    }
}
