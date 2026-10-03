using System;
using System.Linq;
using PirateSlop.Ships;
using UnityEditor;
using UnityEngine;

namespace PirateSlop.EditorTools
{
    public static class ShipV3GameplayRepair
    {
        public static void Configure(GameObject root)
        {
            ConfigureHelm(root);
            ConfigureDispenser(root);
            ConfigureDice(root);
            ConfigureBell(root);
            ConfigureLanterns(root);
            ConfigureFlags(root);
            foreach (var ladder in root.GetComponentsInChildren<ShipLadder>(true))
                if (ladder.FollowRopePath && !ladder.BoardingAccess) ladder.BothSides = true;
            var profile = root.GetComponent<ShipDestruction>().Profile;
            profile.DamageAdjacentFragments = true;
            EditorUtility.SetDirty(profile);
        }

        public static void ConfigureHelm(GameObject root)
        {
            var transforms = root.GetComponentsInChildren<Transform>(true);
            var wheel = transforms.Single(t => t.name == "V3_Transfer_S029_Helm_P0554_Intact");
            var section = wheel.GetComponentInParent<ShipDamageSection>(true);
            var helm = root.GetComponentInChildren<HelmInteraction>(true);
            Vector3 center = wheel.position;
            Vector3 axis = wheel.TransformDirection(ThinAxis(wheel.GetComponent<MeshFilter>().sharedMesh));
            if (Vector3.Dot(axis, root.transform.forward) < 0f) axis = -axis;
            var children = section.transform.Cast<Transform>().ToArray();
            var positions = children.Select(t => t.position).ToArray();
            var rotations = children.Select(t => t.rotation).ToArray();
            var scales = children.Select(t => t.lossyScale).ToArray();
            section.transform.SetParent(root.transform, true);
            section.transform.localScale = Vector3.one;
            section.transform.SetPositionAndRotation(center, root.transform.rotation);
            for (int i = 0; i < children.Length; i++)
            {
                children[i].SetPositionAndRotation(positions[i], rotations[i]);
                children[i].localScale = scales[i];
            }
            var rotor = transforms.FirstOrDefault(t => t.name == "HelmWheelRotor");
            if (rotor == null) rotor = Child(section.transform, "HelmWheelRotor", center, Quaternion.LookRotation(axis, root.transform.up));
            rotor.SetPositionAndRotation(center, Quaternion.LookRotation(axis, root.transform.up));
            foreach (var part in new[] { section.Intact, section.Damaged, section.Critical, section.Destroyed, section.Repaired }.Concat(section.Fragments).Where(p => p != null).Distinct())
                part.transform.SetParent(rotor, true);
            wheel.localPosition = Vector3.zero;
            helm.transform.position = center;
            helm.Configure(rotor, true);
        }

