using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.LowLevel;

namespace PirateSlop.Performance
{
    public enum PerformanceCpuSection
    {
        Capture, Telemetry, PlayerTick, PlayerCarry, PlayerSyncTransforms, PlayerMotor, PlayerSupport, PlayerPublish, ShipTick, ShipPublish,
        ShipHelm, ShipBuoyancy, ShipWorldChecks, ShipHullMotion, ShipHullBroadphase, ShipHullOverlaps, ShipHullCasts, ShipPhysicsSync, ShipTransformWrite, ShipHullProxySetup,
        ShipVisualRig, ShipCloth, ShipRopes, ShipFeaturesUpdate, ShipFeaturesLateUpdate, ShipAttachments, ShipDestructionUpdate, ShipRenderBatch, ShipCollisionBatch, PlayerPresentation, ShipStateSampling
    }

    public static class PerformanceCpuTimings
    {
        const int Slots = 4;
        public const string Version = "wallclock-v5";
        public static string[] Names { get; private set; } = Array.Empty<string>();
        static bool active;
        static int roots, width;
        static readonly int[] frames = new int[Slots];
        static readonly HashSet<string> watched = new()
        {
            "FixedUpdate.ScriptRunBehaviourFixedUpdate", "FixedUpdate.PhysicsFixedUpdate", "FixedUpdate.PhysicsClothFixedUpdate",
            "Update.ScriptRunBehaviourUpdate", "PreLateUpdate.ScriptRunBehaviourLateUpdate", "PreLateUpdate.PhysicsLateUpdate",
            "PreLateUpdate.DirectorUpdateAnimationBegin", "PreLateUpdate.DirectorUpdateAnimationEnd",
            "PostLateUpdate.UpdateAllRenderers", "PostLateUpdate.UpdateAllSkinnedMeshes", "PostLateUpdate.FinishFrameRendering", "PostLateUpdate.PresentAfterDraw"
        };
        static double[] values;

        public readonly struct Scope : IDisposable
        {
            readonly int index;
            readonly long started;
            internal Scope(int value) { index = active ? value : -1; started = index >= 0 ? Stopwatch.GetTimestamp() : 0; }
            public void Dispose() { if (index >= 0 && active) Add(index, started); }
        }

        sealed class Probe
        {
            public int Index;
            long started;
            public void Begin() { started = Stopwatch.GetTimestamp(); }
            public void End() { if (active) Add(Index, started); }
        }

        public static Scope Measure(PerformanceCpuSection section) => new(roots + (int)section);

        static void Add(int index, long started)
        {
            long elapsed = Stopwatch.GetTimestamp() - started;
            int frame = Time.frameCount, slot = frame & (Slots - 1);
            if (frames[slot] != frame)
            {
                Array.Clear(values, slot * width, width);
                frames[slot] = frame;
            }
            values[slot * width + index] += elapsed * (1000d / Stopwatch.Frequency);
        }

        public static void CopyFrame(int frame, float[] destination, int offset)
        {
            int slot = frame & (Slots - 1);
            bool available = active && frames[slot] == frame;
            for (int i = 0; i < width; i++) destination[offset + i] = available ? (float)values[slot * width + i] : -1;
        }

        public static void Start()
        {
            Stop();
            var loop = PlayerLoop.GetCurrentPlayerLoop();
            var names = new List<string>();
            var childIndices = new Dictionary<string, int>();
            foreach (var phase in loop.subSystemList) names.Add("Loop." + phase.type.Name);
            foreach (var phase in loop.subSystemList)
                foreach (var child in phase.subSystemList ?? Array.Empty<PlayerLoopSystem>())
                {
                    string name = phase.type.Name + "." + child.type.Name;
                    if (!watched.Contains(name)) continue;
                    childIndices.Add(name, names.Count);
                    names.Add("Loop." + name);
                }
            roots = names.Count;
            var sections = Enum.GetNames(typeof(PerformanceCpuSection));
            width = roots + sections.Length;
            Names = new string[width]; values = new double[width * Slots];
            names.CopyTo(Names);
            for (int i = 0; i < Slots; i++) frames[i] = int.MinValue;
            for (int i = 0; i < loop.subSystemList.Length; i++)
            {
                var phase = loop.subSystemList[i];
                var probe = new Probe { Index = i };
                var children = phase.subSystemList ?? Array.Empty<PlayerLoopSystem>();
                var wrapped = new List<PlayerLoopSystem>(children.Length + 8);
                wrapped.Add(new PlayerLoopSystem { type = typeof(PerformanceCpuTimings), updateDelegate = probe.Begin });
                foreach (var child in children)
                {
                    if (childIndices.TryGetValue(phase.type.Name + "." + child.type.Name, out int index))
                    {
                        var nested = new Probe { Index = index };
                        wrapped.Add(new PlayerLoopSystem { type = typeof(PerformanceCpuTimings), updateDelegate = nested.Begin });
                        wrapped.Add(child);
                        wrapped.Add(new PlayerLoopSystem { type = typeof(PerformanceCpuTimings), updateDelegate = nested.End });
                    }
                    else wrapped.Add(child);
                }
                wrapped.Add(new PlayerLoopSystem { type = typeof(PerformanceCpuTimings), updateDelegate = probe.End });
                phase.subSystemList = wrapped.ToArray();
                loop.subSystemList[i] = phase;
            }
            for (int i = 0; i < sections.Length; i++) Names[roots + i] = "Code." + sections[i];
            active = true;
            PlayerLoop.SetPlayerLoop(loop);
        }

        static void Remove(ref PlayerLoopSystem system)
        {
            if (system.subSystemList == null) return;
            var kept = new List<PlayerLoopSystem>(system.subSystemList.Length);
            foreach (var original in system.subSystemList)
            {
                if (original.type == typeof(PerformanceCpuTimings)) continue;
                var child = original;
                Remove(ref child);
                kept.Add(child);
            }
            system.subSystemList = kept.ToArray();
        }

        public static void Stop()
        {
            if (!active) return;
            active = false;
            var loop = PlayerLoop.GetCurrentPlayerLoop();
            Remove(ref loop);
            PlayerLoop.SetPlayerLoop(loop);
            values = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { Stop(); }
    }
}
