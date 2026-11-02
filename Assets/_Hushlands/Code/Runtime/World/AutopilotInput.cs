using Hushlands.Core;
using UnityEngine;

namespace Hushlands
{
    /// <summary>Bot input for captures and soak runs ("-hushAutopilot"): BotPilot taps before each required jump.</summary>
    [DefaultExecutionOrder(-90)]
    public sealed class AutopilotInput : MonoBehaviour, IInputSource
    {
        public const string CommandLineFlag = "-hushAutopilot";

        private readonly ScriptedInput _input = new ScriptedInput();
        private BotPilot _pilot;
        private IRider _rider;

        public bool TapThisStep => _input.TapThisStep;
        public bool IsHeld => _input.IsHeld;
        public float HeldSeconds => _input.HeldSeconds;

        public void Initialize(BotPilot pilot, IRider rider)
        {
            _pilot = pilot;
            _rider = rider;
        }

        public static bool Requested()
        {
            foreach (string a in System.Environment.GetCommandLineArgs())
            {
                if (string.Equals(a, CommandLineFlag, System.StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private void FixedUpdate()
        {
            if (_pilot == null || _rider == null) return;
            bool grounded = _rider.State == RiderState.Grounded || _rider.State == RiderState.Tucking;
            _pilot.Drive(_input, _rider.Position.Distance, _rider.Speed, grounded);
        }
    }

}
