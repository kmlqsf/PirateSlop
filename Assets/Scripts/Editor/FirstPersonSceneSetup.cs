using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace PirateSlop.EditorTools
{
    public static class FirstPersonSceneSetup
    {
        [MenuItem("PirateSlop/Configure First Person Ship Controls")]
        public static void Configure()
        {
            var ship = GameObject.Find("SM_PirateSloop");
            var player = GameObject.Find("PlayerCharacter");
            if (ship == null || player == null) throw new System.InvalidOperationException("Open SampleScene first.");
            Undo.RegisterFullObjectHierarchyUndo(ship, "Configure ship controls");
            Undo.RegisterFullObjectHierarchyUndo(player, "Configure player controls");
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(player);
            player.transform.localScale = Vector3.one;
            var extraCollider = player.GetComponent<CapsuleCollider>();
            if (extraCollider != null) extraCollider.enabled = false;
            var renderer = player.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.enabled = false;
            var cc = player.GetComponent<CharacterController>();
            cc.height = 1.8f; cc.radius = 0.3f; cc.center = Vector3.up * 0.9f;
            cc.stepOffset = 0.35f; cc.skinWidth = 0.03f; cc.minMoveDistance = 0f;
            var mover = player.GetComponent<AdvancedPlayerController>();
            mover.enabled = true;
            var data = new SerializedObject(mover);
            data.FindProperty("mouseSensitivity").floatValue = 0.12f;
            data.ApplyModifiedProperties();
            var cam = player.GetComponentInChildren<Camera>(true);
            cam.transform.localPosition = Vector3.up * 1.65f;
            cam.transform.localRotation = Quaternion.identity;
            cam.nearClipPlane = 0.05f; cam.fieldOfView = 80f; cam.enabled = true;
            var rb = ship.GetComponent<Rigidbody>();
            rb.isKinematic = true; rb.useGravity = false; rb.constraints = RigidbodyConstraints.None;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            Transform wheel = null;
            foreach (var t in ship.GetComponentsInChildren<Transform>(true)) if (t.name == "HelmWheel") wheel = t;
            if (wheel == null) throw new System.InvalidOperationException("HelmWheel is missing.");
            var helm = ship.GetComponentInChildren<HelmInteraction>();
            if (helm == null)
            {
                var go = new GameObject("Helm");
                go.transform.SetParent(ship.transform, false);
                helm = go.AddComponent<HelmInteraction>();
            }
            helm.transform.position = wheel.position;
            helm.Configure(wheel);
            var helmData = new SerializedObject(helm);
            helmData.FindProperty("interactionRadius").floatValue = 2.2f;
            helmData.FindProperty("turnSpeed").floatValue = 2f;
            helmData.ApplyModifiedProperties();
            ship.GetComponent<ShipController>().Configure(helm);
            var shipData = new SerializedObject(ship.GetComponent<ShipController>());
            shipData.FindProperty("maxSpeed").floatValue = 10f;
            shipData.ApplyModifiedProperties();
            var sailData = new SerializedObject(ship.GetComponent<SailSystem>());
            sailData.FindProperty("deployPercentage").floatValue = 0f;
            sailData.ApplyModifiedProperties();
            foreach(var t in ship.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.StartsWith("COL_"))
                {
                    var r = t.GetComponent<Renderer>(); if(r != null) r.enabled = false;
                }
            }
            var water = GameObject.Find("OceanWater");
            if (water != null) { var col = water.GetComponent<Collider>(); if (col != null) col.enabled = false; }
            EditorUtility.SetDirty(helm);
            EditorUtility.SetDirty(ship.GetComponent<ShipController>());
            EditorSceneManager.MarkSceneDirty(player.scene);
            EditorSceneManager.SaveScene(player.scene);
            AssetDatabase.SaveAssets();
            Debug.Log("First-person ship controls configured and saved.");
        }
    }
}
