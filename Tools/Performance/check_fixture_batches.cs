if (UnityEditor.EditorApplication.isPlaying || UnityEditor.BuildPipeline.isBuildingPlayer)
    throw new System.InvalidOperationException("Run after the build, outside Play Mode.");
var root = UnityEditor.PrefabUtility.LoadPrefabContents("Assets/Resources/Ships/ShipV3Test.prefab");
var checks = new System.Collections.Generic.List<object>();
try
{
    var batches = root.GetComponentsInChildren<PirateSlop.Ships.ShipV3RenderBatch>(true)
        .Where(b => b.name.StartsWith("ShipV3FixtureBatch_", System.StringComparison.Ordinal)).ToArray();
    foreach (var batch in batches)
    {
        var expected = PirateSlop.Ships.ShipV3RenderBatch.BuildMesh(batch.Sources, batch.SharedMaterial, batch.BatchAnchor, true);
        try
        {
            bool geometry = expected.vertices.SequenceEqual(batch.CachedMesh.vertices) && expected.triangles.SequenceEqual(batch.CachedMesh.triangles);
            bool attributes = expected.normals.SequenceEqual(batch.CachedMesh.normals) && expected.tangents.SequenceEqual(batch.CachedMesh.tangents)
                && expected.uv.SequenceEqual(batch.CachedMesh.uv) && expected.uv2.SequenceEqual(batch.CachedMesh.uv2)
                && expected.colors32.SequenceEqual(batch.CachedMesh.colors32);
            if (!geometry || !attributes) throw new System.Exception("Cached geometry mismatch: " + batch.name);
            checks.Add(new { batch.name, sources = batch.Sources.Length, triangles = expected.triangles.Length / 3, geometry, attributes });
        }
        finally { UnityEngine.Object.DestroyImmediate(expected); }
    }
    var sources = batches.SelectMany(b => b.Sources).Distinct().ToArray();
    foreach (var source in sources)
    {
        var owners = batches.Where(b => b.Sources.Contains(source)).ToArray();
        if (source.enabled || source.sharedMaterials.Any(m => !owners.Any(b => b.SharedMaterial == m)))
            throw new System.Exception("Source enabled or material missing: " + source.name);
    }
    var result = Newtonsoft.Json.JsonConvert.SerializeObject(new { kind = "fixture-batch-cache-check", groups = batches.Length,
        sources = sources.Length, fullMaterialCoverage = true, checks }, Newtonsoft.Json.Formatting.Indented);
    System.IO.File.WriteAllText("Docs/Performance/Reports/20261016-fixture-batch-cache-check.json", result);
}
finally { UnityEditor.PrefabUtility.UnloadPrefabContents(root); }

