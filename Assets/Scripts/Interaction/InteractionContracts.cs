using UnityEngine;

namespace PirateSlop
{
    public enum InteractionKind
    {
        Instant,
        Hold,
        Occupied
    }

    public interface IInteractionAgent
    {
        Transform Transform { get; }
        Camera Camera { get; }
        void SetLocomotionLocked(bool locked);
    }

    public struct InteractionContext
    {
        public IInteractionAgent Agent;
        public GameplayInput.Frame Input;
    }

    public interface IInteractable
    {
        string Prompt { get; }
        bool IsAvailable { get; }
        InteractionKind Kind { get; }
        bool TryBegin(InteractionContext ctx);
        void Tick(InteractionContext ctx);
        void End(InteractionContext ctx);
    }
}
