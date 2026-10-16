int last = UnityEditorInternal.ProfilerDriver.lastFrameIndex - 33;
int first = System.Math.Max(UnityEditorInternal.ProfilerDriver.firstFrameIndex, last - 30);
var totals = new System.Collections.Generic.Dictionary<string, (double ms, int calls)>();
int frames = 0;
for (int frame = first; frame <= last; frame++)
{
    using (var data = UnityEditorInternal.ProfilerDriver.GetRawFrameDataView(frame, 0))
    {
        if (!data.valid) continue;
        frames++;
        var parents = new System.Collections.Generic.Stack<int>();
        for (int i = 0; i < data.sampleCount; i++)
        {
            while (parents.Count > 0 && i > parents.Peek() + data.GetSampleChildrenCountRecursive(parents.Peek())) parents.Pop();
            string name = data.GetSampleName(i) ?? "";
            if (name == "Physics.WriteColliderPoses" || name == "Physics.SphereCastNonAlloc" || name == "Physics.SyncTransforms" || name == "Code.PlayerMotor" || name == "Code.ShipBuoyancy" || name.Contains("TimeManager.OnTick"))
            {
                string owner = "<root>";
                foreach (int parent in parents)
                {
                    string candidate = data.GetSampleName(parent) ?? "";
                    if (candidate.StartsWith("Code.") || candidate.Contains("Cannonball.FixedUpdate") || candidate.Contains("TimeManager.OnTick")) { owner = candidate; break; }
                }
                string key = name + " <- " + owner;
                totals.TryGetValue(key, out var previous);
                totals[key] = (previous.ms + data.GetSampleTimeMs(i), previous.calls + 1);
            }
            parents.Push(i);
        }
    }
}
if (frames == 0) return "No valid frames in selected range";
var lines = new System.Collections.Generic.List<string> { "Frames=" + frames + " range=" + first + ".." + last };
foreach (var item in totals.OrderByDescending(x => x.Value.ms))
    lines.Add(FormattableString.Invariant($"{item.Key} | ms/frame={item.Value.ms / frames:F4} calls/frame={item.Value.calls / (double)frames:F3} us/call={1000 * item.Value.ms / item.Value.calls:F2}"));
string result = string.Join("\n", lines);
System.IO.File.WriteAllText("Temp/performance-physics-callers.txt", result);
return result;
