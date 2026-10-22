#if UNITY_EDITOR || PIRATESLOP_UPPER_DESTRUCTION_V2
using System;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using PirateSlop.Networking;
using PirateSlop.Ships;
using UnityEngine;

namespace PirateSlop.Experimental
{
    public enum UpperAssemblyKind : byte { MainMast, ForeMast, MizzenMast, Helm, Capstan, SailControl, Door, Dice, Harpoon, Casino }

    [Serializable]
    public sealed class UpperAssembly
    {
        public string Key;
        public UpperAssemblyKind Kind;
        public ShipDamageSection[] Sections = Array.Empty<ShipDamageSection>();
        public Transform[] Attachments = Array.Empty<Transform>();
        public Collider[] Controls = Array.Empty<Collider>();
        public int SailIndex = -1;
        public int ParentMast = -1;
        public Mesh RepairPreview;
        public Vector3 CollapsePivot;
        public float CollapseDrop;
        public UpperAttachmentRecipe[] MastVisuals = Array.Empty<UpperAttachmentRecipe>();
        public ShipDamageSection[] BaseSections = Array.Empty<ShipDamageSection>();
        public Bounds RepairBounds;
        public ShipFragmentConnection[] DeckSupports = Array.Empty<ShipFragmentConnection>();
        public Mesh MastCollision;
        public UpperGripBinding[] Grips = Array.Empty<UpperGripBinding>();
    }

    [Serializable]
    public sealed class UpperGripBinding
    {
        public MeshCollider Collider;
        public ShipDamageSection Section;
        [NonSerialized] public Mesh Mesh;
        [NonSerialized] public ulong AppliedMask = ulong.MaxValue;
        [NonSerialized] public bool AppliedVisible;
    }

    public struct UpperStage
    {
        public int Assembly;
        public byte Hits;
    }

    [Serializable]
    public sealed class UpperDebrisRecipe
    {
        public int Section, Fragment;
        public Transform Source;
        public Mesh First, Second;
        public Vector3 FirstOffset, SecondOffset;
        public Material[] Materials;
    }

    [Serializable]
    public sealed class UpperAttachmentLink
    {
        public Transform Source;
        public ShipV3Support[] Supports = Array.Empty<ShipV3Support>();
        public Transform[] Dependencies = Array.Empty<Transform>();
        public int Assembly = -1;
        public bool Protected;
        public int RequiredAssembly = -1;
    }

    [Serializable]
    public sealed class UpperAttachmentRecipe
    {
        public Transform Source;
        public Mesh[] Poses;
        public Vector3[] Offsets;
        public Material[] Materials;
        public int SailIndex;
        public bool Flexible;
        public int RequiredAssembly = -1;
        public Mesh[] DrapedPoses = Array.Empty<Mesh>();
    }

