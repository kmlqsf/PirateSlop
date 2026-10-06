using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PirateSlop.Editor
{
    public static class ShipFreezeVfxSetup
    {
        struct Candidate
        {
            public ShipFreezeVfx.IceSeed Seed;
            public float Weight;
        }
        public static string Configure()
        {
            MakeMaterial("Assets/Resources/VFX/ShipIceShell.mat", "PirateSlop/Ship Ice", false);
            MakeMaterial("Assets/Resources/VFX/ShipIceCrystal.mat", "PirateSlop/Ship Ice", true);
            MakeMaterial("Assets/Resources/VFX/PlayerFreezeScreen.mat", "PirateSlop/Player Freeze Screen", false);
            string meshPath = "Assets/Resources/VFX/ShipIceCrystalMesh.asset";
            if (AssetDatabase.LoadAssetAtPath<Mesh>(meshPath) == null) AssetDatabase.CreateAsset(CrystalMesh(), meshPath);
            var report = new List<string>();
            foreach (string path in new[] { "Assets/Resources/Ships/ShipV3Test.prefab", "Assets/Prefabs/Networking/NetworkShip.prefab" }) report.Add(Bake(path));
            AssetDatabase.SaveAssets();
            return string.Join("\n", report);
        }
        static void MakeMaterial(string path, string shaderName, bool solid)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null) throw new InvalidOperationException("Missing ice shader: " + shaderName);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            material.shader = shader;
            if (material.HasProperty("_Solid")) material.SetFloat("_Solid", solid ? 1f : 0f);
            if (material.HasProperty("_Offset")) material.SetFloat("_Offset", solid ? 0f : .003f);
            material.enableInstancing = solid;
            EditorUtility.SetDirty(material);
        }
        public static string Bake(string path)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var effect = root.GetComponent<ShipFreezeVfx>() ?? root.AddComponent<ShipFreezeVfx>();
                var groups = new List<Candidate>[7];
                for (int i=0;i<groups.Length;i++) groups[i]=new List<Candidate>();
                var sections = new Dictionary<MeshRenderer,ShipDamageSection>();
                foreach (var section in root.GetComponentsInChildren<ShipDamageSection>(true))
                {
                    if (section.Intact != null) foreach (var renderer in section.Intact.GetComponentsInChildren<MeshRenderer>(true)) sections[renderer]=section;
                    foreach (var dependent in section.DependentRenderers) if (dependent is MeshRenderer renderer) sections[renderer]=section;
                }
                var wheels = new HashSet<MeshRenderer>();
                foreach (var helm in root.GetComponentsInChildren<HelmInteraction>(true))
                    if (helm.Wheel != null) foreach (var renderer in helm.Wheel.GetComponentsInChildren<MeshRenderer>(true)) wheels.Add(renderer);
                var random = new System.Random(763);
                foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    string name = AnimationUtility.CalculateTransformPath(renderer.transform, root.transform).ToLowerInvariant();
                    int forced = wheels.Contains(renderer) || name.Contains("helmwheel") || name.Contains("steeringwheel") || name.Contains("wheelmesh") ? 6 : name.Contains("mast") ? 5 : -1;
                    if (!ShipFreezeVfx.Eligible(renderer) || renderer.GetComponent<Ships.ShipV3RenderBatch>() != null || name.Contains("fragment")) continue;
                    var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                    if (mesh == null || !mesh.isReadable) continue;
                    var vertices = mesh.vertices;
                    var indices = mesh.triangles;
                    if (indices.Length < 3) continue;
                    for (int sample=0;sample<12;sample++)
                    {
                        int index = random.Next(indices.Length / 3)*3;
                        Vector3 a=vertices[indices[index]], b=vertices[indices[index+1]], c=vertices[indices[index+2]];
                        Vector3 wa=renderer.transform.TransformPoint(a), wb=renderer.transform.TransformPoint(b), wc=renderer.transform.TransformPoint(c);
                        Vector3 cross=Vector3.Cross(wb-wa,wc-wa);
                        if (cross.sqrMagnitude < 1e-12f) continue;
                        float u=Mathf.Sqrt((float)random.NextDouble()), v=(float)random.NextDouble();
                        Vector3 point=a*(1-u)+b*(u*(1-v))+c*(u*v);
                        Vector3 normal=renderer.transform.InverseTransformDirection(cross.normalized).normalized;
                        Vector3 local=root.transform.InverseTransformPoint(renderer.transform.TransformPoint(point));
                        Vector3 shipNormal=root.transform.InverseTransformDirection(renderer.transform.TransformDirection(normal)).normalized;
                        int group=forced;
                        if (group<0 && shipNormal.y>.65f)
                            group=name.Contains("quarterdeck") ? 4 : name.Contains("forecastle") ? 3 : name.Contains("maindeck") || name.Contains("deck") ? 2 : -1;
                        if (group<0 && Mathf.Abs(local.x)>2.2f && Mathf.Abs(shipNormal.x)>.45f && Mathf.Sign(local.x)==Mathf.Sign(shipNormal.x)) group=local.x<0f ? 0 : 1;
                        if (group<0) continue;
                        float area=cross.magnitude;
                        groups[group].Add(new Candidate { Seed=new ShipFreezeVfx.IceSeed { Surface=renderer, Section=sections.TryGetValue(renderer,out var owner) ? owner : null, Point=point, Normal=normal, Size=Mathf.Lerp(.016f,.048f,(float)random.NextDouble()) }, Weight=Mathf.Max(.0001f,area)*indices.Length/36f });
                    }
                }
                var result=new List<ShipFreezeVfx.IceSeed>(256);
                int[] quotas={64,64,40,20,20,40,8};
                var counts=new int[7];
                for (int group=0;group<groups.Length;group++)
                {
                    var candidates=groups[group];
                    for (int pick=0;pick<quotas[group] && candidates.Count>0;pick++)
                    {
                        float sum=0f; foreach (var candidate in candidates) sum+=candidate.Weight;
                        float at=(float)random.NextDouble()*sum;
                        int selected=candidates.Count-1;
                        for (int i=0;i<candidates.Count;i++) { at-=candidates[i].Weight; if (at<=0f) { selected=i; break; } }
                        result.Add(candidates[selected].Seed); candidates.RemoveAt(selected); counts[group]++;
                    }
                }
                effect.Seeds=result.ToArray();
                PrefabUtility.SaveAsPrefabAsset(root,path);
                return path + ": ice seeds=" + result.Count + ", port/starboard/main/forecastle/quarterdeck/masts/helm=" + string.Join("/",counts);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        static Mesh CrystalMesh()
        {
            var vertices=new List<Vector3>(); var triangles=new List<int>();
            for (int cluster=0;cluster<3;cluster++)
            {
                Vector3 center=cluster==0 ? Vector3.zero : new Vector3(cluster==1 ? .38f : -.3f,0f,.18f);
                Vector3 tip=center+new Vector3(cluster==1 ? .25f : -.15f,cluster==0 ? 1.2f : .7f,.08f);
                float width=cluster==0 ? .32f : .2f;
                for (int side=0;side<5;side++)
                {
                    float a=side*Mathf.PI*.4f, b=(side+1)*Mathf.PI*.4f;
                    int start=vertices.Count;
                    vertices.Add(center+new Vector3(Mathf.Cos(a)*width,0,Mathf.Sin(a)*width));
                    vertices.Add(tip);
                    vertices.Add(center+new Vector3(Mathf.Cos(b)*width,0,Mathf.Sin(b)*width));
                    triangles.Add(start); triangles.Add(start+1); triangles.Add(start+2);
                }
            }
            var mesh=new Mesh { name="ShipIceCrystalCluster" };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles,0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }
    }
}
