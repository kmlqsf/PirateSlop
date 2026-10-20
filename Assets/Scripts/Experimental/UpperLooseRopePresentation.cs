#if UNITY_EDITOR || PIRATESLOP_UPPER_DESTRUCTION_V2
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PirateSlop.Experimental
{
    public sealed class UpperLooseRopePresentation
    {
        sealed class Rope
        {
            public Transform Source;
            public Mesh Mesh, Final;
            public Vector3[] Rest, Target, Work, Normals, TargetNormals, NormalWork;
            public float[] Delay;
            public Matrix4x4 Local;
            public Material[] Materials;
            public float Born, NextUpdate;
            public bool Settled;
        }
        readonly Transform ship;
        readonly List<Rope> ropes = new(12);

        public UpperLooseRopePresentation(Transform owner) => ship = owner;

        public void Emit(UpperAttachmentRecipe recipe, int pose)
        {
            if (recipe.DrapedPoses.Length <= pose) return;
            if (ropes.Count >= 12) { Object.Destroy(ropes[0].Mesh); ropes.RemoveAt(0); }
            var mesh = Object.Instantiate(recipe.Poses[pose]); mesh.MarkDynamic();
            var rest = recipe.Poses[pose].vertices;
            var local = ship.worldToLocalMatrix * recipe.Source.localToWorldMatrix * Matrix4x4.Translate(recipe.Offsets[pose]);
            var delay = new float[rest.Length];
            float low = float.PositiveInfinity, high = float.NegativeInfinity;
            foreach (var point in rest) { float y = local.MultiplyPoint3x4(point).y; low = Mathf.Min(low, y); high = Mathf.Max(high, y); }
            for (int i = 0; i < delay.Length; i++) delay[i] = Mathf.InverseLerp(low, high, local.MultiplyPoint3x4(rest[i]).y) * .35f;
            var bounds = mesh.bounds; bounds.Encapsulate(recipe.DrapedPoses[pose].bounds); mesh.bounds = bounds;
            ropes.Add(new Rope { Source = recipe.Source, Mesh = mesh, Final = recipe.DrapedPoses[pose], Rest = rest, Target = recipe.DrapedPoses[pose].vertices,
                Work = new Vector3[rest.Length], Normals = mesh.normals, TargetNormals = recipe.DrapedPoses[pose].normals, NormalWork = new Vector3[rest.Length],
                Delay = delay, Local = local, Materials = recipe.Materials, Born = Time.time });
        }

        public void Render()
        {
            for (int at = ropes.Count - 1; at >= 0; at--)
            {
                var rope = ropes[at]; float age = Time.time - rope.Born;
                if (age > 3.5f || rope.Source == null || rope.Source.gameObject.activeInHierarchy) { Object.Destroy(rope.Mesh); ropes.RemoveAt(at); continue; }
                if (!rope.Settled && Time.time >= rope.NextUpdate)
                {
                    rope.NextUpdate = Time.time + .04f;
                    for (int i = 0; i < rope.Work.Length; i++)
                    {
                        float t = Mathf.Clamp01((age - rope.Delay[i]) / .95f);
                        float blend = t * t * (3f - 2f * t);
                        rope.Work[i] = Vector3.Lerp(rope.Rest[i], rope.Target[i], blend);
                        rope.NormalWork[i] = Vector3.Lerp(rope.Normals[i], rope.TargetNormals[i], blend).normalized;
                    }
                    rope.Mesh.SetVertices(rope.Work); rope.Mesh.SetNormals(rope.NormalWork);
                    rope.Settled = age >= 1.3f;
                }
                var mesh = rope.Settled ? rope.Final : rope.Mesh;
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                    Graphics.DrawMesh(mesh, ship.localToWorldMatrix * rope.Local, rope.Materials[Mathf.Min(sub, rope.Materials.Length - 1)], 0, null, sub, null, ShadowCastingMode.Off, false);
            }
        }

        public void Dispose() { foreach (var rope in ropes) Object.Destroy(rope.Mesh); ropes.Clear(); }
    }
}
#endif