        public static void ConfigureDispenser(GameObject root)
        {
            var features = root.GetComponent<ShipV3Features>();
            var transforms = root.GetComponentsInChildren<Transform>(true);
            var mask = transforms.Single(t => t.name == "V15_Hold_Cannonball_Dispenser");
            var marker = transforms.Single(t => t.name == "V15_Cannonball_Spawn");
            var bounds = WorldBounds(mask.GetComponent<MeshFilter>());
            Vector3 sourcePoint = mask.InverseTransformPoint(marker.position);
            float factor = 1.8f / bounds.size.y;
            if (Mathf.Abs(factor - 1f) > .001f)
            {
                mask.localScale *= factor;
                marker.position = mask.TransformPoint(sourcePoint);
            }
            Vector3 scale = features.CannonballPrefab.transform.localScale;
            float radius = features.CannonballPrefab.GetComponent<SphereCollider>().radius * Mathf.Max(scale.x, scale.y, scale.z);
            features.DispenserDirection = Vector3.back;
            features.DispenserMouth.position = marker.position - root.transform.TransformDirection(features.DispenserDirection) * radius + root.transform.up * (radius + .025f);
            var source = transforms.Single(t => t.name == "V3_Transfer_Control_Main_Course_Lever");
            var oldLever = transforms.FirstOrDefault(t => t.name == "CannonballDispenserLever");
            if (oldLever != null) UnityEngine.Object.DestroyImmediate(oldLever.gameObject);
            bounds = WorldBounds(mask.GetComponent<MeshFilter>());
            Vector3 mount = root.transform.InverseTransformPoint(bounds.center);
            mount.x -= bounds.extents.x - .16f;
            mount.y = root.transform.InverseTransformPoint(features.DispenserMouth.position).y + .1f;
            mount.z = root.transform.InverseTransformPoint(new Vector3(bounds.center.x, bounds.center.y, bounds.max.z)).z + .015f;
            var sourceMesh = source.GetComponent<MeshFilter>().sharedMesh;
            Vector3 sourceAxis = source.TransformDirection(ThinAxis(sourceMesh));
            Vector3 sourceTip = sourceMesh.vertices.Select(v => source.TransformVector(v)).OrderByDescending(v => Vector3.ProjectOnPlane(v, sourceAxis).sqrMagnitude).First();
            Vector3 sourceDirection = Vector3.ProjectOnPlane(sourceTip, sourceAxis).normalized;
            Quaternion sourceFrame = Quaternion.LookRotation(sourceAxis, sourceDirection);
            Quaternion targetFrame = Quaternion.LookRotation(root.transform.right, root.transform.TransformDirection(features.DispenserDirection));
            var pivot = Child(mask.parent, "CannonballDispenserLever", root.transform.TransformPoint(mount), targetFrame);
            var visual = new GameObject("DispenserLeverModel");
            visual.transform.SetParent(pivot, false);
            visual.transform.localRotation = Quaternion.AngleAxis(90f, Vector3.up) * Quaternion.Inverse(sourceFrame) * source.rotation;
            visual.transform.localScale = source.lossyScale * .8f;
            var length = new GameObject("DispenserLeverLength").transform;
            length.SetParent(pivot, false);
            visual.transform.SetParent(length, false);
            length.localScale = new Vector3(1f, 1.25f, 1f);
            visual.AddComponent<MeshFilter>().sharedMesh = sourceMesh;
            visual.AddComponent<MeshRenderer>().sharedMaterials = source.GetComponent<MeshRenderer>().sharedMaterials;
            Vector3 gripPoint = visual.transform.TransformPoint(source.InverseTransformVector(sourceTip) * .9f);
            var grip = Child(pivot, "DispenserLeverGrip", gripPoint, root.transform.rotation);
            var collider = grip.gameObject.AddComponent<SphereCollider>();
            collider.radius = .22f; collider.isTrigger = true;
            Target(grip, features, ShipV3TargetKind.Dispenser);
            features.DispenserLever = pivot; features.DispenserGrip = grip;
            features.DispenserLeverAxis = Vector3.forward; features.DispenserLeverAngle = -65f;
        }

        public static void ConfigureFlags(GameObject root)
        {
            var transforms = root.GetComponentsInChildren<Transform>(true);
            foreach (var mast in new[] { "Main", "Fore" })
            {
                var cloth = transforms.Single(t => t.name == "V17_" + mast + "_Pirate_Flag_Cloth");
                var skin = cloth.GetComponent<SkinnedMeshRenderer>();
                var stand = transforms.Single(t => t.name == "V5_" + mast + "_Nest_Stand_Aft");
                float bottom = skin.sharedMesh.vertices.Min(v => root.transform.InverseTransformPoint(cloth.TransformPoint(v)).y);
                float desired = root.transform.InverseTransformPoint(stand.position).y + 2.3f;
                cloth.position += root.transform.up * (desired - bottom);
                var staff = transforms.Single(t => t.name == "V17_" + mast + "_Pirate_Flag_Staff");
                var oldMotion = staff.GetComponent<ShipV3ClothMotion>();
                if (oldMotion != null) UnityEngine.Object.DestroyImmediate(oldMotion);
                var mesh = staff.GetComponent<MeshFilter>().sharedMesh;
                var basePoint = staff.TransformPoint(new Vector3(mesh.bounds.center.x, mesh.bounds.center.y, mesh.bounds.min.z));
                float top = skin.sharedMesh.vertices.Max(v => root.transform.InverseTransformPoint(cloth.TransformPoint(v)).y) + .15f;
                float currentTop = root.transform.InverseTransformPoint(staff.TransformPoint(new Vector3(mesh.bounds.center.x, mesh.bounds.center.y, mesh.bounds.max.z))).y;
                float baseHeight = root.transform.InverseTransformPoint(basePoint).y;
                var section = staff.GetComponentInParent<ShipDamageSection>().transform;
                var scale = section.localScale;
                scale.z *= (top - baseHeight) / (currentTop - baseHeight);
                section.localScale = scale;
                section.position += basePoint - staff.TransformPoint(new Vector3(mesh.bounds.center.x, mesh.bounds.center.y, mesh.bounds.min.z));
            }
        }

