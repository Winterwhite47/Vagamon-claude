using Hushlands.Core;
using UnityEngine;

namespace Hushlands
{
    /// <summary>
    /// Calm failure choreography on the rider's placeholder body: after a spill the rider eases back and sits down,
    /// or a soft mist puff closes over them (gap, landslide). Pose comes from FailureChoreography; a retry clears it.
    /// </summary>
    public sealed class FailurePresenter : MonoBehaviour
    {
        private DirectorSettings _s;
        private RunFlow _flow;
        private PhysxRider _rider;
        private Transform[] _parts;
        private Vector3[] _basePositions;
        private Quaternion[] _baseRotations;
        private Transform _puff;
        private CrashCause _cause;
        private bool _active;

        public bool Active => _active;
        public ChoreographyPose Pose { get; private set; }

        public void Initialize(DirectorSettings settings, RunFlow flow, PhysxRider rider, Material puffMaterial)
        {
            _s = settings;
            _flow = flow;
            _rider = rider;
            var parts = new System.Collections.Generic.List<Transform>();
            foreach (string name in new[] { "Body", "Head" })
            {
                Transform part = rider.transform.Find(name);
                if (part != null) parts.Add(part);
            }
            _parts = parts.ToArray();
            _basePositions = new Vector3[_parts.Length];
            _baseRotations = new Quaternion[_parts.Length];
            for (int i = 0; i < _parts.Length; i++)
            {
                _basePositions[i] = _parts[i].localPosition;
                _baseRotations[i] = _parts[i].localRotation;
            }
            GameObject puff = SliceBuilder.Primitive(PrimitiveType.Sphere, "MistPuff", rider.transform, puffMaterial);
            _puff = puff.transform;
            _puff.localScale = Vector3.zero;
            puff.SetActive(false);
            rider.Crashed += OnCrashed;
            flow.Retried += OnRetried;
        }

        private void OnDestroy()
        {
            if (_rider != null) _rider.Crashed -= OnCrashed;
            if (_flow != null) _flow.Retried -= OnRetried;
        }

        private void OnCrashed(CrashCause cause)
        {
            _cause = cause;
            _active = true;
        }

        private void OnRetried(SlopePoint point)
        {
            Clear();
        }

        /// <summary>Back to the riding pose.</summary>
        public void Clear()
        {
            _active = false;
            Pose = default;
            if (_parts == null) return;
            for (int i = 0; i < _parts.Length; i++)
            {
                _parts[i].localPosition = _basePositions[i];
                _parts[i].localRotation = _baseRotations[i];
            }
            if (_puff != null)
            {
                _puff.localScale = Vector3.zero;
                _puff.gameObject.SetActive(false);
            }
        }

        private void LateUpdate()
        {
            if (!_active || _flow == null) return;
            if (_flow.Phase == FlowPhase.Riding)
            {
                Clear();
                return;
            }
            // Hold the settled pose through the retry wipe; the retry itself clears it.
            float t = _flow.Phase == FlowPhase.FailureHold ? _flow.PhaseTime : float.MaxValue;
            Pose = FailureChoreography.Evaluate(_s, _cause, t);
            Quaternion lean = Quaternion.Euler(0f, 0f, Pose.LeanDegrees);
            Vector3 sink = new Vector3(0f, -Pose.SinkMeters, 0f);
            for (int i = 0; i < _parts.Length; i++)
            {
                _parts[i].localPosition = lean * _basePositions[i] + sink;
                _parts[i].localRotation = lean * _baseRotations[i];
            }
            bool cover = Pose.Cover > 0f;
            if (_puff.gameObject.activeSelf != cover) _puff.gameObject.SetActive(cover);
            _puff.localScale = Vector3.one * (_s.puffRadiusMeters * 2f * Pose.Cover);
        }
    }
}
