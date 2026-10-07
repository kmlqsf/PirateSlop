using System;
using System.IO;
using System.Linq;
using PirateSlop.Networking;
using PirateSlop.Ships;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace PirateSlop.Editor
{
    public static class ShipSlotMachineSetup
    {
        const string ModelPath = "Assets/Models/SlotMachine/";
        const string PrefabPath = "Assets/Prefabs/Props/SlotMachine.prefab";
        const string SettingsPath = "Assets/Settings/SlotMachine/DefaultSlotMachine.asset";
        const string ShipPath = "Assets/Resources/Ships/ShipV3Test.prefab";

        public static void Configure()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            foreach (string part in new[] { "Cabinet", "Reel", "Lever" }) ConfigureModel(part);
            var settings = AssetDatabase.LoadAssetAtPath<ShipSlotMachineSettings>(SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<ShipSlotMachineSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }
            var textures = Enum.GetNames(typeof(SlotSymbol)).Select(name =>
                AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/UI/SlotMachine/Reel" + name + ".png")).ToArray();
            var materials = new Material[6];
            for (int i = 0; i < 6; i++)
            {
                string path = ModelPath + "Symbol" + i + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null) { material = new Material(Shader.Find("PirateSlop/SlotReelIcon")); AssetDatabase.CreateAsset(material, path); }
                material.shader = Shader.Find("PirateSlop/SlotReelIcon"); material.shaderKeywords = Array.Empty<string>();
                material.SetTexture("_BaseMap", textures[i]); material.SetFloat("_RemoveBlack", 0f);
                material.renderQueue = 3000;
                EditorUtility.SetDirty(material); materials[i] = material;
            }
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Networking/NetworkPlayer.prefab");
            string dividerPath = ModelPath + "ReelDividers.asset";
            var dividers = AssetDatabase.LoadAssetAtPath<Mesh>(dividerPath);
            if (dividers == null) { dividers = ReelDividers(); AssetDatabase.CreateAsset(dividers, dividerPath); }
            else
            {
                var replacement = ReelDividers(); CopyMesh(replacement, dividers);
                UnityEngine.Object.DestroyImmediate(replacement); EditorUtility.SetDirty(dividers);
            }
            string inkPath = ModelPath + "ReelInk.mat";
            var ink = AssetDatabase.LoadAssetAtPath<Material>(inkPath);
            if (ink == null) { ink = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(ink, inkPath); }
            ink.SetColor("_BaseColor", new Color(.13f, .085f, .05f)); ink.SetFloat("_Smoothness", 0f);
            EditorUtility.SetDirty(ink);
            var root = new GameObject("SlotMachine");
            try
            {
                var machine = root.AddComponent<ShipSlotMachine>();
                root.transform.localScale = Vector3.one * 1.2f;
                machine.Settings = settings; machine.PrizePrefabs = playerPrefab.GetComponent<NetworkWeapon>().DropPrefabs;
                AddModel("Cabinet", root.transform, Vector3.zero);
                machine.Reels = new Transform[3];
                for (int reel = 0; reel < 3; reel++)
                {
                    var pivot = new GameObject("Reel" + reel).transform; pivot.SetParent(root.transform, false);
                    pivot.localPosition = new Vector3((reel - 1) * .3f, 1.01f, .12f);
                    AddModel("Reel", pivot, Vector3.zero);
                    for (int symbol = 0; symbol < 6; symbol++)
                    {
                        var face = new GameObject("Symbol_" + (SlotSymbol)symbol);
                        face.transform.SetParent(pivot, false);
                        string meshPath = ModelPath + "SymbolSurface" + symbol + ".asset";
                        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                        if (mesh == null) { mesh = SymbolSurface(symbol, textures[symbol]); AssetDatabase.CreateAsset(mesh, meshPath); }
                        else
                        {
                            var replacement = SymbolSurface(symbol, textures[symbol]); CopyMesh(replacement, mesh);
                            UnityEngine.Object.DestroyImmediate(replacement); EditorUtility.SetDirty(mesh);
                        }
                        face.AddComponent<MeshFilter>().sharedMesh = mesh;
                        var renderer = face.AddComponent<MeshRenderer>(); renderer.sharedMaterial = materials[symbol];
                        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = true;
                    }
                    var lines = new GameObject("ReelDividers"); lines.transform.SetParent(pivot, false);
                    lines.AddComponent<MeshFilter>().sharedMesh = dividers;
                    var lineRenderer = lines.AddComponent<MeshRenderer>(); lineRenderer.sharedMaterial = ink;
                    lineRenderer.shadowCastingMode = ShadowCastingMode.Off;
                    machine.Reels[reel] = pivot;
                }
                machine.Lever = new GameObject("LeverPivot").transform;
                machine.Lever.SetParent(root.transform, false); machine.Lever.localPosition = new Vector3(-.635f, 1.04f, .03f);
                AddModel("Lever", machine.Lever, Vector3.zero);
                machine.FishSlot = AddTarget(machine, "FishSlot", new Vector3(0, 1.53f, .46f), new Vector3(.58f, .28f, .18f), false);
                machine.LeverGrip = AddTarget(machine, "LeverGrip", new Vector3(-.635f, 1.5f, .03f), new Vector3(.26f, .3f, .28f), true);
                machine.LeverGrip.SetParent(machine.Lever, true);
                var intake = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Fishing/FishVisual.prefab"), root.transform);
                intake.name = "FishIntakeVisual";
                intake.transform.localPosition = machine.IntakeStart;
                intake.transform.localRotation = Quaternion.Euler(0, 0, 90);
                var bounds = new Bounds(intake.transform.position, Vector3.zero);
                foreach (var renderer in intake.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(renderer.bounds);
                float fishSize = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
                intake.transform.localScale *= .36f / Mathf.Max(.001f, fishSize);
                machine.IntakeVisual = intake.transform; intake.SetActive(false);
                machine.PrizeOutlet = new GameObject("PrizeOutlet").transform;
                machine.PrizeOutlet.SetParent(root.transform, false); machine.PrizeOutlet.localPosition = new Vector3(0, .46f, .5f);
                machine.Housing = Box(root.transform, "CabinetBase", new Vector3(0, .33f, 0), new Vector3(1.17f, .66f, .86f));
                Box(root.transform, "CabinetHeader", new Vector3(0, 1.53f, 0), new Vector3(1.16f, .6f, .8f));
                Box(root.transform, "CabinetLeft", new Vector3(-.55f, .97f, 0), new Vector3(.11f, .62f, .83f));
                Box(root.transform, "CabinetRight", new Vector3(.55f, .97f, 0), new Vector3(.11f, .62f, .83f));
                Box(root.transform, "CabinetBack", new Vector3(0, .97f, -.36f), new Vector3(1.05f, .62f, .15f));
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            root = PrefabUtility.LoadPrefabContents(ShipPath);
            try
            {
                var previous = root.transform.Find("SlotMachine");
                if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
                var machine = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), root.transform);
                machine.name = "SlotMachine";
                machine.transform.localPosition = new Vector3(-3.6054f, 4.12f, -18.0709f);
                machine.transform.localRotation = Quaternion.Euler(0, 58f, 0);
                PrefabUtility.SaveAsPrefabAsset(root, ShipPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Networking/NetworkPlayer.prefab");
            try
            {
                if (root.GetComponent<ShipSlotMachinePlayer>() == null) root.AddComponent<ShipSlotMachinePlayer>();
                PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/Networking/NetworkPlayer.prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var bank = AssetDatabase.LoadAssetAtPath<GameAudioBank>("Assets/Resources/GameAudioBank.asset");
            var entries = bank.Entries.ToList();
            foreach (SoundCue cue in Enum.GetValues(typeof(SoundCue)))
                if (cue >= SoundCue.SlotFishInsert && !entries.Any(e => e != null && e.Cue == cue))
                    entries.Add(new GameAudioBank.Entry { Cue = cue, Clips = Array.Empty<AudioClip>(), Volume = .5f, Distance = 16f });
            bank.Entries = entries.ToArray(); EditorUtility.SetDirty(bank);
            var config = AssetDatabase.LoadAssetAtPath<SessionConfig>("Assets/Settings/Networking/SessionConfig.asset");
            config.ProtocolVersion = Mathf.Max(config.ProtocolVersion, 130); EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            Debug.Log("Ship slot machine configured on ShipV3Test.");
        }
        static void ConfigureModel(string name)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath + name + ".fbx");
            importer.materialImportMode = ModelImporterMaterialImportMode.None; importer.SaveAndReimport();
            string normalPath = ModelPath + "Textures/" + name + "_normal.png";
            var normalImporter = (TextureImporter)AssetImporter.GetAtPath(normalPath);
            normalImporter.textureType = TextureImporterType.NormalMap; normalImporter.maxTextureSize = 1024; normalImporter.SaveAndReimport();
            string materialPath = ModelPath + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, materialPath); }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ModelPath + "Textures/" + name + "_basecolor.jpeg"));
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath)); material.EnableKeyword("_NORMALMAP");
            string metalPath = ModelPath + "Textures/" + name + "_MetalSmooth.png";
            var metalImporter = (TextureImporter)AssetImporter.GetAtPath(metalPath);
            metalImporter.sRGBTexture = false; metalImporter.maxTextureSize = 1024; metalImporter.SaveAndReimport();
            material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(metalPath));
            material.EnableKeyword("_METALLICSPECGLOSSMAP"); material.SetFloat("_Smoothness", .65f);
            EditorUtility.SetDirty(material);
        }
        static GameObject AddModel(string name, Transform parent, Vector3 point)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath + name + ".fbx");
            var model = (GameObject)PrefabUtility.InstantiatePrefab(source, parent);
            model.name = name + "Visual"; model.transform.localPosition = point;
            foreach (var renderer in model.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(ModelPath + name + ".mat");
            return model;
        }
        static Transform AddTarget(ShipSlotMachine machine, string name, Vector3 point, Vector3 size, bool lever)
        {
            var box = Box(machine.transform, name, point, size); box.isTrigger = true;
            var target = box.gameObject.AddComponent<ShipSlotMachineTarget>(); target.Machine = machine; target.Lever = lever;
            return box.transform;
        }
        static BoxCollider Box(Transform parent, string name, Vector3 point, Vector3 size)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = point;
            var box = go.AddComponent<BoxCollider>(); box.size = size; return box;
        }
        static Rect IconBounds(Texture2D texture, bool removeBlack)
        {
            var previous = RenderTexture.active;
            var target = RenderTexture.GetTemporary(256, 256, 0, RenderTextureFormat.ARGB32);
            var readable = new Texture2D(256, 256, TextureFormat.RGBA32, false);
            try
            {
                Graphics.Blit(texture, target); RenderTexture.active = target;
                readable.ReadPixels(new Rect(0, 0, 256, 256), 0, 0); readable.Apply();
                var pixels = readable.GetPixels32();
                int left = 255, right = 0, bottom = 255, top = 0;
                for (int y = 0; y < 256; y++) for (int x = 0; x < 256; x++)
                {
                    var color = pixels[y * 256 + x];
                    if (color.a < 32 || removeBlack && Mathf.Max(color.r, Mathf.Max(color.g, color.b)) < 12) continue;
                    left = Mathf.Min(left, x); right = Mathf.Max(right, x); bottom = Mathf.Min(bottom, y); top = Mathf.Max(top, y);
                }
                if (right < left || top < bottom) return new Rect(0, 0, 1, 1);
                left = Mathf.Max(0, left - 2); right = Mathf.Min(255, right + 2); bottom = Mathf.Max(0, bottom - 2); top = Mathf.Min(255, top + 2);
                return new Rect(left / 256f, bottom / 256f, (right - left + 1) / 256f, (top - bottom + 1) / 256f);
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); UnityEngine.Object.DestroyImmediate(readable); }
        }
        static Mesh SymbolSurface(int symbol, Texture2D texture)
        {
            const int steps = 24;
            var crop = IconBounds(texture, false);
            float aspect = crop.width * texture.width / (crop.height * texture.height);
            float width = Mathf.Min(.095f, .15f * aspect), height = width / aspect;
            float halfAngle = height / (.23f * 2f);
            var vertices = new Vector3[(steps + 1) * 2]; var uv = new Vector2[vertices.Length]; var triangles = new int[steps * 6];
            var normals = new Vector3[vertices.Length];
            for (int i = 0; i <= steps; i++)
            {
                float angle = symbol * 60f * Mathf.Deg2Rad + halfAngle - i * halfAngle * 2f / steps;
                for (int side = 0; side < 2; side++)
                {
                    int index = i * 2 + side;
                    normals[index] = new Vector3(0, -Mathf.Sin(angle), Mathf.Cos(angle));
                    vertices[index] = normals[index] * .2305f + Vector3.right * (-.014f + (side == 0 ? -.5f : .5f) * width);
                    uv[index] = new Vector2(Mathf.Lerp(crop.xMin, crop.xMax, symbol == 2 ? 1 - side : side), Mathf.Lerp(crop.yMin, crop.yMax, (float)i / steps));
                }
                if (i == steps) continue;
                int t = i * 6, v = i * 2;
                triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
                triangles[t + 3] = v + 2; triangles[t + 4] = v + 1; triangles[t + 5] = v + 3;
            }
            var mesh = new Mesh { name = "SymbolSurface" + symbol, vertices = vertices, uv = uv, triangles = triangles, normals = normals };
            mesh.RecalculateBounds(); return mesh;
        }
        static Mesh ReelDividers()
        {
            var vertices = new Vector3[24]; var normals = new Vector3[24]; var triangles = new int[36];
            for (int band = 0; band < 6; band++)
            {
                for (int row = 0; row < 2; row++)
                {
                    float angle = (band * 60f + 30f) * Mathf.Deg2Rad + (row == 0 ? 1f : -1f) * .0015f / .23f;
                    for (int side = 0; side < 2; side++)
                    {
                        int v = band * 4 + row * 2 + side;
                        normals[v] = new Vector3(0, -Mathf.Sin(angle), Mathf.Cos(angle));
                        vertices[v] = normals[v] * .2305f + Vector3.right * (-.014f + (side == 0 ? -.049f : .049f));
                    }
                }
                int t = band * 6, start = band * 4;
                triangles[t] = start; triangles[t + 1] = start + 1; triangles[t + 2] = start + 2;
                triangles[t + 3] = start + 2; triangles[t + 4] = start + 1; triangles[t + 5] = start + 3;
            }
            var mesh = new Mesh { name = "ReelDividers", vertices = vertices, normals = normals, triangles = triangles };
            mesh.RecalculateBounds(); return mesh;
        }
        static void CopyMesh(Mesh source, Mesh target)
        {
            target.Clear(); target.vertices = source.vertices; target.normals = source.normals;
            target.uv = source.uv; target.triangles = source.triangles; target.RecalculateBounds();
        }
    }
}
