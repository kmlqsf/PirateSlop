#if UNITY_EDITOR || PIRATESLOP_UPPER_DESTRUCTION_V2
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PirateSlop.Experimental
{
    public sealed class UpperMastPresentation
    {
        sealed class Motion
        {
            public UpperAssembly Assembly;
            public Matrix4x4[] LocalMatrices;
            public int[] Poses;
            public bool Active, ObservedCollapse;
            public float Angle, From, Target, ChangedAt, Duration;
            public float CollapseAt, NextFlex;
            public Flex[] Flexible;
            public Transform Collision;
            public MeshCollider Collider;
            public float ColliderAngle = -1f;
            public Transform Visual;
            public MeshFilter[] Filters;
            public MeshRenderer[] Renderers;
            public uint VisualRevision = uint.MaxValue;
            public float VisualAngle = -1f;
        }

        sealed class Flex
        {
            public Mesh Mesh, Source;
            public Vector3[] Rest, Draped, Normals, DrapedNormals, Work, NormalWork;
            public Vector3 WaveAxis;
            public float[] Heights;
            public float Blend = -1f;
        }

        sealed class Geometry
        {
            public Vector3[] Points, Normals;
        }
        static readonly Dictionary<Mesh, Geometry> geometry = new();
        static int instances;
        bool disposed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetGeometry() { geometry.Clear(); instances = 0; }

        static Geometry GeometryFor(Mesh mesh)
        {
            if (!geometry.TryGetValue(mesh, out var value)) geometry[mesh] = value = new Geometry { Points = mesh.vertices, Normals = mesh.normals };
            return value;
        }

        readonly UpperDestructionV2 owner;
        readonly Motion[] motions;

        public UpperMastPresentation(UpperDestructionV2 value)
        {
            instances++;
            owner = value;
            motions = new Motion[owner.Assemblies.Length];
            for (int i = 0; i < motions.Length; i++)
            {
                var assembly = owner.Assemblies[i];
                if (assembly.Kind != UpperAssemblyKind.ForeMast && assembly.Kind != UpperAssemblyKind.MizzenMast) continue;
                var motion = new Motion { Assembly = assembly, LocalMatrices = new Matrix4x4[assembly.MastVisuals.Length], Poses = new int[assembly.MastVisuals.Length], Flexible = new Flex[assembly.MastVisuals.Length] };
                motions[i] = motion;
                if (assembly.MastCollision != null)
                {
                    motion.Collision = new GameObject("FallenMastCollision_" + assembly.Key).transform;
                    motion.Collision.SetParent(owner.transform, false);
                    motion.Collider = motion.Collision.gameObject.AddComponent<MeshCollider>();
                    motion.Collider.enabled = false;
                    motion.Collider.cookingOptions &= ~MeshColliderCookingOptions.UseFastMidphase;
                    motion.Collider.sharedMesh = assembly.MastCollision;
                }
            }
        }

        void Prepare(Motion motion)
        {
            var sails = owner.GetComponent<SailSystem>();
            for (int i = 0; i < motion.Assembly.MastVisuals.Length; i++)
            {
                var part = motion.Assembly.MastVisuals[i];
                int pose = part.SailIndex >= 0 && sails != null ? Mathf.Clamp(Mathf.RoundToInt(sails.Tension(part.SailIndex) * (part.Poses.Length - 1)), 0, part.Poses.Length - 1) : 0;
                motion.Poses[i] = pose;
                motion.LocalMatrices[i] = owner.transform.worldToLocalMatrix * part.Source.localToWorldMatrix * Matrix4x4.Translate(part.Offsets[pose]);
                if (part.Flexible && part.DrapedPoses.Length > pose)
                {
                    if (motion.Flexible[i]?.Source == part.Poses[pose]) { motion.Flexible[i].Blend = -1f; motion.Flexible[i].WaveAxis = motion.LocalMatrices[i].inverse.MultiplyVector(Vector3.right); continue; }
                    if (motion.Flexible[i]?.Mesh != null) Object.Destroy(motion.Flexible[i].Mesh);
                    var mesh = Object.Instantiate(part.Poses[pose]); mesh.MarkDynamic();
                    var rest = GeometryFor(part.Poses[pose]); var draped = GeometryFor(part.DrapedPoses[pose]);
                    motion.Flexible[i] = new Flex { Mesh = mesh, Source = part.Poses[pose], Rest = rest.Points, Draped = draped.Points,
                        Normals = rest.Normals, DrapedNormals = draped.Normals, Work = new Vector3[mesh.vertexCount], NormalWork = new Vector3[mesh.vertexCount],
                        WaveAxis = motion.LocalMatrices[i].inverse.MultiplyVector(Vector3.right), Heights = new float[mesh.vertexCount] };
                    for (int vertex = 0; vertex < mesh.vertexCount; vertex++) motion.Flexible[i].Heights[vertex] = motion.LocalMatrices[i].MultiplyPoint3x4(motion.Flexible[i].Rest[vertex]).y;
                    var bounds = mesh.bounds; bounds.Encapsulate(part.DrapedPoses[pose].bounds); mesh.bounds = bounds;
                }
            }
        }

        void PrepareVisual(Motion motion)
        {
            if (motion.Visual != null) return;
            motion.Visual = new GameObject("FallenMastVisual_" + motion.Assembly.Key).transform;
            motion.Visual.SetParent(owner.transform, false);
            motion.Filters = new MeshFilter[motion.Poses.Length];
            motion.Renderers = new MeshRenderer[motion.Poses.Length];
            for (int i = 0; i < motion.Poses.Length; i++)
            {
                var node = new GameObject("MastPart_" + i);
                node.transform.SetParent(motion.Visual, false);
                motion.Filters[i] = node.AddComponent<MeshFilter>();
                var renderer = node.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = motion.Assembly.MastVisuals[i].Materials;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                motion.Renderers[i] = renderer;
            }
        }

        public void Collapse(int index)
        {
            if (index < 0 || index >= motions.Length || motions[index] is not { } motion || motion.Active) return;
            Prepare(motion);
            motion.Active = true;
            motion.ObservedCollapse = owner.Stage(index) >= 3;
            motion.Angle = motion.From = 0f;
            motion.Target = 90f;
            motion.ChangedAt = Time.time;
            motion.Duration = 1.8f;
            motion.CollapseAt = Time.time;
        }

        public void Render(bool draw = true)
        {
            for (int index = 0; index < motions.Length; index++)
            {
                var motion = motions[index];
                if (motion == null) continue;
                bool collapsed = owner.Stage(index) >= 3;
                float target = 90f * (1f - owner.MastRepairProgress(index) / (float)UpperDestructionV2.MastRepairStrikes);
                if (!motion.Active)
                {
                    if (!collapsed) continue;
                    Prepare(motion);
                    motion.Active = motion.ObservedCollapse = true;
                    motion.Angle = motion.From = motion.Target = target;
                    motion.CollapseAt = Time.time - 5f;
                }
                motion.ObservedCollapse |= collapsed;
                if (!collapsed && motion.ObservedCollapse) target = 0f;
                if (!Mathf.Approximately(motion.Target, target))
                {
                    motion.From = motion.Angle;
                    motion.Target = target;
                    motion.ChangedAt = Time.time;
                    motion.Duration = UpperDestructionV2.MastRepairStepSeconds;
                }
                float t = motion.Duration > 0f ? Mathf.Clamp01((Time.time - motion.ChangedAt) / motion.Duration) : 1f;
                motion.Angle = Mathf.Lerp(motion.From, motion.Target, Mathf.SmoothStep(0f, 1f, t));
                if (!collapsed && motion.ObservedCollapse && motion.Angle <= .01f)
                {
                    motion.Active = false;
                    if (motion.Collider != null) motion.Collider.enabled = false;
                    if (motion.Visual != null) motion.Visual.gameObject.SetActive(false);
                    continue;
                }
                var pivot = motion.Assembly.CollapsePivot;
                var rotation = Quaternion.AngleAxis(motion.Angle, owner.MastFallsRight(index) ? Vector3.back : Vector3.forward) * Quaternion.AngleAxis(motion.Angle, Vector3.up);
                float fallen = Mathf.Sin(motion.Angle * Mathf.Deg2Rad);
                var matrix = owner.transform.localToWorldMatrix * Matrix4x4.Translate(Vector3.down * motion.Assembly.CollapseDrop * fallen * fallen) * Matrix4x4.Translate(pivot) * Matrix4x4.Rotate(rotation) * Matrix4x4.Translate(-pivot);
                if (motion.Collider != null)
                {
                    if (!Mathf.Approximately(motion.ColliderAngle, motion.Angle))
                    {
                        var local = owner.transform.worldToLocalMatrix * matrix;
                        motion.Collision.SetLocalPositionAndRotation(local.MultiplyPoint3x4(Vector3.zero), rotation);
                        motion.ColliderAngle = motion.Angle;
                    }
                    motion.Collider.enabled = true;
                }
                if (!draw) continue;
                PrepareVisual(motion);
                motion.Visual.gameObject.SetActive(true);
                bool angleChanged = !Mathf.Approximately(motion.VisualAngle, motion.Angle);
                if (angleChanged)
                {
                    var local = owner.transform.worldToLocalMatrix * matrix;
                    motion.Visual.SetLocalPositionAndRotation(local.MultiplyPoint3x4(Vector3.zero), rotation);
                    motion.VisualAngle = motion.Angle;
                }
                if (!angleChanged && Time.time - motion.CollapseAt >= 3f && motion.VisualRevision == owner.Destruction.Revision) continue;
                motion.VisualRevision = owner.Destruction.Revision;
                bool flexTick = Time.time >= motion.NextFlex;
                if (flexTick) motion.NextFlex = Time.time + .04f;
                for (int partIndex = 0; partIndex < motion.Assembly.MastVisuals.Length; partIndex++)
                {
                    var part = motion.Assembly.MastVisuals[partIndex];
                    bool visible = part.RequiredAssembly < 0 || owner.Stage(part.RequiredAssembly) < 3;
                    motion.Renderers[partIndex].enabled = visible;
                    if (!visible) continue;
                    var mesh = part.Poses[motion.Poses[partIndex]];
                    if (motion.Flexible[partIndex] is { } flexible)
                    {
                        float age = Time.time - motion.CollapseAt;
                        float blend = fallen * Mathf.SmoothStep(0f, 1f, age / 2.5f);
                        float shake = age < 3f ? Mathf.Sin(age * 13f) * Mathf.Exp(-age * 1.8f) * fallen * .18f : 0f;
                        if (flexTick && (Mathf.Abs(flexible.Blend - blend) > .0001f || age < 3f))
                        {
                            for (int vertex = 0; vertex < flexible.Work.Length; vertex++)
                            {
                                var point = Vector3.Lerp(flexible.Rest[vertex], flexible.Draped[vertex], blend);
                                point += flexible.WaveAxis * (shake * Mathf.Sin(flexible.Heights[vertex] * 2f + age * 4f));
                                flexible.Work[vertex] = point;
                                flexible.NormalWork[vertex] = Vector3.Lerp(flexible.Normals[vertex], flexible.DrapedNormals[vertex], blend).normalized;
                            }
                            flexible.Mesh.SetVertices(flexible.Work); flexible.Mesh.SetNormals(flexible.NormalWork); flexible.Blend = blend;
                        }
                        mesh = flexible.Mesh;
                    }
                    var poseMatrix = motion.LocalMatrices[partIndex];
                    var display = motion.Filters[partIndex].transform;
                    display.SetLocalPositionAndRotation(poseMatrix.MultiplyPoint3x4(Vector3.zero), poseMatrix.rotation);
                    display.localScale = poseMatrix.lossyScale;
                    motion.Filters[partIndex].sharedMesh = mesh;
                }
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (var motion in motions)
                if (motion != null)
                {
                    if (motion.Collision != null) Object.Destroy(motion.Collision.gameObject);
                    if (motion.Visual != null) Object.Destroy(motion.Visual.gameObject);
                    foreach (var flexible in motion.Flexible) if (flexible?.Mesh != null) Object.Destroy(flexible.Mesh);
                }
            if (--instances <= 0) { instances = 0; geometry.Clear(); }
        }
    }
}
#endif
