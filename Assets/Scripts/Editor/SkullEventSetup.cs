using System.Collections.Generic;
using System.Linq;
using FishNet.Managing.Object;
using FishNet.Object;
using GameKit.Dependencies.Utilities;
using PirateSlop.Networking;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace PirateSlop.EditorTools
{
    public static class SkullEventSetup
    {
        const string ModelFolder = "Assets/Models/SeaEvents/SkullAltar/";
        const string MaterialFolder = "Assets/Materials/SeaEvents/";
        const string PrefabPath = "Assets/Prefabs/Networking/NetworkSkullEvent.prefab";
        const string TextureFolder = "Assets/JMO Assets/Cartoon FX Remaster/CFXR Assets/Graphics/";

        public static void Configure()
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(ModelFolder + "SkullAltar.fbx");
            importer.importAnimation = false;
            importer.isReadable = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.SaveAndReimport();
            var catalog = AssetDatabase.LoadAssetAtPath<LootCatalog>("Assets/Settings/Loot/DefaultLoot.asset");
            var root = new GameObject("NetworkSkullEvent");
            root.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var net = root.AddComponent<NetworkObject>();
                var altar = root.AddComponent<NetworkSkullEvent>();
                altar.Catalog = catalog;
                altar.PlatformRadius = 50f;
                var body = root.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;
                body.constraints = RigidbodyConstraints.FreezeAll;
                var rotating = new GameObject("RotatingRoot");
                rotating.transform.SetParent(root.transform, false);
                altar.RotatingRoot = rotating.transform;
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelFolder + "SkullAltar.fbx"), rotating.transform);
                foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>())
                {
                    string part = renderer.name.StartsWith("Platform") ? "Platform" : "Skull";
                    if (renderer.name.EndsWith("Collision"))
                    {
                        renderer.enabled = false;
                        var collider = renderer.gameObject.AddComponent<MeshCollider>();
                        collider.sharedMesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                    }
                    else renderer.sharedMaterial = MakeStoneMaterial(part);
                }
                if (!model.GetComponentsInChildren<MeshCollider>().Any())
                    foreach (var mesh in model.GetComponentsInChildren<MeshFilter>()) mesh.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh.sharedMesh;
                var systems = new List<ParticleSystem>();
                var lights = new List<Light>();
                var renderers = new List<Renderer>();
                var fire = rotating.AddComponent<SkullFireVfx>();
                altar.Fire = fire;
                var flame = FireMaterial("SkullFlame", "cfxr fire small anim.png", false, 2.5f);
                var glow = FireMaterial("SkullGlow", "cfxr ember blur.png", true, 3f);
                var ember = FireMaterial("SkullEmber", "cfxr ember blur.png", true, 4f);
                foreach (string name in new[] { "Mouth", "EyeLeft", "EyeRight" })
                {
                    var original = model.GetComponentsInChildren<Transform>().Single(t => t.name == name);
                    var anchor = new GameObject(name + "Fire").transform;
                    anchor.SetParent(rotating.transform, false);
                    anchor.SetPositionAndRotation(original.position, original.rotation);
                    bool mouth = name == "Mouth";
                    float size = (mouth ? 1.15f : .85f) * 3f;
                    if (mouth)
                    {
                        altar.Mouth = anchor;
                        var hit = anchor.gameObject.AddComponent<BoxCollider>();
                        hit.center = new Vector3(0f, 0f, 2.5f);
                        hit.size = new Vector3(12.6f, 6.84f, 10f);
                        anchor.gameObject.AddComponent<SkullMouthTarget>().Event = altar;
                    }
                    AddFireLayer(anchor, "Flames", flame, size, mouth ? 36f : 24f, 0, systems, renderers);
                    AddFireLayer(anchor, "Glow", glow, size, 8f, 1, systems, renderers);
                    AddFireLayer(anchor, "Embers", ember, size, mouth ? 7f : 4f, 2, systems, renderers);
                    var light = new GameObject("FireLight").AddComponent<Light>();
                    light.transform.SetParent(anchor, false);
                    light.transform.localPosition = new Vector3(0f, 0f, 1.05f);
                    light.type = LightType.Point;
                    light.color = new Color(1f, .27f, .035f);
                    light.range = size * 8f;
                    light.intensity = 4f;
                    light.shadows = LightShadows.None;
                    light.enabled = false;
                    lights.Add(light);
                }
                fire.Systems = systems.ToArray();
                fire.Lights = lights.ToArray();
                fire.FlameRenderers = renderers.ToArray();
                rotating.transform.localScale = Vector3.one * 2f;
                string hash = new string((PrefabPath + root.name).ToLowerInvariant().Where(c => c >= 'a' && c <= 'z' || c >= '0' && c <= '9').ToArray());
                net.SetAssetPathHash(hash.GetStableHashU64());
                root.hideFlags = HideFlags.None;
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var registry = AssetDatabase.LoadAssetAtPath<SinglePrefabObjects>("Assets/Settings/Networking/NetworkPrefabs.asset");
            registry.AddObject(prefab.GetComponent<NetworkObject>(), true, true);
            EditorUtility.SetDirty(registry);
            catalog.SkullEventPrefab = prefab.GetComponent<NetworkSkullEvent>();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
        }

        static Material MakeStoneMaterial(string name)
        {
            string path = MaterialFolder + name + "Altar.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            string normalPath = ModelFolder + name + "_normal.png";
            var normalImporter = (TextureImporter)AssetImporter.GetAtPath(normalPath);
            normalImporter.textureType = TextureImporterType.NormalMap;
            normalImporter.SaveAndReimport();
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ModelFolder + name + "_basecolor.jpeg"));
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
            material.EnableKeyword("_NORMALMAP");
            material.SetFloat("_BumpScale", .65f);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", .22f);
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material FireMaterial(string name, string texture, bool additive, float intensity)
        {
            string path = MaterialFolder + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("PirateSlop/SkullFire"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + texture));
            material.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_Intensity", intensity);
            material.SetFloat("_Fade", 1f);
            material.SetFloat("_SoftDistance", .72f);
            EditorUtility.SetDirty(material);
            return material;
        }

        static void AddFireLayer(Transform anchor, string name, Material material, float size, float rate, int layer, List<ParticleSystem> systems, List<Renderer> renderers)
        {
            var go = new GameObject(name);
            go.transform.SetParent(anchor, false);
            go.transform.localPosition = new Vector3(0f, 0f, .48f);
            var particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = true;
            main.prewarm = true;
            main.playOnAwake = false;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.duration = 2f;
            main.maxParticles = layer == 0 ? 48 : layer == 1 ? 8 : 16;
            main.simulationSpace = layer == 2 ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            main.startLifetime = layer == 0 ? new ParticleSystem.MinMaxCurve(.45f, .85f) : layer == 1 ? new ParticleSystem.MinMaxCurve(.3f, .5f) : new ParticleSystem.MinMaxCurve(.6f, 1.2f);
            main.startSpeed = layer == 0 ? new ParticleSystem.MinMaxCurve(1.05f, 2.4f) : layer == 1 ? new ParticleSystem.MinMaxCurve(0f) : new ParticleSystem.MinMaxCurve(1.8f, 3.6f);
            main.startSize = layer == 0 ? new ParticleSystem.MinMaxCurve(size * .55f, size * .85f) : layer == 1 ? new ParticleSystem.MinMaxCurve(size * .8f) : new ParticleSystem.MinMaxCurve(size * .02f, size * .04f);
            main.startRotation = new ParticleSystem.MinMaxCurve(-.35f, .35f);
            var emission = particles.emission;
            emission.rateOverTime = rate;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = layer == 0 ? 16f : 24f;
            shape.radius = size * (layer == 0 ? .3f : .2f);
            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(0f);
            velocity.y = new ParticleSystem.MinMaxCurve(layer == 1 ? 0f : size * .9f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f);
            var noise = particles.noise;
            noise.enabled = layer != 1;
            noise.strength = size * .14f;
            noise.frequency = .3f;
            noise.scrollSpeed = .45f;
            var sizes = particles.sizeOverLifetime;
            sizes.enabled = true;
            sizes.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, .15f), new Keyframe(.12f, 1f), new Keyframe(.6f, .75f), new Keyframe(1f, 0f)));
            var colors = particles.colorOverLifetime;
            colors.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(new Color(1f, .8f, .2f), 0f), new GradientColorKey(new Color(1f, .27f, .025f), .4f), new GradientColorKey(new Color(.35f, .025f, .005f), 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(layer == 1 ? .12f : .85f, .12f), new GradientAlphaKey(layer == 1 ? .08f : .55f, .65f), new GradientAlphaKey(0f, 1f) });
            colors.color = new ParticleSystem.MinMaxGradient(gradient);
            if (layer == 0)
            {
                var sheet = particles.textureSheetAnimation;
                sheet.enabled = true;
                sheet.numTilesX = sheet.numTilesY = 3;
                sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, .999f));
                sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, .22f);
                sheet.cycleCount = 1;
            }
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            systems.Add(particles);
            renderers.Add(renderer);
        }
    }
}