    [DefaultExecutionOrder(-80)]
    public sealed partial class UpperDestructionV2 : NetworkBehaviour
    {
        public UpperAssembly[] Assemblies = Array.Empty<UpperAssembly>();
        public UpperDebrisRecipe[] Debris = Array.Empty<UpperDebrisRecipe>();
        public UpperAttachmentRecipe[] AttachmentDebris = Array.Empty<UpperAttachmentRecipe>();
        public UpperAttachmentLink[] AttachmentLinks = Array.Empty<UpperAttachmentLink>();
        public int AssetRevision;
        public Material DustMaterial, SparkMaterial;
        public Transform ProtectedDispenser;
        public ShipDamageSection DiceTopSection;
        public int DiceTopFragment;
        public bool DiceTopLost => DiceTopSection != null && (DiceTopSection.RemovedFragments & (1UL << DiceTopFragment)) != 0;
        readonly SyncList<UpperStage> stages = new();
        readonly Dictionary<int, int> sectionAssembly = new();
        readonly Dictionary<int, int> hitAssembly = new();
        readonly Dictionary<Vector3Int, List<ShipDamageSection>> grid = new();
        readonly HashSet<ShipDamageSection> raySections = new();
        readonly Dictionary<int, Bounds[]> impactBounds = new();
        readonly Dictionary<int, float> stairImpactRadii = new();
        readonly Dictionary<int, ShipSectionDefinition> definitions = new();
        readonly Dictionary<Transform, (Vector3 Position, Quaternion Rotation)> attachmentRest = new();
        readonly Dictionary<Transform, List<int>> attachmentOwners = new();
        readonly Dictionary<Transform, bool> attachmentVisible = new();
        ShipV3RenderBatch[] attachmentBatches;
        uint attachmentRevision = uint.MaxValue;
        readonly List<ShipDamageSection> movingCover = new();
        static readonly Dictionary<Mesh, RayMesh> rayMeshes = new();
        List<ShipFragmentConnection>[] assemblySupports;
        bool[] assemblyAnchors;
        ShipDestruction destruction;
        NetworkShip ship;
        UpperDebrisPresentation presentation;
        internal UpperMastPresentation MastMotion;
        uint appliedStructureRevision = uint.MaxValue;
        float nextSupportCheck;
        int impactFrame = -1;
        byte[] appliedStages;
        double[] mastRepairCompletes;
        int[] reconciledRemoved;
        double nextMastRepair;
        bool stagesPresented;
        bool attachmentsPresented;
        public const int MastRepairStrikes = 6;
        public const float MastRepairStepSeconds = .4f;
        public ShipDestruction Destruction => destruction;
        public static readonly List<UpperDestructionV2> Active = new();
        public double Clock => TimeManager != null ? TimeManager.TicksToTime(TimeManager.Tick) : Time.timeAsDouble;
        public static UpperDestructionV2 For(Component component) => component != null ? component.GetComponentInParent<UpperDestructionV2>() : null;

        sealed class RayMesh
        {
            readonly Vector3[] vertices;
            readonly int[] triangles;
            public RayMesh(Mesh mesh) { vertices = mesh.vertices; triangles = mesh.triangles; }
            public float Fraction(Vector3 origin, Vector3 delta)
            {
                float nearest = float.PositiveInfinity;
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    var a = vertices[triangles[i]];
                    var edge1 = vertices[triangles[i + 1]] - a;
                    var edge2 = vertices[triangles[i + 2]] - a;
                    var p = Vector3.Cross(delta, edge2);
                    float determinant = Vector3.Dot(edge1, p);
                    float scale = Mathf.Sqrt(edge1.sqrMagnitude * edge2.sqrMagnitude * delta.sqrMagnitude);
                    if (scale <= 0f || Mathf.Abs(determinant) <= scale * .0000001f) continue;
                    float inverse = 1f / determinant;
                    var offset = origin - a;
                    float u = Vector3.Dot(offset, p) * inverse;
                    if (u < 0f || u > 1f) continue;
                    var q = Vector3.Cross(offset, edge1);
                    float v = Vector3.Dot(delta, q) * inverse;
                    if (v < 0f || u + v > 1f) continue;
                    float fraction = Vector3.Dot(edge2, q) * inverse;
                    if (fraction >= 0f && fraction <= 1f) nearest = Mathf.Min(nearest, fraction);
                }
                return nearest;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRayGeometry() => rayMeshes.Clear();

        static void PrepareRayGeometry(GameObject source)
        {
            var filter = source != null ? source.GetComponent<MeshFilter>() : null;
            var mesh = filter != null ? filter.sharedMesh : null;
            if (mesh != null && mesh.vertexCount > 0 && !rayMeshes.ContainsKey(mesh)) rayMeshes[mesh] = new RayMesh(mesh);
        }

