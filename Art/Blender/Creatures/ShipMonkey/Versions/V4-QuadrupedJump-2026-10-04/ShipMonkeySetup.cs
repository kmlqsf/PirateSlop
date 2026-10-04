using System;
using System.Collections.Generic;
using System.Linq;
using PirateSlop.Ships;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace PirateSlop.EditorTools
{
    public static class ShipMonkeySetup
    {
        const string Model = "Assets/Models/Creatures/ShipMonkey/ShipMonkeyRigged.fbx";
        const string VisualPath = "Assets/Prefabs/Creatures/ShipMonkey.prefab";
        const string ShipPath = "Assets/Resources/Ships/ShipV3Test.prefab";
        const string ControllerPath = "Assets/Animations/ShipMonkey/ShipMonkey.controller";
        static readonly string[] ClipNames = { "Idle", "IdleRail", "Walk", "RailWalk", "ClimbUp", "ClimbDown", "Run", "SitDown", "Sit", "StandUp", "SitDownPerch", "SitPerch", "StandUpPerch", "LegacyIdle", "LegacyWalk", "LegacyRailWalk", "LegacyClimbUp", "LegacyClimbDown", "JumpStart", "JumpAir", "JumpLand" };

        [MenuItem("PirateSlop/Ship Monkey/Use Original Locomotion")]
        public static void UseOriginalLocomotion() => SetLegacyLocomotion(true);

        [MenuItem("PirateSlop/Ship Monkey/Use Quadruped Locomotion")]
        public static void UseQuadrupedLocomotion() => SetLegacyLocomotion(false);

        static void SetLegacyLocomotion(bool value)
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before changing the ship prefab.");
            var root = PrefabUtility.LoadPrefabContents(ShipPath);
            try
            {
                var monkey = root.GetComponent<ShipMonkey>();
                if (monkey == null) throw new InvalidOperationException("Configure the ship monkey first.");
                monkey.UseLegacyLocomotion = value;
                PrefabUtility.SaveAsPrefabAsset(root, ShipPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }

        [MenuItem("PirateSlop/Configure Ship Monkey")]
        public static void ConfigureGameShip()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before configuring the monkey.");
            PrepareVisual();
            var root = PrefabUtility.LoadPrefabContents(ShipPath);
            try
            {
                Configure(root);
                PrefabUtility.SaveAsPrefabAsset(root, ShipPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var config = AssetDatabase.LoadAssetAtPath<Networking.SessionConfig>("Assets/Settings/Networking/SessionConfig.asset");
            config.ProtocolVersion = Math.Max(117, config.ProtocolVersion);
            EditorUtility.SetDirty(config);
            ShipV3ImportSetup.RegisterNetworkPrefab();
            AssetDatabase.SaveAssets();
        }

        static void PrepareVisual()
        {
            EnsureFolder("Assets/Prefabs/Creatures"); EnsureFolder("Assets/Materials/Creatures"); EnsureFolder("Assets/Animations/ShipMonkey");
            var importer = (ModelImporter)AssetImporter.GetAtPath(Model);
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.motionNodeName = "Root";
            importer.importAnimation = true; importer.importBlendShapes = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            var imports = importer.defaultClipAnimations;
            foreach (var clip in imports)
            {
                clip.name = clip.takeName.Split('|').Last();
                clip.loopTime = clip.loopPose = !clip.name.StartsWith("SitDown", StringComparison.Ordinal) && !clip.name.StartsWith("StandUp", StringComparison.Ordinal) && clip.name != "JumpStart" && clip.name != "JumpLand";
                clip.lockRootPositionXZ = clip.lockRootHeightY = clip.lockRootRotation = true;
            }
            importer.clipAnimations = imports;
            importer.SaveAndReimport();
            var clips = AssetDatabase.LoadAllAssetsAtPath(Model).OfType<AnimationClip>().Where(c => ClipNames.Contains(c.name)).ToDictionary(c => c.name);
            if (clips.Count != ClipNames.Length) throw new InvalidOperationException("The monkey model must contain all current and legacy animation clips.");
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var machine = controller.layers[0].stateMachine;
            foreach (string name in ClipNames)
            {
                var state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == name) ?? machine.AddState(name);
                state.motion = clips[name]; state.writeDefaultValues = true;
                if (name == "Idle") machine.defaultState = state;
            }
            EditorUtility.SetDirty(controller);
            string textures = "Assets/Models/Creatures/ShipMonkey/ShipMonkey.fbm/";
            string basePath = AssetDatabase.FindAssets("t:Texture2D", new[] { textures.TrimEnd('/') }).Select(AssetDatabase.GUIDToAssetPath).Single(p => p.Contains("BaseColor"));
            string normalPath = AssetDatabase.FindAssets("t:Texture2D", new[] { textures.TrimEnd('/') }).Select(AssetDatabase.GUIDToAssetPath).Single(p => p.Contains("Normal_Bake"));
            var normalImporter = (TextureImporter)AssetImporter.GetAtPath(normalPath);
            if (normalImporter.textureType != TextureImporterType.NormalMap) { normalImporter.textureType = TextureImporterType.NormalMap; normalImporter.SaveAndReimport(); }
            const string materialPath = "Assets/Materials/Creatures/ShipMonkey.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, materialPath); }
            material.SetColor("_BaseColor", Color.white); material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(basePath));
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath)); material.SetFloat("_BumpScale", .65f); material.EnableKeyword("_NORMALMAP");
            material.SetFloat("_Smoothness", .28f); material.SetFloat("_Metallic", 0f); EditorUtility.SetDirty(material);
            var preview = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            GameObject root = null;
            try
            {
                root = new GameObject("ShipMonkey"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, preview);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Model), preview);
                model.transform.SetParent(root.transform, false);
                model.name = "Model";
                model.transform.localScale = Vector3.one * ( .9f / .8082748f );
                var animator = model.GetComponent<Animator>();
                if (animator == null) animator = model.AddComponent<Animator>();
                animator.avatar = AssetDatabase.LoadAllAssetsAtPath(Model).OfType<Avatar>().FirstOrDefault();
                animator.runtimeAnimatorController = controller; animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
                    renderer.updateWhenOffscreen = false;
                    renderer.localBounds = new Bounds(new Vector3(0,.35f,0), new Vector3(1.2f,1.2f,1.6f));
                }
                PrefabUtility.SaveAsPrefabAsset(root, VisualPath);
            }
            finally
            {
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        public static void Configure(GameObject root)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VisualPath);
            if (prefab == null) return;
            var monkey = root.GetComponent<ShipMonkey>();
            if (monkey == null) monkey = root.AddComponent<ShipMonkey>();
            if (monkey.Visual == null)
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.scene);
                visual.transform.SetParent(root.transform, false);
                visual.name = "ShipMonkey"; monkey.Visual = visual.transform;
            }
            monkey.Animator = monkey.Visual.GetComponentInChildren<Animator>(true);
            var bones = monkey.Visual.GetComponentsInChildren<Transform>(true);
            monkey.Neck = bones.Single(t => t.name == "Neck");
            monkey.Head = bones.Single(t => t.name == "Head");
            monkey.HeadForward = monkey.Head.InverseTransformDirection(monkey.Visual.forward);
            monkey.HeadUp = monkey.Head.InverseTransformDirection(monkey.Visual.up);
            monkey.HeadYawLimit = Mathf.Clamp(monkey.HeadYawLimit, 0f, 30f);
            monkey.HeadPitchLimit = Mathf.Clamp(monkey.HeadPitchLimit, 0f, 15f);
            ConfigureHitbox(monkey, monkey.Visual, false);
            ConfigureHitbox(monkey, monkey.Head, true);
            new RouteBuilder(root, monkey).Build();
            monkey.Visual.localPosition = monkey.Nodes[monkey.StartNode].Position;
            monkey.Visual.localRotation = Quaternion.identity;
            EditorUtility.SetDirty(monkey);
        }

        static void ConfigureHitbox(ShipMonkey monkey, Transform parent, bool head)
        {
            string name = head ? "HeadHitbox" : "BodyHitbox";
            var child = parent.Find(name);
            if (child == null) { child = new GameObject(name).transform; child.SetParent(parent, false); }
            var proxy = child.GetComponent<ShipMonkeyHitbox>();
            if (proxy == null) proxy = child.gameObject.AddComponent<ShipMonkeyHitbox>();
            proxy.Monkey = monkey;
            if (head)
            {
                var shape = child.GetComponent<SphereCollider>();
                if (shape == null) shape = child.gameObject.AddComponent<SphereCollider>();
                shape.isTrigger = true; shape.radius = .16f; shape.center = new Vector3(0, .10f, 0);
            }
            else
            {
                var shape = child.GetComponent<CapsuleCollider>();
                if (shape == null) shape = child.gameObject.AddComponent<CapsuleCollider>();
                shape.isTrigger = true; shape.direction = 2; shape.radius = .22f; shape.height = .65f; shape.center = new Vector3(0, .30f, 0);
            }
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/'); EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }

        sealed class RouteBuilder
        {
            readonly GameObject root;
            readonly ShipMonkey monkey;
            readonly Collider[] colliders;
            readonly MeshFilter[] meshes;
            readonly List<ShipMonkeyNode> nodes = new();
            readonly List<ShipMonkeyLink> links = new();
            readonly HashSet<ulong> edgeKeys = new();
            readonly List<int> decks = new(), rails = new();
            readonly Dictionary<(int,int),int> grid = new();
            Bounds deckBounds;
            const float Cell = .75f;

            public RouteBuilder(GameObject root, ShipMonkey monkey)
            {
                this.root = root; this.monkey = monkey;
                colliders = root.GetComponentsInChildren<Collider>(true).Where(c => c.enabled && !c.isTrigger && c.gameObject.activeInHierarchy && !c.transform.IsChildOf(monkey.Visual)).ToArray();
                meshes = root.GetComponentsInChildren<MeshFilter>(true).Where(m => m.sharedMesh != null && m.gameObject.activeInHierarchy && !m.transform.IsChildOf(monkey.Visual)).ToArray();
            }

            int Add(Vector3 point, ShipMonkeySurface surface, Transform support, ShipLadder ladder = null)
            {
                nodes.Add(new ShipMonkeyNode { Position = point, Surface = surface, Support = support, SupportParent = support.parent, SupportLocalPosition = support.localPosition, Ladder = ladder });
                return nodes.Count - 1;
            }

            void Link(int a, int b, ShipMonkeyMotion motion, Vector3 facing = default)
            {
                if (a < 0 || b < 0 || a == b) return;
                ulong key = ((ulong)(uint)Math.Min(a,b) << 32) | (uint)Math.Max(a,b);
                if (!edgeKeys.Add(key)) return;
                links.Add(new ShipMonkeyLink { A = a, B = b, Motion = motion, Facing = facing });
            }

            Bounds BoundsOf(MeshFilter mesh)
            {
                var mapping = root.transform.worldToLocalMatrix * mesh.transform.localToWorldMatrix;
                var bounds = mesh.sharedMesh.bounds;
                var result = new Bounds(mapping.MultiplyPoint3x4(bounds.center), Vector3.zero);
                for (int i = 0; i < 8; i++) result.Encapsulate(mapping.MultiplyPoint3x4(bounds.center + Vector3.Scale(bounds.extents, new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1))));
                return result;
            }

            Transform Support(Collider collider, Vector3 point)
            {
                var batch = collider.GetComponent<ShipV3CollisionBatch>();
                if (batch == null) return collider.transform;
                Vector3 local = batch.transform.InverseTransformPoint(point);
                int best = -1; float score = float.PositiveInfinity;
                for (int i = 0; i < batch.Sources.Length; i++)
                {
                    if (batch.Sources[i] == null || !batch.Sources[i].gameObject.activeInHierarchy) continue;
                    var bounds = batch.SourceBounds[i];
                    float candidate = (bounds.ClosestPoint(local)-local).sqrMagnitude + (bounds.center-local).sqrMagnitude*.000001f;
                    if (candidate < score) { score = candidate; best = i; }
                }
                return best >= 0 ? batch.Sources[best].transform : collider.transform;
            }

            bool Surface(Vector3 origin, float depth, out Vector3 point, out Transform support)
            {
                point = default; support = null;
                var ray = new Ray(root.transform.TransformPoint(origin), -root.transform.up);
                float best = depth;
                foreach (var collider in colliders)
                {
                    if (!collider.Raycast(ray, out var hit, best)) continue;
                    if (Vector3.Dot(hit.normal,root.transform.up) < .65f) continue;
                    best = hit.distance; point = root.transform.InverseTransformPoint(hit.point); support = Support(collider, hit.point);
                }
                if (support == null) return false;
                string name = support.name;
                if (name == "V3_Transfer_Hold_Doorway_Frame")
                {
                    var frame = support.GetComponent<MeshFilter>();
                    return frame != null && point.y < BoundsOf(frame).min.y + .4f;
                }
                return name.StartsWith("V3_Deck_SternRoomFloor",StringComparison.Ordinal) || name.StartsWith("V3_Deck_MainDeck",StringComparison.Ordinal) || name.StartsWith("V3_Deck_Quarterdeck",StringComparison.Ordinal) || name.StartsWith("V3_Deck_Forecastle",StringComparison.Ordinal) || name.Contains("_Step") || name.EndsWith("MastTop_Platform",StringComparison.Ordinal);
            }

            bool Clear(Vector3 a, Vector3 b)
            {
                Vector3 delta = root.transform.TransformVector(b-a);
                float length = delta.magnitude;
                if (length < .001f) return true;
                var ray = new Ray(root.transform.TransformPoint(a),delta/length);
                foreach (var collider in colliders) if (collider.Raycast(ray,out _,length)) return false;
                return true;
            }

            bool Walkable(Vector3 a, Vector3 b)
            {
                int steps = Mathf.CeilToInt(Vector3.Distance(a,b)/.20f);
                Vector3 previous = a;
                for (int i=1;i<=steps;i++)
                {
                    var sample = Vector3.Lerp(a,b,(float)i/steps);
                    if (!Surface(sample+Vector3.up*.65f,1.0f,out var ground,out _)) return false;
                    if (Mathf.Abs(ground.y-previous.y)>.36f) return false;
                    for (int side=-1;side<=1;side++)
                    {
                        var lateral = Vector3.Cross(Vector3.up,(b-a).normalized)*(.16f*side);
                        if (!Clear(previous+Vector3.up*.30f+lateral,ground+Vector3.up*.30f+lateral) || !Clear(previous+Vector3.up*.68f+lateral,ground+Vector3.up*.68f+lateral)) return false;
                    }
                    previous = ground;
                }
                return true;
            }

            public void Build()
            {
                var deckMeshes = meshes.Where(m => m.name.StartsWith("V3_Deck_MainDeck",StringComparison.Ordinal) || m.name.StartsWith("V3_Deck_Quarterdeck",StringComparison.Ordinal) || m.name.StartsWith("V3_Deck_Forecastle",StringComparison.Ordinal)).ToArray();
                if (deckMeshes.Length == 0) throw new InvalidOperationException("Ship V3 deck geometry is missing.");
                var bounds = BoundsOf(deckMeshes[0]); foreach (var mesh in deckMeshes.Skip(1)) bounds.Encapsulate(BoundsOf(mesh));
                deckBounds = bounds;
                int minX=Mathf.CeilToInt(bounds.min.x/Cell),maxX=Mathf.FloorToInt(bounds.max.x/Cell),minZ=Mathf.CeilToInt(bounds.min.z/Cell),maxZ=Mathf.FloorToInt(bounds.max.z/Cell);
                for (int x=minX;x<=maxX;x++) for(int z=minZ;z<=maxZ;z++)
                {
                    if (!Surface(new Vector3(x*Cell,bounds.max.y+1f,z*Cell),bounds.size.y+2f,out var point,out var support)) continue;
                    if (point.y<bounds.min.y-.2f) continue;
                    int node=Add(point+Vector3.up*.012f,ShipMonkeySurface.Deck,support); decks.Add(node); grid[(x,z)]=node;
                    nodes[node].RestSpot = x % 3 == 0 && z % 3 == 0;
                }
                foreach(var entry in grid)
                    foreach(var offset in new[]{(1,0),(0,1),(1,1),(1,-1)})
                        if(grid.TryGetValue((entry.Key.Item1+offset.Item1,entry.Key.Item2+offset.Item2),out int next) && Walkable(nodes[entry.Value].Position,nodes[next].Position)) Link(entry.Value,next,ShipMonkeyMotion.Walk);
                BuildLowerDeck(meshes);
                BuildDoorway();
                BuildRails();
                BuildRigging();
                BuildProps();
                Vector3 spawn=root.transform.InverseTransformPoint(root.GetComponent<ShipV3Features>().RespawnPoint.position);
                int start=decks.OrderBy(i=>(nodes[i].Position-spawn).sqrMagnitude).First();
                var connected=new HashSet<int>{start};var queue=new Queue<int>();queue.Enqueue(start);
                var adjacency=Enumerable.Range(0,nodes.Count).Select(_=>new List<int>()).ToArray();
                foreach(var edge in links){adjacency[edge.A].Add(edge.B);adjacency[edge.B].Add(edge.A);}
                while(queue.Count>0)foreach(int next in adjacency[queue.Dequeue()])if(connected.Add(next))queue.Enqueue(next);
                var remap=new Dictionary<int,int>();var kept=new List<ShipMonkeyNode>();
                for(int i=0;i<nodes.Count;i++)if(connected.Contains(i)){remap[i]=kept.Count;kept.Add(nodes[i]);}
                monkey.Nodes=kept.ToArray();monkey.StartNode=remap[start];
                monkey.Links=links.Where(e=>connected.Contains(e.A)&&connected.Contains(e.B)).Select(e=>new ShipMonkeyLink{A=remap[e.A],B=remap[e.B],Motion=e.Motion,Facing=e.Facing}).ToArray();
                if (!monkey.Nodes.Any(n=>n.Surface==ShipMonkeySurface.Rail) || monkey.Nodes.Count(n=>n.Surface==ShipMonkeySurface.Nest)<4) throw new InvalidOperationException("Monkey routes must connect the deck, rails and both crow nests.");
            }

            void BuildLowerDeck(MeshFilter[] deckMeshes)
            {
                var lowerGrid = new Dictionary<(int,int),int>();
                foreach (var mesh in deckMeshes.Where(m => m.name.StartsWith("V3_Deck_MainDeck", StringComparison.Ordinal) || m.name.StartsWith("V3_Deck_SternRoomFloor", StringComparison.Ordinal)))
                {
                    var bounds = BoundsOf(mesh);
                    for (int x = Mathf.CeilToInt(bounds.min.x / Cell); x <= Mathf.FloorToInt(bounds.max.x / Cell); x++)
                    for (int z = Mathf.CeilToInt(bounds.min.z / Cell); z <= Mathf.FloorToInt(bounds.max.z / Cell); z++)
                    {
                        if (!Surface(new Vector3(x * Cell, bounds.max.y + .45f, z * Cell), bounds.size.y + .8f, out var point, out var support)) continue;
                        point += Vector3.up * .012f;
                        int existing = decks.Where(i => (nodes[i].Position - point).sqrMagnitude < .01f).DefaultIfEmpty(-1).First();
                        int node = existing >= 0 ? existing : Add(point, ShipMonkeySurface.Deck, support);
                        if (existing < 0) decks.Add(node);
                        lowerGrid[(x,z)] = node;
                    }
                }
                foreach (var entry in lowerGrid)
                    foreach (var offset in new[]{(1,0),(0,1),(1,1),(1,-1)})
                        if (lowerGrid.TryGetValue((entry.Key.Item1+offset.Item1,entry.Key.Item2+offset.Item2),out int next) && Walkable(nodes[entry.Value].Position,nodes[next].Position)) Link(entry.Value,next,ShipMonkeyMotion.Walk);
            }

            void BuildDoorway()
            {
                var frame = meshes.FirstOrDefault(m => m.name == "V3_Transfer_Hold_Doorway_Frame");
                if (frame == null) return;
                var bounds = BoundsOf(frame);
                int previous = -1;
                for (int i = 0; i <= 8; i++)
                {
                    var origin = new Vector3(bounds.center.x + .25f, bounds.min.y + .65f, bounds.center.z + 1.2f - i * .3f);
                    if (!Surface(origin, 1.2f, out var point, out var support)) { previous = -1; continue; }
                    int node = Add(point + Vector3.up * .012f, ShipMonkeySurface.Deck, support);
                    if (previous >= 0 && Walkable(nodes[previous].Position, nodes[node].Position)) Link(previous, node, ShipMonkeyMotion.Walk);
                    foreach (int deck in decks.Where(d => (nodes[d].Position - nodes[node].Position).sqrMagnitude < 1.3f).ToArray())
                        if (Walkable(nodes[deck].Position, nodes[node].Position)) Link(deck, node, ShipMonkeyMotion.Walk);
                    decks.Add(node); previous = node;
                }
            }

            bool JumpClear(Vector3 a, Vector3 b, Transform support)
            {
                Vector3 previous = a + Vector3.up * .36f;
                float height = Mathf.Max(.8f, Mathf.Abs(b.y - a.y) * .25f);
                for (int i = 1; i <= 16; i++)
                {
                    float t = i / 16f;
                    Vector3 next = Vector3.Lerp(a, b, t) + Vector3.up * (.36f + 4f * height * t * (1f - t));
                    Vector3 delta = root.transform.TransformVector(next - previous);
                    var ray = new Ray(root.transform.TransformPoint(previous), delta.normalized);
                    foreach (var collider in colliders)
                        if (collider.Raycast(ray, out var hit, delta.magnitude))
                        {
                            var part = Support(collider, hit.point);
                            if (part != support && !part.IsChildOf(support)) return false;
                        }
                    previous = next;
                }
                return true;
            }

            void BuildProps()
            {
                foreach (var mesh in meshes.Where(m => m.name == "V9_Anchor_Starboard" || m.name == "V8_Bell_Body" || m.name == "V17_Dice_Game_Barrel" || m.name.StartsWith("V3_Lamp_", StringComparison.Ordinal) && m.name.EndsWith("_Body", StringComparison.Ordinal)))
                {
                    var bounds = BoundsOf(mesh);
                    Vector3 point = bounds.center; point.y = bounds.max.y + .012f;
                    if (mesh.name == "V17_Dice_Game_Barrel") point.x += .36f;
                    if (mesh.name == "V8_Bell_Body") { point.y = bounds.min.y + .10f; point.z = bounds.min.z + .025f; }
                    var candidates = decks.Concat(rails).Where(i => Mathf.Abs(nodes[i].Position.y - point.y) < 3.6f && Vector2.Distance(new Vector2(point.x, point.z), new Vector2(nodes[i].Position.x, nodes[i].Position.z)) < 3.5f)
                        .OrderBy(i => (nodes[i].Position - point).sqrMagnitude).Where(i => JumpClear(nodes[i].Position, point, mesh.transform)).Take(3).ToArray();
                    if (candidates.Length == 0) continue;
                    int prop = Add(point, ShipMonkeySurface.Prop, mesh.transform);
                    nodes[prop].SupportShip = root.transform;
                    nodes[prop].SupportLocalPoint = mesh.transform.InverseTransformPoint(root.transform.TransformPoint(point));
                    foreach (int next in candidates) Link(next, prop, ShipMonkeyMotion.Hop);
                }
            }

            void BuildRails()
            {
                foreach(var mesh in meshes.Where(m=>m.name.StartsWith("V3_Rail_Cap",StringComparison.Ordinal)))
                {
                    var bounds=BoundsOf(mesh);bool alongX=bounds.size.x>bounds.size.z;
                    float length=alongX?bounds.size.x:bounds.size.z;int steps=Mathf.Max(1,Mathf.CeilToInt(length/.55f));int previous=-1;
                    for(int j=0;j<=steps;j++)
                    {
                        var point=bounds.center;point.y=bounds.max.y+.012f;
                        if(alongX)point.x=Mathf.Lerp(bounds.min.x+.06f,bounds.max.x-.06f,(float)j/steps);else point.z=Mathf.Lerp(bounds.min.z+.06f,bounds.max.z-.06f,(float)j/steps);
                        int next=Add(point,ShipMonkeySurface.Rail,mesh.transform);rails.Add(next);Link(previous,next,ShipMonkeyMotion.RailWalk);previous=next;
                        nodes[next].RestSpot = length > .6f && j == steps / 2;
                        float x = (point.x - deckBounds.center.x) / Mathf.Max(.1f, deckBounds.extents.x);
                        float z = (point.z - deckBounds.center.z) / Mathf.Max(.1f, deckBounds.extents.z);
                        nodes[next].SeaFacing = Mathf.Abs(x) > Mathf.Abs(z) ? Vector3.right * Mathf.Sign(x) : Vector3.forward * Mathf.Sign(z);
                    }
                }
                foreach(int rail in rails)
                {
                    foreach(int other in rails.Where(i=>i!=rail && Mathf.Abs(nodes[i].Position.y-nodes[rail].Position.y)<.30f && (nodes[i].Position-nodes[rail].Position).sqrMagnitude<.72f).OrderBy(i=>(nodes[i].Position-nodes[rail].Position).sqrMagnitude).Take(3))Link(rail,other,ShipMonkeyMotion.RailWalk);
                    int deck=decks.Where(i=>nodes[rail].Position.y-nodes[i].Position.y>=-.08f && nodes[rail].Position.y-nodes[i].Position.y<1.15f && Vector2.Distance(new Vector2(nodes[i].Position.x,nodes[i].Position.z),new Vector2(nodes[rail].Position.x,nodes[rail].Position.z))<1.1f).OrderBy(i=>(nodes[i].Position-nodes[rail].Position).sqrMagnitude).DefaultIfEmpty(-1).First();
                    if(deck>=0 && Clear(nodes[deck].Position+Vector3.up*1.0f,nodes[rail].Position+Vector3.up*.55f))Link(deck,rail,ShipMonkeyMotion.Hop);
                }
            }

            void BuildRigging()
            {
                var nestRings = new Dictionary<string,List<int>>();
                foreach(var ladder in root.GetComponentsInChildren<ShipLadder>(true).Where(l=>l.FollowRopePath&&!l.BoardingAccess))
                {
                    string mast=ladder.name.Contains("Fore")?"Fore":"Main";
                    var platform=meshes.Single(m=>m.name=="V3_"+mast+"_MastTop_Platform");
                    var platformBounds=BoundsOf(platform);
                    Vector3 facing=-root.transform.InverseTransformDirection(ladder.transform.forward);
                    int previous=-1,first=-1;int steps=Mathf.CeilToInt(ladder.Height/1.0f);
                    for(int j=0;j<=steps;j++)
                    {
                        float height=ladder.Height*j/steps;
                        Vector3 local=new Vector3(ladder.RopeSide(height),height,ladder.RopeDepth(height)+.18f);
                        Vector3 point=root.transform.InverseTransformPoint(ladder.transform.TransformPoint(local));
                        int next=Add(point,ShipMonkeySurface.Rigging,ladder.transform,ladder);
                        if(first<0)first=next;
                        Link(previous,next,ShipMonkeyMotion.ClimbUp,facing);previous=next;
                    }
                    int deck=decks.Where(i=>(nodes[i].Position-nodes[first].Position).sqrMagnitude<5f).OrderBy(i=>(nodes[i].Position-nodes[first].Position).sqrMagnitude).DefaultIfEmpty(-1).First();
                    Link(deck,first,ShipMonkeyMotion.Hop);
                    Vector3 exit=root.transform.InverseTransformPoint(ladder.transform.TransformPoint(ladder.ExitPoint));exit.y=platformBounds.max.y+.012f;
                    int exitNode=Add(exit,ShipMonkeySurface.Nest,platform.transform,ladder);Link(previous,exitNode,ShipMonkeyMotion.ClimbUp,facing);
                    if (!nestRings.TryGetValue(mast,out var ring))
                    {
                        ring=new List<int>(); nestRings[mast]=ring;
                        for(int j=0;j<8;j++)
                        {
                            float angle=j*Mathf.PI/4;
                            Vector3 point=platformBounds.center+new Vector3(Mathf.Cos(angle)*1.35f,0,Mathf.Sin(angle)*1.35f);point.y=platformBounds.max.y+.012f;
                            int next=Add(point,ShipMonkeySurface.Nest,platform.transform);ring.Add(next);
                            nodes[next].RestSpot = j % 2 == 0;
                            nodes[next].SeaFacing = new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                        }
                        for(int j=0;j<ring.Count;j++)Link(ring[j],ring[(j+1)%ring.Count],ShipMonkeyMotion.Walk);
                    }
                    int closest=ring.OrderBy(i=>(nodes[i].Position-exit).sqrMagnitude).First();Link(exitNode,closest,ShipMonkeyMotion.Walk);
                }
            }
        }
    }
}
