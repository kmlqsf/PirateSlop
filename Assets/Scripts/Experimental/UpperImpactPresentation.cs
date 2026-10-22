#if UNITY_EDITOR || PIRATESLOP_UPPER_DESTRUCTION_V2
using UnityEngine;
using UnityEngine.Rendering;
using PirateSlop.Ships;

namespace PirateSlop.Experimental
{
    public sealed class UpperImpactPresentation : MonoBehaviour
    {
        ParticleSystem dust, sparks, splinters;
        readonly float[] travel = new float[12];
        readonly Vector3[] directions = new Vector3[12];
        float splinterScale;
        sealed class Burst
        {
            public Vector3 Point, Normal, Direction;
            public bool Radial, Active;
            public uint Seed;
            public int Count;
            public ShipV3CollisionBatch[] Dependencies;
        }
        readonly Burst[] bursts = new Burst[8];
        ShipV3CollisionBatch[] collisionBatches;
        Collider[] collisionShapes;
        Networking.NetworkShip ship;
        int nextBurst;

        public void Initialize(Material dustMaterial, Material sparkMaterial, Material woodMaterial)
        {
            dust = Create("UpperImpactDust", dustMaterial, false);
            sparks = Create("UpperImpactSparks", sparkMaterial, true);
            var mesh = Resources.Load<Mesh>("CombatVfx/UpperSplinter");
            if (mesh == null || woodMaterial == null) return;
            ship = GetComponent<Networking.NetworkShip>();
            collisionBatches = GetComponentsInChildren<ShipV3CollisionBatch>(true);
            collisionShapes = new Collider[collisionBatches.Length];
            for (int i = 0; i < collisionBatches.Length; i++) collisionShapes[i] = collisionBatches[i].GetComponent<Collider>();
            for (int i = 0; i < bursts.Length; i++) bursts[i] = new Burst { Dependencies = new ShipV3CollisionBatch[collisionBatches.Length] };
            splinters = Create("UpperImpactSplinters", woodMaterial, false);
            var renderer = splinters.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh; renderer.mesh = mesh;
            var main = splinters.main;
            main.maxParticles = 384; main.startRotation3D = true; main.startSize3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2f);
            main.gravityModifier = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0), new Keyframe(.18f, 0), new Keyframe(.28f, 1), new Keyframe(1, 1)));
            var drag = splinters.limitVelocityOverLifetime; drag.enabled = true;
            drag.limit = 100f; drag.dampen = 0f;
            drag.drag = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0), new Keyframe(.18f, 0), new Keyframe(.32f, 4), new Keyframe(1, 4)));
            drag.multiplyDragByParticleSize = false; drag.multiplyDragByParticleVelocity = false;
            var collision = splinters.collision; collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World; collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.quality = ParticleSystemCollisionQuality.High; collision.enableDynamicColliders = true;
            collision.collidesWith = ~LayerMask.GetMask("Ignore Raycast", "Water", "ShipDebris");
            collision.lifetimeLoss = 1f; collision.bounce = 0f; collision.colliderForce = 0f;
            collision.radiusScale = .15f; collision.maxCollisionShapes = 64; collision.sendCollisionMessages = false;
            var spin = splinters.rotationOverLifetime; spin.enabled = true; spin.separateAxes = true;
            spin.x = new ParticleSystem.MinMaxCurve(4f); spin.y = new ParticleSystem.MinMaxCurve(-6f); spin.z = new ParticleSystem.MinMaxCurve(3f);
            var size = splinters.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 1), new Keyframe(.75f, 1), new Keyframe(1, 0)));
            var bounds = mesh.bounds.size;
            splinterScale = .07f / Mathf.Max(.001f, Mathf.Max(bounds.x, Mathf.Max(bounds.y, bounds.z)));
        }

        ParticleSystem Create(string label, Material material, bool streak)
        {
            var go = new GameObject(label);
            go.transform.SetParent(transform, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.loop = false; main.playOnAwake = false; main.maxParticles = 192;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = system.emission; emission.enabled = false;
            var shape = system.shape; shape.enabled = false;
            var color = system.colorOverLifetime; color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(.8f, 0f), new GradientAlphaKey(.65f, .6f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            if (streak) { renderer.renderMode = ParticleSystemRenderMode.Stretch; renderer.lengthScale = 1.5f; renderer.velocityScale = .04f; }
            system.Play();
            return system;
        }

        public void Emit(UpperImpactShape shape, uint seed)
        {
            float range = shape.Radial ? UpperImpactShape.Radius : UpperImpactShape.Length;
            for (int i = 0; i < directions.Length; i++)
            {
                var direction = shape.SampleDirection(i, directions.Length, seed);
                directions[i] = direction;
                travel[i] = UpperCannonImpact.ClearDistance(shape.Origin, shape.Origin + direction * range);
            }
            EmitParticles(dust, shape, seed, 48, 2.5f, new Color(.68f, .51f, .31f, .9f), .28f, 2.4f);
            if (!shape.Metal && splinters != null) QueueSplinters(shape, seed + 127);
            if (shape.Metal) EmitParticles(sparks, shape, seed + 71, 16, 11f, new Color(1f, .65f, .2f, 1f), .035f, .8f);
        }

        void EmitParticles(ParticleSystem system, UpperImpactShape shape, uint seed, int count, float speed, Color color, float size, float lifetime)
        {
            if (system == null) return;
            for (int i = 0; i < count; i++)
            {
                int branch = i % directions.Length;
                bool blocked = travel[branch] < .05f;
                if (blocked) continue;
                float particleSpeed = speed * (.85f + i % 7 * .05f);
                var particle = new ParticleSystem.EmitParams
                {
                    position = shape.Origin, velocity = directions[branch] * particleSpeed,
                    startLifetime = Mathf.Min(lifetime, travel[branch] / particleSpeed), startSize = size * (1f + i % 3 * .3f),
                    startColor = color, randomSeed = seed + (uint)i + 1
                };
                system.Emit(particle, 1);
            }
        }

        void QueueSplinters(UpperImpactShape shape, uint seed)
        {
            var burst = bursts[nextBurst++ % bursts.Length];
            burst.Point = transform.InverseTransformPoint(shape.Point);
            burst.Normal = transform.InverseTransformDirection(shape.Normal);
            burst.Direction = transform.InverseTransformDirection(shape.Direction);
            burst.Radial = shape.Radial; burst.Seed = seed; burst.Count = 0; burst.Active = true;
            for (int i = 0; i < collisionBatches.Length; i++)
                if (!collisionBatches[i].ReadyForParticles && collisionShapes[i].bounds.SqrDistance(shape.Point) <= 4f)
                    burst.Dependencies[burst.Count++] = collisionBatches[i];
            TryEmit(burst);
        }

        void LateUpdate()
        {
            if (splinters == null) return;
            foreach (var burst in bursts) if (burst.Active) TryEmit(burst);
        }

        void TryEmit(Burst burst)
        {
            for (int i = 0; i < burst.Count; i++)
                if (burst.Dependencies[i] != null && burst.Dependencies[i].isActiveAndEnabled && !burst.Dependencies[i].ReadyForParticles) return;
            burst.Active = false;
            var shape = new UpperImpactShape { Point = transform.TransformPoint(burst.Point), Normal = transform.TransformDirection(burst.Normal), Direction = transform.TransformDirection(burst.Direction), Radial = burst.Radial };
            var inherited = ship != null ? ship.Motor.CannonPointVelocity(shape.Point) : Vector3.zero;
            float range = shape.Radial ? UpperImpactShape.Radius : UpperImpactShape.Length;
            for (int i = 0; i < 160; i++)
            {
                uint random = unchecked((burst.Seed + (uint)i * 747796405u) * 277803737u);
                float variation = (random & 65535) / 65535f;
                float size = splinterScale * Mathf.Lerp(.5f, 1.5f, variation);
                var particle = new ParticleSystem.EmitParams
                {
                    position = shape.Origin, velocity = inherited + shape.SampleDirection(i, 160, burst.Seed) * (range * Mathf.Lerp(.95f, 2.35f, (random >> 16) / 65535f)),
                    startLifetime = Mathf.Lerp(1.8f, 2.4f, variation),
                    startSize3D = new Vector3(size * (.65f + .4f * ((random >> 8) & 255) / 255f), size, size),
                    startColor = Color.white, randomSeed = random | 1u
                };
                splinters.Emit(particle, 1);
            }
        }

        void OnDestroy()
        {
            if (dust != null) Destroy(dust.gameObject);
            if (sparks != null) Destroy(sparks.gameObject);
            if (splinters != null) Destroy(splinters.gameObject);
        }
    }
}
#endif