        void Awake()
        {
            Active.Add(this);
            destruction = GetComponent<ShipDestruction>();
            ship = GetComponent<NetworkShip>();
            appliedStages = new byte[Assemblies.Length];
            mastRepairCompletes = new double[Assemblies.Length];
            reconciledRemoved = new int[Assemblies.Length];
            Array.Fill(appliedStages, byte.MaxValue);
            foreach (var definition in destruction.Profile.Sections) definitions[definition.SectionId] = definition;
            for (int i = 0; i < Assemblies.Length; i++)
            {
                if (Assemblies[i].Kind == UpperAssemblyKind.Door) movingCover.AddRange(Assemblies[i].Sections);
                foreach (var section in Assemblies[i].Sections) if (section != null) sectionAssembly[section.SectionId] = i;
                foreach (var attached in Assemblies[i].Attachments)
                    if (attached != null)
                    {
                        attachmentRest.TryAdd(attached, (attached.localPosition, attached.localRotation));
                        if (!attachmentOwners.TryGetValue(attached, out var owners)) attachmentOwners[attached] = owners = new List<int>();
                        owners.Add(i);
                    }
            }
            foreach (var section in destruction.Sections)
            {
                if (section == null) continue;
                PrepareRayGeometry(section.Intact);
                foreach (var fragment in section.Fragments) PrepareRayGeometry(fragment);
                if (section.Intact == null) continue;
                var filter = section.Intact.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                var bounds = LocalBounds(filter.transform, filter.sharedMesh.bounds);
                var low = Cell(bounds.min); var high = Cell(bounds.max);
                for (int x = low.x; x <= high.x; x++) for (int y = low.y; y <= high.y; y++) for (int z = low.z; z <= high.z; z++)
                {
                    var key = new Vector3Int(x, y, z);
                    if (!grid.TryGetValue(key, out var entries)) grid[key] = entries = new List<ShipDamageSection>();
                    entries.Add(section);
                }
            }
            InitializeImpactGeometry();
            InitializeAssemblySupports();
            foreach (var link in AttachmentLinks)
                if (link.Source != null)
                {
                    if (link.RequiredAssembly >= 0 && link.Source.TryGetComponent<ShipDamageSection>(out var controlled)) hitAssembly[controlled.SectionId] = link.RequiredAssembly;
                    attachmentRest.TryAdd(link.Source, (link.Source.localPosition, link.Source.localRotation));
                    attachmentVisible[link.Source] = link.Source.gameObject.activeSelf;
                }
            attachmentBatches = GetComponentsInChildren<ShipV3RenderBatch>(true);
            InitializeWeapons();
            MastMotion = new UpperMastPresentation(this);
        }

        public override void OnStartServer()
        {
            weapons.Clear();
            Array.Clear(mastRepairCompletes, 0, mastRepairCompletes.Length);
            Array.Clear(reconciledRemoved, 0, reconciledRemoved.Length);
            nextMastRepair = 0;
            stages.Clear();
            for (int i = 0; i < Assemblies.Length; i++) stages.Add(new UpperStage { Assembly = i });
        }

        public override void OnStartClient()
        {
            Array.Fill(appliedStages, byte.MaxValue);
            stagesPresented = false;
            presentation = gameObject.AddComponent<UpperDebrisPresentation>();
            presentation.Initialize(this);
            InitializeWeaponPresentation();
        }

        public override void OnStopNetwork()
        {
            StopWeapons();
            if (presentation != null) Destroy(presentation);
            presentation = null;
            raySections.Clear();
        }
        void OnDestroy()
        {
            MastMotion?.Dispose();
            foreach (var assembly in Assemblies) foreach (var grip in assembly.Grips) if (grip.Mesh != null) Destroy(grip.Mesh);
            Active.Remove(this);
            if (Active.Count == 0) rayMeshes.Clear();
        }

