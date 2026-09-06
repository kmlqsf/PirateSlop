using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PirateSlop.EditorTools
{
    // Generic clips need bind-pose conversion, even when all bone names match.
    public static class PirateAnimationRetargeter
    {
        public static void Bake(string sourcePath, AnimationClip source, AnimationClip output, bool removeJumpHeight)
        {
            var sourceAsset = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            var targetAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Characters/Pirate/PirateCharacter.fbx");
            var sourceBind = BindPose(sourceAsset);
            var targetBind = BindPose(targetAsset);
            Quaternion alignment = Basis(targetBind) * Quaternion.Inverse(Basis(sourceBind));
            float scale = Vector3.Distance(targetBind["Head"].GetColumn(3), targetBind["Hips"].GetColumn(3)) /
                Vector3.Distance(sourceBind["Head"].GetColumn(3), sourceBind["Hips"].GetColumn(3));
            var sourceObject = UnityEngine.Object.Instantiate(sourceAsset);
            var targetObject = UnityEngine.Object.Instantiate(targetAsset);
            sourceObject.hideFlags = targetObject.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var sourceBones = sourceObject.GetComponentsInChildren<Transform>().ToDictionary(t => t.name);
                var targetBones = targetObject.GetComponentsInChildren<Transform>().Where(t => targetBind.ContainsKey(t.name)).ToArray();
                var animated = new[] { targetObject.transform.Find("PirateRig") }.Concat(targetBones).ToArray();
                int frames = Mathf.CeilToInt(source.length * source.frameRate);
                var values = new float[animated.Length, 7, frames + 1];
                var previous = new Quaternion[animated.Length];
                for (int frame = 0; frame <= frames; frame++)
                {
                    source.SampleAnimation(sourceObject, Mathf.Min(frame / source.frameRate, source.length));
                    var displacement = alignment * sourceBones["Root"].position * scale;
                    if (!removeJumpHeight) displacement.y = 0;
                    // Parent-before-child writes preserve the sampled world pose.
                    foreach (var bone in targetBones)
                    {
                        var sample = sourceBones[bone.name];
                        var rotation = alignment * sample.rotation * Quaternion.Inverse(sourceBind[bone.name].rotation) *
                            Quaternion.Inverse(alignment) * targetBind[bone.name].rotation;
                        bone.SetPositionAndRotation(alignment * sample.position * scale - displacement, rotation);
                    }
                    for (int i = 0; i < animated.Length; i++)
                    {
                        var p = animated[i].localPosition;
                        var q = animated[i].localRotation;
                        if (frame > 0 && Quaternion.Dot(previous[i], q) < 0) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                        previous[i] = q;
                        values[i, 0, frame] = p.x; values[i, 1, frame] = p.y; values[i, 2, frame] = p.z;
                        values[i, 3, frame] = q.x; values[i, 4, frame] = q.y; values[i, 5, frame] = q.z; values[i, 6, frame] = q.w;
                    }
                }
                output.ClearCurves(); output.frameRate = source.frameRate;
                string[] properties = { "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z", "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w" };
                for (int i = 0; i < animated.Length; i++)
                    for (int channel = 0; channel < 7; channel++)
                    {
                        bool constant = true;
                        for (int f = 1; f <= frames; f++) if (Mathf.Abs(values[i, channel, f] - values[i, channel, 0]) > .00001f) constant = false;
                        AnimationCurve curve;
                        if (constant) curve = AnimationCurve.Constant(0, source.length, values[i, channel, 0]);
                        else
                        {
                            var keys = new Keyframe[frames + 1];
                            for (int f = 0; f <= frames; f++) keys[f] = new Keyframe(Mathf.Min(f / source.frameRate, source.length), values[i, channel, f]);
                            curve = new AnimationCurve(keys);
                            for (int f = 0; f <= frames; f++)
                            {
                                AnimationUtility.SetKeyLeftTangentMode(curve, f, AnimationUtility.TangentMode.Linear);
                                AnimationUtility.SetKeyRightTangentMode(curve, f, AnimationUtility.TangentMode.Linear);
                            }
                        }
                        output.SetCurve(AnimationUtility.CalculateTransformPath(animated[i], targetObject.transform), typeof(Transform), properties[channel], curve);
                    }
                output.EnsureQuaternionContinuity();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(sourceObject);
                UnityEngine.Object.DestroyImmediate(targetObject);
            }
        }
        static Dictionary<string, Matrix4x4> BindPose(GameObject model)
        {
            var renderer = model.GetComponentInChildren<SkinnedMeshRenderer>();
            var result = new Dictionary<string, Matrix4x4>();
            for (int i = 0; i < renderer.bones.Length; i++) result.Add(renderer.bones[i].name, renderer.transform.localToWorldMatrix * renderer.sharedMesh.bindposes[i].inverse);
            return result;
        }
        static Quaternion Basis(Dictionary<string, Matrix4x4> bind)
        {
            Vector3 right = bind["Thigh.L"].GetColumn(3) - bind["Thigh.R"].GetColumn(3);
            Vector3 up = bind["Head"].GetColumn(3) - bind["Hips"].GetColumn(3);
            return Quaternion.LookRotation(Vector3.Cross(right, up), up);
        }
    }
}
