using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FishNet.Managing.Object;
using FishNet.Object;
using GameKit.Dependencies.Utilities;
using PirateSlop.Networking;
using PirateSlop.World;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace PirateSlop.Editor
{
    public static class KamikazeEventSetup
    {
        public const string ModelPath = "Assets/Models/Characters/Kamikaze/Kamikaze.fbx";
        public const string UnitPath = "Assets/Prefabs/Enemies/Kamikaze.prefab";
        public const string EventPath = "Assets/Resources/SeaEvents/KamikazeEvent.prefab";
        const string NavigationPath = "Assets/Settings/World/KamikazeRockRingNavigation.asset";
        const string ControllerPath = "Assets/Animations/Enemies/Kamikaze.controller";
        const string IslandPath = "Assets/Prefabs/World/SandyIslands/SingleLandingRockIsland.prefab";
        const string ExplosionPath = "Assets/Resources/CombatVfx/KamikazeExplosion.prefab";

        [MenuItem("PirateSlop/Prepare Kamikaze Rock Ring Event")]
        public static string Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before configuring the kamikaze event.");
            var catalog = AssetDatabase.LoadAssetAtPath<LootCatalog>("Assets/Settings/Loot/DefaultLoot.asset");
            if (catalog == null || catalog.ChestPrefab == null || !File.Exists(ModelPath)) throw new InvalidOperationException("Kamikaze FBX and default loot chest are required.");
            foreach (var folder in new[] { "Assets/Prefabs/Enemies", "Assets/Animations/Enemies", "Assets/Materials/Characters", "Assets/Settings/World", "Assets/Resources/SeaEvents" }) Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            ImportModel();
            var controller = Controller();
            var unit = PrepareUnit(controller, PaletteMaterial());
            var navigation = PrepareNavigation();
            var explosion = PrepareExplosion();
            var explosionSound = ImportExplosionSound();
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            var root = new GameObject("KamikazeRockRingEvent");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                var net = root.AddComponent<NetworkObject>();
                net.SetAssetPathHash(new string((EventPath + root.name).ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray()).GetStableHashU64());
                var owner = root.AddComponent<NetworkKamikazeEvent>();
                owner.Catalog = catalog; owner.UnitPrefab = unit;
                owner.Navigation = navigation.Data; owner.AgentTypeID = navigation.AgentType;
                owner.SpawnPoints = navigation.Points;
                owner.Scream = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Enemies/Kamikaze/KamikazeScream.wav");
                owner.ExplosionPrefab = explosion; owner.ExplosionSound = explosionSound;
                if (owner.Scream == null) throw new InvalidOperationException("Kamikaze scream is missing.");
                owner.ChestSocket = new GameObject("ChestSocket").transform;
                owner.ChestSocket.SetParent(root.transform, false);
                owner.ChestSocket.localPosition = new Vector3(0, navigation.Floor - ChestBottom(catalog.ChestPrefab) + .03f, 0);
                owner.TriggerDistance = 3; owner.RunSpeed = 8.24f; owner.MinimumSpawnDistance = 12;
                owner.SprintSpeedMultiplier = 1.03f; owner.LeapSeconds = .55f; owner.LeapHeight = 6f;
                owner.IslandRadius = 78; owner.DetonationDistance = 2.5f;
                owner.BlastDamage = 30; owner.BlastInnerRadius = 2.75f; owner.BlastRadius = 8;
                owner.BodyHitDamage = 45;
                PrefabUtility.SaveAsPrefabAsset(root, EventPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            }
            var prefab = AssetDatabase.LoadAssetAtPath<NetworkKamikazeEvent>(EventPath);
            var registry = AssetDatabase.LoadAssetAtPath<SinglePrefabObjects>("Assets/Settings/Networking/NetworkPrefabs.asset");
            if (registry == null) throw new InvalidOperationException("FishNet prefab registry is missing.");
            registry.AddObject(prefab.GetComponent<NetworkObject>(), true, true);
            EditorUtility.SetDirty(registry); AssetDatabase.SaveAssetIfDirty(registry);
            catalog.KamikazeEventPrefab = prefab;
            EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssetIfDirty(catalog);
            var config = AssetDatabase.LoadAssetAtPath<SessionConfig>("Assets/Settings/Networking/SessionConfig.asset");
            config.ProtocolVersion = Mathf.Max(config.ProtocolVersion, 200);
            EditorUtility.SetDirty(config); AssetDatabase.SaveAssetIfDirty(config);
            return $"Kamikaze event saved: 12 units, {navigation.Points.Length} reachable spawn candidates, trigger 3m, protocol {config.ProtocolVersion}.";
        }

        static void ImportModel()
        {
            AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.optimizeGameObjects = false;
            importer.bakeAxisConversion = true;
            importer.globalScale = 1;
            importer.importCameras = importer.importLights = false;
            importer.importBlendShapes = false;
            importer.importAnimation = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.None;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            var clips = importer.defaultClipAnimations.Where(c => c.name.IndexOf("Idle", StringComparison.OrdinalIgnoreCase) >= 0 || c.name.IndexOf("Run", StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
            if (clips.Length != 2) throw new InvalidOperationException("Expected precisely one Idle and one Run take; found: " + string.Join(", ", importer.defaultClipAnimations.Select(c => c.name)));
            foreach (var clip in clips)
            {
                clip.name = clip.name.IndexOf("Run", StringComparison.OrdinalIgnoreCase) >= 0 ? "KamikazeRun" : "KamikazeIdle";
                clip.loopTime = true; clip.loopPose = true;
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        static AudioClip ImportExplosionSound()
        {
            const string path = "Assets/Audio/Enemies/Kamikaze/KamikazeExplosion.wav";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null) throw new InvalidOperationException("Original explosion clip is missing.");
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings; importer.forceToMono = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        static GameObject PrepareExplosion()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/CombatParticles.mat");
            if (material == null) throw new InvalidOperationException("Combat particle material is missing.");
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            var root = new GameObject("KamikazeExplosion");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                ExplosionLayer(root, material, 10, .32f, 1.25f, 4f, new Color(1f, .63f, .12f), new Color(.65f, .13f, .025f), false);
                var smoke = new GameObject("Smoke"); smoke.transform.SetParent(root.transform, false);
                ExplosionLayer(smoke, material, 12, 2.3f, .7f, 2.5f, new Color(.22f, .2f, .17f), new Color(.12f, .115f, .105f), true);
                var sparks = new GameObject("Sparks"); sparks.transform.SetParent(root.transform, false);
                ExplosionLayer(sparks, material, 14, .65f, .12f, 8f, new Color(1f, .82f, .24f), new Color(.8f, .19f, .035f), false);
                return PrefabUtility.SaveAsPrefabAsset(root, ExplosionPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        static void ExplosionLayer(GameObject root, Material material, int count, float lifetime, float size, float speed, Color start, Color end, bool smoke)
        {
            var system = root.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.loop = false; main.playOnAwake = false; main.duration = .1f;
            main.maxParticles = count; main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * .8f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * .5f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * .7f, size);
            main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
            main.startColor = Color.white; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = smoke ? -.15f : .5f;
            var emission = system.emission; emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0, (short)count) });
            var shape = system.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = .35f;
            var colors = system.colorOverLifetime; colors.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(start, 0), new GradientColorKey(end, 1) }, new[] { new GradientAlphaKey(smoke ? .7f : 1, 0), new GradientAlphaKey(smoke ? .45f : .8f, .4f), new GradientAlphaKey(0, 1) });
            colors.color = new ParticleSystem.MinMaxGradient(gradient);
            var sizes = system.sizeOverLifetime; sizes.enabled = true;
            sizes.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, smoke ? .5f : 1, 1, smoke ? 3.4f : .15f));
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material; renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
        }

        static RuntimeAnimatorController Controller()
        {
            var clips = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal)).ToArray();
            var idle = clips.Single(c => c.name == "KamikazeIdle");
            var run = clips.Single(c => c.name == "KamikazeRun");
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.parameters = new[] { new AnimatorControllerParameter { name = "Speed", type = AnimatorControllerParameterType.Float } };
            var machine = controller.layers[0].stateMachine;
            var state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == "Locomotion") ?? machine.AddState("Locomotion");
            var tree = state.motion as BlendTree;
            if (tree == null)
            {
                tree = new BlendTree { name = "KamikazeLocomotion" };
                AssetDatabase.AddObjectToAsset(tree, controller);
                state.motion = tree;
            }
            tree.blendType = BlendTreeType.Simple1D; tree.blendParameter = "Speed"; tree.useAutomaticThresholds = false;
            tree.children = new[] { new ChildMotion { motion = idle, threshold = 0, timeScale = 1 }, new ChildMotion { motion = run, threshold = .2f, timeScale = 1 } };
            machine.defaultState = state;
            EditorUtility.SetDirty(tree); EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssetIfDirty(controller);
            return controller;
        }

        static Material PaletteMaterial()
        {
            const string path = "Assets/Materials/Characters/Kamikaze.mat";
            const string palette = "Assets/Models/Characters/Kamikaze/KamikazePalette.png";
            AssetDatabase.ImportAsset(palette, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(palette) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("KamikazePalette.png is required.");
            importer.sRGBTexture = true; importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Point; importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader is missing.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            material.shader = shader;
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(palette));
            material.SetColor("_BaseColor", Color.white); material.SetFloat("_Smoothness", .18f);
            material.SetFloat("_Metallic", 0); material.enableInstancing = true;
            EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }

        static KamikazeUnit PrepareUnit(RuntimeAnimatorController controller, Material material)
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Kamikaze"); root.SetActive(false);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                var model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath), root.transform, false);
                model.name = "Visual";
                var forward = Vector3.ProjectOnPlane(Bone(model.transform, "Toe_L").position - Bone(model.transform, "Foot_L").position, Vector3.up).normalized;
                if (forward.sqrMagnitude < .5f) throw new InvalidOperationException("Kamikaze toe does not define a horizontal forward direction.");
                model.transform.localRotation = Quaternion.FromToRotation(forward, Vector3.forward);
                var skins = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if (skins.Length != 1 || skins[0].sharedMesh.subMeshCount != 1) throw new InvalidOperationException("Kamikaze must use one skinned renderer and one atlas submesh.");
                foreach (var renderer in model.GetComponentsInChildren<Renderer>(true)) renderer.sharedMaterials = new[] { material };
                skins[0].updateWhenOffscreen = false;
                var animator = model.GetComponent<Animator>() ?? model.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                ConfigureAnimatedBounds(model, skins[0]);
                var agent = root.AddComponent<NavMeshAgent>(); agent.enabled = false;
                agent.agentTypeID = NavMesh.GetSettingsByIndex(0).agentTypeID;
                agent.radius = .35f; agent.height = 1.8f; agent.baseOffset = 0;
                agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
                var unit = root.AddComponent<KamikazeUnit>();
                unit.VisualRoot = model; unit.Animator = animator; unit.AnimationRunSpeed = 7;
                var hitboxes = new List<Collider>();
                var chest = Bone(model.transform, "Chest");
                var torso = HitNode(chest, root.transform, "TorsoHitbox", new Vector3(0, 1.42f, 0));
                var box = torso.AddComponent<BoxCollider>(); box.size = new Vector3(.72f, .46f, .38f);
                AttachHitbox(box, unit, false, hitboxes);
                var neck = HitNode(chest, root.transform, "NeckHitbox", new Vector3(0, 1.735f, 0));
                var neckSphere = neck.AddComponent<SphereCollider>(); neckSphere.radius = .14f;
                AttachHitbox(neckSphere, unit, false, hitboxes);
                var waist = HitNode(Bone(model.transform, "Hips"), root.transform, "WaistHitbox", new Vector3(0, 1.08f, 0));
                var waistBox = waist.AddComponent<BoxCollider>(); waistBox.size = new Vector3(.48f, .35f, .33f);
                AttachHitbox(waistBox, unit, false, hitboxes);
                foreach (string side in new[] { "L", "R" })
                {
                    Limb(model.transform, "UpperArm_" + side, "Forearm_" + side, .14f, unit, hitboxes);
                    Limb(model.transform, "Forearm_" + side, "Hand_" + side, .11f, unit, hitboxes);
                    Limb(model.transform, "Thigh_" + side, "Calf_" + side, .145f, unit, hitboxes);
                    Limb(model.transform, "Calf_" + side, "Foot_" + side, .12f, unit, hitboxes);
                    var foot = Bone(model.transform, "Foot_" + side);
                    var footPoint = root.transform.InverseTransformPoint(foot.position);
                    var boot = HitNode(foot, root.transform, "BootHitbox", new Vector3(footPoint.x, .16f, .08f));
                    var bootBox = boot.AddComponent<BoxCollider>(); bootBox.size = new Vector3(.22f, .32f, .44f);
                    AttachHitbox(bootBox, unit, false, hitboxes);
                    var socket = Bone(model.transform, "BombSocket_" + side);
                    var bomb = new GameObject("BombHitbox_" + side); bomb.transform.SetParent(socket, false);
                    var sphere = bomb.AddComponent<SphereCollider>(); sphere.radius = .22f;
                    AttachHitbox(sphere, unit, true, hitboxes);
                }
                unit.Hitboxes = hitboxes.ToArray();
                root.SetActive(true);
                PrefabUtility.SaveAsPrefabAsset(root, UnitPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            }
            return AssetDatabase.LoadAssetAtPath<KamikazeUnit>(UnitPath);
        }

        static GameObject HitNode(Transform bone, Transform root, string name, Vector3 point)
        {
            var node = new GameObject(name); node.transform.SetParent(bone, false);
            node.transform.SetPositionAndRotation(root.TransformPoint(point), root.rotation);
            return node;
        }

        static void ConfigureAnimatedBounds(GameObject model, SkinnedMeshRenderer renderer)
        {
            var nodes = model.GetComponentsInChildren<Transform>(true);
            var positions = nodes.Select(t => t.localPosition).ToArray();
            var rotations = nodes.Select(t => t.localRotation).ToArray();
            var scales = nodes.Select(t => t.localScale).ToArray();
            var mesh = new Mesh();
            Bounds bounds = default; bool first = true;
            try
            {
                var clips = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>().Where(c => c.name == "KamikazeIdle" || c.name == "KamikazeRun");
                foreach (var clip in clips)
                {
                    int count = Mathf.CeilToInt(clip.length * clip.frameRate);
                    for (int frame = 0; frame <= count; frame++)
                    {
                        clip.SampleAnimation(model, Mathf.Min(clip.length, frame / clip.frameRate));
                        renderer.BakeMesh(mesh);
                        var space = renderer.rootBone != null ? renderer.rootBone : renderer.transform;
                        foreach (var vertex in mesh.vertices)
                        {
                            var point = space.InverseTransformPoint(renderer.transform.TransformPoint(vertex));
                            if (first) { bounds = new Bounds(point, Vector3.zero); first = false; }
                            else bounds.Encapsulate(point);
                        }
                    }
                }
            }
            finally
            {
                for (int i = 0; i < nodes.Length; i++)
                {
                    nodes[i].localPosition = positions[i]; nodes[i].localRotation = rotations[i]; nodes[i].localScale = scales[i];
                }
                Object.DestroyImmediate(mesh);
            }
            if (first) throw new InvalidOperationException("Animated kamikaze bounds could not be sampled.");
            bounds.Expand(.12f); renderer.localBounds = bounds;
        }

        static Transform Bone(Transform root, string name)
        {
            return root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name || t.name == name.Replace('_', '.')) ??
                throw new InvalidOperationException("Kamikaze bone missing: " + name);
        }

        static void Limb(Transform root, string start, string end, float radius, KamikazeUnit unit, List<Collider> colliders)
        {
            var a = Bone(root, start); var b = Bone(root, end);
            var node = new GameObject(start + "Hitbox"); node.transform.SetParent(a, false);
            node.transform.SetPositionAndRotation((a.position + b.position) * .5f, Quaternion.FromToRotation(Vector3.up, b.position - a.position));
            var capsule = node.AddComponent<CapsuleCollider>();
            capsule.radius = radius; capsule.height = Mathf.Max(radius * 2f, Vector3.Distance(a.position, b.position) + radius);
            AttachHitbox(capsule, unit, false, colliders);
        }

        static void AttachHitbox(Collider collider, KamikazeUnit unit, bool bomb, List<Collider> colliders)
        {
            collider.isTrigger = true;
            var hitbox = collider.gameObject.AddComponent<KamikazeHitbox>(); hitbox.Unit = unit; hitbox.Bomb = bomb;
            var surface = collider.gameObject.AddComponent<BulletSurface>();
            surface.Kind = bomb ? BulletSurfaceKind.Metal : BulletSurfaceKind.Flesh; surface.Marks = false;
            colliders.Add(collider);
        }

        static (NavMeshData Data, Vector3[] Points, float Floor, int AgentType) PrepareNavigation()
        {
            var previous = AssetDatabase.LoadAssetAtPath<NetworkKamikazeEvent>(EventPath);
            if (previous != null && previous.Navigation != null && previous.SpawnPoints.Length >= 36)
                return (previous.Navigation, previous.SpawnPoints.ToArray(), previous.ChestSocket.localPosition.y + ChestBottom(previous.Catalog.ChestPrefab) - .03f, previous.AgentTypeID);
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            var island = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(IslandPath));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(island, scene);
            NavMeshDataInstance instance = default;
            try
            {
                var terrain = island.GetComponentInChildren<Terrain>();
                float Height(float x, float z)
                {
                    var local = terrain.transform.InverseTransformPoint(new Vector3(x, 0, z));
                    return terrain.transform.position.y + terrain.terrainData.GetInterpolatedHeight(local.x / terrain.terrainData.size.x, local.z / terrain.terrainData.size.z);
                }
                var sources = new List<NavMeshBuildSource>();
                foreach (var collider in island.GetComponentsInChildren<Collider>())
                {
                    if (!collider.enabled || collider.isTrigger || !collider.gameObject.activeInHierarchy) continue;
                    if (collider is TerrainCollider terrainCollider && terrainCollider.terrainData != null)
                        sources.Add(new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Terrain, sourceObject = terrainCollider.terrainData, transform = collider.transform.localToWorldMatrix, area = 0 });
                    else if (collider is MeshCollider meshCollider && meshCollider.sharedMesh != null)
                        sources.Add(new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Mesh, sourceObject = meshCollider.sharedMesh, transform = collider.transform.localToWorldMatrix, area = 0 });
                    else throw new InvalidOperationException("Unsupported Rock Ring collider: " + collider.GetType().Name);
                }
                if (sources.Count == 0) throw new InvalidOperationException("Rock Ring navigation has no collision sources.");
                var settings = NavMesh.GetSettingsByIndex(0);
                settings.agentRadius = .35f; settings.agentHeight = 1.8f; settings.agentClimb = .35f; settings.agentSlope = 42;
                settings.overrideVoxelSize = true; settings.voxelSize = .12f;
                settings.overrideTileSize = true; settings.tileSize = 128; settings.minRegionArea = 2;
                var data = NavMeshBuilder.BuildNavMeshData(settings, sources, new Bounds(new Vector3(0, 10.5f, 0), new Vector3(184, 20, 184)), Vector3.zero, Quaternion.identity);
                if (data == null) throw new InvalidOperationException("Rock Ring navigation bake failed.");
                var existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(NavigationPath);
                if (existing == null) { data.name = "KamikazeRockRingNavigation"; AssetDatabase.CreateAsset(data, NavigationPath); }
                else { EditorUtility.CopySerialized(data, existing); Object.DestroyImmediate(data); data = existing; }
                EditorUtility.SetDirty(data); AssetDatabase.SaveAssetIfDirty(data);
                instance = NavMesh.AddNavMeshData(data);
                var filter = new NavMeshQueryFilter { agentTypeID = settings.agentTypeID, areaMask = NavMesh.AllAreas };
                float floor = Height(0, 0);
                if (!NavMesh.SamplePosition(new Vector3(0, floor, 0), out var center, 2f, filter)) throw new InvalidOperationException("Rock Ring center is not walkable.");
                var points = new List<Vector3>(); var path = new NavMeshPath();
                foreach (float radius in new[] { 20f, 30f, 42f })
                    for (int i = 0; i < 24; i++)
                    {
                        float angle = i * Mathf.PI * 2f / 24;
                        float x = Mathf.Sin(angle) * radius, z = Mathf.Cos(angle) * radius;
                        var point = new Vector3(x, Height(x, z), z);
                        if (!NavMesh.SamplePosition(point, out var hit, 1f, filter) || !NavMesh.CalculatePath(hit.position, center.position, filter, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                        if (points.Any(p => (p - hit.position).sqrMagnitude < 1f)) continue;
                        points.Add(hit.position);
                    }
                if (points.Count < 36) throw new InvalidOperationException("Insufficient reachable Rock Ring spawn points: " + points.Count);
                return (data, points.ToArray(), floor, settings.agentTypeID);
            }
            finally
            {
                if (instance.valid) instance.Remove();
                Object.DestroyImmediate(island);
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        static float ChestBottom(NetworkLootChest prefab)
        {
            float bottom = float.PositiveInfinity;
            foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                var bounds = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var point = bounds.center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    bottom = Mathf.Min(bottom, prefab.transform.InverseTransformPoint(filter.transform.TransformPoint(point)).y);
                }
            }
            return float.IsFinite(bottom) ? bottom : 0;
        }
    }
}