        public bool IsUpper(int sectionId) => definitions.TryGetValue(sectionId, out var d) && d.Type != ShipSectionType.Hull && d.Type != ShipSectionType.Rudder && !d.SourceGroup.StartsWith("BilgeBeam_", StringComparison.Ordinal);
        public bool IsProtectedSail(int index) => index == 0 || index == 1;
        public int AssemblyFor(int sectionId) => sectionAssembly.TryGetValue(sectionId, out int index) ? index : -1;
        public int HitAssemblyFor(int sectionId) => hitAssembly.TryGetValue(sectionId, out int index) ? index : AssemblyFor(sectionId);
        public int Stage(int index) => index >= 0 && index < stages.Count ? stages[index].Hits & 3 : 0;
        public int MastRepairProgress(int index) => index >= 0 && index < stages.Count ? (stages[index].Hits >> 2) & 7 : 0;
        public bool MastFallsRight(int index) => index >= 0 && index < stages.Count && (stages[index].Hits & 32) != 0;
        int EffectiveStage(int index) => Assemblies[index].ParentMast >= 0 && Stage(Assemblies[index].ParentMast) >= 3 ? 3 : Stage(index);
        public bool IsMain(int sectionId) => AssemblyFor(sectionId) is var index && index >= 0 && Assemblies[index].Kind == UpperAssemblyKind.MainMast;
        public bool IsMast(int index) => index >= 0 && (Assemblies[index].Kind == UpperAssemblyKind.ForeMast || Assemblies[index].Kind == UpperAssemblyKind.MizzenMast || Assemblies[index].Kind == UpperAssemblyKind.MainMast);
        public bool IsMechanism(int index) => index >= 0 && !IsMast(index);
        public bool OwnsAttachment(Transform target)
        {
            for (var current = target; current != null && current != transform; current = current.parent)
                if (attachmentRest.ContainsKey(current)) return true;
            return false;
        }
        void InitializeAssemblySupports()
        {
            assemblySupports = new List<ShipFragmentConnection>[Assemblies.Length];
            assemblyAnchors = new bool[Assemblies.Length];
            for (int i = 0; i < Assemblies.Length; i++) assemblySupports[i] = new List<ShipFragmentConnection>(Assemblies[i].DeckSupports);
            var nodes = destruction.Profile.Structure;
            foreach (var node in nodes)
            {
                int index = AssemblyFor(node.SectionId);
                if (index < 0 || IsMast(index) || Assemblies[index].DeckSupports.Length > 0) continue;
                assemblyAnchors[index] |= node.Anchor;
                foreach (int neighbour in node.Neighbours)
                {
                    var support = nodes[neighbour];
                    if (AssemblyFor(support.SectionId) != index && !assemblySupports[index].Contains(support)) assemblySupports[index].Add(support);
                }
            }
        }

        bool HasAssemblySupport(int index, Dictionary<int, ulong> unsupported = null)
        {
            if (index < 0 || index >= Assemblies.Length) return false;
            if (assemblyAnchors[index]) return true;
            foreach (var support in assemblySupports[index])
            {
                var section = destruction.Section(support.SectionId);
                if (section == null) continue;
                ulong removed = section.RemovedFragments;
                if (Assemblies[index].DeckSupports.Length == 0 && unsupported != null && unsupported.TryGetValue(support.SectionId, out ulong pending)) removed |= pending;
                if ((removed & (1UL << support.Fragment)) == 0) return true;
            }
            return false;
        }

        public bool PreserveMechanism(int sectionId, Dictionary<int, ulong> unsupported)
        {
            int index = AssemblyFor(sectionId);
            if (index < 0 || IsMast(index) || Stage(index) >= 3) return false;
            if (HasAssemblySupport(index, unsupported)) return true;
            SetStage(index, 3);
            return false;
        }

        public bool MechanismLostSupport(int index) => IsMechanism(index) && Stage(index) < 3 && Assemblies[index].DeckSupports.Length > 0 && !HasAssemblySupport(index);

        public bool CanRepairSection(int sectionId)
        {
            int index = AssemblyFor(sectionId);
            return index < 0 || IsMast(index) || (Assemblies[index].ParentMast < 0 || Stage(Assemblies[index].ParentMast) < 3) && HasAssemblySupport(index);
        }

