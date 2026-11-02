using System;
using Hushlands.Core;
using UnityEngine;

namespace Hushlands
{
    /// <summary>
    /// PhysX rider on the 2.5D gameplay plane (x = distance, y = height; z and off-plane rotation frozen).
    /// Ground: horizontal speed integrated by SpeedModel (gravity, per-surface friction, drag, tuck, slope assist),
    /// velocity laid along the slope tangent. Air: PhysX ballistics, flip by angular velocity.
    /// Landing within safeLandingToleranceDegrees of the ground normal is safe; otherwise a BadLanding crash.
    /// Obstacles and pits crash through HazardMarker. Uses the same RiderBrain as KinematicRider.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public sealed class PhysxRider : MonoBehaviour, IRider
    {
        [SerializeField] private bool autoStep = true;

        private RiderSettings _r;
        private SpeedSettings _s;
        private SpeedModel _speed;
        private ISurfaceProvider _surfaces;
        private ISlopeSpline _slope;
        private IInputSource _input;
        private RiderBrain _brain;
        private Rigidbody _rb;
        private CapsuleCollider _col;
        private PhysicsMaterial _material;

        private double _runSeconds;
        private float _ignoreGround;
        private float _flipAccum;
        private float _landingBonusDegrees;
        private bool _grounded;
        private RiderState _state = RiderState.Grounded;

        public RiderState State => _state;
        public SlopePoint Position => new SlopePoint(_rb.position.x, _rb.position.y);
        public float Speed => _rb.linearVelocity.x;
        public float FlipAngle => _flipAccum;
        public int CompletedFlips { get; private set; }
        public bool Initialized => _rb != null && _r != null;

        public event Action<RiderState, RiderState> StateChanged;
        public event Action<CrashCause> Crashed;

        public void Initialize(RiderSettings rider, SpeedSettings speed, SurfaceSettings surfaces,
            ISlopeSpline slope, IInputSource input, bool stepAutomatically)
        {
            _r = rider ?? throw new ArgumentNullException(nameof(rider));
            _s = speed ?? throw new ArgumentNullException(nameof(speed));
            _slope = slope ?? throw new ArgumentNullException(nameof(slope));
            _input = input;
            _surfaces = new SurfaceTable(surfaces);
            _speed = new SpeedModel(speed);
            _brain = new RiderBrain(rider);
            autoStep = stepAutomatically;

            Physics.gravity = new Vector3(0f, -speed.gravity, 0f);
            gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");

            _rb = GetComponent<Rigidbody>();
            _rb.mass = rider.mass;
            _rb.linearDamping = 0f;
            _rb.angularDamping = 0f;
            _rb.useGravity = true;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotationX |
                              RigidbodyConstraints.FreezeRotationY;

            _col = GetComponent<CapsuleCollider>();
            if (_col == null) _col = gameObject.AddComponent<CapsuleCollider>();
            _col.direction = 0;
            _col.height = rider.colliderLength;
            _col.radius = rider.colliderRadius;
            _col.center = new Vector3(0f, rider.colliderRadius, 0f);
            if (_material == null)
            {
                _material = new PhysicsMaterial("RiderFrictionless")
                {
                    dynamicFriction = 0f,
                    staticFriction = 0f,
                    bounciness = 0f,
                    frictionCombine = PhysicsMaterialCombine.Minimum,
                    bounceCombine = PhysicsMaterialCombine.Minimum,
                };
            }
            _col.sharedMaterial = _material;
        }

        /// <summary>Extra landing tolerance granted by assists (T5).</summary>
        public void SetLandingBonus(float degrees)
        {
            _landingBonusDegrees = Mathf.Max(0f, degrees);
        }

        private void FixedUpdate()
        {
            if (autoStep && Initialized && _input != null) FixedStep(_input, Time.fixedDeltaTime);
        }

        public void FixedStep(IInputSource input, float dt)
        {
            if (_state == RiderState.Crashed || !Initialized) return;
            _runSeconds += dt;
            if (_ignoreGround > 0f) _ignoreGround -= dt;

            bool wasGrounded = _grounded;
            bool hit = ProbeGround(out RaycastHit ground, out float gap);
            float allowed = wasGrounded ? _r.groundSnapDistance : _r.groundContactSkin;
            bool grounded = hit && _ignoreGround <= 0f && gap <= allowed;

            if (grounded && !wasGrounded && !Land(ground.normal)) return;

            RiderIntent intent = _brain.Step(input, grounded, dt);
            if (grounded) GroundStep(intent, ground.normal, gap, dt);
            else AirStep(intent, dt);
        }

        private void GroundStep(RiderIntent intent, Vector3 normal, float gap, float dt)
        {
            var tangent = new Vector3(normal.y, -normal.x, 0f).normalized;
            float angle = Mathf.Atan2(-tangent.y, tangent.x);
            float friction = _surfaces.FrictionFor(_slope.SurfaceAt(_rb.position.x));
            float vx = _speed.Step(_rb.linearVelocity.x, angle, friction, intent.Tuck, _runSeconds, dt);
            Vector3 v = tangent * (vx / Mathf.Max(tangent.x, 1e-3f));
            if (gap > 0f) v.y -= gap / dt;

            if (intent.Jump)
            {
                v.y = _r.jumpVelocity;
                _ignoreGround = _r.takeoffGroundIgnoreSeconds;
                _grounded = false;
                SetState(RiderState.Airborne);
            }
            else
            {
                _grounded = true;
                SetState(intent.Tuck ? RiderState.Tucking : RiderState.Grounded);
            }

            // Gravity off while riding so PhysX does not push the sled into the slope; on again in the air.
            _rb.useGravity = intent.Jump;
            _rb.linearVelocity = v;
            _rb.angularVelocity = Vector3.zero;
            Quaternion target = Quaternion.FromToRotation(Vector3.up, normal);
            float k = 1f - Mathf.Exp(-_r.groundAlignSharpness * dt);
            _rb.MoveRotation(Quaternion.Slerp(_rb.rotation, target, k));
        }

        private void AirStep(RiderIntent intent, float dt)
        {
            _grounded = false;
            _rb.useGravity = true;
            if (!Mathf.Approximately(_r.airGravityScale, 1f))
                _rb.AddForce(Physics.gravity * (_r.airGravityScale - 1f), ForceMode.Acceleration);

            if (intent.Flip)
            {
                // Forward flip while travelling +x is a clockwise (negative z) rotation.
                _rb.angularVelocity = new Vector3(0f, 0f, -_r.flipDegreesPerSecond * Mathf.Deg2Rad);
                _flipAccum += _r.flipDegreesPerSecond * dt;
                SetState(RiderState.Flipping);
            }
            else
            {
                _rb.angularVelocity = Vector3.zero;
                SetState(RiderState.Airborne);
            }
        }

        /// <summary>Returns false if the landing crashed.</summary>
        private bool Land(Vector3 normal)
        {
            float off = Vector3.Angle(_rb.rotation * Vector3.up, normal);
            if (off > _r.safeLandingToleranceDegrees + _landingBonusDegrees)
            {
                Crash(CrashCause.BadLanding);
                return false;
            }
            CompletedFlips += Mathf.RoundToInt(_flipAccum / 360f);
            _flipAccum = 0f;
            return true;
        }

        private bool ProbeGround(out RaycastHit hit, out float gap)
        {
            Vector3 origin = _rb.position + Vector3.up * _r.colliderRadius;
            float reach = _r.colliderRadius + _r.groundProbeDistance;
            if (Physics.Raycast(origin, Vector3.down, out hit, reach, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
                hit.collider.GetComponent<GroundMarker>() != null)
            {
                gap = hit.distance - _r.colliderRadius;
                return true;
            }
            gap = float.MaxValue;
            return false;
        }

        private void OnCollisionEnter(Collision collision)
        {
            HazardMarker h = collision.collider.GetComponent<HazardMarker>();
            if (h != null) Crash(h.cause);
        }

        private void OnTriggerEnter(Collider other)
        {
            HazardMarker h = other.GetComponent<HazardMarker>();
            if (h != null) Crash(h.cause);
        }

        private void Crash(CrashCause cause)
        {
            if (_state == RiderState.Crashed) return;
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.isKinematic = true;
            _grounded = false;
            SetState(RiderState.Crashed);
            Crashed?.Invoke(cause);
        }

        public void ResetTo(SlopePoint point, float speed)
        {
            if (!Initialized) throw new InvalidOperationException("PhysxRider not initialized");
            _rb.isKinematic = false;
            float angle = _slope.AngleAt(point.Distance);
            var tangent = new Vector3(Mathf.Cos(angle), -Mathf.Sin(angle), 0f);
            Quaternion rot = Quaternion.Euler(0f, 0f, -angle * Mathf.Rad2Deg);
            var pos = new Vector3((float)point.Distance, point.Height, 0f);
            transform.SetPositionAndRotation(pos, rot);
            _rb.position = pos;
            _rb.rotation = rot;
            _rb.linearVelocity = tangent * (speed / Mathf.Max(tangent.x, 1e-3f));
            _rb.angularVelocity = Vector3.zero;
            _flipAccum = 0f;
            _ignoreGround = 0f;
            _grounded = true;
            _brain.Reset();
            SetState(RiderState.Grounded);
        }

        private void SetState(RiderState next)
        {
            if (next == _state) return;
            RiderState prev = _state;
            _state = next;
            StateChanged?.Invoke(prev, next);
        }

        private void OnDestroy()
        {
            if (_material != null) Destroy(_material);
        }
    }
}