        static void ConfigureDice(GameObject root)
        {
            var features = root.GetComponent<ShipV3Features>();
            var barrel = root.GetComponentsInChildren<MeshFilter>(true).Single(m => m.name == "V17_Dice_Game_Barrel");
            var bounds = WorldBounds(barrel);
            var table = features.DiceTable;
            table.position = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
            var floor = table.GetComponent<BoxCollider>();
            floor.center = new Vector3(0, -.025f, 0);
            features.DiceRadius = Mathf.Min(bounds.extents.x, bounds.extents.z) - .025f;
            floor.size = new Vector3(features.DiceRadius * 2f, .05f, features.DiceRadius * 2f);
            features.DiceSupport = barrel.gameObject;
            Target(barrel.transform, features, ShipV3TargetKind.Dice);
            Target(table.parent, features, ShipV3TargetKind.Dice);
            foreach (var child in table.Cast<Transform>().Where(t => t.name.StartsWith("DiceRim_", StringComparison.Ordinal)).ToArray())
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            for (int i = 0; i < 18; i++)
            {
                float angle = i * Mathf.PI * 2f / 18;
                var rim = Child(table, "DiceRim_" + i, table.position + root.transform.TransformDirection(new Vector3(Mathf.Cos(angle) * features.DiceRadius, .07f, Mathf.Sin(angle) * features.DiceRadius)), root.transform.rotation * Quaternion.Euler(0, -angle * Mathf.Rad2Deg, 0));
                rim.gameObject.AddComponent<BoxCollider>().size = new Vector3(.024f, .14f, features.DiceRadius * .37f);
            }
            foreach (var slot in features.DiceSlots)
            {
                var cupBounds = WorldBounds(slot.Cup.GetComponent<MeshFilter>());
                var owner = slot.Cup.GetComponentInParent<ShipDamageSection>();
                (owner != null ? owner.transform : slot.Cup.transform).position += root.transform.up * (bounds.max.y + .006f - cupBounds.min.y);
                slot.RestCup = root.transform.InverseTransformPoint(slot.Cup.transform.position);
                slot.RestRotation = Quaternion.Inverse(root.transform.rotation) * slot.Cup.transform.rotation;
                slot.Cup.isKinematic = true;
                foreach (var die in slot.Dice)
                {
                    var dieBounds = WorldBounds(die.GetComponent<MeshFilter>());
                    die.transform.position += root.transform.up * (bounds.max.y + .006f - dieBounds.min.y);
                    die.isKinematic = true;
                    die.solverIterations = 12; die.solverVelocityIterations = 4;
                    die.linearDamping = .45f; die.angularDamping = .8f;
                }
            }
        }

        static void ConfigureBell(GameObject root)
        {
            var features = root.GetComponent<ShipV3Features>();
            var clapper = features.BellClapper;
            var joint = clapper.GetComponent<ConfigurableJoint>();
            Anchor(joint);
            joint.enableCollision = true;
            var striker = clapper.GetComponentsInChildren<SphereCollider>(true).Single(c => c.name == "BellClapperContact");
            foreach (var grip in clapper.GetComponentsInChildren<SphereCollider>(true).Where(c => c != striker)) grip.isTrigger = true;
            var mesh = clapper.GetComponentsInChildren<MeshFilter>(true).Single(m => m.name == "V8_Bell_Clapper");
            var bounds = WorldBounds(mesh);
            striker.radius = Mathf.Max(bounds.extents.x, bounds.extents.z) + .006f;
            striker.transform.position = new Vector3(bounds.center.x, bounds.min.y + striker.radius, bounds.center.z);
            var walls = joint.connectedBody.GetComponentsInChildren<BoxCollider>(true).Where(c => c.name.StartsWith("BellInnerWall_", StringComparison.Ordinal)).ToArray();
            float innerRadius = walls.Min(c => Vector3.ProjectOnPlane(c.transform.position - striker.transform.position, root.transform.up).magnitude - c.size.x * .5f);
            float length = Vector3.Distance(clapper.transform.position, striker.transform.position);
            float limit = Mathf.Clamp(Mathf.Asin(Mathf.Clamp01((innerRadius - striker.radius) / length)) * Mathf.Rad2Deg + 5f, 20f, 36f);
            joint.lowAngularXLimit = new SoftJointLimit { limit = -limit };
            joint.highAngularXLimit = new SoftJointLimit { limit = limit };
            joint.angularYMotion = ConfigurableJointMotion.Locked;
            joint.angularZLimit = new SoftJointLimit { limit = limit };
            joint.projectionMode = JointProjectionMode.PositionAndRotation;
            joint.projectionDistance = .005f; joint.projectionAngle = 1f;
            clapper.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            clapper.solverIterations = 16; clapper.solverVelocityIterations = 8;
            clapper.maxAngularVelocity = 6f; clapper.angularDamping = .65f;
            Anchor(joint.connectedBody.GetComponent<ConfigurableJoint>());
        }