        public bool CanChipFragment(int index, ShipDamageSection section, int fragment)
        {
            if (Assemblies[index].Kind is UpperAssemblyKind.Helm or UpperAssemblyKind.Dice) return true;
            if (section.Intact.name.Contains("Rotating_Drum") && section.Fragments.Length == 18) return fragment % 9 != 4;
            var point = Assemblies[index].RepairBounds.center;
            point.y = Assemblies[index].RepairBounds.min.y;
            int root = 0; float nearest = float.PositiveInfinity;
            for (int bit = 0; bit < section.Fragments.Length; bit++)
            {
                var source = section.RepairTransform(bit);
                var center = transform.InverseTransformPoint(source.TransformPoint(section.RepairBounds(bit).center));
                float distance = (center - point).sqrMagnitude;
                if (distance < nearest) { nearest = distance; root = bit; }
            }
            if (fragment == root) return false;
            int outside = fragment < root ? fragment - 1 : fragment + 1;
            return outside < 0 || outside >= section.Fragments.Length || (section.RemovedFragments & (1UL << outside)) != 0;
        }

        public bool AttachmentSupported(Transform source) => source == null || !attachmentVisible.TryGetValue(source, out bool supported) || supported;

        public float Efficiency(UpperAssemblyKind kind, int sail = -1)
        {
            float value = 1f;
            for (int i = 0; i < Assemblies.Length; i++)
                if (Assemblies[i].Kind == kind && (sail < 0 || Assemblies[i].SailIndex == sail))
                    value = Mathf.Min(value, EffectiveStage(i) >= 3 ? 0f : Stage(i) == 2 ? .35f : Stage(i) == 1 ? .8f : 1f);
            return value;
        }

        public void SetStage(int index, int stage)
        {
            if (!IsServerInitialized || index < 0 || index >= stages.Count) return;
            var current = stages[index];
            if (Stage(index) == Mathf.Clamp(stage, 0, 3) && (stage != 0 || current.Hits == 0)) return;
            current.Hits = (byte)Mathf.Clamp(stage, 0, 3); stages[index] = current;
        }

        public void BeginMastCollapse(int index, Vector3 velocity)
        {
            if (!IsServerInitialized || !IsMast(index) || Assemblies[index].Kind == UpperAssemblyKind.MainMast) return;
            var current = stages[index];
            current.Hits = (byte)(3 | (Vector3.Dot(velocity, transform.right) >= 0f ? 32 : 0));
            stages[index] = current;
            mastRepairCompletes[index] = 0;
        }

        public bool StrikeMastRepair(int sectionId)
        {
            if (!IsServerInitialized || !MastAssembly(sectionId, out int index) || Assemblies[index].Kind == UpperAssemblyKind.MainMast ||
                !destruction.MastRepairPoint(sectionId, out _) || MastRepairProgress(index) >= MastRepairStrikes) return false;
            int progress = MastRepairProgress(index) + 1;
            var current = stages[index];
            current.Hits = (byte)((current.Hits & 35) | (progress << 2));
            stages[index] = current;
            if (progress == MastRepairStrikes)
            {
                mastRepairCompletes[index] = Clock + MastRepairStepSeconds;
                if (nextMastRepair <= 0 || nextMastRepair > mastRepairCompletes[index]) nextMastRepair = mastRepairCompletes[index];
            }
            return true;
        }

        void TickMastRepair()
        {
            if (!IsServerInitialized || nextMastRepair <= 0 || Clock < nextMastRepair) return;
            nextMastRepair = 0;
            for (int i = 0; i < Assemblies.Length; i++)
            {
                if (mastRepairCompletes[i] <= 0) continue;
                if (Clock < mastRepairCompletes[i])
                {
                    if (nextMastRepair <= 0 || nextMastRepair > mastRepairCompletes[i]) nextMastRepair = mastRepairCompletes[i];
                    continue;
                }
                mastRepairCompletes[i] = 0;
                if (MastRepairProgress(i) == MastRepairStrikes && Assemblies[i].Sections.Length > 0)
                    destruction.RepairMast(Assemblies[i].Sections[0].SectionId);
            }
        }