var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
var editorType = typeof(UnityEditor.SceneManagement.EditorSceneManager);
var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
var getTarget = editorType.GetMethod("GetTargetSceneForNewGameObjects", flags);
var setTarget = editorType.GetMethod("SetTargetSceneForNewGameObjects", flags, null, new[] { typeof(UnityEngine.SceneManagement.Scene) }, null);
var clearTarget = editorType.GetMethod("ClearTargetSceneForNewGameObjects", flags);
var previous = (UnityEngine.SceneManagement.Scene)getTarget.Invoke(null, null);
setTarget.Invoke(null, new object[] { scene });
UnityEngine.GameObject testRoot = null;
var resources = new System.Collections.Generic.List<UnityEngine.Object>();
var testBatches = new System.Collections.Generic.List<PirateSlop.Ships.ShipV3RenderBatch>();
var methods = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
var batchType = typeof(PirateSlop.Ships.ShipV3RenderBatch);
System.Action<PirateSlop.Ships.ShipV3RenderBatch, string> invoke = (b, method) => batchType.GetMethod(method, methods).Invoke(b, null);
try
{
    testRoot = new UnityEngine.GameObject("FixtureBatchCheck");
    var sourceObject = new UnityEngine.GameObject("TwoMaterialSource");
    sourceObject.transform.SetParent(testRoot.transform, false);
    var sourceMesh = new UnityEngine.Mesh { name = "FixtureCheckSource", vertices = new[] {
        new UnityEngine.Vector3(0,0,0), new UnityEngine.Vector3(1,0,0), new UnityEngine.Vector3(0,1,0),
        new UnityEngine.Vector3(2,0,0), new UnityEngine.Vector3(3,0,0), new UnityEngine.Vector3(2,1,0) } };
    resources.Add(sourceMesh);
    sourceMesh.subMeshCount = 2;
    sourceMesh.SetTriangles(new[] { 0, 1, 2 }, 0);
    sourceMesh.SetTriangles(new[] { 3, 4, 5 }, 1);
    sourceMesh.RecalculateNormals();
    sourceObject.AddComponent<UnityEngine.MeshFilter>().sharedMesh = sourceMesh;
    var sourceRenderer = sourceObject.AddComponent<UnityEngine.MeshRenderer>();
    var materials = new[] { new UnityEngine.Material(UnityEngine.Shader.Find("Universal Render Pipeline/Lit")),
        new UnityEngine.Material(UnityEngine.Shader.Find("Universal Render Pipeline/Lit")) };
    resources.AddRange(materials);
    sourceRenderer.sharedMaterials = materials;
    for (int i = 0; i < 2; i++)
    {
        var node = new UnityEngine.GameObject("FixtureCheckBatch" + i);
        node.transform.SetParent(testRoot.transform, false);
        var b = node.AddComponent<PirateSlop.Ships.ShipV3RenderBatch>();
        b.Sources = new[] { sourceRenderer };
        b.SharedMaterial = materials[i];
        b.BatchAnchor = testRoot.transform;
        b.CachedMesh = PirateSlop.Ships.ShipV3RenderBatch.BuildMesh(b.Sources, b.SharedMaterial, b.BatchAnchor, true);
        resources.Add(b.CachedMesh);
        testBatches.Add(b);
        invoke(b, "OnEnable");
        if (b.CachedMesh.triangles.Length != 3) throw new System.Exception("Material split failed");
    }
    System.Action<bool> checkVisibility = visible => {
        foreach (var b in testBatches)
        {
            batchType.GetField("nextVisibilityCheck", methods).SetValue(b, -1f);
            invoke(b, "LateUpdate");
            var mesh = b.GetComponent<UnityEngine.MeshFilter>().sharedMesh;
            if (b.GetComponent<UnityEngine.MeshRenderer>().enabled != visible || mesh.triangles.Length != (visible ? 3 : 0))
                throw new System.Exception("Visibility or restored triangle range mismatch");
        }
    };
    sourceObject.SetActive(false);
    checkVisibility(false);
    sourceObject.SetActive(true);
    checkVisibility(true);
    sourceRenderer.forceRenderingOff = true;
    checkVisibility(false);
    sourceRenderer.forceRenderingOff = false;
    checkVisibility(true);
    invoke(testBatches[0], "OnDisable");
    if (sourceRenderer.enabled) throw new System.Exception("First owner released a shared source too early");
    invoke(testBatches[0], "OnDestroy");
    if (sourceRenderer.enabled) throw new System.Exception("Repeated release changed the shared source");
    invoke(testBatches[1], "OnDisable");
    if (!sourceRenderer.enabled) throw new System.Exception("Last owner did not restore the source");
    System.IO.File.WriteAllText("Docs/Performance/Reports/20261016-fixture-lifecycle-check.json",
        "{\"kind\":\"native-editor-preview\",\"materialSplit\":true,\"hideRestore\":true,\"forceOffRestore\":true,\"sharedOwners\":true,\"idempotentRelease\":true,\"playMode\":false,\"networkTest\":false}");
}
finally
{
    foreach (var b in testBatches) if (b != null) invoke(b, "OnDisable");
    if (testRoot != null) UnityEngine.Object.DestroyImmediate(testRoot);
    foreach (var resource in resources) if (resource != null) UnityEngine.Object.DestroyImmediate(resource);
    clearTarget.Invoke(null, null);
    if (previous.IsValid() && previous.isLoaded && UnityEditor.SceneManagement.EditorSceneManager.IsPreviewScene(previous))
        setTarget.Invoke(null, new object[] { previous });
    UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
}
return "18 cached fixture groups and synthetic shared-owner/visibility lifecycle passed; Editor preview only.";
