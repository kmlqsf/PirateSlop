using System;
using System.Linq;
using PirateSlop.Networking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PirateSlop.EditorTools
{
    public static class DirectShipControlsSetup
    {
        [MenuItem("PirateSlop/Configure Direct Ship Controls")]
        public static void Configure()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            foreach (string path in new[] { "Assets/Prefabs/Cannons/CannonStation.prefab", "Assets/Prefabs/Cannons/DeployableCannon.prefab", "Assets/Prefabs/Networking/NetworkShip.prefab", "Assets/Prefabs/Networking/NetworkPlayer.prefab" })
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (root.GetComponent<SimpleCannon>() != null) ConfigureCannon(root);
                    else if (root.GetComponent<ShipController>() != null) ConfigureShip(root);
                    else ConfigurePlayer(root);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            var config = AssetDatabase.LoadAssetAtPath<SessionConfig>("Assets/Settings/Networking/SessionConfig.asset");
            config.ProtocolVersion = Mathf.Max(config.ProtocolVersion, 4);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }

        static void ConfigurePlayer(GameObject player)
        {
            if (player.GetComponent<DirectShipControls>() == null) player.AddComponent<DirectShipControls>();
        }

        static void ConfigureShip(GameObject ship)
        {
            var helm = ship.GetComponentInChildren<HelmInteraction>(true);
            var wheel = new SerializedObject(helm).FindProperty("wheelMesh").objectReferenceValue as Transform;
            var handle = wheel.GetComponent<ShipControlHandle>();
            if (handle == null) handle = wheel.gameObject.AddComponent<ShipControlHandle>();
            handle.Helm = helm;
            var box = wheel.GetComponent<BoxCollider>();
            if (box == null) box = wheel.gameObject.AddComponent<BoxCollider>();
            var bounds = wheel.GetComponent<MeshFilter>().sharedMesh.bounds;
            box.center = bounds.center; box.size = bounds.size;
            var collisionRoot = ship.transform.Find("SchoonerCollision");
            var masts = collisionRoot.GetComponentsInChildren<BoxCollider>().Where(c => c.name == "Mast").ToArray();
            foreach (var mast in masts)
            {
                string name = mast.transform.localPosition.z < 0 ? "MainMast" : "ForeMast";
                var visual = ship.transform.Find("SchoonerVisual/" + name);
                var mesh = visual.GetComponent<MeshFilter>().sharedMesh.bounds;
                Vector3 min = ship.transform.InverseTransformPoint(visual.TransformPoint(mesh.min));
                Vector3 max = ship.transform.InverseTransformPoint(visual.TransformPoint(mesh.max));
                mast.transform.localPosition = (min + max) * .5f;
                Vector3 size = max - min;
                mast.size = new Vector3(Mathf.Max(.5f, Mathf.Abs(size.x)), Mathf.Abs(size.y), Mathf.Max(.5f, Mathf.Abs(size.z)));
                mast.center = Vector3.zero;
            }
            ship.GetComponent<SailSystem>().MastControls = masts;
        }

        static void ConfigureCannon(GameObject root)
        {
            var cannon = root.GetComponent<SimpleCannon>();
            var transforms = root.GetComponentsInChildren<Transform>(true);
            var pivot = transforms.First(t => t.name == "BarrelPitch");
            var breech = transforms.First(t => t.name == "BreechKnob");
            cannon.BarrelPivot = pivot; cannon.Breech = breech;
            cannon.Muzzle.SetParent(pivot, true);
            var handle = breech.GetComponent<ShipControlHandle>();
            if (handle == null) handle = breech.gameObject.AddComponent<ShipControlHandle>();
            handle.Cannon = cannon;
            var gripCollider = breech.GetComponent<SphereCollider>();
            if (gripCollider == null) gripCollider = breech.gameObject.AddComponent<SphereCollider>();
            gripCollider.radius = .22f / Mathf.Max(.001f, breech.lossyScale.x);
            foreach (var box in root.GetComponents<BoxCollider>().Where(c => c.center.y > .75f).ToArray())
            {
                var collision = new GameObject("BarrelCollision");
                collision.transform.SetPositionAndRotation(root.transform.position, root.transform.rotation);
                collision.transform.SetParent(pivot, true);
                var moved = collision.AddComponent<BoxCollider>(); moved.center = box.center; moved.size = box.size;
                UnityEngine.Object.DestroyImmediate(box);
            }
        }
    }
}
