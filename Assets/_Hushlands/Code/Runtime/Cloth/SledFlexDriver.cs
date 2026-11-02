using Hushlands.Core;
using UnityEngine;

namespace Hushlands
{
    /// <summary>Bends the sled runner bone on safe landings (SledFlex spring), then springs back.</summary>
    public sealed class SledFlexDriver : MonoBehaviour
    {
        [SerializeField] private Transform runner;
        private SledFlex _flex;
        private PhysxRider _rider;
        private Quaternion _rest;

        public SledFlex Flex => _flex;

        public void Initialize(ClothSettings settings, PhysxRider rider, Transform runnerBone)
        {
            if (_rider != null) _rider.Landed -= OnLanded;
            _flex = new SledFlex(settings);
            _rider = rider;
            runner = runnerBone;
            if (runner != null) _rest = runner.localRotation;
            if (_rider != null) _rider.Landed += OnLanded;
        }

        private void OnLanded(float verticalSpeed)
        {
            _flex?.Land(verticalSpeed);
        }

        private void FixedUpdate()
        {
            _flex?.Step(Time.fixedDeltaTime);
        }

        private void LateUpdate()
        {
            if (_flex == null || runner == null) return;
            runner.localRotation = _rest * Quaternion.Euler(0f, 0f, -_flex.BendDegrees);
        }

        private void OnDestroy()
        {
            if (_rider != null) _rider.Landed -= OnLanded;
        }
    }
}
