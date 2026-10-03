using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using PirateSlop.Harpoon;
using PirateSlop.Ships;
using UnityEditor;
using UnityEngine;

namespace PirateSlop.EditorTools
{
    public static class ShipV3BindingRepair
    {
        public static void Configure(GameObject root, JObject document)
        {
            var names = root.GetComponentsInChildren<Transform>(true).GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
            var rig = root.GetComponent<ShipV3VisualRig>();
            var features = root.GetComponent<ShipV3Features>();
            var sails = root.GetComponent<SailSystem>();
            Transform Find(string name) => names.TryGetValue(name, out var target) ? target : throw new InvalidOperationException("Missing ship binding: " + name);
            Vector3 origin = Find("UnityV3_Origin").position;
            var axes = Matrix4x4.identity;
            axes.SetColumn(0, Find("UnityV3_Right").position - origin);
            axes.SetColumn(1, Find("UnityV3_Forward").position - origin);
            axes.SetColumn(2, Find("UnityV3_Up").position - origin);
            var motions = new List<ShipV3Motion>();
            foreach (var source in document["sail_samples"].Concat(document["anchor_samples"]))
            {
                var target = Find((string)source["name"]);
                var section = target.GetComponentInParent<ShipDamageSection>();
                if (section != null && section.Intact.transform == target) target = section.transform;
                int sailIndex = (int)source["index"];
                var samples = source["samples"].ToArray();
                var baseline = samples[sailIndex < 0 ? 0 : samples.Length - 1];
                if (baseline["parent_basis"] == null) throw new InvalidOperationException("Re-export evaluated mechanism poses before repairing bindings.");
                var parent = Matrix4x4.identity;
                for (int row = 0; row < 4; row++)
                    for (int column = 0; column < 4; column++) parent[row, column] = (float)baseline["parent_basis"][row][column];
                var mapping = (target.parent != null ? target.parent.worldToLocalMatrix : Matrix4x4.identity) * axes * parent;
                var baseRotation = Rotation(baseline["rotation"]);
                Vector3 basePosition = Vector(baseline["position"]);
                var poses = samples.Select(sample => new ShipV3Pose {
                    Position = target.localPosition + mapping.MultiplyVector(Vector(sample["position"]) - basePosition),
                    Rotation = (mapping * Matrix4x4.Rotate(Rotation(sample["rotation"]) * Quaternion.Inverse(baseRotation)) * mapping.inverse).rotation * target.localRotation,
                    Scale = target.localScale
                }).ToArray();
                if ((string)source["name"] == "V9_Capstan_Telescoping_Pivot")
                    for (int i = 0; i < poses.Length; i++)
                        poses[i].Position = Vector3.Lerp(poses[0].Position, poses[poses.Length - 1].Position, (float)i / (poses.Length - 1));
                motions.Add(new ShipV3Motion { Target = target, SailIndex = sailIndex, Poses = poses });
            }
            rig.Motions = motions.ToArray();
            ShipV3GameplayRepair.ConfigureHelm(root);
            string[] tags = { "Main_Course", "Main_Topsail", "Fore_Course", "Fore_Topsail", "Mizzen" };
            for (int i = 0; i < tags.Length; i++)
            {
                var lever = Find("V3_Transfer_Control_" + tags[i] + "_Lever");
                var handle = lever.GetComponent<ShipControlHandle>();
                if (handle == null) handle = lever.gameObject.AddComponent<ShipControlHandle>();
                handle.Sails = sails; handle.RopeIndex = i; sails.RopeHandles[i] = handle;
            }
            RemoveDoor(root);
            ConfigureDispenserBindings(root);
            ConfigureSpyglass(root);
            var rope = Find("V8_Bell_PullRope");
            var ropeBounds = rope.GetComponent<Renderer>().bounds;
            var grip = names.TryGetValue("BellRopeGrip", out var previous) ? previous : new GameObject("BellRopeGrip").transform;
            grip.SetParent(features.BellClapper.transform, true);
            grip.SetPositionAndRotation(ropeBounds.center, root.transform.rotation);
            var scale = grip.parent.lossyScale;
            grip.localScale = new Vector3(1f / Mathf.Abs(scale.x), 1f / Mathf.Abs(scale.y), 1f / Mathf.Abs(scale.z));
            var gripCollider = grip.GetComponent<CapsuleCollider>();
            if (gripCollider == null) gripCollider = grip.gameObject.AddComponent<CapsuleCollider>();
            gripCollider.direction = 1; gripCollider.radius = .22f; gripCollider.height = Mathf.Max(.6f, ropeBounds.size.y + .16f); gripCollider.isTrigger = true;
            var bellTarget = grip.GetComponent<ShipV3InteractionTarget>();
            if (bellTarget == null) bellTarget = grip.gameObject.AddComponent<ShipV3InteractionTarget>();
            bellTarget.Kind = ShipV3TargetKind.Bell; bellTarget.Ship = features;
            features.BellGrip = grip;
            features.BellClapper.angularDamping = .45f;
            var clapperJoint = features.BellClapper.GetComponent<ConfigurableJoint>();
            var low = clapperJoint.lowAngularXLimit; low.limit = -40f; clapperJoint.lowAngularXLimit = low;
            var high = clapperJoint.highAngularXLimit; high.limit = 40f; clapperJoint.highAngularXLimit = high;
            var y = clapperJoint.angularYLimit; y.limit = 40f; clapperJoint.angularYLimit = y;
            var z = clapperJoint.angularZLimit; z.limit = 40f; clapperJoint.angularZLimit = z;
            var spring = new SoftJointLimitSpring { spring = .25f, damper = .15f };
            clapperJoint.angularXLimitSpring = spring; clapperJoint.angularYZLimitSpring = spring;
            ConfigureClimbing(root);
            var anchorBody = features.MovingAnchorJoint.GetComponent<Rigidbody>();
            anchorBody.transform.SetParent(root.transform, true);
            anchorBody.linearDamping = .4f; anchorBody.angularDamping = .5f;
            rig.ChainHawse = Find("V9_Anchor_Starboard_Hawse");
            rig.ChainRoute = new[] { Find("V9_Anchor_Deck_Chain_Outlet"), rig.ChainHawse, Find("V9_Anchor_Chain_Swing_Pivot") }
                .Select(t => root.transform.InverseTransformPoint(t.position)).ToArray();
            var iron = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Ships/ShipV3/Materials/V2_Oxidized_Iron.mat");
            foreach (var link in rig.ChainLinks) if (link != null) link.GetComponent<MeshRenderer>().sharedMaterial = iron;
            foreach (var gun in root.GetComponentsInChildren<HarpoonGun>(true))
            {
                var serialized = new SerializedObject(gun);
                var yaw = (Transform)serialized.FindProperty("baseYaw").objectReferenceValue;
                var pitch = (Transform)serialized.FindProperty("barrelPitch").objectReferenceValue;
                var muzzle = (Transform)serialized.FindProperty("muzzle").objectReferenceValue;
                gun.YawAxis = yaw.InverseTransformDirection(root.transform.up);
                gun.PitchAxis = -pitch.InverseTransformDirection(muzzle.right);
                var camera = gun.CameraMount;
                camera.SetPositionAndRotation(muzzle.position - muzzle.forward * 1.45f + root.transform.up * .48f, Quaternion.LookRotation(muzzle.forward, root.transform.up));
            }
            typeof(ShipV3VisualRig).GetMethod("UpdateChain", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(rig, null);
            ShipV3GameplayRepair.Configure(root);
        }

        public static void RemoveDoor(GameObject root)
        {
            var features = root.GetComponent<ShipV3Features>();
            var mount = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "V16_Hold_Door_Mount");
            if (mount != null) mount.gameObject.SetActive(false);
            foreach (var target in root.GetComponentsInChildren<ShipV3InteractionTarget>(true).Where(t => t.Kind == ShipV3TargetKind.Door))
                UnityEngine.Object.DestroyImmediate(target);
            features.DoorHinge = features.DoorGrip = null;
            features.DoorAssembly = Array.Empty<Transform>();
        }

