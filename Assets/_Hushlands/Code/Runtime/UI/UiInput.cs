using System.Collections.Generic;
using Hushlands.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hushlands
{
    /// <summary>
    /// Menu commands from keyboard, gamepad and mouse. Sampled in FixedUpdate because input events are processed there
    /// (see HushInputSource); commands queue until the UI reads them in Update.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class UiInput : MonoBehaviour
    {
        private readonly Queue<UiCommand> _queue = new Queue<UiCommand>();
        private readonly List<KeyValuePair<UiCommand, InputAction>> _actions = new List<KeyValuePair<UiCommand, InputAction>>();

        public bool MouseClicked { get; private set; }
        public Vector2 MousePosition { get; private set; }

        private void OnEnable()
        {
            Add(UiCommand.Up, "<Keyboard>/upArrow", "<Keyboard>/w", "<Gamepad>/dpad/up");
            Add(UiCommand.Down, "<Keyboard>/downArrow", "<Keyboard>/s", "<Gamepad>/dpad/down");
            Add(UiCommand.Left, "<Keyboard>/leftArrow", "<Keyboard>/a", "<Gamepad>/dpad/left");
            Add(UiCommand.Right, "<Keyboard>/rightArrow", "<Keyboard>/d", "<Gamepad>/dpad/right");
            Add(UiCommand.Submit, "<Keyboard>/enter", "<Keyboard>/space", "<Gamepad>/buttonSouth");
            Add(UiCommand.Back, "<Keyboard>/escape", "<Gamepad>/buttonEast");
            Add(UiCommand.Pause, "<Gamepad>/start");
        }

        private void Add(UiCommand cmd, params string[] bindings)
        {
            var a = new InputAction(cmd.ToString(), InputActionType.Button);
            foreach (string b in bindings) a.AddBinding(b);
            a.Enable();
            _actions.Add(new KeyValuePair<UiCommand, InputAction>(cmd, a));
        }

        private void OnDisable()
        {
            foreach (var kv in _actions)
            {
                kv.Value.Disable();
                kv.Value.Dispose();
            }
            _actions.Clear();
        }

        private void FixedUpdate()
        {
            foreach (var kv in _actions)
            {
                if (kv.Value.WasPressedThisFrame()) _queue.Enqueue(kv.Key);
            }
            Mouse m = Mouse.current;
            if (m != null)
            {
                MousePosition = m.position.ReadValue();
                if (m.leftButton.wasPressedThisFrame) MouseClicked = true;
            }
        }

        public void Inject(UiCommand cmd)
        {
            _queue.Enqueue(cmd);
        }

        public bool TryNext(out UiCommand cmd)
        {
            if (_queue.Count > 0)
            {
                cmd = _queue.Dequeue();
                return true;
            }
            cmd = UiCommand.None;
            return false;
        }

        public bool ConsumeClick()
        {
            bool c = MouseClicked;
            MouseClicked = false;
            return c;
        }
    }
}
