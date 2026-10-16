var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/Resources/Ships/ShipV3Test.prefab");
var nets = prefab.GetComponentsInChildren<PirateSlop.Ships.ShipV3ClothMotion>(true).Where(c => c.PinTop && c.GetComponent<UnityEngine.MeshFilter>() != null).ToArray();
var preview = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
var editor = typeof(UnityEditor.SceneManagement.EditorSceneManager);
var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
var get = editor.GetMethod("GetTargetSceneForNewGameObjects", flags);
var set = editor.GetMethod("SetTargetSceneForNewGameObjects", flags, null, new[] { typeof(UnityEngine.SceneManagement.Scene) }, null);
var clear = editor.GetMethod("ClearTargetSceneForNewGameObjects", flags);
var previous = (UnityEngine.SceneManagement.Scene)get.Invoke(null, null);
set.Invoke(null, new object[] { preview });
double maximumWorldError = 0;
int poses = 0;
try
{
    foreach (var net in nets)
    {
        UnityEngine.GameObject node = null;
        UnityEngine.Mesh mesh = null, baked = null;
        try
        {
            node = new UnityEngine.GameObject("PinnedClothPrototype");
            var renderer = node.AddComponent<UnityEngine.SkinnedMeshRenderer>();
            mesh = UnityEngine.Object.Instantiate(net.GetComponent<UnityEngine.MeshFilter>().sharedMesh);
            var rest = mesh.vertices;
            var axis = net.transform.InverseTransformDirection(prefab.transform.up).normalized;
            float minimum = rest.Min(v => UnityEngine.Vector3.Dot(v, axis));
            float maximum = rest.Max(v => UnityEngine.Vector3.Dot(v, axis));
            var weights = rest.Select(v => 1f - UnityEngine.Mathf.InverseLerp(minimum, maximum, UnityEngine.Vector3.Dot(v, axis))).ToArray();
            mesh.boneWeights = weights.Select(w => new UnityEngine.BoneWeight { boneIndex0 = 0, boneIndex1 = 1, weight0 = 1f - w, weight1 = w }).ToArray();
            mesh.bindposes = new[] { UnityEngine.Matrix4x4.identity, UnityEngine.Matrix4x4.identity };
            var fixedBone = new UnityEngine.GameObject("Fixed").transform;
            var movingBone = new UnityEngine.GameObject("Moving").transform;
            fixedBone.SetParent(node.transform, false);
            movingBone.SetParent(node.transform, false);
            renderer.bones = new[] { fixedBone, movingBone };
            renderer.rootBone = node.transform;
            renderer.quality = UnityEngine.SkinQuality.Bone2;
            renderer.sharedMesh = mesh;
            renderer.localBounds = mesh.bounds;
            baked = new UnityEngine.Mesh();
            float units = UnityEngine.Mathf.Max(.01f, net.transform.lossyScale.magnitude / UnityEngine.Mathf.Sqrt(3f));
            for (int pose = 0; pose < 12; pose++)
            {
                var sway = new UnityEngine.Vector3(UnityEngine.Mathf.Sin(pose * 1.7f), UnityEngine.Mathf.Cos(pose * .6f), UnityEngine.Mathf.Sin(pose * .9f)) * (.2f / units);
                movingBone.localPosition = sway;
                renderer.BakeMesh(baked, true);
                var result = baked.vertices;
                for (int i = 0; i < result.Length; i++)
                {
                    var difference = result[i] - (rest[i] + sway * weights[i]);
                    maximumWorldError = System.Math.Max(maximumWorldError, net.transform.TransformVector(difference).magnitude);
                }
                poses++;
            }
        }
        finally
        {
            if (node != null) UnityEngine.Object.DestroyImmediate(node);
            if (mesh != null) UnityEngine.Object.DestroyImmediate(mesh);
            if (baked != null) UnityEngine.Object.DestroyImmediate(baked);
        }
    }
}
finally
{
    clear.Invoke(null, null);
    if (previous.IsValid() && previous.isLoaded && UnityEditor.SceneManagement.EditorSceneManager.IsPreviewScene(previous)) set.Invoke(null, new object[] { previous });
    UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(preview);
}
var report = Newtonsoft.Json.JsonConvert.SerializeObject(new { kind = "pinned-cloth-two-bone-prototype", nets = nets.Length, poses, maximumWorldError,
    editorPreviewOnly = true, gpuPerformanceMeasured = false });
System.IO.File.WriteAllText("Docs/Performance/Reports/20261016-pinned-cloth-prototype.json", report);
if (nets.Length == 0 || maximumWorldError > .0001) throw new System.Exception(report);
return report;
