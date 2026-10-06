using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PirateSlop
{
    [DefaultExecutionOrder(120)]
    [DisallowMultipleComponent]
    public sealed class ShipFreezeVfx : MonoBehaviour
    {
        [Serializable]
        public struct IceSeed
        {
            public MeshRenderer Surface;
            public ShipDamageSection Section;
            public Vector3 Point, Normal;
            public float Size;
        }
        public IceSeed[] Seeds = Array.Empty<IceSeed>();
        struct Surface
        {
            public MeshRenderer Renderer;
            public MeshFilter Filter;
            public int Priority;
        }
        readonly List<Surface> surfaces = new();
        readonly HashSet<MeshRenderer> batchedSources = new();
        readonly Matrix4x4[] crystals = new Matrix4x4[256];
        Material shell, crystal;
        Mesh crystalMesh;
        MaterialPropertyBlock properties, crystalProperties;
        float remaining;
        float appeared;
        float meltRemaining, meltAge, meltFade;
        const float MeltSeconds = .25f;
        uint impact;
        Vector3 origin;
        bool initialized;

        public static bool Eligible(MeshRenderer renderer)
        {
            if (renderer == null || renderer.shadowCastingMode == ShadowCastingMode.ShadowsOnly) return false;
            for (var at = renderer.transform; at != null && at.GetComponent<ShipController>() == null; at = at.parent)
            {
                string name = at.name.ToLowerInvariant();
                if (name.Contains("sail") || name.Contains("cloth") || name.Contains("rope") || name.Contains("netting") || name.Contains("chain") || name.Contains("lantern") || name.Contains("candle") || name.Contains("flame") || name.Contains("water")) return false;
            }
            foreach (var material in renderer.sharedMaterials)
                if (material != null && material.HasProperty("_Surface") && material.GetFloat("_Surface") > .5f) return false;
            return renderer.GetComponent<MeshFilter>() != null;
        }

        void Initialize()
        {
            if (initialized) return;
            initialized = true;
            shell = Resources.Load<Material>("VFX/ShipIceShell");
            crystal = Resources.Load<Material>("VFX/ShipIceCrystal");
            crystalMesh = Resources.Load<Mesh>("VFX/ShipIceCrystalMesh");
            properties = new MaterialPropertyBlock();
            crystalProperties = new MaterialPropertyBlock();
            foreach (var batch in GetComponentsInChildren<Ships.ShipV3RenderBatch>(true))
                foreach (var source in batch.Sources) if (source != null) batchedSources.Add(source);
            foreach (var renderer in GetComponentsInChildren<MeshRenderer>(true))
                if (Eligible(renderer) && !batchedSources.Contains(renderer))
                {
                    int priority = renderer.GetComponent<Ships.ShipV3RenderBatch>() != null ? 0 : 4;
                    for (var at=renderer.transform;at!=null && at!=transform;at=at.parent)
                    {
                        string name=at.name.ToLowerInvariant();
                        if (name.Contains("helmwheel") || name.Contains("wheelmesh")) priority=Mathf.Min(priority,1);
                        else if (name.Contains("mast")) priority=Mathf.Min(priority,2);
                        else if (name.Contains("deck")) priority=Mathf.Min(priority,3);
                    }
                    surfaces.Add(new Surface { Renderer=renderer, Filter=renderer.GetComponent<MeshFilter>(), Priority=priority });
                }
            surfaces.Sort((a,b)=>a.Priority.CompareTo(b.Priority));
        }

        public void Present(float seconds, Vector3 localPoint, uint sequence)
        {
            if (seconds <= 0f)
            {
                if (remaining > 0f)
                {
                    meltRemaining = MeltSeconds;
                    meltAge = Mathf.Max(5f - remaining, Time.time - appeared);
                    meltFade = Mathf.SmoothStep(0f, 1f, remaining / .5f);
                    remaining = 0f;
                }
                return;
            }
            meltRemaining = 0f;
            if (remaining <= 0f && seconds > 0f) appeared = Time.time - Mathf.Max(0f, 5f - seconds);
            remaining = Mathf.Clamp(seconds, 0f, 5f);
            if (remaining <= 0f || Application.isBatchMode) return;
            Initialize();
            origin = localPoint;
            if (sequence != impact)
            {
                impact = sequence;
                FreezeBurst(transform.TransformPoint(localPoint));
            }
        }

        void LateUpdate()
        {
            if ((remaining <= 0f && meltRemaining <= 0f) || shell == null || Application.isBatchMode) return;
            float age, fade;
            if (remaining > 0f)
            {
                age = Mathf.Max(5f - remaining, Time.time - appeared);
                fade = Mathf.SmoothStep(0f, 1f, remaining / .5f);
            }
            else
            {
                meltRemaining = Mathf.Max(0f, meltRemaining - Time.deltaTime);
                age = meltAge;
                fade = meltFade * Mathf.SmoothStep(0f, 1f, meltRemaining / MeltSeconds);
            }
            properties.SetMatrix("_ShipWorldToLocal", transform.worldToLocalMatrix);
            properties.SetVector("_FreezeOrigin", origin);
            properties.SetFloat("_FreezeAge", age);
            properties.SetFloat("_FreezeFade", fade);
            int draws = 0;
            foreach (var surface in surfaces)
            {
                if (draws >= 144) break;
                var renderer = surface.Renderer;
                var mesh = surface.Filter != null ? surface.Filter.sharedMesh : null;
                if (renderer == null || !renderer.enabled || renderer.forceRenderingOff || !renderer.gameObject.activeInHierarchy || mesh == null) continue;
                for (int submesh = 0; submesh < mesh.subMeshCount && draws < 144; submesh++, draws++)
                    Graphics.DrawMesh(mesh, renderer.localToWorldMatrix, shell, renderer.gameObject.layer, null, submesh, properties, ShadowCastingMode.Off, true, null, LightProbeUsage.BlendProbes);
            }
            if (crystal == null || crystalMesh == null) return;
            int count = 0;
            foreach (var seed in Seeds)
            {
                if (count >= crystals.Length) break;
                var surface = seed.Surface;
                if (surface == null || !surface.gameObject.activeInHierarchy || surface.forceRenderingOff || !surface.enabled && !batchedSources.Contains(surface)) continue;
                var section = seed.Section;
                if (section != null && (section.State == ShipSectionState.Destroyed || section.RemovedFragments != 0)) continue;
                Vector3 point = surface.transform.TransformPoint(seed.Point);
                Vector3 normal = surface.transform.TransformDirection(seed.Normal).normalized;
                float distance = Vector3.Distance(transform.InverseTransformPoint(point), origin);
                float growth = Mathf.SmoothStep(0f, 1f, (age - distance / 160f) / .12f) * fade;
                if (growth < .001f) continue;
                crystals[count++] = Matrix4x4.TRS(point + normal * .004f, Quaternion.FromToRotation(Vector3.up, normal), Vector3.one * seed.Size * growth);
            }
            crystalProperties.SetFloat("_FreezeFade", fade);
            if (count > 0)
                Graphics.DrawMeshInstanced(crystalMesh, 0, crystal, crystals, count, crystalProperties, ShadowCastingMode.Off, true, gameObject.layer, null, LightProbeUsage.Off);
        }

        void FreezeBurst(Vector3 point)
        {
            if (crystal == null || crystalMesh == null) return;
            var root = new GameObject("IceImpact");
            root.transform.position = point;
            var particles = root.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = false; main.playOnAwake = false; main.duration = .1f;
            main.maxParticles = 64; main.startLifetime = new ParticleSystem.MinMaxCurve(.35f, .8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 5f);
            main.startSize = new ParticleSystem.MinMaxCurve(.02f, .08f);
            main.gravityModifier = .65f; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
            main.startRotationY = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
            var emission = particles.emission; emission.enabled = false;
            var shape = particles.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = .25f;
            var size = particles.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(.65f, 1f), new Keyframe(1f, 0f)));
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh; renderer.mesh = crystalMesh; renderer.sharedMaterial = crystal;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            particles.Play(); particles.Emit(56);
            var mistMaterial = Resources.Load<Material>("VFX/ShipFireSmoke");
            if (mistMaterial != null)
            {
                var mistRoot = new GameObject("ColdMist"); mistRoot.transform.SetParent(root.transform,false);
                var mist = mistRoot.AddComponent<ParticleSystem>();
                mist.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var mistMain=mist.main;
                mistMain.loop=false; mistMain.playOnAwake=false; mistMain.duration=.1f; mistMain.maxParticles=18;
                mistMain.startLifetime=new ParticleSystem.MinMaxCurve(.55f,1f);
                mistMain.startSpeed=new ParticleSystem.MinMaxCurve(.3f,.9f);
                mistMain.startSize=new ParticleSystem.MinMaxCurve(.35f,.8f);
                mistMain.startColor=new Color(.62f,.78f,.82f,.22f);
                mistMain.simulationSpace=ParticleSystemSimulationSpace.World;
                var mistEmission=mist.emission; mistEmission.enabled=false;
                var mistShape=mist.shape; mistShape.shapeType=ParticleSystemShapeType.Sphere; mistShape.radius=.35f;
                var sheet=mist.textureSheetAnimation; sheet.enabled=true; sheet.numTilesX=sheet.numTilesY=2;
                sheet.animation=ParticleSystemAnimationType.WholeSheet;
                sheet.frameOverTime=new ParticleSystem.MinMaxCurve(0f);
                sheet.startFrame=new ParticleSystem.MinMaxCurve(0f,.999f);
                var velocity=mist.velocityOverLifetime; velocity.enabled=true; velocity.space=ParticleSystemSimulationSpace.World; velocity.y=.5f;
                var color=mist.colorOverLifetime; color.enabled=true;
                var gradient=new Gradient();
                gradient.SetKeys(new[]{new GradientColorKey(Color.white,0f),new GradientColorKey(Color.white,1f)},new[]{new GradientAlphaKey(0f,0f),new GradientAlphaKey(.7f,.12f),new GradientAlphaKey(0f,1f)});
                color.color=gradient;
                var mistRenderer=mist.GetComponent<ParticleSystemRenderer>(); mistRenderer.sharedMaterial=mistMaterial;
                mistRenderer.shadowCastingMode=ShadowCastingMode.Off; mistRenderer.receiveShadows=false;
                mist.Play(); mist.Emit(18);
            }
            Destroy(root, 1.2f);
        }

        public void StopPresentation() { remaining = meltRemaining = 0f; impact = 0; }
        void OnDisable() => StopPresentation();
    }
}
