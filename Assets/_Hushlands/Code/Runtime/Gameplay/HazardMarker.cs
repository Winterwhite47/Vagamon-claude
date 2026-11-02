using Hushlands.Core;
using UnityEngine;

namespace Hushlands
{
    /// <summary>Touching this collider (or entering this trigger) crashes the rider with the given cause.</summary>
    public sealed class HazardMarker : MonoBehaviour
    {
        public CrashCause cause = CrashCause.Obstacle;
    }
}