        public static void ConfigureDispenserBindings(GameObject root)
        {
            ShipV3GameplayRepair.ConfigureDispenser(root);
        }

        public static void ConfigureSpyglass(GameObject root)
        {
            var tube = root.GetComponentsInChildren<MeshFilter>(true).Single(m => m.name == "V9_Telescope_Tube");
            var station = tube.GetComponent<ShipSpyglass>();
            if (station == null) station = tube.gameObject.AddComponent<ShipSpyglass>();
            var bounds = LocalBounds(root.transform, tube);
            var viewpoint = tube.transform.Find("SpyglassViewpoint");
            if (viewpoint == null) { viewpoint = new GameObject("SpyglassViewpoint").transform; viewpoint.SetParent(tube.transform, false); }
            viewpoint.SetPositionAndRotation(root.transform.TransformPoint(new Vector3(bounds.center.x, bounds.center.y, bounds.min.z - .08f)), root.transform.rotation);
            station.Viewpoint = viewpoint; station.ViewpointLift = 0;
            var collider = tube.GetComponent<BoxCollider>();
            if (collider == null) collider = tube.gameObject.AddComponent<BoxCollider>();
            collider.center = tube.sharedMesh.bounds.center; collider.size = tube.sharedMesh.bounds.size;
        }

        public static void ConfigureClimbing(GameObject root)
        {
            foreach (var ladder in root.GetComponentsInChildren<ShipLadder>(true)) UnityEngine.Object.DestroyImmediate(ladder.gameObject);
            var meshes = root.GetComponentsInChildren<MeshFilter>(true);
            foreach (string mast in new[] { "Fore", "Main" })
            {
                var platform = meshes.Single(m => m.name == "V3_" + mast + "_MastTop_Platform");
                var platformBounds = LocalBounds(root.transform, platform);
                var ropes = meshes.Where(m => m.name.StartsWith("V3_" + mast + "_Shroud", StringComparison.Ordinal))
                    .Select(m => new { Mesh = m, Bottom = EndBounds(root.transform, m, false) }).ToArray();
                foreach (int side in new[] { -1, 1 })
                {
                    var group = ropes.Where(r => Mathf.Sign(r.Bottom.center.x) == side).ToArray();
                    if (group.Length == 0) throw new InvalidOperationException("Missing mast climbing ropes: " + mast + side);
                    var lower = group[0].Bottom;
                    foreach (var rope in group.Skip(1)) lower.Encapsulate(rope.Bottom);
                    Vector3 bottom = new Vector3(lower.center.x, lower.min.y, lower.center.z);
                    var rungs = meshes.Where(m => m.name.Contains(mast) && m.name.Contains(side < 0 ? "Port_Ratline" : "Starboard_Ratline"))
                        .Select(m => LocalBounds(root.transform, m)).ToArray();
                    float lastRung = rungs.Length > 0 ? rungs.Max(b => b.max.y) : platformBounds.max.y;
                    Vector3 top = new Vector3(side * (platformBounds.extents.x + .2f), Mathf.Min(lastRung, platformBounds.max.y), platformBounds.center.z);
                    var entry = root.GetComponentsInChildren<Transform>(true).Single(t => t.name == "V5_" + mast + (side < 0 ? "_Port" : "_Starboard") + "_CrowNest_ClimbEntry");
                    Vector3 exit = root.transform.InverseTransformPoint(entry.position);
                    exit.x = side * (platformBounds.extents.x - .65f);
                    exit.y = platformBounds.max.y + .035f;
                    AddClimb(root, group[0].Mesh.transform, "Climb_" + mast + (side < 0 ? "_Port" : "_Starboard"), bottom, top, exit,
                        Mathf.Clamp(lower.extents.z + .35f, .55f, 1.6f), false);
                }
            }
            foreach (string sideName in new[] { "Port", "Starboard" })
            {
                var mesh = meshes.Single(m => m.name == "V3_Transfer_Boarding_Net_" + sideName);
                var low = EndBounds(root.transform, mesh, false); var high = EndBounds(root.transform, mesh, true);
                Vector3 bottom = low.center; bottom.y = low.min.y;
                Vector3 top = high.center; top.y = high.max.y + 1.1f;
                var exit = top - Vector3.right * Mathf.Sign(bottom.x) * 1.2f;
                AddClimb(root, mesh.transform, "Climb_Boarding_" + sideName, bottom, top, exit, LocalBounds(root.transform, mesh).extents.z, true);
            }
        }

