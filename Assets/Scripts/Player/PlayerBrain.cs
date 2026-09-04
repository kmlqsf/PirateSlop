using UnityEngine;

namespace PirateSlop
{
    public class PlayerBrain : MonoBehaviour, IInteractionAgent
    {
        [SerializeField] GameplayInput input;
        [SerializeField] PlayerMotor motor;
        [SerializeField] PlayerInteractor interactor;
        [SerializeField] bool lockCursor = true;

        Transform IInteractionAgent.Transform => transform;
        Camera IInteractionAgent.Camera => interactor != null ? interactor.Camera : null;

        void Awake()
        {
            if (input == null) input = GetComponent<GameplayInput>();
            if (motor == null) motor = GetComponent<PlayerMotor>();
            if (interactor == null) interactor = GetComponent<PlayerInteractor>();
        }

        void OnEnable()
        {
            if (lockCursor)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        void OnDisable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void Update()
        {
            if (input == null || motor == null) return;
            var frame = input.Current;
            if (interactor != null) interactor.Tick(frame);
            motor.Tick(frame, Time.deltaTime);
        }

        public void SetLocomotionLocked(bool locked)
        {
            if (motor != null) motor.SetLocomotionLocked(locked);
        }
    }
}
