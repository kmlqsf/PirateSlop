using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace PirateSlop
{
    public static class DeathRagdoll
    {
        public static Transform FindBone(Transform root, string name)
        {
            if (root == null) return null;
            string mixamo = name switch
            {
                "Hips" => "Hips", "Spine" => "Spine", "Chest" => "Spine2", "Neck" => "Neck", "Head" => "Head", "HeadTip" => "HeadTop_End",
                "UpperArm.L" => "LeftArm", "Forearm.L" => "LeftForeArm", "Hand.L" => "LeftHand", "Thigh.L" => "LeftUpLeg", "Shin.L" => "LeftLeg", "Foot.L" => "LeftFoot",
                "UpperArm.R" => "RightArm", "Forearm.R" => "RightForeArm", "Hand.R" => "RightHand", "Thigh.R" => "RightUpLeg", "Shin.R" => "RightLeg", "Foot.R" => "RightFoot",
                _ => name
            };
            return root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name || t.name == "mixamorig:" + mixamo);
        }
        public static void Spawn(Transform player, Vector3 position, Vector3 velocity, Vector3 spin)
            => Create(player, position, velocity, spin);

        public static GameObject Create(Transform player, Vector3 position, Vector3 velocity, Vector3 spin, float lifetime = 8f)
        {
            if (Application.isBatchMode || !Application.isPlaying) return null;
            return Build(player, position, velocity, spin, lifetime, true);
        }

        public static GameObject CreateKnockdown(Transform player, Vector3 position, Vector3 velocity, Vector3 spin)
        {
            if (!Application.isPlaying) return null;
            return Build(player, position, velocity, spin, 6f, true, true);
        }

        static GameObject Build(Transform player, Vector3 position, Vector3 velocity, Vector3 spin, float lifetime, bool simulate, bool gameplay = false)
        {
            var source = player.Find("PlayerGraphics");
            if (source == null) return null;
            var viewArms = player.GetComponent<WeaponArmRig>()?.ViewArms;
            var bodyHips = FindBone(player.GetComponent<WeaponArmRig>()?.BodyRig ?? source, "Hips");
            if (bodyHips == null) return null;
            var map = new Dictionary<Transform, Transform>();
            Transform Copy(Transform original, Transform parent)
            {
                var copy = new GameObject(original.name) { layer = original.gameObject.layer }.transform;
                copy.SetParent(parent, false);
                copy.localPosition = original.localPosition;
                copy.localRotation = original.localRotation;
                copy.localScale = original.localScale;
                map.Add(original, copy);
                foreach (Transform child in original)
                    if (child != viewArms) Copy(child, copy);
                copy.gameObject.SetActive(original.gameObject.activeSelf);
                return copy;
            }
            var root = Copy(source, null);
            root.name = "PirateCorpse";
            if (!simulate)
            {
                root.gameObject.hideFlags = HideFlags.HideAndDontSave;
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root.gameObject, player.gameObject.scene);
            }
            root.SetPositionAndRotation(source.position + position - player.position, source.rotation);
            root.localScale = source.lossyScale;
            root.gameObject.SetActive(false);
            foreach (var original in source.GetComponentsInChildren<Renderer>(true))
            {
                if (!map.ContainsKey(original.transform)) continue;
                if (original.name == "KickLegView") continue;
                Renderer copy = null;
                if (original is SkinnedMeshRenderer skin)
                {
                    if (!skin.bones.Any(b => b != null && b.IsChildOf(bodyHips)) || skin.bones.Any(b => b == null || !map.ContainsKey(b))) continue;
                    var mesh = map[skin.transform].gameObject.AddComponent<SkinnedMeshRenderer>();
                    mesh.sharedMesh = skin.sharedMesh;
                    mesh.bones = skin.bones.Select(b => b != null && map.TryGetValue(b, out var target) ? target : null).ToArray();
                    mesh.rootBone = skin.rootBone != null && map.TryGetValue(skin.rootBone, out var bone) ? bone : root;
                    mesh.localBounds = skin.localBounds;
                    mesh.updateWhenOffscreen = true;
                    if (skin.sharedMesh != null)
                        for (int i = 0; i < skin.sharedMesh.blendShapeCount; i++) mesh.SetBlendShapeWeight(i, skin.GetBlendShapeWeight(i));
                    copy = mesh;
                }
                else if (original is MeshRenderer && original.TryGetComponent<MeshFilter>(out var filter))
                {
                    if (!original.gameObject.activeInHierarchy) continue;
                    if (!original.transform.IsChildOf(bodyHips)) continue;
                    map[original.transform].gameObject.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                    copy = map[original.transform].gameObject.AddComponent<MeshRenderer>();
                }
                if (copy == null) continue;
                copy.sharedMaterials = original.sharedMaterials;
                copy.renderingLayerMask = original.renderingLayerMask;
                copy.enabled = true;
                copy.forceRenderingOff = false;
                copy.shadowCastingMode = ShadowCastingMode.On;
                copy.receiveShadows = original.receiveShadows;
                var properties = new MaterialPropertyBlock();
                original.GetPropertyBlock(properties); copy.SetPropertyBlock(properties);
                for (var visible = copy.transform; visible != null && visible != root; visible = visible.parent)
                    visible.gameObject.SetActive(true);
            }
            Transform Bone(string name) => FindBone(root, name);
            var hips = Bone("Hips");
            if (hips == null || !root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any(r => r.sharedMesh != null && r.bones.Length > 0))
            {
                if (Application.isPlaying) Object.Destroy(root.gameObject); else Object.DestroyImmediate(root.gameObject);
                return null;
            }
            if (simulate) CorpsePhysicsWorld.Add(root.gameObject, position, gameplay);
            root.gameObject.AddComponent<RagdollRendererBounds>().Configure(hips);
            var bodies = new List<Rigidbody>();
            var colliders = new List<Collider>();
            Rigidbody Segment(string name, string endName, float radius, float mass, Rigidbody parent)
            {
                var bone = Bone(name);
                if (bone == null) return parent;
                var end = Bone(endName);
                Vector3 delta = bone.InverseTransformPoint(end != null ? end.position : bone.position + player.up * .19f);
                var size = new Vector3(Mathf.Abs(delta.x), Mathf.Abs(delta.y), Mathf.Abs(delta.z));
                int axis = size.x > size.y && size.x > size.z ? 0 : size.z > size.y ? 2 : 1;
                var collider = bone.gameObject.AddComponent<CapsuleCollider>();
                collider.direction = axis; collider.center = delta * .5f;
                collider.radius = radius; collider.height = Mathf.Max(radius * 2f, delta.magnitude + radius);
                colliders.Add(collider);
                var body = bone.gameObject.AddComponent<Rigidbody>();
                body.mass = mass; body.linearDamping = .15f; body.angularDamping = .6f;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.maxAngularVelocity = 12f; body.solverIterations = 10; body.solverVelocityIterations = 4;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                bodies.Add(body);
                if (parent != null)
                {
                    var joint = bone.gameObject.AddComponent<CharacterJoint>();
                    joint.connectedBody = parent;
                    joint.axis = delta.sqrMagnitude > .001f ? delta.normalized : Vector3.up;
                    joint.swingAxis = Vector3.Cross(joint.axis, Mathf.Abs(joint.axis.y) < .9f ? Vector3.up : Vector3.right).normalized;
                    joint.lowTwistLimit = new SoftJointLimit { limit = -25f };
                    joint.highTwistLimit = new SoftJointLimit { limit = 25f };
                    joint.swing1Limit = new SoftJointLimit { limit = name.Contains("Shin") || name.Contains("Forearm") ? 75f : 45f };
                    joint.swing2Limit = new SoftJointLimit { limit = 30f };
                    joint.enableProjection = true; joint.projectionDistance = .08f; joint.projectionAngle = 15f;
                    joint.enablePreprocessing = false;
                }
                return body;
            }
            var pelvis = Segment("Hips", "Spine", .13f, 12f, null);
            var chest = Segment("Chest", "Neck", .14f, 14f, pelvis);
            Segment("Head", "HeadTip", .105f, 4f, chest);
            foreach (string side in new[] { ".L", ".R" })
            {
                var arm = Segment("UpperArm" + side, "Forearm" + side, .06f, 3f, chest);
                Segment("Forearm" + side, "Hand" + side, .05f, 2f, arm);
                var leg = Segment("Thigh" + side, "Shin" + side, .085f, 6f, pelvis);
                Segment("Shin" + side, "Foot" + side, .065f, 4f, leg);
            }
            root.gameObject.SetActive(true);
            for (int i = 0; i < colliders.Count; i++)
                for (int j = i + 1; j < colliders.Count; j++) Physics.IgnoreCollision(colliders[i], colliders[j]);
            for (int i = 0; i < bodies.Count; i++)
            {
                bodies[i].linearVelocity = velocity;
                bodies[i].angularVelocity = spin * (i == 0 ? 1f : .35f + .55f * Mathf.Sin(i * 2.4f + spin.x));
            }
            if (simulate) Object.Destroy(root.gameObject, lifetime);
            return root.gameObject;
        }
    }

    public sealed class RagdollRendererBounds : MonoBehaviour
    {
        Transform pelvis;
        SkinnedMeshRenderer[] skins;
        public void Configure(Transform bone)
        {
            pelvis = bone;
            skins = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            LateUpdate();
        }
        void LateUpdate()
        {
            if (pelvis == null || skins == null) return;
            foreach (var skin in skins)
                if (skin != null) skin.localBounds = new Bounds(skin.transform.InverseTransformPoint(pelvis.position), Vector3.one * 4f);
        }
    }
}
