#if UNITY_EDITOR || PIRATESLOP_UPPER_DESTRUCTION_V2
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PirateSlop.Experimental
{
    public sealed class UpperDebrisPresentation : MonoBehaviour
    {
        struct Piece
        {
            public Mesh Mesh;
            public Material[] Materials;
            public Vector3 Origin, Velocity, Scale, Spin;
            public Quaternion Rotation;
            public float Born, Lifetime;
        }
        sealed class DrawGroup
        {
            public Mesh Mesh;
            public Material Material;
            public int Submesh, Count;
            public readonly Matrix4x4[] Matrices = new Matrix4x4[2];
        }
        readonly Dictionary<long, UpperDebrisRecipe> recipes = new();
        readonly List<Piece> pieces = new(256);
        readonly Dictionary<(Mesh, Material, int), DrawGroup> groups = new();
        readonly List<Rigidbody> physical = new();
        readonly List<DrawGroup> activeGroups = new(128);
        readonly Dictionary<Transform, UpperAttachmentRecipe> attachmentRecipes = new();
        UpperDestructionV2 owner;
        UpperImpactPresentation impacts;
        UpperMastPresentation masts;
        UpperLooseRopePresentation ropes;
        const int PhysicalLimit = 12;
        static int physicalActive;
        public static int ActivePhysicalCount => physicalActive;
        public int ActiveCosmeticCount => pieces.Count;
        int breakSoundFrame = -1;
        static readonly Unity.Profiling.ProfilerMarker marker = new("Ships.UpperDebris");

        public void Initialize(UpperDestructionV2 value)
        {
            owner = value;
            masts = owner.MastMotion;
            ropes = new UpperLooseRopePresentation(owner.transform);
            impacts = gameObject.AddComponent<UpperImpactPresentation>();
            impacts.Initialize(owner.DustMaterial, owner.SparkMaterial, owner.Destruction.Profile.SplinterMaterial);
            foreach (var recipe in owner.Debris)
            {
                recipes[((long)recipe.Section << 7) | (uint)(recipe.Fragment + 1)] = recipe;
                PrepareGroups(recipe.First, recipe.Materials); PrepareGroups(recipe.Second, recipe.Materials);
            }
            foreach (var recipe in owner.AttachmentDebris)
            {
                attachmentRecipes[recipe.Source] = recipe;
                foreach (var mesh in recipe.Poses) PrepareGroups(mesh, recipe.Materials);
            }
            for (int i = 0; i < PhysicalLimit; i++)
            {
                var go = new GameObject("UpperPhysicalDebris");
                go.layer = LayerMask.NameToLayer("ShipDebris");
                go.transform.SetParent(transform, false); go.SetActive(false);
                go.AddComponent<MeshFilter>(); go.AddComponent<MeshRenderer>(); go.AddComponent<BoxCollider>();
                var body = go.AddComponent<Rigidbody>(); body.isKinematic = true; body.mass = 5f;
                physical.Add(body);
            }
        }

        void PrepareGroups(Mesh mesh, Material[] materials)
        {
            if (mesh == null || materials == null || materials.Length == 0) return;
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                var material = materials[Mathf.Min(sub, materials.Length - 1)];
                if (material == null) continue;
                var key = (mesh, material, sub);
                if (!groups.ContainsKey(key)) groups[key] = new DrawGroup { Mesh = mesh, Material = material, Submesh = sub };
            }
        }

        public void Emit(ShipDestructionEvent impact)
        {
            if (impact.DetachedFragments == 0) return;
            var section = owner.Destruction.Section(impact.SectionId);
            if (section == null) return;
            int mast = owner.AssemblyFor(impact.SectionId);
            if (impact.UpperStage >= 3 && owner.IsMast(mast)) masts.Collapse(mast);
            for (int i = 0; i < section.RepairCount; i++)
            {
                if ((impact.DetachedFragments & (1UL << i)) == 0) continue;
                if (!recipes.TryGetValue(((long)impact.SectionId << 7) | (uint)(i + 1), out var recipe)) continue;
                EmitRecipe(recipe, impact);
            }
            if (breakSoundFrame != Time.frameCount)
            {
                breakSoundFrame = Time.frameCount;
                GameAudio.Play(impact.Reason == ShipDamageReason.SupportLost ? SoundCue.ShipCollision : SoundCue.Creak, owner.transform.TransformPoint(impact.LocalPoint));
            }
        }

        void EmitRecipe(UpperDebrisRecipe recipe, ShipDestructionEvent impact)
        {
            if (recipe.Source == null) return;
            var point = owner.transform.TransformPoint(impact.LocalPoint);
            var normal = owner.transform.TransformDirection(impact.LocalNormal).normalized;
            var direction = owner.transform.TransformDirection(impact.LocalVelocity).normalized;
            var shape = UpperImpactShape.Create(point, normal, direction, owner.transform.up, false);
            for (int n = 0; n < 2; n++)
            {
                var mesh = n == 0 ? recipe.First : recipe.Second;
                if (mesh == null) continue;
                float side = n == 0 ? -.22f : .22f;
                var axis = Vector3.Cross(shape.Direction, owner.transform.up);
                if (axis.sqrMagnitude < .01f) axis = owner.transform.right;
                var spread = shape.Radial ? Quaternion.AngleAxis((impact.Seed % 360) + n * 180f, shape.Normal) * owner.transform.right : (shape.Direction + axis.normalized * side).normalized;
                bool struck = impact.Reason == ShipDamageReason.Hit;
                if (struck) spread = shape.SampleDirection(n, 2, unchecked((uint)impact.Seed + (uint)(recipe.Section * 31 + recipe.Fragment)));
                var origin = recipe.Source.TransformPoint(n == 0 ? recipe.FirstOffset : recipe.SecondOffset);
                var velocity = impact.PointVelocity + spread * (struck ? 11f : 5f) + owner.transform.up * (struck ? 0f : 1.8f);
                float lifetime = 3.5f;
                if (impact.Reason == ShipDamageReason.Hit)
                {
                    float speed = (velocity - impact.PointVelocity).magnitude;
                    float gravity = Mathf.Max(.01f, Physics.gravity.magnitude);
                    float range = shape.Radial ? UpperImpactShape.Radius : UpperImpactShape.Length;
                    lifetime = Mathf.Min(lifetime, (Mathf.Sqrt(speed * speed + 2f * gravity * range) - speed) / gravity);
                }
                bool solid = !owner.MastAssembly(recipe.Section, out _) && impact.Reason == ShipDamageReason.SupportLost && n == 0 && TryPhysical(mesh, recipe.Materials, recipe.Source, origin, velocity);
                if (solid) continue;
                AddPiece(new Piece { Mesh = mesh, Materials = recipe.Materials, Origin = origin, Rotation = recipe.Source.rotation,
                    Scale = recipe.Source.lossyScale, Velocity = velocity, Spin = new Vector3(85f + n * 40f, 120f, 60f), Born = Time.time, Lifetime = lifetime });
            }
        }

        bool TryPhysical(Mesh mesh, Material[] materials, Transform source, Vector3 origin, Vector3 velocity)
        {
            if (physicalActive >= PhysicalLimit) return false;
            foreach (var body in physical)
            {
                if (body.gameObject.activeSelf) continue;
                body.transform.SetParent(null, true);
                body.transform.SetPositionAndRotation(origin, source.rotation); body.transform.localScale = source.lossyScale;
                body.GetComponent<MeshFilter>().sharedMesh = mesh; body.GetComponent<MeshRenderer>().sharedMaterials = materials;
                var box = body.GetComponent<BoxCollider>(); box.center = mesh.bounds.center; box.size = Vector3.Max(mesh.bounds.size, Vector3.one * .015f);
                body.gameObject.SetActive(true); body.isKinematic = false;
                physicalActive++;
                body.linearVelocity = velocity; body.angularVelocity = Vector3.one * 1.5f;
                StartCoroutine(ReturnPhysical(body));
                return true;
            }
            return false;
        }

        System.Collections.IEnumerator ReturnPhysical(Rigidbody body)
        {
            yield return new WaitForSeconds(3.5f);
            if (body == null) yield break;
            if (body.gameObject.activeSelf) physicalActive = Mathf.Max(0, physicalActive - 1);
            body.gameObject.SetActive(false); body.isKinematic = true;
            body.transform.SetParent(transform, false);
        }

        public void EmitAttachment(Transform source, int assembly)
        {
            if (assembly >= 0) return;
            if (source.name.Contains("Free_Rope") || source.name.Contains("Harpoon_Rope_Knot")) return;
            if (owner.DiceTopLost && Array.Exists(owner.GetComponent<PirateSlop.Ships.ShipV3Features>().DiceSlots, slot => source == slot.Cup.transform.parent)) return;
            var filters = source.GetComponentsInChildren<MeshFilter>(true);
            foreach (var filter in filters)
            {
                if (filter.sharedMesh == null || !filter.gameObject.activeInHierarchy) continue;
                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null) continue;
                var materials = renderer.sharedMaterials;
                if (attachmentRecipes.TryGetValue(filter.transform, out var flexible) && flexible.Flexible)
                {
                    ropes.Emit(flexible, 0);
                    continue;
                }
                PrepareGroups(filter.sharedMesh, materials);
                AddPiece(new Piece { Mesh = filter.sharedMesh, Materials = materials, Origin = filter.transform.position,
                    Rotation = filter.transform.rotation, Scale = filter.transform.lossyScale, Velocity = owner.GetComponent<Networking.NetworkShip>().Motor.CannonPointVelocity(filter.transform.position),
                    Spin = new Vector3(18f, 0f, 12f), Born = Time.time, Lifetime = 3.5f });
            }
            foreach (var renderer in source.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!renderer.gameObject.activeInHierarchy || !attachmentRecipes.TryGetValue(renderer.transform, out var recipe)) continue;
                int pose = Mathf.Clamp(Mathf.RoundToInt(owner.GetComponent<SailSystem>().Tension(recipe.SailIndex) * (recipe.Poses.Length - 1)), 0, recipe.Poses.Length - 1);
                if (recipe.Flexible) { ropes.Emit(recipe, pose); continue; }
                AddPiece(new Piece { Mesh = recipe.Poses[pose], Materials = recipe.Materials, Origin = renderer.transform.TransformPoint(recipe.Offsets[pose]), Rotation = renderer.transform.rotation,
                    Scale = renderer.transform.lossyScale, Born = Time.time, Lifetime = 3.5f });
            }
        }

        void AddPiece(Piece piece)
        {
            if (pieces.Count >= 128) pieces.RemoveAt(0);
            pieces.Add(piece);
        }

        public void EmitImpact(UpperImpactShape shape, uint seed)
        {
            impacts?.Emit(shape, seed);
        }

        void LateUpdate()
        {
            using var sample = marker.Auto();
            activeGroups.Clear();
            for (int i = pieces.Count - 1; i >= 0; i--)
            {
                var piece = pieces[i]; float age = Time.time - piece.Born;
                if (age > piece.Lifetime) { pieces.RemoveAt(i); continue; }
                var position = piece.Origin + piece.Velocity * age + Physics.gravity * (.5f * age * age);
                var rotation = piece.Rotation * Quaternion.Euler(piece.Spin * age);
                var matrix = Matrix4x4.TRS(position, rotation, piece.Scale);
                for (int sub = 0; sub < piece.Mesh.subMeshCount; sub++)
                {
                    var material = piece.Materials[Mathf.Min(sub, piece.Materials.Length - 1)];
                    if (material == null || !groups.TryGetValue((piece.Mesh, material, sub), out var group)) continue;
                    if (group.Count == 0) activeGroups.Add(group);
                    group.Matrices[group.Count++] = matrix;
                    if (group.Count == group.Matrices.Length) Flush(group);
                }
            }
            foreach (var group in activeGroups) if (group.Count > 0) Flush(group);
            ropes?.Render();
            masts?.Render();
        }

        static void Flush(DrawGroup group)
        {
            if (group.Material.enableInstancing && SystemInfo.supportsInstancing)
                Graphics.DrawMeshInstanced(group.Mesh, group.Submesh, group.Material, group.Matrices, group.Count, null, ShadowCastingMode.Off, false);
            else for (int i = 0; i < group.Count; i++) Graphics.DrawMesh(group.Mesh, group.Matrices[i], group.Material, 0, null, group.Submesh, null, false, false);
            group.Count = 0;
        }

        void OnDestroy()
        {
            StopAllCoroutines(); pieces.Clear(); groups.Clear(); recipes.Clear();
            ropes?.Dispose();
            foreach (var body in physical) if (body != null)
            {
                if (body.gameObject.activeSelf) physicalActive = Mathf.Max(0, physicalActive - 1);
                Destroy(body.gameObject);
            }
            physical.Clear();
            if (impacts != null) Destroy(impacts);
        }
    }
}
#endif
