using UnityEngine;
namespace PirateSlop
{
    public sealed class CannonShotDamage : MonoBehaviour
    {
        public bool Authoritative;
        public Transform Source;
        public Vector3 Velocity;
        public float Radius = .12f, Drag = .015f;
        bool spent;
        void FixedUpdate()
        {
            if (spent) return;
            float dt=Time.fixedDeltaTime;
            Vector3 nextVelocity=(Velocity+Physics.gravity*dt)*Mathf.Exp(-Drag*dt);
            Vector3 delta=(Velocity+nextVelocity)*(.5f*dt);
            RaycastHit nearest=default;float distance=delta.magnitude;
            foreach(var hit in Physics.SphereCastAll(transform.position,Radius,delta.normalized,distance,~0,QueryTriggerInteraction.Ignore))
            {
                if(hit.transform.IsChildOf(transform) || (Source!=null && hit.transform.IsChildOf(Source)) || hit.collider.GetComponentInParent<CannonShotDamage>()!=null) continue;
                if(hit.distance<=distance) {nearest=hit;distance=hit.distance;}
            }
            var ocean=OceanSurface.Instance;
            if(ocean!=null && (transform.position+delta).y-Radius<=ocean.Height(transform.position+delta))
            {
                float low=0,high=1;
                for(int i=0;i<8;i++)
                {
                    float t=(low+high)*.5f;var sample=transform.position+delta*t;
                    if(sample.y-Radius>ocean.Height(sample))low=t;else high=t;
                }
                if(nearest.collider==null || delta.magnitude*high<nearest.distance)
                {
                    var point=transform.position+delta*high;point.y=ocean.Height(point);
                    CombatVfx.Splash(point);GameAudio.Play(SoundCue.Splash,point);spent=true;Destroy(gameObject);return;
                }
            }
            if(nearest.collider!=null) { Impact(nearest.collider,nearest.point,nearest.normal);return; }
            transform.position+=delta;Velocity=nextVelocity;
        }

        void Impact(Collider collider,Vector3 point,Vector3 normal)
        {
            spent=true;
            if(Authoritative)
            {
                var ship=collider.GetComponentInParent<ShipController>();
                if(ship!=null) ship.ApplyCannonImpulse(point,Velocity.normalized*Mathf.Clamp(Velocity.magnitude/40f,.5f,1.5f),1.5f);
                var network=collider.GetComponentInParent<PirateSlop.Networking.NetworkShip>();
                if(network!=null) network.ImpactVfx(point,normal);else CombatVfx.Impact(point,normal,true);
                var health=collider.GetComponentInParent<CombatHealth>();
                if(health!=null && health.IsShip)
                {
                    health.GetComponent<PirateSlop.Networking.ShipRepair>()?.AddImpact(collider,point,normal,120f);
                    health.Damage(120f);
                }
            }
            Destroy(gameObject);
        }
    }
}