        public void ReconcileRepair()
        {
            if (!IsServerInitialized) return;
            for (int i = 0; i < Assemblies.Length; i++)
            {
                int removed = 0, total = 0;
                foreach (var section in Assemblies[i].Sections)
                {
                    if (section == null) continue;
                    total += section.RepairCount;
                    ulong mask = section.RemovedFragments;
                    while (mask != 0) { mask &= mask - 1; removed++; }
                }
                bool repaired = removed < reconciledRemoved[i];
                reconciledRemoved[i] = removed;
                if (removed == 0 && repaired) SetStage(i, 0);
                else if (repaired && !IsMast(i) && total > 0)
                    SetStage(i, Mathf.Min(Stage(i), removed <= Mathf.CeilToInt(total / 3f) ? 1 : removed <= Mathf.CeilToInt(total * 2f / 3f) ? 2 : 3));
                if (Assemblies[i].Kind == UpperAssemblyKind.Harpoon) SynchronizeHarpoonStage(i);
            }
        }

        public void Present(ShipDestructionEvent impact)
        {
            presentation?.Emit(impact);
        }

        public bool MastAssembly(int sectionId, out int index)
        {
            index = AssemblyFor(sectionId);
            if (index >= 0 && IsMast(index)) return true;
            if (index >= 0 && Assemblies[index].ParentMast >= 0)
            {
                index = Assemblies[index].ParentMast;
                return IsMast(index);
            }
            return false;
        }

        public bool DirectMastAssembly(int sectionId, out int index)
        {
            index = AssemblyFor(sectionId);
            return IsMast(index);
        }

        public bool HideStandingMastPart(int sectionId)
        {
            if (!MastAssembly(sectionId, out int index) || Stage(index) < 3) return false;
            foreach (var part in Assemblies[index].BaseSections) if (part != null && part.SectionId == sectionId) return false;
            return true;
        }

        public bool RepairableMechanism(int sectionId, out int index)
        {
            index = AssemblyFor(sectionId);
            return IsMechanism(index) && Assemblies[index].Kind != UpperAssemblyKind.Harpoon &&
                (Assemblies[index].ParentMast < 0 || Stage(Assemblies[index].ParentMast) < 3);
        }

        void Update()
        {
            MastMotion?.Render(false);
            ApplyStages();
            RefreshGrips();
            ApplyAttachments();
            TickWeapons();
            TickMastRepair();
            if (!IsServerInitialized || Time.frameCount <= impactFrame) return;
            if (destruction.Revision == appliedStructureRevision && Time.time < nextSupportCheck) return;
            appliedStructureRevision = destruction.Revision; nextSupportCheck = Time.time + .5f;
            destruction.DetachUpperMechanisms();
            CheckWeaponSupports();
        }

        void ApplyStages()
        {
            bool changed = false;
            for (int i = 0; i < Assemblies.Length; i++)
            {
                byte stage = (byte)EffectiveStage(i);
                if (appliedStages[i] == stage) continue;
                changed = true;
                appliedStages[i] = stage;
                var assembly = Assemblies[i];
                foreach (var section in assembly.Sections) if (section != null) section.RefreshUpperVisibility();
                foreach (var control in assembly.Controls) if (control != null) control.enabled = stage < 3;
            }
            if (!changed) return;
            if (AttachmentLinks.Length == 0) foreach (var pair in attachmentOwners)
            {
                var attached = pair.Key;
                if (attached == null) continue;
                bool visible = true;
                int fallingMast = -1;
                foreach (int index in pair.Value)
                {
                    if (EffectiveStage(index) >= 3) visible = false;
                    if (IsMast(index) && Stage(index) >= 3) fallingMast = index;
                }
                if (!visible && attached.gameObject.activeSelf && stagesPresented && presentation != null) presentation.EmitAttachment(attached, fallingMast);
                if (visible && !attached.gameObject.activeSelf && attachmentRest.TryGetValue(attached, out var rest)) attached.SetLocalPositionAndRotation(rest.Position, rest.Rotation);
                attached.gameObject.SetActive(visible);
            }
            stagesPresented = true;
            attachmentRevision = uint.MaxValue;
            if (ship.Helm != null) ship.Helm.UpperResponse = Efficiency(UpperAssemblyKind.Helm);
            if (ship.Capstan != null) ship.Capstan.UpperResponse = Efficiency(UpperAssemblyKind.Capstan);
            var sails = GetComponent<SailSystem>();
            if (sails != null)
                for (int i = 0; i < sails.UpperControlResponse.Length; i++) sails.UpperControlResponse[i] = Efficiency(UpperAssemblyKind.SailControl, i);
        }

