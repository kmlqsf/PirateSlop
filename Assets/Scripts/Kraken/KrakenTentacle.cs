using System;
using UnityEngine;
using PirateSlop.Networking;

namespace PirateSlop
{
    [SelectionBase]
    public sealed class KrakenTentacle : MonoBehaviour, IWeaponTarget
    {
        [SerializeField] TentacleAnimator animator;
        [SerializeField] float maxHealth = 150f;
        float currentHealth;
        NetworkShip ship;
        int index;
        bool defeated;
        double defeatedAt;
        Vector3 defeatPosition;
        Mesh variantMesh;
        World.WhirlpoolTentacleRing whirlpool;

        public TentacleAnimator Animator => animator;
        public Vector3 TipPosition => animator != null ? animator.TipPosition : transform.position;
        public bool IsAttacking => animator != null && animator.IsAttacking;
        public bool IsDefeated => defeated;
        public float CurrentHealth => currentHealth;

        void Awake()
        {
            if (animator == null) animator=GetComponent<TentacleAnimator>();
            currentHealth=maxHealth;
        }

        public void Configure(NetworkShip owner, int tentacleIndex, int seed)
        {
            ship=owner; index=tentacleIndex;
            whirlpool=GetComponentInParent<World.WhirlpoolTentacleRing>();
            var random=new System.Random(unchecked(seed+index*7919));
            float length=Mathf.Lerp(.88f,1.16f,(float)random.NextDouble());
            float width=Mathf.Lerp(.76f,1.24f,(float)random.NextDouble());
            float depth=Mathf.Lerp(.85f,1.15f,(float)random.NextDouble());
            transform.localScale=new Vector3(width*1.18f,length*2.05f,width*depth*1.18f);
            var renderer=GetComponentInChildren<SkinnedMeshRenderer>();
            if (renderer != null && renderer.sharedMesh != null && renderer.sharedMesh.isReadable)
            {
                variantMesh=Instantiate(renderer.sharedMesh);
                variantMesh.name="KrakenVariant_"+index;
                Vector3 size=variantMesh.bounds.size;
                int axis=size.y>=size.x && size.y>=size.z ? 1 : size.z>=size.x ? 2 : 0;
                var vertices=variantMesh.vertices;
                float taper=Mathf.Lerp(-.22f,.22f,(float)random.NextDouble());
                for (int v=0;v<vertices.Length;v++)
                {
                    float height=Mathf.InverseLerp(variantMesh.bounds.min[axis],variantMesh.bounds.max[axis],vertices[v][axis]);
                    float factor=1+taper*Mathf.Sin(height*Mathf.PI);
                    for(int a=0;a<3;a++) if(a!=axis) vertices[v][a]*=factor;
                }
                variantMesh.vertices=vertices;
                variantMesh.RecalculateBounds();
                variantMesh.RecalculateNormals();
                variantMesh.RecalculateTangents();
                renderer.sharedMesh=variantMesh;
                var properties=new MaterialPropertyBlock();
                float tint=Mathf.Lerp(.88f,1f,(float)random.NextDouble());
                properties.SetColor("_BaseColor",new Color(tint,Mathf.Lerp(.9f,1f,(float)random.NextDouble()),1f,1f));
                renderer.SetPropertyBlock(properties);
            }
            animator?.Configure(owner,random);
        }

        public void Attack(Vector3 targetPosition, Action onImpact, double startedAt=-1)
        {
            if (defeated || animator == null) return;
            if (!animator.IsAttacking) GameAudio.Play(SoundCue.KrakenThreat,TipPosition);
            animator.TriggerAttack(targetPosition,onImpact,startedAt);
        }

        public void ReceiveWeaponHit(float damage,GameObject attacker) => TakeDamage(damage,attacker);

        public void TakeDamage(float damage,GameObject attacker=null)
        {
            if(whirlpool!=null) { whirlpool.DamageTentacle(index,damage); return; }
            if (defeated || damage<=0 || !float.IsFinite(damage) || (ship!=null && !ship.IsServerInitialized)) return;
            currentHealth=Mathf.Max(0,currentHealth-damage);
            GameAudio.Play(SoundCue.BulletFlesh,TipPosition);
            CombatVfx.Impact(TipPosition,Vector3.up,false,true);
            if (currentHealth>0) return;
            if(ship!=null) ship.KrakenDefeatTentacle(index); else DefeatLocal();
        }

        public void ApplyRingHealth(float health,float defeatedElapsed)
        {
            currentHealth=Mathf.Clamp(health,0,maxHealth);
            if(currentHealth<=0) DefeatLocal(defeatedElapsed);
        }

        public void DefeatLocal(float elapsed=0)
        {
            if (defeated) return;
            defeated=true; currentHealth=0;
            defeatedAt=(animator!=null ? animator.Clock : Time.timeAsDouble)-elapsed;
            defeatPosition=transform.position;
            animator?.CancelAttack();
            if(animator!=null) animator.enabled=false;
            foreach(var collider in GetComponentsInChildren<Collider>()) collider.enabled=false;
            if(elapsed>=3) gameObject.SetActive(false);
        }

        void Update()
        {
            if(!defeated) return;
            double now=animator!=null ? animator.Clock : Time.timeAsDouble;
            float elapsed=(float)(now-defeatedAt);
            transform.position=defeatPosition+Vector3.down*(elapsed*12f);
            if(elapsed>=3) gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if(variantMesh==null) return;
            if(Application.isPlaying) Destroy(variantMesh); else DestroyImmediate(variantMesh);
        }
    }
}
