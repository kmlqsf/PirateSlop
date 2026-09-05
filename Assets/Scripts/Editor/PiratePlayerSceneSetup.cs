using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace PirateSlop.EditorTools
{
    public static class PiratePlayerSceneSetup
    {
        [MenuItem("PirateSlop/Attach Pirate To Player")]
        public static void Configure()
        {
            if (Application.isPlaying) throw new System.InvalidOperationException("Stop Play Mode first.");
            var player = GameObject.Find("PlayerCharacter");
            if (player == null || player.GetComponent<AdvancedPlayerController>() == null)
                throw new System.InvalidOperationException("Open the player scene first.");
            var camera = player.GetComponentInChildren<Camera>();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/PirateCharacter.prefab");
            if (prefab == null || camera == null) throw new System.InvalidOperationException("Pirate prefab or camera missing.");
            Undo.RegisterFullObjectHierarchyUndo(player, "Attach pirate visual");
            var visual = player.transform.Find("PirateVisual");
            if (visual == null)
            {
                visual = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, player.transform)).transform;
                visual.name = "PirateVisual";
                Undo.RegisterCreatedObjectUndo(visual.gameObject,"Create pirate visual");
            }
            visual.localPosition = Vector3.zero;
            // Imported toe/nose direction is -Z; gameplay forward is +Z.
            visual.localRotation = Quaternion.Euler(0f, 180f, 0f);
            visual.localScale = Vector3.one;
            var animator = visual.GetComponent<Animator>();
            if (animator != null) { animator.enabled = false; animator.applyRootMotion = false; }
            var visibility = visual.GetComponent<FirstPersonModelVisibility>();
            if (visibility == null) visibility = visual.gameObject.AddComponent<FirstPersonModelVisibility>();
            visibility.Configure(camera);
            var placeholder = player.GetComponent<MeshRenderer>();
            if (placeholder != null) placeholder.enabled = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(visual);
            if (animator != null) PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
            EditorUtility.SetDirty(visibility);
            EditorSceneManager.MarkSceneDirty(player.scene);
            EditorSceneManager.SaveScene(player.scene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = player;
            Debug.Log("Pirate attached to PlayerCharacter; FPS controls retained, animation disabled.");
        }
    }
}

