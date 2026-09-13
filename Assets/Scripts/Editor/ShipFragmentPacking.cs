using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PirateSlop.EditorTools
{
    public static class ShipFragmentPacking
    {
        const string Folder = "Assets/Models/Ships/MainShip/Destruction/WoodFragments";
        public static Mesh Store(Mesh source, string path, string name)
        {
            var stored = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().FirstOrDefault(m => m.name == name);
            if (stored == null && (name == "Collision00" || name.EndsWith("_Intact", StringComparison.Ordinal))) stored = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (stored != null)
            {
                EditorUtility.CopySerialized(source, stored);
                stored.name = name;
                EditorUtility.SetDirty(stored);
                UnityEngine.Object.DestroyImmediate(source);
                return stored;
            }
            source.name = name;
            if (AssetDatabase.LoadMainAssetAtPath(path) == null) AssetDatabase.CreateAsset(source, path);
            else AssetDatabase.AddObjectToAsset(source, path);
            return source;
        }
        public static string Pack(int id)
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode");
            var oldPaths = Directory.GetFiles(Folder + "/SD" + id, "*.asset").Select(p => p.Replace('\\','/')).OrderBy(p => p, StringComparer.Ordinal).ToArray();
            var replacements = new Dictionary<Mesh, Mesh>();
            foreach (var oldPath in oldPaths)
            {
                var source = AssetDatabase.LoadAssetAtPath<Mesh>(oldPath);
                if (source == null) throw new InvalidOperationException(oldPath);
                var copy = UnityEngine.Object.Instantiate(source);
                var packed = Store(copy, Folder + "/SD" + id + ".asset", Path.GetFileNameWithoutExtension(oldPath));
                replacements.Add(source, packed);
            }
            AssetDatabase.SaveAssets();
            Rebind(replacements);
            return id + ": " + replacements.Count;
        }
        public static int RebindWood()
        {
            var replacements = new Dictionary<Mesh, Mesh>();
            foreach (var dir in Directory.GetDirectories(Folder))
            {
                string packedPath = dir.Replace('\\','/') + ".asset";
                var packed = AssetDatabase.LoadAllAssetsAtPath(packedPath).OfType<Mesh>().ToDictionary(m => m.name);
                foreach (var file in Directory.GetFiles(dir, "*.asset"))
                {
                    string name = Path.GetFileNameWithoutExtension(file);
                    replacements.Add(AssetDatabase.LoadAssetAtPath<Mesh>(file.Replace('\\','/')), name == "Collision00" ? AssetDatabase.LoadAssetAtPath<Mesh>(packedPath) : packed[name]);
                }
            }
            Rebind(replacements);
            return replacements.Count;
        }
        public static string PackConnected(int offset, int count)
        {
            const string folder = "Assets/Models/Ships/MainShip/Destruction/ConnectedFragments";
            var files = Directory.GetFiles(folder, "*.asset").Where(p => Path.GetFileName(p).Contains("_"));
            var groups = files.GroupBy(p => Path.GetFileNameWithoutExtension(p).Split('_')[0]).OrderBy(g => g.Key, StringComparer.Ordinal).Skip(offset).Take(count).ToArray();
            var replacements = new Dictionary<Mesh, Mesh>();
            foreach (var group in groups)
                foreach (var file in group.OrderByDescending(p => p.EndsWith("_Intact.asset", StringComparison.Ordinal)).ThenBy(p => p, StringComparer.Ordinal))
                {
                    var source = AssetDatabase.LoadAssetAtPath<Mesh>(file.Replace('\\','/'));
                    var packed = Store(UnityEngine.Object.Instantiate(source), folder + "/SD" + group.Key + ".asset", Path.GetFileNameWithoutExtension(file));
                    replacements.Add(source, packed);
                }
            AssetDatabase.SaveAssets();
            Rebind(replacements);
            return groups.Length + " sections, " + replacements.Count + " meshes";
        }
        static void Rebind(Dictionary<Mesh, Mesh> replacements)
        {
            foreach (var prefab in new[]{"Assets/Prefabs/Networking/NetworkShip.prefab", "Assets/Prefabs/ShipDestruction/MainShipSections.prefab"})
            {
                var root = PrefabUtility.LoadPrefabContents(prefab);
                try
                {
                    foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                        if (filter.sharedMesh != null && replacements.TryGetValue(filter.sharedMesh, out var mesh)) filter.sharedMesh = mesh;
                    foreach (var collider in root.GetComponentsInChildren<MeshCollider>(true))
                        if (collider.sharedMesh != null && replacements.TryGetValue(collider.sharedMesh, out var mesh)) collider.sharedMesh = mesh;
                    PrefabUtility.SaveAsPrefabAsset(root, prefab);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
        }
    }
}
