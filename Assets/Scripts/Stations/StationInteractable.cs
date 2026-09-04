using UnityEngine;

namespace PirateSlop
{
    public abstract class StationInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] protected ShipMotor ship;
        [SerializeField] protected string prompt = "Use";
        [SerializeField] protected InteractionKind kind = InteractionKind.Instant;

        public virtual string Prompt => prompt;
        public virtual bool IsAvailable => ship != null;
        public InteractionKind Kind => kind;

        public void Bind(ShipMotor motor, string usePrompt, InteractionKind useKind)
        {
            ship = motor;
            prompt = usePrompt;
            kind = useKind;
        }

        public abstract bool TryBegin(InteractionContext ctx);
        public virtual void Tick(InteractionContext ctx) { }
        public virtual void End(InteractionContext ctx) { }
    }
}
