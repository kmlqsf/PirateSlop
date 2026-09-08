using System.Linq;
using UnityEngine;
using UnityEditor;

namespace PirateSlop.EditorTools
{
    public static class WeaponArmsSetup
    {
        public static void Apply(GameObject player)
        {
            var body = player.GetComponentsInChildren<Animator>(true).First(a => a.name == "PirateVisual").transform;
            var camera = player.GetComponentInChildren<Camera>(true);
            var previous = camera.transform.Find("WeaponViewArms");
            if (previous != null) Object.DestroyImmediate(previous.gameObject);
            var arms = Object.Instantiate(body.gameObject, camera.transform);
            arms.name = "WeaponViewArms"; arms.transform.localPosition = new Vector3(0, -1.58f, -.035f);
            arms.transform.localRotation = body.localRotation;
            foreach (var c in arms.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(c);
            foreach (var c in arms.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(c);
            foreach (var c in arms.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var r in arms.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name != "Pirate_Hand_R" && r.name != "Pirate_Hand_L" && r.name != "Pirate_Sleeves") Object.DestroyImmediate(r);
                else
                {
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.receiveShadows = false;
                    if (r is SkinnedMeshRenderer skin) skin.updateWhenOffscreen = true;
                }
            }
            foreach (var bone in arms.GetComponentsInChildren<Transform>(true))
                if (bone.name.StartsWith("Index") || bone.name.StartsWith("Middle") || bone.name.StartsWith("Ring") || bone.name.StartsWith("Little"))
                    bone.localRotation *= Quaternion.Euler(60, 0, 0);
            foreach (var bone in arms.GetComponentsInChildren<Transform>(true))
            {
                if (bone.name == "Thumb1.R") bone.localRotation *= Quaternion.Euler(35, 0, -25);
                if (bone.name == "Thumb2.R") bone.localRotation *= Quaternion.Euler(65, 0, 0);
            }
            foreach (var bone in arms.GetComponentsInChildren<Transform>(true))
                if (bone != arms.transform) bone.name = "View_" + bone.name;
            var rig = player.GetComponent<WeaponArmRig>() ?? player.AddComponent<WeaponArmRig>();
            rig.BodyRig = body; rig.ViewArms = arms.transform;
            EditorUtility.SetDirty(rig);
        }
        public static void Install()
        {
            const string path = "Assets/Prefabs/Networking/NetworkPlayer.prefab";
            var player = PrefabUtility.LoadPrefabContents(path);
            try { Apply(player); PrefabUtility.SaveAsPrefabAsset(player, path); }
            finally { PrefabUtility.UnloadPrefabContents(player); }
            var local = GameObject.Find("PlayerCharacter");
            if (local != null && local.GetComponent<PirateWeapon>() != null)
            {
                Apply(local);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(local.scene);
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(local.scene);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
