using Hushlands.Core;
using UnityEngine;

namespace Hushlands
{
    /// <summary>
    /// Unity Cloth alternative for the scarf: a procedural strip on a SkinnedMeshRenderer, root row pinned to the anchor
    /// (this object is parented to it), body sphere collider. Built for the T6 cost comparison.
    /// </summary>
    [RequireComponent(typeof(SkinnedMeshRenderer))]
    public sealed class UnityClothScarf : MonoBehaviour
    {
        private Cloth _cloth;
        private Mesh _mesh;

        public Cloth Cloth => _cloth;

        public void Initialize(ClothSettings s, SphereCollider bodySphere)
        {
            _mesh = BuildStrip(s);
            var smr = GetComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = _mesh;
            _cloth = GetComponent<Cloth>();
            if (_cloth == null) _cloth = gameObject.AddComponent<Cloth>();
            _cloth.stretchingStiffness = s.clothStretchingStiffness;
            _cloth.bendingStiffness = s.clothBendingStiffness;
            _cloth.damping = s.clothDamping;
            _cloth.worldVelocityScale = s.clothWorldVelocityScale;
            _cloth.worldAccelerationScale = s.clothWorldAccelerationScale;
            _cloth.externalAcceleration = new Vector3(s.windX, s.windY, s.windZ) * s.airDrag;
            if (bodySphere != null) _cloth.sphereColliders = new[] { new ClothSphereColliderPair(bodySphere) };

            ClothSkinningCoefficient[] coeff = _cloth.coefficients;
            for (int i = 0; i < coeff.Length; i++)
            {
                coeff[i].maxDistance = i < 2 ? 0f : float.MaxValue;
                coeff[i].collisionSphereDistance = 0f;
            }
            _cloth.coefficients = coeff;
        }

        /// <summary>Two vertices per row along -X (trailing); rows 0 is the pinned root.</summary>
        private static Mesh BuildStrip(ClothSettings s)
        {
            int rows = s.scarfSegments + 1;
            var verts = new Vector3[rows * 2];
            var uv = new Vector2[rows * 2];
            var tris = new int[s.scarfSegments * 6];
            float seg = s.scarfLength / s.scarfSegments;
            float half = s.scarfWidth * 0.5f;
            for (int r = 0; r < rows; r++)
            {
                verts[r * 2] = new Vector3(-seg * r, 0f, -half);
                verts[r * 2 + 1] = new Vector3(-seg * r, 0f, half);
                float v = (float)r / s.scarfSegments;
                uv[r * 2] = new Vector2(0f, v);
                uv[r * 2 + 1] = new Vector2(1f, v);
            }
            for (int r = 0; r < s.scarfSegments; r++)
            {
                int t = r * 6;
                int a = r * 2;
                tris[t] = a;
                tris[t + 1] = a + 2;
                tris[t + 2] = a + 1;
                tris[t + 3] = a + 1;
                tris[t + 4] = a + 2;
                tris[t + 5] = a + 3;
            }
            var mesh = new Mesh { name = "ScarfStrip", vertices = verts, uv = uv, triangles = tris };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
        }
    }
}
