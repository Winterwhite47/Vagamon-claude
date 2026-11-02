namespace Hushlands.Core
{
    public struct RiderIntent
    {
        public bool Jump;
        public bool Tuck;
        public bool Flip;
    }

    /// <summary>
    /// Single-lane input rules, shared by KinematicRider and the PhysX rider.
    /// Tap (press and release within holdThreshold) = jump, fired on release; coyote time and jump buffer apply.
    /// Hold on ground past the threshold = tuck. Hold in air past the threshold, on a press that began in air, = flip.
    /// Taps never flip; a held tuck that leaves the ground never flips.
    /// </summary>
    public sealed class RiderBrain
    {
        private readonly RiderSettings _s;
        private bool _wasHeld;
        private bool _pressStartedAirborne;
        private float _sinceGrounded;
        private float _buffer;
        private bool _jumpedSinceGrounded;

        public RiderBrain(RiderSettings settings)
        {
            _s = settings;
        }

        public void Reset()
        {
            _wasHeld = false;
            _pressStartedAirborne = false;
            _sinceGrounded = 0f;
            _buffer = 0f;
            _jumpedSinceGrounded = false;
        }

        public RiderIntent Step(IInputSource input, bool grounded, float dt)
        {
            var intent = new RiderIntent();
            if (grounded)
            {
                _sinceGrounded = 0f;
                _jumpedSinceGrounded = false;
            }
            else
            {
                _sinceGrounded += dt;
            }

            if (input.IsHeld && !_wasHeld) _pressStartedAirborne = !grounded;
            _wasHeld = input.IsHeld;

            bool canJump = !_jumpedSinceGrounded && (grounded || _sinceGrounded <= _s.coyoteSeconds);
            if (input.TapThisStep)
            {
                if (canJump) intent.Jump = true;
                else _buffer = _s.jumpBufferSeconds;
            }
            else if (_buffer > 0f && grounded)
            {
                intent.Jump = true;
            }

            if (intent.Jump)
            {
                _buffer = 0f;
                _jumpedSinceGrounded = true;
            }
            else if (_buffer > 0f)
            {
                _buffer -= dt;
            }

            bool longHold = input.IsHeld && input.HeldSeconds >= _s.holdThresholdSeconds;
            intent.Tuck = grounded && longHold && !_pressStartedAirborne && !intent.Jump;
            intent.Flip = !grounded && longHold && _pressStartedAirborne;
            return intent;
        }
    }

    /// <summary>
    /// Reference pilot used by the bot validator and PhysX parity tests: taps once,
    /// botTakeoffSeconds before each required jump. Output is a raw input frame.
    /// </summary>
    public sealed class BotPilot
    {
        private readonly System.Collections.Generic.List<Span> _hazards;
        private readonly double _takeoffSeconds;
        private int _next;
        private int _jumpedFor = -1;

        public BotPilot(System.Collections.Generic.IReadOnlyList<IChunk> stretch, double takeoffSeconds)
        {
            _takeoffSeconds = takeoffSeconds;
            _hazards = new System.Collections.Generic.List<Span>();
            Kinds = new System.Collections.Generic.List<ChunkFeature>();
            var order = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<Span, ChunkFeature>>();
            foreach (IChunk c in stretch)
            {
                foreach (ChunkFeature f in c.Features)
                {
                    if (!f.IsRequiredJump) continue;
                    order.Add(new System.Collections.Generic.KeyValuePair<Span, ChunkFeature>(
                        new Span(c.StartDistance + f.start, c.StartDistance + f.End), f));
                }
            }
            order.Sort((a, b) => a.Key.Start.CompareTo(b.Key.Start));
            foreach (var kv in order)
            {
                _hazards.Add(kv.Key);
                Kinds.Add(kv.Value);
            }
        }

        public System.Collections.Generic.IReadOnlyList<Span> Hazards => _hazards;
        public System.Collections.Generic.List<ChunkFeature> Kinds { get; }
        public int NextIndex => _next;
        public int JumpsTaken { get; private set; }

        /// <summary>Fills input for this step from rider distance, horizontal speed and grounded state.</summary>
        public void Drive(ScriptedInput input, double distance, double speed, bool grounded)
        {
            while (_next < _hazards.Count && distance > _hazards[_next].End) _next++;
            input.TapThisStep = false;
            input.IsHeld = false;
            input.HeldSeconds = 0f;
            if (grounded && _next < _hazards.Count && _jumpedFor != _next &&
                _hazards[_next].Start - distance <= speed * _takeoffSeconds)
            {
                input.TapThisStep = true;
                _jumpedFor = _next;
                JumpsTaken++;
            }
        }
    }
}
