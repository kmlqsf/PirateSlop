using UnityEngine;

namespace PirateSlop
{
    /// <summary>
    /// Mast station. Default: press to toggle sails. Hold mode keeps throttle while E/LMB is held.
    /// </summary>
    public class SailStation : StationInteractable
    {
        public enum Mode
        {
            Toggle,
            Hold
        }

        [SerializeField] Mode mode = Mode.Toggle;
        [SerializeField, Range(0f, 1f)] float onThrottle = 1f;

        public override string Prompt
        {
            get
            {
                if (ship == null) return prompt;
                if (mode == Mode.Hold) return "Hold E — set sail";
                return ship.ThrottleTarget > 0.15f ? "E — furl sails" : "E — set sail";
            }
        }

        public void Configure(ShipMotor motor, Mode useMode)
        {
            mode = useMode;
            Bind(motor, Prompt, useMode == Mode.Hold ? InteractionKind.Hold : InteractionKind.Instant);
        }

        public override bool TryBegin(InteractionContext ctx)
        {
            if (ship == null) return false;
            if (mode == Mode.Hold)
            {
                ship.SetThrottle01(onThrottle);
                return true;
            }

            ship.ToggleThrottle();
            return true;
        }

        public override void Tick(InteractionContext ctx)
        {
            if (mode == Mode.Hold && ship != null)
                ship.SetThrottle01(ctx.Input.InteractHeld ? onThrottle : 0f);
        }

        public override void End(InteractionContext ctx)
        {
            if (mode == Mode.Hold && ship != null)
                ship.SetThrottle01(0f);
        }
    }
}
