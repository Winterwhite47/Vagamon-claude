using UnityEngine;

namespace Hushlands
{
    /// <summary>Renders the verlet scarf bones as a ribbon (after VerletScarf has moved them).</summary>
    [RequireComponent(typeof(LineRenderer))]
    [DefaultExecutionOrder(100)]
    public sealed class ScarfLine : MonoBehaviour
    {
        private Transform[] _bones;
        private LineRenderer _line;

        public void Initialize(Transform[] bones, Material mat, float width)
        {
            _bones = bones;
            _line = GetComponent<LineRenderer>();
            _line.sharedMaterial = mat;
            _line.positionCount = bones.Length;
            _line.widthMultiplier = width;
            _line.useWorldSpace = true;
        }

        private void LateUpdate()
        {
            if (_bones == null) return;
            for (int i = 0; i < _bones.Length; i++) _line.SetPosition(i, _bones[i].position);
        }
    }
}
