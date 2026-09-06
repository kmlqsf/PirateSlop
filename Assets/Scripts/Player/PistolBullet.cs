using UnityEngine;
namespace PirateSlop
{
    public sealed class PistolBullet : MonoBehaviour
    {
        GameObject shooter;
        Vector3 position, velocity, visualOffset;
        bool authoritative;
        float age, distance;
        public void Initialize(GameObject owner, Vector3 start, Vector3 speed, Vector3 offset, bool authority)
        { shooter=owner; position=start; velocity=speed; visualOffset=offset; authoritative=authority; transform.position=start+offset; }
        void Update()
        {
            float remaining = Mathf.Min(Time.deltaTime,.2f);
            while(remaining > 0)
            {
                float dt=Mathf.Min(remaining,1f/120); remaining-=dt;
                Vector3 step=velocity*dt+Vector3.down*(3f*dt*dt);
                float length=step.magnitude; RaycastHit nearest=default; float closest=length;
                foreach(var hit in Physics.SphereCastAll(position,.08f,step.normalized,length,~0,QueryTriggerInteraction.Ignore))
                    if((shooter==null || !hit.transform.IsChildOf(shooter.transform)) && hit.distance<=closest) { nearest=hit; closest=hit.distance; }
                if(nearest.collider!=null)
                {
                    var health = nearest.collider.GetComponentInParent<CombatHealth>();
                    if(authoritative && shooter!=null && health!=null)
                        health.ReceivePistolHit(distance+closest,nearest.point,shooter);
                    else if(authoritative && shooter!=null)
                        foreach(var component in nearest.collider.GetComponentsInParent<MonoBehaviour>())
                            if(component is IWeaponTarget target) { target.ReceiveWeaponHit(Mathf.Lerp(45,25,Mathf.InverseLerp(15,35,distance+closest)),shooter); break; }
                    Destroy(gameObject); return;
                }
                position+=step; velocity+=Vector3.down*(6f*dt); distance+=length; age+=dt;
            }
            transform.position=position+visualOffset*Mathf.Max(0,1-age/.08f);
            if(age>=3f || distance>=100f)Destroy(gameObject);
        }
    }
}
