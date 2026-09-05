using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using FishNet.Managing;
using UnityEngine;
namespace PirateSlop.Networking
{
    public sealed class SessionMetrics : MonoBehaviour
    {
        NetworkManager manager;
        long begin;
        float next;
        readonly List<double> samples = new(4096);
        void Start() { manager = GetComponent<NetworkManager>(); manager.TimeManager.OnPreTick += Pre; manager.TimeManager.OnPostTick += Post; }
        void Pre() { begin = Stopwatch.GetTimestamp(); }
        void Post() { if (manager.IsServerStarted) samples.Add((Stopwatch.GetTimestamp() - begin) * 1000d / Stopwatch.Frequency); }
        void Update()
        {
            if (Time.realtimeSinceStartup < next || manager == null || !manager.IsServerStarted) return;
            next = Time.realtimeSinceStartup + 10;
            if (samples.Count == 0) return;
            samples.Sort(); double p95 = samples[Math.Min(samples.Count-1, (int)(samples.Count*.95))];
            using var process = Process.GetCurrentProcess();
            UnityEngine.Debug.Log($"SESSION_METRICS session={SessionController.Instance.SessionId} clients={manager.ServerManager.Clients.Count} tick_p95_ms={p95:F3} working_set_mb={process.WorkingSet64/1048576} samples={samples.Count}");
            samples.Clear();
        }
    }
}
