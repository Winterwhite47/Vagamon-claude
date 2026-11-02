using Hushlands.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hushlands
{
    /// <summary>
    /// Single button (Space, left mouse, gamepad South) sampled per fixed step.
    /// TapThisStep is true on the step a press shorter than holdThresholdSeconds is released.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class HushInputSource : MonoBehaviour, IInputSource
    {
        public static readonly string[] Bindings = { "<Keyboard>/space", "<Mouse>/leftButton", "<Gamepad>/buttonSouth" };

        [SerializeField] private RiderConfig riderConfig;
        private InputAction _action;

        private bool _suppress;

        /// <summary>Gentle timing: overrides RiderSettings.holdThresholdSeconds when set (> 0).</summary>
        public float HoldThresholdOverride { get; set; }

        /// <summary>Ignore the current press until it is released (the press that chose "Ride" must not jump).</summary>
        public void SuppressUntilRelease()
        {
            _suppress = true;
            TapThisStep = false;
            IsHeld = false;
            HeldSeconds = 0f;
        }

        public bool TapThisStep { get; private set; }
        public bool IsHeld { get; private set; }
        public float HeldSeconds { get; private set; }

        public void Configure(RiderConfig config)
        {
            riderConfig = config;
        }

        private void OnEnable()
        {
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsInFixedUpdate;
            _action = new InputAction("Ride", InputActionType.Button);
            foreach (string b in Bindings) _action.AddBinding(b);
            _action.Enable();
        }

        private void OnDisable()
        {
            if (_action == null) return;
            _action.Disable();
            _action.Dispose();
            _action = null;
        }

        private void FixedUpdate()
        {
            if (_action == null || riderConfig == null) return;
            if (_suppress)
            {
                if (!_action.IsPressed()) _suppress = false;
                return;
            }
            float threshold = HoldThresholdOverride > 0f ? HoldThresholdOverride : riderConfig.Settings.holdThresholdSeconds;
            Sample(_action.WasPressedThisFrame(), _action.WasReleasedThisFrame(), _action.IsPressed(), Time.fixedDeltaTime, threshold);
        }

        /// <summary>Pure sampling rule, exposed for tests.</summary>
        public void Sample(bool pressed, bool released, bool down, float dt, float holdThreshold)
        {
            TapThisStep = false;
            if (pressed)
            {
                IsHeld = true;
                HeldSeconds = 0f;
            }
            if (IsHeld) HeldSeconds += dt;
            if (released || (IsHeld && !down))
            {
                TapThisStep = IsHeld && HeldSeconds < holdThreshold;
                IsHeld = false;
                HeldSeconds = 0f;
            }
        }
    }
}
