using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace PirateSlop.EditorTools
{
    public static class CharacterActionsSetup
    {
        [MenuItem("PirateSlop/Import Musket Actions")]
        public static void ImportMusket()
        {
            ImportFirearm("Musket", "SniperMusket", 1, new Vector3(-.0093f, .00115f, 0));
        }

        [MenuItem("PirateSlop/Import Shotgun Actions")]
        public static void ImportShotgun()
        {
            ImportFirearm("Shotgun", "PirateDoubleBarrel", 2, new Vector3(-.0052f, .00119f, 0));
        }

        static void ImportFirearm(string prefix, string originalName, int modelIndex, Vector3 muzzlePoint)
        {
            string[] names = { prefix + "Ready", prefix + "Aim", prefix + "Fire", prefix + "Reload" };
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Animations/Player/PiratePlayer.controller");
            if (!controller.layers.Any(l => l.name == "ItemActions")) controller.AddLayer("ItemActions");
            var layers = controller.layers;
            var layer = layers.First(l => l.name == "ItemActions");
            const string maskPath = "Assets/Animations/Player/ItemActions.mask";
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(maskPath);
            if (mask == null)
            {
                mask = Object.Instantiate(AssetDatabase.LoadAssetAtPath<AvatarMask>("Assets/Animations/Player/SabreUpperBody.mask"));
                mask.name = "ItemActions";
                int count = mask.transformCount;
                mask.transformCount += 2;
                mask.SetTransformPath(count, "ActionProp"); mask.SetTransformActive(count, true);
                mask.SetTransformPath(count + 1, "ActionProp/ActionRamrod"); mask.SetTransformActive(count + 1, true);
                AssetDatabase.CreateAsset(mask, maskPath);
            }
            layer.avatarMask = mask;
            if (!Enumerable.Range(0, mask.transformCount).Any(i => mask.GetTransformPath(i) == "ActionProp/ActionCartridge"))
            {
                int index = mask.transformCount;
                mask.transformCount++;
                mask.SetTransformPath(index, "ActionProp/ActionCartridge");
                mask.SetTransformActive(index, true);
                EditorUtility.SetDirty(mask);
            }
            layer.defaultWeight = 0;
            foreach (string name in names)
            {
                var clip = CharacterActionImport.Import(name, name.EndsWith("Ready") || name.EndsWith("Aim"));
                AddPropCurves(name, clip);
                var state = layer.stateMachine.states.Select(s => s.state).FirstOrDefault(s => s.name == name) ?? layer.stateMachine.AddState(name);
                state.motion = clip;
                if (name == prefix + "Ready") layer.stateMachine.defaultState = state;
            }
            controller.layers = layers;
            string sourcePath = "Assets/Models/Characters/Pirate/" + prefix + "Aim.fbx";
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            var sourceProp = source.transform.Find("ActionProp");
            var model = Object.Instantiate(sourceProp.gameObject);
            model.name = prefix + "ActionModel";
            model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            model.transform.localScale = Vector3.one;
            var muzzle = Child(model.transform, "ActionMuzzle");
            muzzle.localPosition = muzzlePoint;
            muzzle.localRotation = Quaternion.LookRotation(Vector3.left, Vector3.up);
            var original = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Props/PirateEquipment/" + originalName + ".prefab");
            var materials = original.GetComponentInChildren<Renderer>().sharedMaterials;
            foreach (var renderer in model.GetComponentsInChildren<Renderer>()) renderer.sharedMaterials = materials;
            var propAsset = PrefabUtility.SaveAsPrefabAsset(model, "Assets/Prefabs/Props/PirateEquipment/" + prefix + "ActionModel.prefab");
            Object.DestroyImmediate(model);
            const string path = "Assets/Prefabs/Networking/NetworkPlayer.prefab";
            var player = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var rig = player.GetComponent<WeaponArmRig>();
                var actions = player.GetComponent<CharacterActions>() ?? player.AddComponent<CharacterActions>();
                actions.enabled = true;
                actions.Animator = rig.BodyRig.GetComponent<Animator>();
                actions.BodyProp = Child(rig.BodyRig, "ActionProp");
                actions.ViewProp = Child(rig.ViewArms, "ActionProp");
                Child(actions.BodyProp, "ActionRamrod");
                Child(actions.BodyProp, "ActionCartridge");
                player.GetComponent<PirateSlop.Networking.NetworkEquipment>().Models[modelIndex] = propAsset;
                PrefabUtility.SaveAsPrefabAsset(player, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(player); }
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
        }

        static Transform Child(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null) return child;
            child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        static void AddPropCurves(string name, AnimationClip output)
        {
            string path = "Assets/Models/Characters/Pirate/" + name + ".fbx";
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var target = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Characters/Pirate/PirateCharacter.fbx");
            var sb = PirateAnimationRetargeter.BindPose(asset);
            var tb = PirateAnimationRetargeter.BindPose(target);
            var alignment = PirateAnimationRetargeter.Basis(tb) * Quaternion.Inverse(PirateAnimationRetargeter.Basis(sb));
            float scale = Vector3.Distance(tb["Head"].GetColumn(3), tb["Hips"].GetColumn(3)) / Vector3.Distance(sb["Head"].GetColumn(3), sb["Hips"].GetColumn(3));
            var raw = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
            var source = Object.Instantiate(asset);
            source.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var prop = source.transform.Find("ActionProp");
                var rod = prop.Find("ActionRamrod");
                string[] properties = { "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z", "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w", "m_LocalScale.x", "m_LocalScale.y", "m_LocalScale.z" };
                foreach (var item in new[] { prop, rod, prop.Find("ActionCartridge") }.Where(t => t != null))
                {
                    var curves = properties.Select(p => new AnimationCurve()).ToArray();
                    Quaternion previous = Quaternion.identity;
                    int frames = Mathf.RoundToInt(raw.length * 60);
                    for (int f = 0; f <= frames; f++)
                    {
                        float time = Mathf.Min(f / 60f, raw.length);
                        raw.SampleAnimation(source, time);
                        var position = item == prop ? alignment * item.position * scale : item.localPosition;
                        var rotation = item == prop ? alignment * item.rotation : item.localRotation;
                        var size = item == prop ? item.lossyScale * scale : item.localScale;
                        if (f > 0 && Quaternion.Dot(previous, rotation) < 0) rotation = new Quaternion(-rotation.x, -rotation.y, -rotation.z, -rotation.w);
                        previous = rotation;
                        float[] values = { position.x, position.y, position.z, rotation.x, rotation.y, rotation.z, rotation.w, size.x, size.y, size.z };
                        for (int i = 0; i < curves.Length; i++) curves[i].AddKey(time, values[i]);
                    }
                    for (int i = 0; i < curves.Length; i++)
                    {
                        for (int k = 0; k < curves[i].length; k++)
                        {
                            AnimationUtility.SetKeyLeftTangentMode(curves[i], k, AnimationUtility.TangentMode.Linear);
                            AnimationUtility.SetKeyRightTangentMode(curves[i], k, AnimationUtility.TangentMode.Linear);
                        }
                        output.SetCurve(item == prop ? "ActionProp" : "ActionProp/" + item.name, typeof(Transform), properties[i], curves[i]);
                    }
                }
                output.EnsureQuaternionContinuity();
                EditorUtility.SetDirty(output);
            }
            finally { Object.DestroyImmediate(source); }
        }
    }
}
