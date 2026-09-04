using UnityEngine;

namespace PirateSlop
{
    public class PlayerInteractor : MonoBehaviour, IInteractionAgent
    {
        [SerializeField] Camera view;
        [SerializeField] float range = 3.2f;
        [SerializeField] float radius = 0.18f;
        [SerializeField] LayerMask mask = ~0;

        PlayerMotor _motor;
        IInteractable _occupied;
        IInteractable _focus;

        public Transform Transform => transform;
        public Camera Camera => view;
        public IInteractable Focus => _occupied ?? _focus;
        public IInteractable Occupied => _occupied;
        public bool IsOccupying => _occupied != null;

        public void SetView(Camera camera) => view = camera;

        void Awake()
        {
            _motor = GetComponent<PlayerMotor>();
            if (view == null) view = GetComponentInChildren<Camera>();
        }

        public void SetLocomotionLocked(bool locked)
        {
            if (_motor != null) _motor.SetLocomotionLocked(locked);
        }

        public void Tick(GameplayInput.Frame input)
        {
            var ctx = new InteractionContext { Agent = this, Input = input };

            if (_occupied != null)
            {
                bool leave = input.CancelPressed
                    || (_occupied.Kind == InteractionKind.Occupied && input.InteractPressed)
                    || (_occupied.Kind == InteractionKind.Hold && !input.InteractHeld);

                if (leave)
                {
                    _occupied.End(ctx);
                    _occupied = null;
                    SetLocomotionLocked(false);
                    return;
                }

                _occupied.Tick(ctx);
                return;
            }

            _focus = Probe();

            if (_focus == null || !input.InteractPressed || !_focus.IsAvailable)
                return;

            if (!_focus.TryBegin(ctx))
                return;

            if (_focus.Kind == InteractionKind.Occupied || _focus.Kind == InteractionKind.Hold)
                _occupied = _focus;
        }

        IInteractable Probe()
        {
            if (view == null) return null;
            Ray ray = new Ray(view.transform.position, view.transform.forward);
            if (Physics.SphereCast(ray, radius, out RaycastHit hit, range, mask, QueryTriggerInteraction.Collide))
            {
                var found = hit.collider.GetComponentInParent<IInteractable>();
                if (found != null && found.IsAvailable) return found;
            }

            Collider[] nearby = Physics.OverlapSphere(transform.position + Vector3.up, 1.4f, mask, QueryTriggerInteraction.Collide);
            IInteractable best = null;
            float bestDot = 0.35f;
            for (int i = 0; i < nearby.Length; i++)
            {
                var found = nearby[i].GetComponentInParent<IInteractable>();
                if (found == null || !found.IsAvailable) continue;
                Vector3 to = nearby[i].bounds.center - view.transform.position;
                float dot = Vector3.Dot(view.transform.forward, to.normalized);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    best = found;
                }
            }

            return best;
        }
    }
}
