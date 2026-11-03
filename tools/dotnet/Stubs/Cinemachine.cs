// API surface of com.unity.cinemachine 3.1.4 used by Hushlands, mirrored from the registry tarball
// (sha1 b66fdb7c...): Runtime/Behaviours/{CinemachineCamera,CinemachineBrain}.cs, Runtime/Core/LensSettings.cs.
using UnityEngine;

namespace Unity.Cinemachine
{
    public struct LensSettings
    {
        public float FieldOfView;
    }

    public sealed class CinemachineCamera : MonoBehaviour
    {
        public LensSettings Lens;
    }

    public class CinemachineBrain : MonoBehaviour { }
}
