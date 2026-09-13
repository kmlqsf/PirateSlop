using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using PirateSlop.Networking;
using UnityEditor;
using UnityEngine;

namespace PirateSlop.EditorTools
{
    public static class ShipDestructionSetup
    {
        const string Folder = "Assets/Models/Ships/MainShip/Destruction";
        const string ShipPath = "Assets/Prefabs/Networking/NetworkShip.prefab";
        [Serializable] sealed class Manifest { public Record[] sections; }
        [Serializable] sealed class Record { public int id; public string name, source, kind; public int[] supports; public bool preserve; public float[] center, breach; public int chunks; }
        static Vector3 Vector(float[] value) => new(value[0], value[1], value[2]);
        [MenuItem("PirateSlop/Configure Ship Destruction")]
        public static void Configure()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first");
            Directory.CreateDirectory(Folder + "/Meshes");
            Directory.CreateDirectory("Assets/Settings/ShipDestruction");
            Directory.CreateDirectory("Assets/Prefabs/ShipDestruction");
            AssetDatabase.Refresh();
            Layer("ShipDebris"); Layer("FractureChunk");
            int layer = LayerMask.NameToLayer("ShipDebris");
            for (int i = 0; i < 32; i++) Physics.IgnoreLayerCollision(layer, i, i != LayerMask.NameToLayer("WorldStatic"));
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Folder + "/sections.json"));
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/MainShipDestruction.fbx");
            if (model == null) throw new InvalidOperationException("Destruction FBX is not imported");
            var anchors = model.GetComponentsInChildren<Transform>(true);
            var origin = anchors.First(t => t.name == "Anchor_Origin").position;
            var basis = Matrix4x4.identity;
            var anchorNames = new[] { "Anchor_Right", "Anchor_Up", "Anchor_Forward" };
            for (int i = 0; i < 3; i++) { var v = anchors.First(t => t.name == anchorNames[i]).position - origin; basis.SetColumn(i, new Vector4(v.x,v.y,v.z,0)); }
            basis.SetColumn(3, new Vector4(origin.x,origin.y,origin.z,1));
            var correction = basis.inverse;
            var meshes = model.GetComponentsInChildren<MeshFilter>(true).ToDictionary(f => f.name);
            var profilePath = "Assets/Settings/ShipDestruction/MainShipDestruction.asset";
            var profile = AssetDatabase.LoadAssetAtPath<ShipDestructionProfile>(profilePath);
            bool newProfile = profile == null;
            if (newProfile) { profile = ScriptableObject.CreateInstance<ShipDestructionProfile>(); AssetDatabase.CreateAsset(profile, profilePath); }
            var definitions = new List<ShipSectionDefinition>();
            var root = PrefabUtility.LoadPrefabContents(ShipPath);
            try
            {
                var old = root.transform.Find("ShipDestructionSections");
                if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
                var container = Child(root.transform,"ShipDestructionSections");
                var sections = new List<ShipDamageSection>();
                foreach (var record in manifest.sections)
                {
                    var original = root.transform.Find("MainShipVisual/" + record.source);
                    if (original == null) throw new InvalidOperationException("Missing original " + record.source);
                    original.GetComponent<Renderer>().enabled = false;
                    foreach (var collider in original.GetComponents<Collider>()) collider.enabled = false;
                    var section = Child(container,record.name).gameObject.AddComponent<ShipDamageSection>();
                    section.SectionId = record.id;
                    var center = Vector(record.center); section.transform.localPosition = center;
                    var definition = profile.Sections.FirstOrDefault(d => d.SectionId == record.id) ?? new ShipSectionDefinition
                    {
                        SectionId = record.id, Name = record.name, Type = Enum.Parse<ShipSectionType>(record.kind), Supports = record.supports,
                        MaxHealth = record.kind == "Hull" ? 240f : record.kind == "Mast" ? 220f : 150f,
                        CanFlood = record.kind == "Hull", BreachArea = record.kind == "Hull" ? 1.5f : 0f,
                        BreachAnchor = Vector(record.breach), Repairable = false,
                        DestroyedEfficiency = record.kind == "Rudder" ? .08f : 0f,
                        DebrisMass = record.kind == "Mast" ? 240f : 80f,
                        Ammo = new[] { new ShipAmmoMultiplier { Ammo = InventoryItem.BoardingHook, Multiplier = 0f }, new ShipAmmoMultiplier { Ammo = InventoryItem.IceCannonball, Multiplier = .6f }, new ShipAmmoMultiplier { Ammo = InventoryItem.PushCannonball, Multiplier = .5f } }
                    };
                    if (newProfile)
                        definition.SailNames = record.id == 300 || record.id == 301 ? new[] { "F2_SailFront" } : record.id == 310 ? new[] { "F2_SailMid1", "F2_SailMid2" } : record.id == 311 ? new[] { "F2_SailMid2" } : record.id == 320 || record.id == 321 ? new[] { "F2_SailBack" } : Array.Empty<string>();
                    definitions.Add(definition);
                    var states = new List<GameObject>();
                    foreach (string state in new[] { "Intact", "Damaged", "Critical", "Destroyed" })
                    {
                        string key = "SD_" + record.id.ToString("000") + "_" + state;
                        var go = Visual(section.transform, state, meshes[key], correction, center, original.GetComponent<Renderer>().sharedMaterial);
                        states.Add(go); go.SetActive(state == "Intact");
                    }
                    section.Intact = states[0]; section.Damaged = states[1]; section.Critical = states[2]; section.Destroyed = states[3];
                    var intactCollider = Child(section.transform,"GameplayIntact").gameObject.AddComponent<MeshCollider>();
                    intactCollider.sharedMesh = states[0].GetComponent<MeshFilter>().sharedMesh;
                    section.GameplayColliders = new Collider[] { intactCollider };
                    section.DamageColliders = new Collider[] { intactCollider };
                    var damagedCollider = Child(section.transform,"GameplayDamaged").gameObject.AddComponent<MeshCollider>();
                    damagedCollider.sharedMesh = states[1].GetComponent<MeshFilter>().sharedMesh; damagedCollider.enabled = false;
                    var criticalCollider = Child(section.transform,"GameplayCritical").gameObject.AddComponent<MeshCollider>();
                    criticalCollider.sharedMesh = states[2].GetComponent<MeshFilter>().sharedMesh; criticalCollider.enabled = false;
                    section.DamagedColliders = new Collider[] { damagedCollider };
                    section.CriticalColliders = new Collider[] { criticalCollider };
                    var replacement = Child(section.transform,"GameplayRemnant").gameObject.AddComponent<MeshCollider>();
                    replacement.sharedMesh = states[3].GetComponent<MeshFilter>().sharedMesh; replacement.enabled = false;
                    section.ReplacementColliders = new Collider[] { replacement };
                    section.SafeColliderReplacement = !record.preserve;
                    var chunks = new List<GameObject>();
                    for (int i = 0; i < record.chunks; i++)
                    {
                        var key = "SD_" + record.id.ToString("000") + "_Chunk" + i;
                        if (!meshes.TryGetValue(key,out var filter)) continue;
                        var chunk = Visual(section.transform,"Chunk"+i,filter,correction,center,original.GetComponent<Renderer>().sharedMaterial);
                        chunk.SetActive(false); chunks.Add(chunk);
                    }
                    section.Debris = chunks.ToArray();
                    section.DamageColliders = new Collider[] { intactCollider, damagedCollider, criticalCollider, replacement };
                    if (record.kind == "Mast")
                    {
                        var control = root.transform.Find("MainShipCollision/"+record.source+"Control");
                        if (control != null)
                        {
                            section.DisabledControls = control.GetComponents<Collider>();
                            section.DamageColliders = section.DamageColliders.Concat(section.DisabledControls).ToArray();
                        }
                    }
                    sections.Add(section);
                }
                var destruction = root.GetComponent<ShipDestruction>() ?? root.AddComponent<ShipDestruction>();
                destruction.Profile = profile; destruction.Sections = sections.ToArray();
                var flooding = root.GetComponent<ShipFlooding>() ?? root.AddComponent<ShipFlooding>();
                if (root.GetComponent<ShipDestructionVisuals>() == null) root.AddComponent<ShipDestructionVisuals>();
                if (root.GetComponent<ShipDebrisPool>() == null) root.AddComponent<ShipDebrisPool>();
                var water = root.transform.Find("HoldFloodWater");
                if (water == null && profile.EnableFlooding)
                {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = "HoldFloodWater";
                    water = go.transform; water.SetParent(root.transform,false);
                    water.localPosition = new Vector3(0,-1f,0); water.localScale = new Vector3(6f,.04f,27f);
                    UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
                    var mat = Material("HoldFloodWater",new Color(.045f,.2f,.23f,1f));
                    go.GetComponent<Renderer>().sharedMaterial = mat;
                }
                if (!profile.EnableFlooding && water != null) { UnityEngine.Object.DestroyImmediate(water.gameObject); water = null; }
                flooding.WaterVisual = water; if (water != null) water.gameObject.SetActive(false);
                profile.Sections = definitions.ToArray(); profile.FallbackSectionId = 102;
                EditorUtility.SetDirty(profile);
                profile.SectionsPrefab = PrefabUtility.SaveAsPrefabAsset(container.gameObject,"Assets/Prefabs/ShipDestruction/MainShipSections.prefab");
                PrefabUtility.SaveAsPrefabAsset(root,ShipPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var config = AssetDatabase.LoadAssetAtPath<SessionConfig>("Assets/Settings/Networking/SessionConfig.asset");
            config.ProtocolVersion = Mathf.Max(config.ProtocolVersion,70); EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }
        static Transform Child(Transform parent,string name) { var go = new GameObject(name); go.transform.SetParent(parent,false); return go.transform; }
        static GameObject Visual(Transform parent,string name,MeshFilter source,Matrix4x4 correction,Vector3 pivot,Material outside)
        {
            var matrix = correction * source.transform.localToWorldMatrix;
            var mesh = UnityEngine.Object.Instantiate(source.sharedMesh); mesh.name = source.name;
            mesh.vertices = mesh.vertices.Select(v=>matrix.MultiplyPoint3x4(v)-pivot).ToArray();
            if (matrix.determinant < 0f)
                for (int s=0;s<mesh.subMeshCount;s++) { var triangles=mesh.GetTriangles(s); for(int i=0;i<triangles.Length;i+=3) (triangles[i],triangles[i+2])=(triangles[i+2],triangles[i]); mesh.SetTriangles(triangles,s); }
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            string path=Folder+"/Meshes/"+source.name+".asset";
            var stored=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(stored==null) { AssetDatabase.CreateAsset(mesh,path); stored=mesh; }
            else { EditorUtility.CopySerialized(mesh,stored); UnityEngine.Object.DestroyImmediate(mesh); }
            var go=Child(parent,name).gameObject; go.AddComponent<MeshFilter>().sharedMesh=stored;
            go.AddComponent<MeshRenderer>().sharedMaterials=source.GetComponent<Renderer>().sharedMaterials.Select(m=>m.name.Contains("FreshSplitWood")?Material("FreshSplitWood",new Color(.56f,.31f,.12f)):outside).ToArray();
            return go;
        }
        static Material Material(string name,Color color)
        {
            string path=Folder+"/"+name+".mat"; var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null) { material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.color=color;AssetDatabase.CreateAsset(material,path); }
            return material;
        }
        static void Layer(string name)
        {
            if(LayerMask.NameToLayer(name)>=0)return;
            var tags=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers=tags.FindProperty("layers");
            for(int i=9;i<32;i++) if(string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue)) { layers.GetArrayElementAtIndex(i).stringValue=name;tags.ApplyModifiedPropertiesWithoutUndo();return; }
            throw new InvalidOperationException("No layer slot for "+name);
        }
    }
}
