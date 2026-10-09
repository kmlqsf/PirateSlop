using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace PirateSlop.EditorTools
{
    public static class MenuScenerySetup
    {
        public static void PrepareAssets()
        {
            const string folder = "Assets/Resources/Menu";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Resources", "Menu");
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var scenery = new GameObject("MenuScenery");
                SceneManager.MoveGameObjectToScene(scenery, scene);
                AddRock(scenery.transform, "Reef_Spires_A", new Vector3(-48, 0, 55), 31);
                AddRock(scenery.transform, "Reef_Spires_B", new Vector3(50, 0, 180), -57);
                AddMonkey(scenery.transform);
                SetLayer(scenery, 30);
                PrefabUtility.SaveAsPrefabAsset(scenery, folder + "/Scenery.prefab");
                Object.DestroyImmediate(scenery);

                var source = Resources.Load<GameObject>("BRStormVolume");
                if (source == null) throw new InvalidOperationException("Missing BRStormVolume resource.");
                var storm = (GameObject)PrefabUtility.InstantiatePrefab(source, scene);
                PrefabUtility.UnpackPrefabInstance(storm, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                storm.name = "MenuStormBackdrop";
                foreach (var component in storm.GetComponentsInChildren<MonoBehaviour>(true))
                    if (component is not StormVolumeController && component is not StormWeatherController) Object.DestroyImmediate(component);
                foreach (var collider in storm.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
                var volume = storm.GetComponent<StormVolumeController>();
                volume.FeedbackStrength = 0;
                volume.InnerThickness = 450;
                volume.OuterThickness = 1350;
                volume.EdgeSoftness = 160;
                volume.CloudEdgeBreakup = 900;
                volume.DensityMultiplier = .5f;
                volume.NoiseScale = 45;
                volume.ShapeContrast = .6f;
                var weather = storm.GetComponent<StormWeatherController>();
                weather.LightningInterval = .85f;
                weather.BoltChance = 1;
                weather.LightningIntensity = .55f;
                SetLayer(storm, 30);
                PrefabUtility.SaveAsPrefabAsset(storm, folder + "/StormBackdrop.prefab");
                Object.DestroyImmediate(storm);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        static GameObject Required(string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) throw new InvalidOperationException("Missing menu source: " + path);
            return asset;
        }

        static void AddRock(Transform parent, string name, Vector3 position, float yaw)
        {
            var gallery = Resources.Load<GameObject>(World.EnvironmentTestGallery.ResourcePath);
            var source = gallery != null ? gallery.transform.Find(name) : null;
            if (source == null) throw new InvalidOperationException("Missing gallery rock: " + name);
            var branch = source.transform.Find("CoastalVisual");
            if (branch == null) throw new InvalidOperationException("Missing coastal visual: " + name);
            var rock = Object.Instantiate(branch.gameObject, parent);
            rock.name = "Menu" + name;
            rock.transform.localPosition = position;
            rock.transform.localRotation = Quaternion.Euler(0, yaw, 0) * branch.localRotation;
            rock.transform.localScale = Vector3.Scale(branch.localScale, source.localScale);
            foreach (var collider in rock.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
            foreach (var behaviour in rock.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(behaviour);
        }

        static void AddMonkey(Transform parent)
        {
            var monkey = Object.Instantiate(Required("Assets/Prefabs/Creatures/ShipMonkey.prefab"), parent);
            monkey.name = "MonkeyOnCrowNest";
            monkey.transform.localRotation = Quaternion.Euler(0, 180, 0);
            monkey.transform.localScale = Vector3.one * 1.6f;
            var animator = monkey.GetComponentInChildren<Animator>(true);
            var clip = Array.Find(animator.runtimeAnimatorController.animationClips, value => value.name == "SitPerch");
            if (clip == null) throw new InvalidOperationException("Missing monkey SitPerch animation.");
            clip.SampleAnimation(animator.gameObject, 0);
            var bones = monkey.GetComponentsInChildren<Transform>(true);
            var pelvis = Array.Find(bones, bone => bone.name == "Pelvis");
            var perch = new GameObject("CrowNestPerch").transform;
            perch.SetParent(parent, false);
            var perchPoint = new Vector2(.47f, -4.45f);
            float surface = FindPerchHeight(Required("Assets/Resources/Ships/ShipV3Menu.prefab"), perchPoint);
            perch.localPosition = new Vector3(perchPoint.x, surface, perchPoint.y);
            monkey.transform.localPosition = perch.localPosition + Vector3.up * .035f * 1.6f - parent.InverseTransformVector(pelvis.position - monkey.transform.position);
            var pose = monkey.AddComponent<MenuMonkeyPose>();
            pose.Pose = animator;
            pose.Pelvis = pelvis;
            pose.Seat = perch;
            pose.LeftShin = Array.Find(bones, bone => bone.name == "Shin.L");
            pose.RightShin = Array.Find(bones, bone => bone.name == "Shin.R");
            pose.LeftFoot = Array.Find(bones, bone => bone.name == "Foot.L");
            pose.RightFoot = Array.Find(bones, bone => bone.name == "Foot.R");
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        }

        static float FindPerchHeight(GameObject ship, Vector2 point)
        {
            float height = float.NegativeInfinity;
            foreach (var filter in ship.GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                var mesh = filter.sharedMesh;
                if (mesh == null || renderer == null || !renderer.enabled || renderer.forceRenderingOff) continue;
                bool active = true;
                for (var part = filter.transform; part != null && part != ship.transform; part = part.parent)
                    if (!part.gameObject.activeSelf) { active = false; break; }
                if (!active) continue;
                var matrix = ship.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                var vertices = mesh.vertices;
                var indices = mesh.triangles;
                for (int i = 0; i < indices.Length; i += 3)
                {
                    var a = matrix.MultiplyPoint3x4(vertices[indices[i]]);
                    var b = matrix.MultiplyPoint3x4(vertices[indices[i + 1]]);
                    var c = matrix.MultiplyPoint3x4(vertices[indices[i + 2]]);
                    float determinant = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
                    if (Mathf.Abs(determinant) < .000001f) continue;
                    float u = ((b.z - c.z) * (point.x - c.x) + (c.x - b.x) * (point.y - c.z)) / determinant;
                    float v = ((c.z - a.z) * (point.x - c.x) + (a.x - c.x) * (point.y - c.z)) / determinant;
                    if (u < 0 || v < 0 || u + v > 1) continue;
                    float y = u * a.y + v * b.y + (1 - u - v) * c.y;
                    if (y > 22 && y < 30) height = Mathf.Max(height, y);
                }
            }
            if (float.IsNegativeInfinity(height)) throw new InvalidOperationException("No visible crow nest rail at the menu perch.");
            return height;
        }

        static void SetLayer(GameObject root, int layer)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = layer;
        }
    }
}
