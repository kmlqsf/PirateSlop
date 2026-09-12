using System.Collections.Generic;
using UnityEngine;
using PirateSlop.Networking;

namespace PirateSlop
{
    public sealed class CannonCarriage : MonoBehaviour
    {
        public float ForwardPush = 1.1f, SidePush = .12f, RollDrag = 2.6f, RecoilSpeed = 2.8f;
        SimpleCannon cannon;
        BoxCollider footprint;
        Transform ship;
        Vector3 velocity;
        float spin, nextPublish;
        bool movable;
        readonly HashSet<AdvancedPlayerController> pushers = new();
        void Start()
        {
            cannon = GetComponent<SimpleCannon>();
            footprint = GetComponent<BoxCollider>();
            ship = cannon.Crate != null ? cannon.Crate.Ship.transform : null;
            movable = ship != null && Vector3.Dot(transform.up, ship.up) > .97f && Supported(transform.localPosition, transform.localRotation);
        }
        public void Push(AdvancedPlayerController player, Vector3 desired, float dt)
        {
            if (!movable || (cannon.Network != null && !cannon.Network.IsServerInitialized) || !pushers.Add(player)) return;
            if (player.IsDead || player.IsSwimming || player.IsClimbing || player.LocomotionLocked) return;
            var local = ship.InverseTransformDirection(desired);
            var forward = Vector3.ProjectOnPlane(transform.localRotation * Vector3.forward, Vector3.up).normalized;
            var right = Vector3.Cross(Vector3.up, forward);
            velocity += (forward * Vector3.Dot(local, forward) * ForwardPush + right * Vector3.Dot(local, right) * SidePush) * Mathf.Min(dt,.05f);
            velocity = Vector3.ClampMagnitude(velocity, RecoilSpeed * 1.4f);
        }
        public void Recoil()
        {
            if (!movable) return;
            var backward = Vector3.ProjectOnPlane(-ship.InverseTransformDirection(cannon.Muzzle.forward), Vector3.up).normalized;
            velocity += Quaternion.Euler(0, Random.Range(-8f,8f),0) * backward * Random.Range(RecoilSpeed*.85f, RecoilSpeed*1.15f);
            spin += Random.Range(-16f,16f);
        }
        bool Supported(Vector3 position, Quaternion rotation)
        {
            foreach(var corner in new[]{new Vector3(-.62f,0,-.72f),new Vector3(.62f,0,-.72f),new Vector3(-.62f,0,.72f),new Vector3(.62f,0,.72f)})
            {
                var point = ship.TransformPoint(position + rotation * corner);
                bool found = false;
                foreach(var hit in Physics.RaycastAll(point + ship.up * .16f, -ship.up, .26f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (!hit.transform.IsChildOf(ship) || hit.collider.GetComponentInParent<SimpleCannon>() != null || hit.collider.GetComponentInParent<Cannonball>() != null) continue;
                    if (Vector3.Dot(hit.normal,ship.up) > .95f) { found=true; break; }
                }
                if (!found) return false;
            }
            return true;
        }
        bool Free(Vector3 position, Quaternion rotation)
        {
            if (!Supported(position,rotation)) return false;
            var center = ship.TransformPoint(position + rotation * footprint.center);
            var extents = footprint.size * .5f - new Vector3(.04f,.07f,.04f);
            foreach(var hit in Physics.OverlapBox(center,extents,ship.rotation*rotation,~0,QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(transform) || hit.GetComponentInParent<AdvancedPlayerController>() != null || hit.GetComponentInParent<Cannonball>() != null) continue;
                return false;
            }
            return true;
        }
        void FixedUpdate()
        {
            pushers.Clear();
            if (!movable || (cannon.Network != null && !cannon.Network.IsServerInitialized)) return;
            float dt = Time.fixedDeltaTime;
            velocity = Vector3.ClampMagnitude(velocity,RecoilSpeed*1.4f);
            int steps = Mathf.Max(1,Mathf.CeilToInt(velocity.magnitude*dt/.06f));
            bool moved=false;
            if(velocity.sqrMagnitude<.0001f && Mathf.Abs(spin)<.05f) steps=0;
            for(int i=0;i<steps;i++)
            {
                var position=transform.localPosition + velocity*dt/steps;
                var rotation=Quaternion.AngleAxis(spin*dt/steps,Vector3.up)*transform.localRotation;
                if(!Free(position,rotation)) { velocity=Vector3.zero;spin=0;break; }
                transform.SetLocalPositionAndRotation(position,rotation); moved=true;
            }
            velocity *= Mathf.Exp(-RollDrag*dt); spin *= Mathf.Exp(-4f*dt);
            if(velocity.sqrMagnitude<.0001f) velocity=Vector3.zero;
            if(moved && cannon.Network != null && Time.time>=nextPublish)
            { nextPublish=Time.time+.05f; cannon.Network.MoveCarriage(cannon.Index,transform.localPosition,transform.localRotation); }
            else if(!moved && cannon.Network != null && nextPublish>0)
            { nextPublish=0; cannon.Network.MoveCarriage(cannon.Index,transform.localPosition,transform.localRotation); }
        }
    }
}
