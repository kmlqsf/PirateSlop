var totals = new System.Collections.Generic.Dictionary<string, (double inclusive, double self, int calls)>();
int frames = 0;
int last = UnityEditorInternal.ProfilerDriver.lastFrameIndex;
int first = System.Math.Max(UnityEditorInternal.ProfilerDriver.firstFrameIndex, last - 32);
for (int frame = first; frame <= last - 2; frame++)
{
    using (var data = UnityEditorInternal.ProfilerDriver.GetRawFrameDataView(frame, 0))
    {
        if (!data.valid) continue;
        frames++;
        var self = new double[data.sampleCount];
        var parents = new System.Collections.Generic.Stack<int>();
        for (int i = 0; i < data.sampleCount; i++) self[i] = data.GetSampleTimeMs(i);
        for (int i = 0; i < data.sampleCount; i++)
        {
            while (parents.Count > 0 && i > parents.Peek() + data.GetSampleChildrenCountRecursive(parents.Peek())) parents.Pop();
            if (parents.Count > 0) self[parents.Peek()] -= data.GetSampleTimeMs(i);
            parents.Push(i);
        }
        for (int i = 0; i < data.sampleCount; i++)
        {
            string name = data.GetSampleName(i) ?? "<unnamed>";
            totals.TryGetValue(name, out var previous);
            totals[name] = (previous.inclusive + data.GetSampleTimeMs(i), previous.self + self[i], previous.calls + 1);
        }
    }
}
var lines = new System.Collections.Generic.List<string> { "Frames=" + frames + " profilerRange=" + first + ".." + (last - 2) };
foreach (var item in totals.OrderByDescending(x => x.Value.self).Take(65))
    lines.Add(FormattableString.Invariant($"{item.Key} | incl={item.Value.inclusive / frames:F3}ms self={item.Value.self / frames:F3}ms calls={item.Value.calls / (double)frames:F1}"));
string result = string.Join("\n", lines);
System.IO.File.WriteAllText("Temp/performance-editor-profile.txt", result);
return result;
