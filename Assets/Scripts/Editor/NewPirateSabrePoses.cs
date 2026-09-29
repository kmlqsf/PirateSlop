using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PirateSlop.EditorTools
{
    public static class NewPirateSabrePoses
    {
        const string Folder = "Assets/Animations/Player/";
        const string Models = "Assets/Models/Characters/NewPirate/";
        static readonly Vector3 Grip = new Vector3(-.009597f, .086534f, .021396f);
        static readonly Quaternion GripRotation = Quaternion.LookRotation(new Vector3(-.081345f, .918127f, .387847f), new Vector3(-.980509f, -.143544f, .134156f));

        public static void Rebuild()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before rebuilding sabre poses.");
            var scene = EditorSceneManager.NewPreviewScene();
            GameObject model = null;
            try
            {
                model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Models + "Walking.fbx"));
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(model, scene);
                var all = model.GetComponentsInChildren<Transform>();
                var spine = all.Single(t => t.name == "mixamorig:Spine");
                var bones = spine.GetComponentsInChildren<Transform>();
                var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "New_Idle.anim");
                var outward = Source("Stable Sword Outward Slash.fbx");
                var inward = Source("Stable Sword Inward Slash.fbx");
                var ready = Guard(model, all, bones, idle, outward, new Vector3(.34f, 1.22f, .34f), new Vector3(.18f, .90f, .40f));
                var opposite = Guard(model, all, bones, idle, outward, new Vector3(-.12f, 1.34f, .38f), new Vector3(-.25f, .92f, .30f));
                Write(model.transform, bones, "New_SabreReady.anim", new[] { 0f, 1.5f }, new[] { ready, ready }, true);
                Attack(model, bones, outward, "New_SwordSlash1.anim", ready, ready, .65f, 1.12f);
                Attack(model, bones, inward, "New_SwordSlash2.anim", ready, opposite, 1.25f, 1.50f);
            }
            finally
            {
                if (model != null) UnityEngine.Object.DestroyImmediate(model);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        static AnimationClip Source(string name)
        {
            return AssetDatabase.LoadAllAssetsAtPath(Models + name).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
        }

        static Quaternion[] Capture(Transform[] bones)
        {
            return bones.Select(t => t.localRotation).ToArray();
        }

        static Quaternion[] Guard(GameObject model, Transform[] all, Transform[] bones, AnimationClip idle, AnimationClip fist, Vector3 gripPoint, Vector3 blade)
        {
            fist.SampleAnimation(model, 0);
            var fingers = bones.Where(t => t.name.StartsWith("mixamorig:RightHand") && t.name != "mixamorig:RightHand").ToArray();
            var fingerRotations = Capture(fingers);
            idle.SampleAnimation(model, 0);
            for (int i = 0; i < fingers.Length; i++) fingers[i].localRotation = fingerRotations[i];
            var upper = all.Single(t => t.name == "mixamorig:RightArm");
            var fore = all.Single(t => t.name == "mixamorig:RightForeArm");
            var hand = all.Single(t => t.name == "mixamorig:RightHand");
            var swordRotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(Vector3.forward, blade).normalized, blade.normalized);
            var handRotation = swordRotation * Quaternion.Inverse(GripRotation);
            var wrist = gripPoint - handRotation * Grip;
            var origin = upper.position;
            float first = Vector3.Distance(origin, fore.position);
            float second = Vector3.Distance(fore.position, hand.position);
            var delta = wrist - origin;
            float distance = Mathf.Clamp(delta.magnitude, Mathf.Abs(first - second) + .001f, first + second - .001f);
            var forward = delta.normalized;
            var pole = new Vector3(.7f, .95f, .2f);
            var bend = Vector3.ProjectOnPlane(pole - origin, forward).normalized;
            float along = (first * first + distance * distance - second * second) / (2 * distance);
            var elbow = origin + forward * along + bend * Mathf.Sqrt(Mathf.Max(0, first * first - along * along));
            upper.rotation = Quaternion.FromToRotation(fore.position - origin, elbow - origin) * upper.rotation;
            fore.rotation = Quaternion.FromToRotation(hand.position - fore.position, origin + forward * distance - fore.position) * fore.rotation;
            hand.rotation = handRotation;
            return Capture(bones);
        }

        static Quaternion[] Blend(Quaternion[] from, Quaternion[] to, float weight)
        {
            var pose = new Quaternion[from.Length];
            for (int i = 0; i < pose.Length; i++) pose[i] = Quaternion.Slerp(from[i], to[i], weight);
            return pose;
        }

        static void Attack(GameObject model, Transform[] bones, AnimationClip source, string name, Quaternion[] ready, Quaternion[] preparation, float start, float end)
        {
            source.SampleAnimation(model, start);
            var first = Capture(bones);
            source.SampleAnimation(model, end);
            var last = Capture(bones);
            var times = new float[28];
            var poses = new Quaternion[times.Length][];
            for (int frame = 0; frame < times.Length; frame++)
            {
                float time = frame / 30f;
                times[frame] = time;
                if (time < .13333334f)
                    poses[frame] = Blend(ready, preparation, Mathf.SmoothStep(0, 1, time / .13333334f));
                else if (time < .26666668f)
                    poses[frame] = Blend(preparation, first, Mathf.SmoothStep(0, 1, (time - .13333334f) / .13333334f));
                else if (time <= .6f)
                {
                    source.SampleAnimation(model, Mathf.Lerp(start, end, (time - .26666668f) / .33333334f));
                    poses[frame] = Capture(bones);
                }
                else
                    poses[frame] = Blend(last, ready, Mathf.SmoothStep(0, 1, (time - .6f) / .3f));
            }
            Write(model.transform, bones, name, times, poses, false);
        }

        static void Write(Transform root, Transform[] bones, string name, float[] times, Quaternion[][] poses, bool loop)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + name);
            if (clip == null)
            {
                clip = new AnimationClip { name = System.IO.Path.GetFileNameWithoutExtension(name) };
                AssetDatabase.CreateAsset(clip, Folder + name);
            }
            clip.ClearCurves();
            clip.frameRate = 30;
            for (int bone = 0; bone < bones.Length; bone++)
            {
                for (int frame = 1; frame < poses.Length; frame++)
                    if (Quaternion.Dot(poses[frame - 1][bone], poses[frame][bone]) < 0)
                    {
                        var q = poses[frame][bone];
                        poses[frame][bone] = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                    }
                string path = AnimationUtility.CalculateTransformPath(bones[bone], root);
                for (int axis = 0; axis < 4; axis++)
                {
                    var keys = new Keyframe[times.Length];
                    for (int frame = 0; frame < times.Length; frame++) keys[frame] = new Keyframe(times[frame], poses[frame][bone][axis]);
                    var curve = new AnimationCurve(keys);
                    for (int key = 0; key < curve.length; key++)
                    {
                        AnimationUtility.SetKeyLeftTangentMode(curve, key, AnimationUtility.TangentMode.Linear);
                        AnimationUtility.SetKeyRightTangentMode(curve, key, AnimationUtility.TangentMode.Linear);
                    }
                    clip.SetCurve(path, typeof(Transform), "m_LocalRotation." + "xyzw"[axis], curve);
                }
            }
            clip.EnsureQuaternionContinuity();
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            settings.startTime = 0;
            settings.stopTime = times[times.Length - 1];
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssetIfDirty(clip);
        }
    }
}
