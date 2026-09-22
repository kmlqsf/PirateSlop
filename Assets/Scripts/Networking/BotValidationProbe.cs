#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using Unity.Profiling;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed class BotValidationProbe : MonoBehaviour
    {
        [Serializable]
        sealed class Sample
        {
            public float Seconds, FrameMeanMs, FrameMaxMs;
            public int Frames, Players, Bots, AliveBots, MovingShips;
            public long AllocatedBytes;
            public bool Server, ClientAiViolation, DuplicateBotNumber, UnexpectedBot;
            public string[] BotStates;
            public float[] SailDeploy;
            public double[] CpuMeanMs;
            public bool[] CounterAvailable;
        }

        static readonly string[] names = { "Bots.Paths", "Bots.Crews", "Bots.Actions", "Bots.Diagnostics" };
        readonly ProfilerRecorder[] counters = new ProfilerRecorder[4];
        readonly double[] elapsed = new double[4];
        readonly HashSet<int> initialBots = new();
        StreamWriter writer;
        float nextSample, frameTotal, frameMax;
        int frames;
        bool initialized, rosterCaptured;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-validatebots") < 0) return;
            var probe = new GameObject("BotValidationProbe").AddComponent<BotValidationProbe>();
            DontDestroyOnLoad(probe.gameObject);
            int index = Array.IndexOf(args, "-botValidationLog");
            if (index >= 0 && index + 1 < args.Length)
                probe.writer = new StreamWriter(args[index + 1], false) { AutoFlush = true };
        }

        void LateUpdate()
        {
            if (Time.time < 20f) return;
            if (!initialized)
            {
                for (int i = 0; i < counters.Length; i++)
                    counters[i] = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, names[i], 1,
                        ProfilerRecorderOptions.StartImmediately | ProfilerRecorderOptions.WrapAroundWhenCapacityReached | ProfilerRecorderOptions.SumAllSamplesInFrame);
                initialized = true;
                nextSample = Time.unscaledTime + 10f;
            }
            frames++;
            frameTotal += Time.unscaledDeltaTime;
            frameMax = Mathf.Max(frameMax, Time.unscaledDeltaTime);
            for (int i = 0; i < counters.Length; i++)
                if (counters[i].Valid) elapsed[i] += counters[i].LastValue / 1000000.0;
            if (Time.unscaledTime < nextSample) return;
            nextSample = Time.unscaledTime + 10f;
            Capture();
        }

        void Capture()
        {
            var sample = new Sample { Seconds = Time.time, Frames = frames,
                FrameMeanMs = frameTotal * 1000f / Mathf.Max(1, frames), FrameMaxMs = frameMax * 1000f,
                AllocatedBytes = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong(),
                CpuMeanMs = new double[4], CounterAvailable = new bool[4] };
            var states = new List<string>();
            var numbers = new HashSet<int>();
            foreach (var player in FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None))
            {
                sample.Players++;
                sample.Server |= player.IsServerInitialized;
                if (!player.IsBot.Value) continue;
                sample.Bots++;
                if (!player.Motor.IsDead) sample.AliveBots++;
                sample.DuplicateBotNumber |= !numbers.Add(player.BotNumber);
                sample.ClientAiViolation |= !player.IsServerInitialized && player.BotTaskRunning;
                sample.UnexpectedBot |= player.IsServerInitialized && rosterCaptured && !initialBots.Contains(player.BotNumber);
                states.Add(player.BotNumber + ":" + player.TeamId.Value + ":" + player.BotTaskKey + ":" + player.BotStatus);
            }
            if (!rosterCaptured && sample.Bots > 0)
            {
                initialBots.UnionWith(numbers);
                rosterCaptured = true;
            }
            var sails = new List<float>();
            foreach (var ship in FindObjectsByType<NetworkShip>(FindObjectsSortMode.None))
            {
                if (Mathf.Abs(ship.Motor.Speed) > .5f) sample.MovingShips++;
                sails.Add(ship.GetComponent<SailSystem>().EffectiveDeploy);
            }
            sample.BotStates = states.ToArray();
            sample.SailDeploy = sails.ToArray();
            for (int i = 0; i < counters.Length; i++)
            {
                sample.CounterAvailable[i] = counters[i].Valid;
                sample.CpuMeanMs[i] = elapsed[i] / Mathf.Max(1, frames);
                elapsed[i] = 0;
            }
            string json = JsonUtility.ToJson(sample);
            writer?.WriteLine(json);
            Debug.Log("BOT_VALIDATION " + json);
            frameTotal = frameMax = 0f;
            frames = 0;
        }

        void OnDestroy()
        {
            for (int i = 0; i < counters.Length; i++) counters[i].Dispose();
            writer?.Dispose();
        }
    }
}
#endif
