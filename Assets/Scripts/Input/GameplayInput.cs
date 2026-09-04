using UnityEngine;
using UnityEngine.InputSystem;

namespace PirateSlop
{
    /// <summary>
    /// Thin Input System wrapper. Swap bindings here without touching gameplay.
    /// </summary>
    public class GameplayInput : MonoBehaviour
    {
        public struct Frame
        {
            public Vector2 Move;
            public Vector2 Look;
            public bool Jump;
            public bool InteractPressed;
            public bool InteractHeld;
            public bool CancelPressed;
        }

        [SerializeField] float mouseSensitivity = 0.08f;

        public Frame Current { get; private set; }

        void Update()
        {
            var move = Vector2.zero;
            var look = Vector2.zero;
            bool jump = false, interactPressed = false, interactHeld = false, cancel = false;

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.y -= 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move.x -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1f;
                if (move.sqrMagnitude > 1f) move.Normalize();

                jump = kb.spaceKey.wasPressedThisFrame;
                interactPressed = kb.eKey.wasPressedThisFrame;
                interactHeld = kb.eKey.isPressed;
                cancel = kb.escapeKey.wasPressedThisFrame || kb.qKey.wasPressedThisFrame;
            }

            var mouse = Mouse.current;
            if (mouse != null)
                look = mouse.delta.ReadValue() * mouseSensitivity;

            Current = new Frame
            {
                Move = move,
                Look = look,
                Jump = jump,
                InteractPressed = interactPressed,
                InteractHeld = interactHeld,
                CancelPressed = cancel
            };
        }
    }
}