        static void ConfigureLanterns(GameObject root)
        {
            var transforms = root.GetComponentsInChildren<Transform>(true);
            Vector3 originalPort = root.transform.InverseTransformPoint(transforms.Single(t => t.name == "V3_Lamp_Hold_Port_Mount").position);
            foreach (string side in new[] { "Port", "Starboard" })
            {
                var lampMount = transforms.Single(t => t.name == "V3_Lamp_Hold_" + side + "_Mount");
                var bracket = transforms.Single(t => t.name == "V3_Lamp_Hold_" + side + "_Bracket");
                if (bracket.parent == lampMount) continue;
                Vector3 original = originalPort;
                if (side == "Starboard") original.x = -original.x;
                bracket.position += lampMount.position - root.transform.TransformPoint(original);
                bracket.SetParent(lampMount, true);
            }
            var mask = transforms.Single(t => t.name == "V15_Hold_Cannonball_Dispenser");
            var maskBounds = WorldBounds(mask.GetComponent<MeshFilter>());
            var mount = transforms.Single(t => t.name == "V3_Lamp_Hold_Starboard_Mount");
            var lampMesh = mount.GetComponentsInChildren<MeshFilter>(true).First(m => m.name == "V3_Lamp_Hold_Starboard_Body");
            float shift = maskBounds.min.z - WorldBounds(lampMesh).max.z - .3f;
            if (shift < 0f) mount.position += root.transform.forward * shift;
            foreach (var body in root.GetComponent<ShipV3Features>().PhysicsBodies.Where(b => b != null && b.name.Contains("Lamp_Hold")))
            {
                var joint = body.GetComponent<ConfigurableJoint>();
                var bodyMesh = body.GetComponentsInChildren<MeshFilter>(true).Single(m => m.name.EndsWith("_Body", StringComparison.Ordinal));
                var bounds = WorldBounds(bodyMesh);
                joint.anchor = body.transform.InverseTransformPoint(new Vector3(bounds.center.x, bounds.max.y - .025f, bounds.center.z));
                Anchor(joint);
                joint.lowAngularXLimit = new SoftJointLimit { limit = -20f };
                joint.highAngularXLimit = new SoftJointLimit { limit = 20f };
                joint.angularZLimit = new SoftJointLimit { limit = 20f };
                body.useGravity = true; body.angularDamping = .3f;
                body.solverIterations = 12;
            }
        }

        static void Anchor(ConfigurableJoint joint)
        {
            Vector3 point = joint.transform.TransformPoint(joint.anchor);
            joint.autoConfigureConnectedAnchor = false;
            joint.connectedAnchor = joint.connectedBody != null ? joint.connectedBody.transform.InverseTransformPoint(point) : point;
        }

        static Transform Child(Transform parent, string name, Vector3 position, Quaternion rotation)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            child.SetPositionAndRotation(position, rotation);
            var scale = parent.lossyScale;
            child.localScale = new Vector3(1f / Mathf.Abs(scale.x), 1f / Mathf.Abs(scale.y), 1f / Mathf.Abs(scale.z));
            return child;
        }

        static void Target(Transform grip, ShipV3Features features, ShipV3TargetKind kind)
        {
            var target = grip.GetComponent<ShipV3InteractionTarget>();
            if (target == null) target = grip.gameObject.AddComponent<ShipV3InteractionTarget>();
            target.Ship = features; target.Kind = kind;
        }

        static Bounds WorldBounds(MeshFilter filter)
        {
            var bounds = filter.sharedMesh.bounds;
            var result = new Bounds(filter.transform.TransformPoint(bounds.center), Vector3.zero);
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                        result.Encapsulate(filter.transform.TransformPoint(bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z))));
            return result;
        }

        static Vector3 ThinAxis(Mesh mesh)
        {
            var size = mesh.bounds.size;
            return size.x <= size.y && size.x <= size.z ? Vector3.right : size.y <= size.z ? Vector3.up : Vector3.forward;
        }
    }
}
