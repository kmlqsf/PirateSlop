using UnityEngine;

namespace PirateSlop
{
    /// <summary>
    /// Helm station. Occupy with E, steer with A/D (and mouse X), leave with E / Q / Esc.
    /// </summary>
    public class HelmStation : StationInteractable
    {
        [SerializeField] bool steerWithLook = true;
        [SerializeField] Transform standPoint;

        public override string Prompt => IsOccupied ? "E / Q — leave helm" : "E — take the helm";
        public bool IsOccupied { get; private set; }

        public void Configure(ShipMotor motor, Transform stand)
        {
            standPoint = stand;
            Bind(motor, Prompt, InteractionKind.Occupied);
        }

        public override bool TryBegin(InteractionContext ctx)
        {
            if (ship == null || ctx.Agent == null) return false;
            IsOccupied = true;
            ctx.Agent.SetLocomotionLocked(true);
            if (standPoint != null)
                ctx.Agent.Transform.SetPositionAndRotation(standPoint.position, standPoint.rotation);
            return true;
        }

        public override void Tick(InteractionContext ctx)
        {
            if (ship == null) return;
            float tiller = ctx.Input.Move.x;
            if (steerWithLook)
                tiller = Mathf.Clamp(tiller + Mathf.Clamp(ctx.Input.Look.x * 0.35f, -1f, 1f), -1f, 1f);
            ship.SetRudder(tiller, true);
        }

        public override void End(InteractionContext ctx)
        {
            IsOccupied = false;
            if (ship != null)
                ship.SetRudder(0f, false);
            if (ctx.Agent != null)
                ctx.Agent.SetLocomotionLocked(false);
        }
    }
}
