using System;
using UnityEngine;

namespace PirateSlop.Networking
{
    public interface IBotApproachConstraint
    {
        bool AllowsApproach(Vector3 worldPosition);
    }
    public interface IBotApproachRange
    {
        float ApproachRadius { get; }
    }

    public interface IBotShipStation
    {
        string Name { get; }
        Vector3 Position { get; }
        bool Available { get; }
        bool Busy { get; }
        bool Complete { get; }
        void Validate();
        bool Owned(NetworkPlayer player);
        bool Acquire(NetworkPlayer player);
        void Work(NetworkPlayer player, float delta);
        void Release(NetworkPlayer player);
    }

    public sealed class BotHelmStation : IBotShipStation
    {
        readonly HelmInteraction helm;
        readonly Func<float> steering;
        public BotHelmStation(HelmInteraction helm, Func<float> steering = null) { this.helm = helm; this.steering = steering; }
        bool acquired;
        public string Name => "Штурвал";
        public Vector3 Position => helm.transform.position;
        public bool Available => helm != null && helm.gameObject.activeInHierarchy && helm.StructurallyAvailable;
        public bool Busy => helm.IsControlling;
        public bool Complete => false;
        public void Validate() => helm.ValidateGrip();
        public bool Owned(NetworkPlayer player) => helm != null && helm.IsControlledBy(player.Motor);
        public bool Acquire(NetworkPlayer player)
        {
            if (!helm.TryTakeControl(player.Motor)) return false;
            acquired = true; player.Motor.SetLocomotionLocked(true); return true;
        }
        public void Work(NetworkPlayer player, float delta)
        {
            if (!Owned(player)) return;
            if (steering == null) helm.TryTakeControl(player.Motor);
            else helm.Drag(player.Motor, Mathf.Clamp(helm.DragToRudder(steering()), -180f * delta, 180f * delta), true);
        }
        public void Release(NetworkPlayer player)
        {
            if (Owned(player)) helm.ReleaseControl();
            if (acquired) player.Motor.SetLocomotionLocked(false);
            acquired = false;
        }
    }

    public sealed class BotSailStation : IBotShipStation
    {
        readonly SailSystem sails;
        readonly int index;
        readonly Func<float> target;
        readonly Func<bool> ready;
        public BotSailStation(SailSystem sails, int index, Func<float> target, Func<bool> ready = null)
        { this.sails = sails; this.index = index; this.target = target; this.ready = ready; }
        public string Name => sails != null ? sails.RopeName(index) : "Парус";
        public Vector3 Position => sails.RopeHandles[index].transform.position;
        public bool Available => sails != null && index >= 0 && index < sails.RopeCount && sails.RopeHandles[index] != null &&
            sails.RopeHandles[index].gameObject.activeInHierarchy && sails.Efficiency(index) > 0;
        public bool Busy => sails.Owner(index) != 0;
        public bool Complete => (ready == null || ready()) && Mathf.Abs(sails.Tension(index) - Mathf.Clamp01(target())) < .025f;
        public void Validate() => sails.ValidateGrips();
        public bool Owned(NetworkPlayer player) => sails != null && sails.Owner(index) == player.ParticipantId.Value;
        public bool Acquire(NetworkPlayer player) { sails.Drag(index, player.Motor, 0, true); return Owned(player); }
        public void Work(NetworkPlayer player, float delta)
        {
            if (Owned(player)) sails.Drag(index, player.Motor,
                ready != null && !ready() && target() >= sails.Tension(index) ? 0f :
                    Mathf.Clamp(target() - sails.Tension(index), -delta, delta), true);
        }
        public void Release(NetworkPlayer player) { if (Owned(player)) sails.Drag(index, player.Motor, 0, false); }
    }
}