        static void AddClimb(GameObject root, Transform owner, string name, Vector3 bottom, Vector3 top, Vector3 exit, float width, bool boarding)
        {
            var access = new GameObject(name).transform;
            access.SetParent(owner, false);
            access.SetPositionAndRotation(root.transform.TransformPoint(bottom), Quaternion.LookRotation(-Mathf.Sign(bottom.x) * root.transform.right, root.transform.up));
            var scale = owner.lossyScale;
            access.localScale = new Vector3(1f / Mathf.Abs(scale.x), 1f / Mathf.Abs(scale.y), 1f / Mathf.Abs(scale.z));
            var ladder = access.gameObject.AddComponent<ShipLadder>();
            ladder.FollowRopePath = true; ladder.RopeClimb = true; ladder.BoardingAccess = boarding;
            ladder.BothSides = !boarding;
            ladder.RopeStandOff = boarding ? -.45f : .38f;
            ladder.HalfWidth = Mathf.Max(.55f, width); ladder.ExitClearance = boarding ? 1.1f : 1.35f;
            var endpoint = access.InverseTransformPoint(root.transform.TransformPoint(top));
            ladder.Height = endpoint.y; ladder.TopLean = endpoint.z; ladder.TopSideOffset = endpoint.x;
            ladder.ExitPoint = access.InverseTransformPoint(root.transform.TransformPoint(exit));
        }

        static Bounds LocalBounds(Transform root, MeshFilter mesh)
        {
            var mapping = root.worldToLocalMatrix * mesh.transform.localToWorldMatrix;
            var vertices = mesh.sharedMesh.vertices;
            var bounds = new Bounds(mapping.MultiplyPoint3x4(vertices[0]), Vector3.zero);
            foreach (var vertex in vertices) bounds.Encapsulate(mapping.MultiplyPoint3x4(vertex));
            return bounds;
        }

        static Bounds EndBounds(Transform root, MeshFilter mesh, bool upper)
        {
            var mapping = root.worldToLocalMatrix * mesh.transform.localToWorldMatrix;
            var bounds = LocalBounds(root, mesh);
            var vertices = mesh.sharedMesh.vertices.Select(mapping.MultiplyPoint3x4)
                .Where(v => upper ? v.y >= bounds.max.y - bounds.size.y * .015f : v.y <= bounds.min.y + bounds.size.y * .015f).ToArray();
            var end = new Bounds(vertices[0], Vector3.zero);
            foreach (var vertex in vertices) end.Encapsulate(vertex);
            return end;
        }

        static Vector3 Vector(JToken value) => new((float)value[0], (float)value[1], (float)value[2]);
        static Quaternion Rotation(JToken value) => new((float)value[1], (float)value[2], (float)value[3], (float)value[0]);
    }
}
