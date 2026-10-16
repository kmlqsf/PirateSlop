if (UnityEditor.EditorApplication.isPlaying || UnityEditor.BuildPipeline.isBuildingPlayer)
    throw new System.InvalidOperationException("Run outside Play Mode and after the build.");
var root = UnityEditor.PrefabUtility.LoadPrefabContents("Assets/Resources/Ships/ShipV3Test.prefab");
var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
var type = typeof(PirateSlop.Ships.ShipV3ClothMotion);
int poses = 0, vertices = 0;
double maximumWorldError = 0;
try
{
    foreach (var motion in root.GetComponentsInChildren<PirateSlop.Ships.ShipV3ClothMotion>(true).Where(c => c.GpuPinnedMotion))
    {
        var skin = motion.GetComponent<UnityEngine.SkinnedMeshRenderer>();
        var source = motion.GetComponent<UnityEngine.MeshFilter>().sharedMesh;
        var rest = source.vertices;
        var axis = motion.transform.InverseTransformDirection(root.transform.up).normalized;
        float minimum = rest.Min(v => UnityEngine.Vector3.Dot(v, axis));
        float maximum = rest.Max(v => UnityEngine.Vector3.Dot(v, axis));
        bool boardingNet = motion.PinTop && motion.name.StartsWith("V3_Transfer_Boarding_Net_", System.StringComparison.Ordinal);
        var weights = rest.Select(v =>
        {
            float pin = UnityEngine.Mathf.InverseLerp(minimum, maximum, UnityEngine.Vector3.Dot(v, axis));
            return boardingNet ? 1f - UnityEngine.Mathf.SmoothStep(0f, 1f, UnityEngine.Mathf.Clamp01(pin / .82f)) : 1f - pin;
        }).ToArray();
        UnityEngine.Mesh baked = null;
        try
        {
            type.GetMethod("Awake", flags).Invoke(motion, null);
            type.GetMethod("Initialize", flags).Invoke(motion, null);
            if (skin.sharedMesh == source || skin.bones.Length != 2 || skin.sharedMesh.triangles.Length != source.triangles.Length)
                throw new System.Exception("GPU net did not initialize an independent two-bone mesh");
            var bone = (UnityEngine.Transform)type.GetField("pinnedBone", flags).GetValue(motion);
            float units = UnityEngine.Mathf.Max(.01f, motion.transform.lossyScale.magnitude / UnityEngine.Mathf.Sqrt(3f));
            baked = new UnityEngine.Mesh();
            for (int pose = 0; pose < 12; pose++)
            {
                var sway = new UnityEngine.Vector3(UnityEngine.Mathf.Sin(pose * 1.7f), UnityEngine.Mathf.Cos(pose * .6f), UnityEngine.Mathf.Sin(pose * .9f)) * (.2f / units);
                bone.localPosition = sway;
                skin.BakeMesh(baked, true);
                var result = baked.vertices;
                for (int i = 0; i < rest.Length; i++)
                    maximumWorldError = System.Math.Max(maximumWorldError, motion.transform.TransformVector(result[i] - (rest[i] + sway * weights[i])).magnitude);
                poses++;
                vertices += rest.Length;
            }
        }
        finally
        {
            var mesh = (UnityEngine.Mesh)type.GetField("mesh", flags).GetValue(motion);
            var bones = (UnityEngine.Transform)type.GetField("windRoot", flags).GetValue(motion);
            type.GetField("mesh", flags).SetValue(motion, null);
            type.GetField("windRoot", flags).SetValue(motion, null);
            if (mesh != null) UnityEngine.Object.DestroyImmediate(mesh);
            if (baked != null) UnityEngine.Object.DestroyImmediate(baked);
            if (bones != null) UnityEngine.Object.DestroyImmediate(bones.gameObject);
            skin.sharedMesh = source;
        }
    }
}
finally { UnityEditor.PrefabUtility.UnloadPrefabContents(root); }
var report = Newtonsoft.Json.JsonConvert.SerializeObject(new { kind = "actual-pinned-cloth-initialize", poses, vertices, maximumWorldError,
    editorPreviewOnly = true, networkTest = false });
System.IO.File.WriteAllText("Docs/Performance/Reports/20261016-pinned-cloth-runtime-check.json", report);
if (poses != 24 || maximumWorldError > .0001) throw new System.Exception(report);
return report;
