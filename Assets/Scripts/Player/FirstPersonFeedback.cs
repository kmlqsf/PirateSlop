using Unity.Cinemachine;
using UnityEngine;
using PirateSlop.Networking;

namespace PirateSlop
{
    [DefaultExecutionOrder(350)]
    public sealed class FirstPersonFeedback : MonoBehaviour
    {
        NetworkPlayer player;
        Camera view;
        Vector3 lastPosition, offset, lastPlayerPosition;
        Quaternion lastRotation, rotationOffset = Quaternion.identity;
        float blend;
        float blendDuration = .22f;
        int mode = -1;
        bool initialized;
        static readonly CinemachineImpulseDefinition impulse = new()
        {
            ImpulseChannel = 1,
            ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Bump,
            ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Dissipating,
            ImpulseDuration = .22f,
            DissipationDistance = 28f
        };
        public static void Kick(Vector3 point, Vector3 direction, float strength)
        {
            if (!Application.isPlaying || Application.isBatchMode) return;
            impulse.CreateEvent(point, direction.normalized * Mathf.Clamp(strength, 0f, .075f));
        }
        void Awake() => player = GetComponent<NetworkPlayer>();
        void LateUpdate()
        {
            if (player == null || !player.IsOwner || player.Motor.IsThirdPerson) { initialized = false; return; }
            view = player.Motor.PlayerCamera;
            if (view == null || !view.isActiveAndEnabled) { initialized = false; return; }
            int nextMode = ShipSpyglassView.IsViewing ? 2 : player.Motor.ActiveCannon != null ? (player.Motor.ActiveCannon.IsMortar ? 3 : 1) : 0;
            Vector3 targetPosition = view.transform.position;
            Quaternion targetRotation = view.transform.rotation;
            if (!initialized || Vector3.Distance(player.transform.position, lastPlayerPosition) > 12f)
            { initialized = true; lastPosition = targetPosition; lastRotation = targetRotation; mode = nextMode; blend = 0f; }
            lastPlayerPosition = player.transform.position;
            if (mode != nextMode)
            {
                offset = lastPosition - targetPosition;
                rotationOffset = Quaternion.Inverse(targetRotation) * lastRotation;
                blendDuration = mode == 3 || nextMode == 3 ? .65f : .22f;
                blend = 1f; mode = nextMode;
            }
            blend = Mathf.MoveTowards(blend, 0f, Time.unscaledDeltaTime / blendDuration);
            float weight = Mathf.SmoothStep(0f, 1f, blend);
            lastPosition = targetPosition + offset * weight;
            lastRotation = targetRotation * Quaternion.Slerp(Quaternion.identity, rotationOffset, weight);
            view.transform.SetPositionAndRotation(lastPosition, lastRotation);
            if (!SessionController.MenuOpen && !player.Motor.IsDead &&
                CinemachineImpulseManager.Instance.GetImpulseAt(lastPosition, false, 1, out var shake, out var unused))
            {
                float gain = ShipSpyglassView.IsViewing ? .15f : 1f;
                view.transform.position += Vector3.ClampMagnitude(shake, .065f) * gain;
            }
        }
    }
}
