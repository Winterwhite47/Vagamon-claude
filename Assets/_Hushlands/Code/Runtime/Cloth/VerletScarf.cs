using Hushlands.Core;
using UnityEngine;

namespace Hushlands
{
    /// <summary>Drives a scarf bone chain from VerletScarfSolver. Bones are optional (benchmark runs without a mesh).</summary>
    public sealed class VerletScarf : MonoBehaviour
    {
        [SerializeField] private ClothConfig config;
        [SerializeField] private Transform anchor;
        [SerializeField] private Transform body;
        [SerializeField] private Transform[] bones;
        [SerializeField] private bool autoTick = true;

        private VerletScarfSolver _solver;

        public VerletScarfSolver Solver => _solver;

        public void Initialize(ClothSettings settings, float gravity, Transform anchorPoint, Transform bodyCenter,
            Transform[] boneChain, bool tickAutomatically)
        {
            anchor = anchorPoint;
            body = bodyCenter;
            bones = boneChain;
            autoTick = tickAutomatically;
            _solver = new VerletScarfSolver(settings, gravity);
            ResetPose();
        }

        private void Start()
        {
            if (_solver == null && config != null && anchor != null)
                Initialize(config.Settings, -Physics.gravity.y, anchor, body, bones, autoTick);
        }

        /// <summary>Lay the scarf straight behind the anchor (spawn, retry).</summary>
        public void ResetPose()
        {
            if (_solver == null || anchor == null) return;
            Vector3 a = anchor.position;
            Vector3 back = -anchor.right;
            _solver.Reset(a.x, a.y, a.z, back.x, back.y, back.z);
            ApplyBones();
        }

        private void LateUpdate()
        {
            if (autoTick) Tick(Time.deltaTime);
        }

        public void Tick(float dt)
        {
            if (_solver == null || anchor == null) return;
            Vector3 a = anchor.position;
            Vector3 b = body != null ? body.position : a;
            _solver.Step(dt, a.x, a.y, a.z, b.x, b.y, b.z);
            ApplyBones();
        }

        private void ApplyBones()
        {
            if (bones == null) return;
            int n = Mathf.Min(bones.Length, _solver.Count);
            for (int i = 0; i < n; i++)
            {
                if (bones[i] == null) continue;
                var p = new Vector3(_solver.X(i), _solver.Y(i), _solver.Z(i));
                int j = i + 1 < _solver.Count ? i + 1 : i - 1;
                var q = new Vector3(_solver.X(j), _solver.Y(j), _solver.Z(j));
                Vector3 dir = j > i ? q - p : p - q;
                bones[i].position = p;
                if (dir.sqrMagnitude > 1e-8f) bones[i].rotation = Quaternion.LookRotation(dir, Vector3.up);
            }
        }
    }
}
