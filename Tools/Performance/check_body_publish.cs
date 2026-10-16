var editor = typeof(UnityEditor.SceneManagement.EditorSceneManager);
var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
var getTarget = editor.GetMethod("GetTargetSceneForNewGameObjects", flags);
var setTarget = editor.GetMethod("SetTargetSceneForNewGameObjects", flags, null, new[] { typeof(UnityEngine.SceneManagement.Scene) }, null);
var clearTarget = editor.GetMethod("ClearTargetSceneForNewGameObjects", flags);
var previous = (UnityEngine.SceneManagement.Scene)getTarget.Invoke(null, null);
var scenes = new[] { UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene(), UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene() };
var roots = new UnityEngine.Rigidbody[2];
var children = new UnityEngine.Transform[2][];var scratch = new[] { new System.Collections.Generic.List<UnityEngine.Rigidbody>(64), new System.Collections.Generic.List<UnityEngine.Rigidbody>(64) };
var failures = new System.Collections.Generic.List<string>();
try
{
    for (int test = 0; test < 2; test++)
    {
        setTarget.Invoke(null, new object[] { scenes[test] });
        var root = new UnityEngine.GameObject("ShipPoseFixture");
        roots[test] = root.AddComponent<UnityEngine.Rigidbody>();
        roots[test].isKinematic = true;
        roots[test].interpolation = UnityEngine.RigidbodyInterpolation.None;
        var nodes = new System.Collections.Generic.List<UnityEngine.Transform>();
        for (int i = 0; i < 128; i++)
        {
            var part = new UnityEngine.GameObject("Surface" + i);
            part.transform.SetParent(root.transform, false);
            part.transform.localPosition = new UnityEngine.Vector3((i % 8) * 2, 0, (i / 8) * 2);
            part.AddComponent<UnityEngine.BoxCollider>();
            nodes.Add(part.transform);
        }
        for (int i = 0; i < 2; i++)
        {
            var part = new UnityEngine.GameObject("NestedBody" + i);
            part.transform.SetParent(i == 0 ? root.transform : nodes[nodes.Count - 1], false);
            part.transform.localPosition = new UnityEngine.Vector3(3 + i * 2, 3, 4);
            var body = part.AddComponent<UnityEngine.Rigidbody>();
            body.isKinematic = i == 0;
            body.useGravity = false;
            part.AddComponent<UnityEngine.SphereCollider>();
            nodes.Add(part.transform);
        }
        children[test] = nodes.ToArray();
    }
    UnityEngine.Physics.SyncTransforms();
    int queries = 0;
    for (int sample = 0; sample < 24; sample++)
    {
        for(int test = 0; test < 2; test++)
        {
            if(sample == 6) children[test][0].gameObject.AddComponent<UnityEngine.Rigidbody>().isKinematic = true;
            if(sample == 9) children[test][129].SetParent(null, true);
            if(sample == 12) children[test][129].SetParent(children[test][128], true);
            if(sample == 15) children[test][128].gameObject.SetActive(false);
            if(sample == 18) children[test][128].gameObject.SetActive(true);
            roots[test].interpolation = sample == 19 ? UnityEngine.RigidbodyInterpolation.Interpolate : UnityEngine.RigidbodyInterpolation.None;
            if(sample == 20)
            {
                var node = new UnityEngine.GameObject("ControllerFallback");
                node.transform.SetParent(roots[test].transform, false);
                node.transform.localPosition = new UnityEngine.Vector3(2, 4, 2);
                node.AddComponent<UnityEngine.CharacterController>();
                var old = children[test];
                System.Array.Resize(ref old, old.Length + 1);
                old[old.Length - 1] = node.transform;
                children[test] = old;
            }
        }
        var position = new UnityEngine.Vector3(1200 + sample * .15f, -sample * 4, -1300 + sample * .2f);
        var rotation = UnityEngine.Quaternion.Euler(sample * 3, sample * 17, sample * -2);
        for (int test = 0; test < 2; test++)
        {
            var body = roots[test];
            PirateSlop.Ships.ShipBodyPose.Enabled = test != 0; PirateSlop.Ships.ShipBodyPose.Apply(body, position, rotation, scratch[test], true);
        }
        if ((roots[0].transform.position - roots[1].transform.position).sqrMagnitude > .000004f || UnityEngine.Quaternion.Angle(roots[0].transform.rotation, roots[1].transform.rotation) > .001f) failures.Add("Root pose " + sample);
        for (int i = 0; i < children[0].Length; i++)
            if ((children[0][i].position - children[1][i].position).sqrMagnitude > .000004f) failures.Add("Child pose " + sample + ":" + i);
        UnityEngine.Physics.SyncTransforms();
        for (int i = 0; i < children[0].Length; i++)
        {
            var a = children[0][i].GetComponent<UnityEngine.Collider>().bounds;
            var b = children[1][i].GetComponent<UnityEngine.Collider>().bounds;
            if ((a.center - b.center).sqrMagnitude > .000004f || (a.size - b.size).sqrMagnitude > .000004f) failures.Add("Collider bounds " + sample + ":" + i);
            var rb0 = children[0][i].GetComponent<UnityEngine.Rigidbody>();
            var rb1 = children[1][i].GetComponent<UnityEngine.Rigidbody>();
            if (rb0 != null && (rb0.position - rb1.position).sqrMagnitude > .000004f) failures.Add("Nested physics pose " + sample + ":" + i);
        }
        for (int i = 0; i < 8; i++)
        {
            var origin = roots[0].transform.TransformPoint(new UnityEngine.Vector3(i * 2, 5, 0));
            bool a = scenes[0].GetPhysicsScene().Raycast(origin, -roots[0].transform.up, out var hit0, 10);
            bool b = scenes[1].GetPhysicsScene().Raycast(origin, -roots[1].transform.up, out var hit1, 10);
            if (a != b || a && (UnityEngine.Mathf.Abs(hit0.distance - hit1.distance) > .002f || (hit0.normal - hit1.normal).sqrMagnitude > .000001f)) failures.Add("Query " + sample + ":" + i);
            queries++;
        }
    }
    for(int test=0;test<2;test++)
        {
            var controller = roots[test].GetComponentInChildren<UnityEngine.CharacterController>();
            if(controller!=null) UnityEngine.Object.DestroyImmediate(controller.gameObject);
        }
    var elapsed = new double[2];
    for (int test = 0; test < 2; test++)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        for (int sample = 0; sample < 200; sample++)
        {
            var position = new UnityEngine.Vector3(1200 + sample * .01f, -90, -1300);
            var rotation = UnityEngine.Quaternion.Euler(20, sample * .1f, -10);
            PirateSlop.Ships.ShipBodyPose.Enabled = test != 0; PirateSlop.Ships.ShipBodyPose.Apply(roots[test], position, rotation, scratch[test], true);
            UnityEngine.Physics.SyncTransforms();
        }
        elapsed[test] = watch.Elapsed.TotalMilliseconds;
    }
    return new { samples = 24, children = children[0].Length, queries, failures = failures.Take(16).ToArray(), totalFailures = failures.Count, loopMilliseconds = elapsed };
}
finally
{
    PirateSlop.Ships.ShipBodyPose.Enabled = true; clearTarget.Invoke(null, null);
    if (previous.IsValid() && previous.isLoaded && UnityEditor.SceneManagement.EditorSceneManager.IsPreviewScene(previous)) setTarget.Invoke(null, new object[] { previous });
    foreach (var scene in scenes) UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
}
