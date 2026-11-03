// API surface of com.unity.render-pipelines.core used by Hushlands, mirrored from
// Unity-Technologies/Graphics @ 6000.0/staging: Runtime/Volume/{Volume,VolumeProfile,VolumeComponent,VolumeParameter}.cs.
// Compile-time only; never shipped.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEngine.Rendering
{
    public class VolumeParameter<T>
    {
        public T value;
        public bool overrideState;
        public void Override(T x) { overrideState = true; value = x; }
    }

    public class BoolParameter : VolumeParameter<bool> { }
    public class FloatParameter : VolumeParameter<float> { }
    public class MinFloatParameter : FloatParameter { }
    public class ClampedFloatParameter : FloatParameter { }
    public class ColorParameter : VolumeParameter<Color> { }
    public class NoInterpIntParameter : VolumeParameter<int> { }

    public class VolumeComponent : ScriptableObject { }

    public sealed class VolumeProfile : ScriptableObject
    {
        public List<VolumeComponent> components = new List<VolumeComponent>();
        public T Add<T>(bool overrides = false) where T : VolumeComponent { return null; }
        public bool TryGet<T>(out T component) where T : VolumeComponent { component = null; return false; }
    }

    public class Volume : MonoBehaviour
    {
        public bool isGlobal { get; set; }
        public VolumeProfile sharedProfile = null;
    }
}
