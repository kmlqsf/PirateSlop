using System;
using PirateSlop.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace PirateSlop.Editor
{
    public static class AmbientFishSetup
    {
        const string Folder = "Assets/Resources/Underwater";

        [MenuItem("PirateSlop/Prepare Ambient Fish")]
        public static void Prepare()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            string[] names = { "Fish", "Pufferfish", "Swordfish" };
            string[] sources = { "Assets/Models/Fishing/FishVisual.prefab", "Assets/Models/FishingWeapons/PufferfishVisual.prefab", "Assets/Models/FishingWeapons/SwordfishVisual.prefab" };
            for (int i = 0; i < names.Length; i++) PrepareOne(names[i], sources[i], i);
            AssetDatabase.SaveAssets();
        }

        static void PrepareOne(string name, string sourcePath, int species)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (source == null) throw new InvalidOperationException("Missing fish visual: " + sourcePath);
            var filters = source.GetComponentsInChildren<MeshFilter>(true);
            if (filters.Length != 1 || !filters[0].sharedMesh.isReadable) throw new InvalidOperationException("Expected one readable fish mesh: " + sourcePath);
            var filter = filters[0];
            var renderer = filter.GetComponent<MeshRenderer>();
            var toBody = source.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            if (species == 0) toBody = Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, 90f)) * toBody;
            var normalMatrix = toBody.inverse.transpose;
            var mesh = UnityEngine.Object.Instantiate(filter.sharedMesh);
            mesh.name = "Ambient" + name + "Skin";
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            var tangents = mesh.tangents;
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = toBody.MultiplyPoint3x4(vertices[i]);
                if (normals.Length == vertices.Length) normals[i] = normalMatrix.MultiplyVector(normals[i]).normalized;
                if (tangents.Length == vertices.Length)
                {
                    var tangent = toBody.MultiplyVector(new Vector3(tangents[i].x, tangents[i].y, tangents[i].z)).normalized;
                    tangents[i] = new Vector4(tangent.x, tangent.y, tangent.z, tangents[i].w * Mathf.Sign(toBody.determinant));
                }
            }
            var bounds = new Bounds(vertices[0], Vector3.zero);
            foreach (var vertex in vertices) bounds.Encapsulate(vertex);
            var center = bounds.center;
            for (int i = 0; i < vertices.Length; i++) vertices[i] -= center;
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.tangents = tangents;
            mesh.RecalculateBounds();
            bounds = mesh.bounds;
            var preview = EditorSceneManager.NewPreviewScene();
            GameObject root = null;
            try
            {
                root = new GameObject("Ambient" + name);
                SceneManager.MoveGameObjectToScene(root, preview);
                var visual = root.AddComponent<AmbientFishVisual>();
                visual.Species = species;
                visual.Length = bounds.size.z;
                visual.BodyRadius = Mathf.Max(bounds.extents.x, bounds.extents.y);
                visual.TailBones = new Transform[5];
                var bindposes = new Matrix4x4[5];
                float pivot = Mathf.Lerp(bounds.min.z, bounds.max.z, species == 1 ? .32f : species == 2 ? .48f : .62f);
                var boneZ = new float[5];
                for (int i = 0; i < 5; i++)
                {
                    boneZ[i] = Mathf.Lerp(pivot, bounds.min.z, i / 4f);
                    var bone = new GameObject(i == 0 ? "Body" : "Tail" + i).transform;
                    bone.SetParent(i == 0 ? root.transform : visual.TailBones[i - 1], false);
                    bone.position = new Vector3(0f, 0f, boneZ[i]);
                    visual.TailBones[i] = bone;
                    bindposes[i] = bone.worldToLocalMatrix * root.transform.localToWorldMatrix;
                }
                var weights = new BoneWeight[vertices.Length];
                for (int i = 0; i < vertices.Length; i++)
                {
                    float coordinate = Mathf.Clamp01((pivot - vertices[i].z) / Mathf.Max(.001f, pivot - bounds.min.z)) * 4f;
                    int a = Mathf.Min(3, Mathf.FloorToInt(coordinate));
                    float blend = coordinate - a;
                    weights[i] = new BoneWeight { boneIndex0 = a, boneIndex1 = a + 1, weight0 = 1f - blend, weight1 = blend };
                }
                mesh.boneWeights = weights;
                mesh.bindposes = bindposes;
                string meshPath = Folder + "/Ambient" + name + "Skin.asset";
                var saved = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (saved == null) { AssetDatabase.CreateAsset(mesh, meshPath); saved = mesh; }
                else { EditorUtility.CopySerialized(mesh, saved); UnityEngine.Object.DestroyImmediate(mesh); EditorUtility.SetDirty(saved); }
                var body = root.AddComponent<SkinnedMeshRenderer>();
                body.sharedMesh = saved;
                body.sharedMaterials = renderer.sharedMaterials;
                body.bones = visual.TailBones;
                body.rootBone = visual.TailBones[0];
                body.quality = SkinQuality.Bone2;
                body.updateWhenOffscreen = false;
                body.shadowCastingMode = ShadowCastingMode.Off;
                body.receiveShadows = true;
                var skinBounds = bounds;
                skinBounds.center -= new Vector3(0f, 0f, pivot);
                skinBounds.Expand(new Vector3(bounds.size.z * .55f, .1f, .2f));
                body.localBounds = skinBounds;
                visual.Body = body;
                PrefabUtility.SaveAsPrefabAsset(root, Folder + "/Ambient" + name + ".prefab");
            }
            finally
            {
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }
    }
}
