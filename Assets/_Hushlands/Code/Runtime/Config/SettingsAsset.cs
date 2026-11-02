using System.Collections.Generic;
using Hushlands.Core;
using UnityEngine;

namespace Hushlands
{
    /// <summary>ScriptableObject wrapper around an engine-free settings block.</summary>
    public abstract class SettingsAsset<T> : ScriptableObject where T : class, ISettings, new()
    {
        [SerializeField] private T settings = new T();

        public T Settings => settings;

        public List<string> Validate()
        {
            var errors = new List<string>();
            if (settings == null) errors.Add(GetType().Name + ": settings null");
            else settings.Validate(errors);
            return errors;
        }

        private void OnValidate()
        {
            foreach (string e in Validate()) Debug.LogError("[Hushlands] " + name + ": " + e, this);
        }
    }
}