        public void MarkImpact() => impactFrame = Time.frameCount;

        void ApplyAttachments()
        {
            if (attachmentRevision == destruction.Revision) return;
            attachmentRevision = destruction.Revision;
            GetComponent<ShipV3Features>().UpdateUpperDice(DiceTopLost);
            bool changed = false;
            foreach (var link in AttachmentLinks)
            {
                var source = link.Source;
                if (source == null) continue;
                bool supported = link.Protected || link.Assembly < 0 || EffectiveStage(link.Assembly) < 3;
                if (link.RequiredAssembly >= 0) supported &= EffectiveStage(link.RequiredAssembly) < 3;
                if (!link.Protected)
                {
                    foreach (var support in link.Supports) supported &= support.Alive();
                    foreach (var dependency in link.Dependencies) supported &= dependency != null && dependency.gameObject.activeInHierarchy;
                }
                if (attachmentVisible.TryGetValue(source, out bool previous) && previous == supported && source.gameObject.activeSelf == supported) continue;
                attachmentVisible[source] = supported;
                if (supported && attachmentRest.TryGetValue(source, out var rest)) source.SetLocalPositionAndRotation(rest.Position, rest.Rotation);
                int mast = link.Assembly;
                if (mast >= 0 && !IsMast(mast)) mast = Assemblies[mast].ParentMast;
                if (mast < 0 || Stage(mast) < 3) mast = -1;
                if (!supported && source.gameObject.activeInHierarchy && attachmentsPresented && presentation != null) presentation.EmitAttachment(source, mast);
                source.gameObject.SetActive(supported);
                changed = true;
            }
            if (changed) foreach (var batch in attachmentBatches) batch.InvalidateVisibility();
            attachmentsPresented = true;
        }
        static Vector3Int Cell(Vector3 point) => new(Mathf.FloorToInt(point.x / 2f), Mathf.FloorToInt(point.y / 2f), Mathf.FloorToInt(point.z / 2f));
        Bounds LocalBounds(Transform source, Bounds bounds)
        {
            var matrix = transform.worldToLocalMatrix * source.localToWorldMatrix;
            var value = new Bounds(matrix.MultiplyPoint3x4(bounds.center), Vector3.zero);
            for (int i = 0; i < 8; i++) value.Encapsulate(matrix.MultiplyPoint3x4(bounds.center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
            return value;
        }

        void InitializeImpactGeometry()
        {
            var stairs = new Dictionary<int, Bounds>();
            foreach (var section in destruction.Sections)
            {
                if (section == null || section.Intact == null || !definitions.TryGetValue(section.SectionId, out var definition) ||
                    definition.Type is not (ShipSectionType.Deck or ShipSectionType.Stairs or ShipSectionType.Railing)) continue;
                var bounds = new Bounds[section.Fragments.Length];
                for (int i = 0; i < bounds.Length; i++) bounds[i] = LocalBounds(section.RepairTransform(i), section.RepairBounds(i));
                impactBounds[section.SectionId] = bounds;
                if (definition.Type == ShipSectionType.Stairs)
                {
                    var filter = section.Intact.GetComponent<MeshFilter>();
                    stairs[section.SectionId] = LocalBounds(filter.transform, filter.sharedMesh.bounds);
                }
            }
            foreach (var entry in stairs)
            {
                var center = entry.Value.center;
                var low = center; var high = center;
                foreach (var candidate in stairs)
                {
                    var point = candidate.Value.center;
                    if (!definitions[candidate.Key].SourceGroup.Contains("_Step") || Mathf.Abs(point.x - center.x) > 1f || Mathf.Abs(point.z - center.z) > 6f) continue;
                    low = Vector3.Min(low, point); high = Vector3.Max(high, point);
                }
                stairImpactRadii[entry.Key] = Mathf.Clamp((high - low).magnitude * .28f, .7f, 1.6f);
            }
        }

        public bool ImpactStructure(ShipDamageSection section) => impactBounds.ContainsKey(section.SectionId);
        public float StructureImpactRadius(ShipDamageSection section) => stairImpactRadii.TryGetValue(section.SectionId, out float radius) ? radius : 1.4f;

        public void CollectImpactSections(Vector3 point, float radius, HashSet<ShipDamageSection> result)
        {
            result.Clear();
            var local = transform.InverseTransformPoint(point);
            var low = Cell(local - Vector3.one * radius); var high = Cell(local + Vector3.one * radius);
            for (int x = low.x; x <= high.x; x++) for (int y = low.y; y <= high.y; y++) for (int z = low.z; z <= high.z; z++)
                if (grid.TryGetValue(new Vector3Int(x, y, z), out var entries))
                    foreach (var section in entries) if (ImpactStructure(section)) result.Add(section);
        }

        public ulong StructureImpactMask(ShipDamageSection section, Vector3 point, float radius)
        {
            ulong mask = section.RemovedFragments;
            if (section.Indestructible || !impactBounds.TryGetValue(section.SectionId, out var bounds)) return mask;
            var local = transform.InverseTransformPoint(point);
            bool deck = definitions[section.SectionId].Type == ShipSectionType.Deck;
            for (int i = 0; i < bounds.Length; i++)
            {
                if (((mask | section.ProtectedFragments) & (1UL << i)) != 0) continue;
                var closest = bounds[i].ClosestPoint(local) - local;
                var center = bounds[i].center - local;
                if (deck) { closest.y *= 3f; center.y *= 3f; }
                uint seed = unchecked((uint)(section.SectionId * 73856093 ^ i * 19349663));
                float edge = radius * (.85f + .3f * ((seed >> 8) & 255) / 255f);
                if (closest.sqrMagnitude + .2f * center.sqrMagnitude <= edge * edge) mask |= 1UL << i;
            }
            return mask;
        }

        public bool BlocksRay(Vector3 start, Vector3 end) => BlockDistance(start, end) < Vector3.Distance(start, end) - .025f;

        public float BlockDistance(Vector3 start, Vector3 end)
        {
            raySections.Clear();
            float nearest = Vector3.Distance(start, end);
            var a = transform.InverseTransformPoint(start); var b = transform.InverseTransformPoint(end);
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) * 2f));
            for (int n = 0; n <= steps; n++)
                if (grid.TryGetValue(Cell(Vector3.Lerp(a, b, n / (float)steps)), out var entries)) foreach (var section in entries) raySections.Add(section);
            foreach (var section in movingCover) if (section != null) raySections.Add(section);
            foreach (var section in raySections)
            {
                if (section.State == ShipSectionState.Destroyed && section.RemovedFragments == section.AllFragments && section.Fragments.Length > 0) continue;
                if (section.RemovedFragments == 0 && section.Intact != null && section.Intact.activeInHierarchy)
                {
                    nearest = Mathf.Min(nearest, IntersectionDistance(section.Intact.transform, start, end));
                }
                else for (int i = 0; i < section.Fragments.Length; i++)
                    if ((section.RemovedFragments & (1UL << i)) == 0) nearest = Mathf.Min(nearest, IntersectionDistance(section.Fragments[i].transform, start, end));
            }
            return nearest;
        }

        static float IntersectionDistance(Transform source, Vector3 start, Vector3 end)
        {
            if (!source.gameObject.activeInHierarchy) return float.PositiveInfinity;
            var filter = source.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) return float.PositiveInfinity;
            var origin = source.InverseTransformPoint(start); var delta = source.InverseTransformPoint(end) - origin;
            var mesh = filter.sharedMesh;
            if (delta.sqrMagnitude <= .000001f || !mesh.bounds.IntersectRay(new Ray(origin, delta.normalized), out _)) return float.PositiveInfinity;
            if (!rayMeshes.TryGetValue(mesh, out var geometry)) rayMeshes[mesh] = geometry = new RayMesh(mesh);
            return geometry.Fraction(origin, delta) * Vector3.Distance(start, end);
        }
    }
}
#endif
