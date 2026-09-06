using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace PirateSlop.EditorTools
{
    public static class SwimmingSetup
    {
        [MenuItem("PirateSlop/Configure Swimming")]
        public static void Configure()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Animations/Player/PiratePlayer.controller");
            var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Player/Idle.anim");
            var model = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Characters/PirateCharacter.prefab");
            try
            {
                var animator = model.GetComponentInChildren<Animator>(true);
                var root = animator.gameObject;
                var bones = root.GetComponentsInChildren<Transform>(true).Where(t => t != root.transform).ToArray();
                foreach (bool moving in new[] { false, true })
                {
                    string name = moving ? "Swim" : "TreadWater";
                    string path = "Assets/Animations/Player/" + name + ".anim";
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                    if (clip == null) { clip = new AnimationClip { name = name, frameRate = 30 }; AssetDatabase.CreateAsset(clip, path); }
                    clip.ClearCurves();
                    var curves = new AnimationCurve[bones.Length, 7];
                    for (int b = 0; b < bones.Length; b++) for (int c = 0; c < 7; c++) curves[b, c] = new AnimationCurve();
                    for (int frame = 0; frame <= 48; frame++)
                    {
                        float time = frame / 30f, phase = frame / 48f * Mathf.PI * 2f;
                        idle.SampleAnimation(root, 0);
                        Vector3 right = root.transform.right, forward = -root.transform.forward;
                        foreach (var bone in bones)
                        {
                            float side = bone.name.EndsWith(".L") ? -1 : 1;
                            if (bone.name == "Hips") bone.rotation = Quaternion.AngleAxis(moving ? -50 : -8, right) * bone.rotation;
                            if (bone.name.StartsWith("UpperArm"))
                            {
                                bone.rotation = Quaternion.AngleAxis(-55 + 40 * Mathf.Cos(phase), right) * bone.rotation;
                                bone.rotation = Quaternion.AngleAxis(side * (30 + 30 * Mathf.Sin(phase)), forward) * bone.rotation;
                            }
                            if (bone.name.StartsWith("Forearm")) bone.rotation = Quaternion.AngleAxis(-25 - 20 * Mathf.Sin(phase), right) * bone.rotation;
                            if (bone.name.StartsWith("Thigh")) bone.rotation = Quaternion.AngleAxis(side * Mathf.Sin(phase * (moving ? 2 : 1)) * (moving ? 20 : 12), right) * bone.rotation;
                            if (bone.name.StartsWith("Shin")) bone.rotation = Quaternion.AngleAxis(15 + 12 * Mathf.Sin(phase * 2 + side), right) * bone.rotation;
                        }
                        for (int b = 0; b < bones.Length; b++)
                        {
                            var q = bones[b].localRotation; var p = bones[b].localPosition;
                            float[] values = { q.x, q.y, q.z, q.w, p.x, p.y, p.z };
                            for (int c = 0; c < 7; c++) curves[b, c].AddKey(time, values[c]);
                        }
                    }
                    string[] properties = { "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w", "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z" };
                    for (int b = 0; b < bones.Length; b++) for (int c = 0; c < 7; c++)
                        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(bones[b], root.transform), typeof(Transform), properties[c]), curves[b, c]);
                    clip.EnsureQuaternionContinuity();
                    var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = true; AnimationUtility.SetAnimationClipSettings(clip, settings);
                    var machine = controller.layers[0].stateMachine;
                    var state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == name) ?? machine.AddState(name);
                    state.motion = clip; EditorUtility.SetDirty(clip);
                }
                EditorUtility.SetDirty(controller); AssetDatabase.SaveAssets();
            }
            finally { PrefabUtility.UnloadPrefabContents(model); }
            OceanSetup.Configure();
        }
    }
}
