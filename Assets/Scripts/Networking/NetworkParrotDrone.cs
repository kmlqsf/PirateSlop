using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed class NetworkParrotDrone : NetworkBehaviour
    {
        readonly SyncVar<Vector3> flightPosition = new();
        readonly SyncVar<Quaternion> flightRotation = new(Quaternion.identity);
        NetworkPlayer shooter, target;
        int team;
        NetworkShip homeShip;
        Vector3 launchPosition;
        float launched, nextSearch;
        bool exploded;
        readonly System.Collections.Generic.Dictionary<Transform,Quaternion> wings = new();
        public void Launch(NetworkPlayer owner)
        {
            shooter=owner; team=owner.TeamId.Value; homeShip=owner.Ship;
            launchPosition=transform.position; launched=Time.time;
            flightPosition.Value=transform.position; flightRotation.Value=transform.rotation;
        }
        public override void OnStartNetwork()
        {
            foreach(var child in GetComponentsInChildren<Transform>())
                if(child.name=="WingLeft" || child.name=="WingRight") wings[child]=child.localRotation;
        }
        bool Enemy(NetworkPlayer player)
        {
            return player!=null && player!=shooter && player.IsSpawned && !player.Motor.IsDead &&
                !(team>0 && player.TeamId.Value==team) && !(homeShip!=null && player.Ship==homeShip);
        }
        void Update()
        {
            if(!IsSpawned) return;
            if(IsServerInitialized && !exploded) Fly(Time.deltaTime);
            else if(!IsServerInitialized)
            {
                transform.position=Vector3.Lerp(transform.position,flightPosition.Value,1-Mathf.Exp(-18*Time.deltaTime));
                transform.rotation=Quaternion.Slerp(transform.rotation,flightRotation.Value,1-Mathf.Exp(-15*Time.deltaTime));
            }
            foreach(var wing in wings) wing.Key.localRotation=wing.Value*Quaternion.Euler(0,Mathf.Sin(Time.time*20)*65*(wing.Key.name=="WingLeft"?1:-1),0);
        }
        void Fly(float dt)
        {
            if(Time.time-launched>12) { Explode(); return; }
            if(!Enemy(target)) target=null;
            if(target==null && Time.time>=nextSearch)
            {
                nextSearch=Time.time+.4f;
                float distance=100*100;
                foreach(var player in FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None))
                {
                    float candidate=(player.transform.position-transform.position).sqrMagnitude;
                    if(Enemy(player) && candidate<=distance) { target=player; distance=candidate; }
                }
            }
            float age=Time.time-launched;
            Vector3 destination=age<.65f ? launchPosition+Vector3.up*2.5f+transform.forward : target!=null ? target.transform.position+Vector3.up : launchPosition+new Vector3(Mathf.Sin(age)*3,3,Mathf.Cos(age)*3);
            Vector3 delta=destination-transform.position;
            if(age>=.65f && target!=null && delta.magnitude<.65f) { Explode(); return; }
            Vector3 step=Vector3.ClampMagnitude(delta,(age<.65f || target==null ? 6 : 18)*dt);
            float nearest=step.magnitude;
            Collider obstacle=null;
            foreach(var hit in Physics.SphereCastAll(transform.position,.18f,step.normalized,nearest,~0,QueryTriggerInteraction.Ignore))
            {
                if(hit.transform.IsChildOf(transform) || (shooter!=null && hit.transform.IsChildOf(shooter.transform))) continue;
                if(hit.distance<=nearest) { nearest=hit.distance; obstacle=hit.collider; }
            }
            if(obstacle!=null) { transform.position+=step.normalized*nearest; Explode(); return; }
            transform.position+=step;
            if(step.sqrMagnitude>.0001f) transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(step),1-Mathf.Exp(-10*dt));
            flightPosition.Value=transform.position; flightRotation.Value=transform.rotation;
        }
        void Explode()
        {
            if(exploded) return;
            exploded=true;
            var damaged=new System.Collections.Generic.HashSet<CombatHealth>();
            foreach(var collider in Physics.OverlapSphere(transform.position,3,~0,QueryTriggerInteraction.Ignore))
            {
                var player=collider.GetComponentInParent<NetworkPlayer>();
                if(!Enemy(player)) continue;
                var health=player.GetComponent<CombatHealth>();
                if(health==null || !damaged.Add(health)) continue;
                Vector3 center=player.transform.position+Vector3.up;
                if(FirearmTrace.Cast(gameObject,transform.position,center,out var hit) && hit.collider.GetComponentInParent<NetworkPlayer>()!=player) continue;
                health.Damage(50,shooter!=null ? shooter.gameObject : null);
            }
            ExplosionObserversRpc(transform.position);
            ServerManager.Despawn(NetworkObject);
        }
        [ObserversRpc(RunLocally=true)]
        void ExplosionObserversRpc(Vector3 point)
        {
            CombatVfx.Fire(point,Vector3.up,true);
            CombatVfx.Impact(point,Vector3.up,true);
            GameAudio.Play(SoundCue.Cannon,point);
        }
    }
}
