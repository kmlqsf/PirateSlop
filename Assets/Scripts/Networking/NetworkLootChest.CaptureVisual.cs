using FishNet.Object.Synchronizing;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkLootChest
    {
        sealed class SignalFlag
        {
            public Transform Transform;
            public Mesh Mesh;
            public Vector3[] Rest, Vertices;
            public Renderer Renderer;

            public SignalFlag(Transform target)
            {
                Transform = target;
                var filter = target.GetComponent<MeshFilter>();
                Mesh = Instantiate(filter.sharedMesh);
                Mesh.MarkDynamic(); filter.sharedMesh = Mesh;
                Rest = Mesh.vertices; Vertices = new Vector3[Rest.Length];
                Renderer = target.GetComponent<Renderer>();
            }

            public void Resize(Vector2 size)
            {
                int columns = Mathf.Max(16, Mathf.CeilToInt(size.x / .18f));
                int rows = Mathf.Max(6, Mathf.CeilToInt(size.y / .25f));
                Rest = new Vector3[(columns + 1) * (rows + 1)];
                Vertices = new Vector3[Rest.Length];
                var uv = new Vector2[Rest.Length];
                var triangles = new int[columns * rows * 6];
                int next = 0;
                for (int y = 0; y <= rows; y++)
                    for (int x = 0; x <= columns; x++)
                    {
                        int i = y * (columns + 1) + x;
                        float u = x / (float)columns, v = y / (float)rows;
                        Rest[i] = new Vector3(u * size.x, v * size.y, 0f);
                        uv[i] = new Vector2(u, v);
                        if (y == rows || x == columns) continue;
                        triangles[next++] = i; triangles[next++] = i + columns + 1; triangles[next++] = i + 1;
                        triangles[next++] = i + 1; triangles[next++] = i + columns + 1; triangles[next++] = i + columns + 2;
                    }
                Mesh.Clear();
                Mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                Mesh.vertices = Rest; Mesh.uv = uv; Mesh.triangles = triangles;
                Mesh.RecalculateNormals(); Mesh.RecalculateBounds();
            }

            public void Wave(float time, float phase)
            {
                float width = 0f;
                foreach (var point in Rest) width = Mathf.Max(width, point.x);
                for (int i = 0; i < Rest.Length; i++)
                {
                    var p = Rest[i]; float edge = p.x / Mathf.Max(.1f, width);
                    p.z = edge * (.12f * Mathf.Sin(time * 3.7f - edge * 7f + phase) + .045f * Mathf.Sin(time * 6f - edge * 13f + p.y * 2f));
                    p.y += edge * (.045f * Mathf.Sin(time * 2.4f - edge * 5f + phase) - .09f * edge);
                    Vertices[i] = p;
                }
                Mesh.vertices = Vertices; Mesh.RecalculateNormals(); Mesh.RecalculateBounds();
            }
        }

        Transform captureStation;
        const float CaptureChestDepth = 2.4f;
        readonly SyncVar<float> captureBreakAge = new(-1f);
        readonly SyncVar<bool> captureRewardTaken = new();
        readonly SyncVar<int> captureTeam = new();
        Vector3 captureReleasePoint;
        readonly Transform[] captureBuoys = new Transform[24];
        readonly float[] captureBuoyHeights = new float[24];
        readonly Renderer[] boundaryFlags = new Renderer[24];
        readonly SeaSignalRig[] boundaryRigs = new SeaSignalRig[24];
        SeaSignalRig stationRig;
        readonly SeaRopeVisual[] captureChestTethers = new SeaRopeVisual[2];
        readonly Vector3[] tetherPath = new Vector3[3];
        Transform[] stationDebris;
        Vector3[] stationDebrisRest;
        Quaternion[] stationDebrisRotations;
        Vector3[] stationDebrisScales;
        SignalFlag captureFlag, contestFlag, boundaryFlag, boundaryContestFlag;
        SeaCaptureFlagText captureFlagText;
        readonly System.Collections.Generic.List<string> shownCaptureNames = new();
        MaterialPropertyBlock signalColor;
        float shownCaptureProgress, nextCaptureWaves, nextFlagWave;
        Vector3 stationTarget, stationNormal = Vector3.up;
        Color captureColor;
        float shownBreakAge = -1f;
        int captureAudioState = -1, pendingCaptureAudioState = -1;
        float pendingCaptureAudioAt, nextCaptureStartAudio, nextCaptureContestedAudio;
        AudioSource captureStartAudio, captureContestedAudio;
        bool captureVictoryKnown, captureVictoryWasLocked;

        void CreateCaptureStation()
        {
            captureAudioState = pendingCaptureAudioState = -1;
            captureVictoryKnown = false;
            nextCaptureStartAudio = nextCaptureContestedAudio = 0f;
            captureStation = Instantiate(seaArt.CaptureStation, eventVisual.transform, false).transform;
            captureFlag = new SignalFlag(captureStation.Find("CaptureFlag"));
            var lettering = new GameObject("CapturingShips");
            lettering.transform.SetParent(captureFlag.Transform, false);
            captureFlagText = lettering.AddComponent<SeaCaptureFlagText>();
            captureFlagText.Initialize(seaArt.FlagInk);
            contestFlag = new SignalFlag(captureStation.Find("ContestPennant"));
            stationRig = captureStation.GetComponent<SeaSignalRig>();
            var debris = new System.Collections.Generic.List<Transform>();
            foreach (var renderer in captureStation.GetComponentsInChildren<MeshRenderer>())
                if (renderer.transform.parent == captureStation && renderer.transform != captureFlag.Transform && renderer.transform != contestFlag.Transform)
                    debris.Add(renderer.transform);
            stationDebris = debris.ToArray();
            stationDebrisRest = new Vector3[stationDebris.Length]; stationDebrisRotations = new Quaternion[stationDebris.Length]; stationDebrisScales = new Vector3[stationDebris.Length];
            for (int i = 0; i < stationDebris.Length; i++)
            {
                stationDebrisRest[i] = stationDebris[i].localPosition; stationDebrisRotations[i] = stationDebris[i].localRotation; stationDebrisScales[i] = stationDebris[i].localScale;
            }
            for (int i = 0; i < captureChestTethers.Length; i++)
            {
                var tether = new GameObject("ChestMooring" + i); tether.transform.SetParent(eventVisual.transform, false);
                captureChestTethers[i] = tether.AddComponent<SeaRopeVisual>(); tether.GetComponent<MeshRenderer>().sharedMaterial = seaArt.Hemp;
            }
            signalColor = new MaterialPropertyBlock();
            shownCaptureProgress = progress.Value;
            for (int i = 0; i < captureBuoys.Length; i++)
            {
                var buoy = Instantiate(seaArt.CaptureBuoy, eventVisual.transform, false).transform;
                buoy.name = "BoundaryBuoy" + i;
                float angle = i * Mathf.PI * 2f / captureBuoys.Length;
                buoy.localPosition = new Vector3(Mathf.Cos(angle) * Catalog.CaptureRadius, 0, Mathf.Sin(angle) * Catalog.CaptureRadius);
                captureBuoys[i] = buoy;
                var flag = buoy.Find("BoundaryFlag"); boundaryFlags[i] = flag.GetComponent<Renderer>();
                boundaryRigs[i] = buoy.GetComponent<SeaSignalRig>();
                if (i == 0) { boundaryFlag = new SignalFlag(flag); boundaryContestFlag = new SignalFlag(boundaryRigs[i].Contest); }
                else { flag.GetComponent<MeshFilter>().sharedMesh = boundaryFlag.Mesh; boundaryRigs[i].Contest.GetComponent<MeshFilter>().sharedMesh = boundaryContestFlag.Mesh; }
            }
            SampleCaptureWaves();
            captureStation.SetPositionAndRotation(stationTarget, Quaternion.FromToRotation(Vector3.up, stationNormal));
            for (int i = 0; i < captureBuoys.Length; i++)
            {
                var p = captureBuoys[i].position; p.y = captureBuoyHeights[i]; captureBuoys[i].position = p;
            }
            UpdateCaptureStation();
        }

        void SampleCaptureWaves()
        {
            var ocean = OceanSurface.Instance;
            stationTarget = eventPoint.Value;
            if (ocean != null)
            {
                stationTarget.y = ocean.Height(stationTarget);
                stationNormal = new Vector3(ocean.Height(stationTarget - Vector3.right) - ocean.Height(stationTarget + Vector3.right), 2f,
                    ocean.Height(stationTarget - Vector3.forward) - ocean.Height(stationTarget + Vector3.forward)).normalized;
                stationNormal = Vector3.Slerp(Vector3.up, stationNormal, .65f);
            }
            for (int i = 0; i < captureBuoys.Length; i++)
                if (captureBuoys[i] != null) captureBuoyHeights[i] = ocean != null ? ocean.Height(captureBuoys[i].position) : eventPoint.Value.y;
            nextCaptureWaves = Time.time + .15f;
        }

        Color CaptureSignalColor()
        {
            if (contested.Value) return new Color(.95f, .12f, .08f);
            if (captureShip.Value == 0 || !capturing.Value && phase.Value == SeaLootState.Locked && captureShipNames.Count == 0) return new Color(.55f, .58f, .6f);
            int team = captureTeam.Value, viewerTeam = 0;
            foreach (var player in NetworkPlayer.Active)
                if (player != null && player.IsOwner) { viewerTeam = player.TeamId.Value; break; }
            var color = team > 0 && viewerTeam > 0 && team != viewerTeam ? new Color(.76f, .19f, .12f) : new Color(.15f, .64f, .59f);
            return !capturing.Value && phase.Value == SeaLootState.Locked ? Color.Lerp(color, new Color(.45f, .48f, .5f), .45f) : color;
        }

        void UpdateCaptureStation()
        {
            if (captureStation == null) return;
            float time = OceanSurface.Instance != null ? OceanSurface.Instance.WaveTime : Time.time;
            if (Time.time >= nextCaptureWaves) SampleCaptureWaves();
            float blend = 1f - Mathf.Exp(-5f * Time.deltaTime);
            captureStation.position = Vector3.Lerp(captureStation.position, stationTarget, blend);
            captureStation.rotation = Quaternion.Slerp(captureStation.rotation, Quaternion.FromToRotation(Vector3.up, stationNormal), blend);
            shownCaptureProgress = Mathf.MoveTowards(shownCaptureProgress, progress.Value, Time.deltaTime * 2f / Mathf.Max(1f, Catalog.CaptureSeconds));
            bool locked = phase.Value == SeaLootState.Locked;
            bool disputed = contested.Value && locked;
            UpdateCaptureVictory(locked);
            UpdateCaptureAudio(locked, disputed);
            bool lightActive = locked && (disputed || capturing.Value || progress.Value > 0f && captureShipNames.Count > 0);
            Color lightColor = capturing.Value && !disputed ? new Color(.1f, 1f, .2f) : new Color(1f, .08f, .03f);
            float pulse = .5f + .5f * Mathf.Cos(time * Mathf.PI * 2f / (disputed ? 2.8f : 3.6f));
            pulse = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((pulse - .2f) / .6f));
            stationRig.ShowLight(lightActive, lightColor, pulse);
            UpdateCaptureFlagNames();
            if (locked) stationRig.Show(shownCaptureProgress, disputed);
            float buoySinkAge = locked ? 0f : Mathf.Max(0f, Mathf.Max(captureBreakAge.Value, shownBreakAge));
            float buoySinkDepth = 14f * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(buoySinkAge / 5f));
            for (int i = 0; i < captureBuoys.Length; i++)
            {
                var buoy = captureBuoys[i];
                if (buoy == null) continue;
                if (!locked && buoySinkAge >= 5f)
                {
                    Destroy(buoy.gameObject);
                    captureBuoys[i] = null; boundaryRigs[i] = null; boundaryFlags[i] = null;
                    continue;
                }
                boundaryRigs[i].Show(locked ? shownCaptureProgress : 1f, disputed);
                boundaryRigs[i].ShowLight(lightActive, lightColor, pulse);
                var p = buoy.position; p.y = Mathf.Lerp(p.y, captureBuoyHeights[i] - buoySinkDepth, blend); buoy.position = p;
                buoy.rotation = Quaternion.Euler(Mathf.Sin(time * .8f + i * 1.8f) * 4f, i * 137.5f, Mathf.Sin(time * 1.1f + i * 2.1f) * 5f);
            }
            var color = CaptureSignalColor();
            if (color != captureColor)
            {
                captureColor = color; signalColor.SetColor("_BaseColor", color);
                captureFlag.Renderer.SetPropertyBlock(signalColor);
                foreach (var flag in boundaryFlags) if (flag != null) flag.SetPropertyBlock(signalColor);
                signalColor.SetColor("_BaseColor", new Color(.95f, .12f, .08f)); contestFlag.Renderer.SetPropertyBlock(signalColor);
                foreach (var rig in boundaryRigs) if (rig != null) rig.Contest.GetComponent<Renderer>().SetPropertyBlock(signalColor);
            }
            if (Time.time >= nextFlagWave)
            {
                captureFlag.Wave(time, eventPoint.Value.x * .03f); contestFlag.Wave(time, 2.4f);
                if (captureBuoys[0] != null) { boundaryFlag.Wave(time, 1.3f); boundaryContestFlag.Wave(time, 2.4f); }
                captureFlagText.Wave(time, eventPoint.Value.x * .03f);
                float distance = Camera.main != null ? Vector3.Distance(Camera.main.transform.position, eventPoint.Value) : 0f;
                nextFlagWave = Time.time + (distance < 350f ? .05f : .2f);
            }
            UpdateCaptureRelease(locked);
        }

        void UpdateCaptureVictory(bool locked)
        {
            if (!IsClientInitialized) return;
            bool won = captureVictoryKnown && captureVictoryWasLocked && !locked;
            captureVictoryKnown = true;
            captureVictoryWasLocked = locked;
            if (!won) return;
            GameAudio.StopLoop(captureStartAudio);
            GameAudio.StopLoop(captureContestedAudio);
            var listener = SpatialAudioTone.FindListener();
            if (SessionController.MenuOpen || listener == null || Vector3.Distance(listener.transform.position, eventPoint.Value) > Mathf.Min(120f, Catalog.CaptureRadius + 35f)) return;
            GameAudio.Play(SoundCue.CaptureVictory, eventPoint.Value + Vector3.up * 4f);
        }

        void UpdateCaptureAudio(bool locked, bool disputed)
        {
            if (!IsClientInitialized) return;
            int state = !locked ? 0 : disputed ? 2 : capturing.Value ? 1 : 0;
            float now = Time.unscaledTime;
            if (pendingCaptureAudioState != state)
            {
                pendingCaptureAudioState = state;
                pendingCaptureAudioAt = now;
            }
            bool stable = now - pendingCaptureAudioAt >= .5f;
            if (stable) captureAudioState = state;
            var listener = SpatialAudioTone.FindListener();
            bool audible = stable && !SessionController.MenuOpen && listener != null &&
                Vector3.Distance(listener.transform.position, eventPoint.Value) <= Mathf.Min(120f, Catalog.CaptureRadius + 35f);
            GameAudio.Repeat(ref captureStartAudio, ref nextCaptureStartAudio, SoundCue.CaptureStart, eventVisual.transform, audible && captureAudioState == 1, 6f);
            GameAudio.Repeat(ref captureContestedAudio, ref nextCaptureContestedAudio, SoundCue.CaptureContested, eventVisual.transform, audible && captureAudioState == 2, 4.5f);
        }

        void UpdateCaptureFlagNames()
        {
            shownCaptureNames.Clear();
            foreach (string name in captureShipNames) shownCaptureNames.Add(name);
            if (shownCaptureNames.Count == 0 && progress.Value > 0f && !string.IsNullOrEmpty(captureOwnerName.Value)) shownCaptureNames.Add(captureOwnerName.Value);
            if (!captureFlagText.SetNames(shownCaptureNames)) return;
            captureFlag.Resize(captureFlagText.Size);
            nextFlagWave = 0f;
        }

        void UpdateCaptureRelease(bool locked)
        {
            for (int i = 0; i < captureChestTethers.Length; i++)
            {
                var rope = captureChestTethers[i]; rope.gameObject.SetActive(locked);
                if (!locked) continue;
                float side = i == 0 ? -1f : 1f;
                var upper = captureStation.TransformPoint(new Vector3(side * .38f, .08f, 0));
                var lower = transform.TransformPoint(new Vector3(side * .43f, .32f, 0));
                tetherPath[0] = eventVisual.transform.InverseTransformPoint(upper);
                tetherPath[1] = eventVisual.transform.InverseTransformPoint(Vector3.Lerp(upper, lower, .55f));
                tetherPath[2] = eventVisual.transform.InverseTransformPoint(lower);
                rope.SetPath(tetherPath, tetherPath.Length, .025f);
            }
            if (locked || captureBreakAge.Value < 0) return;
            if (shownBreakAge < 0)
            {
                shownBreakAge = captureBreakAge.Value;
                stationRig.Release();
                if (shownBreakAge < .5f && IsClientInitialized)
                {
                    GameAudio.Play(SoundCue.WaterSplash, eventPoint.Value); CombatVfx.Splash(eventPoint.Value);
                }
            }
            shownBreakAge = Mathf.Max(captureBreakAge.Value, shownBreakAge + Time.deltaTime);
            float age = shownBreakAge;
            captureStation.gameObject.SetActive(age < 5f);
            if (age >= 5f) return;
            for (int i = 0; i < stationDebris.Length; i++)
            {
                float a = i * 2.399f;
                var velocity = new Vector3(Mathf.Cos(a), .65f + i % 3 * .16f, Mathf.Sin(a)) * (1.1f + i % 3 * .2f);
                stationDebris[i].localPosition = stationDebrisRest[i] + velocity * age + Vector3.down * age * age * .5f;
                stationDebris[i].localRotation = stationDebrisRotations[i] * Quaternion.Euler(age * (18f + i * 4f), age * (i % 2 == 0 ? 13f : -15f), age * (25f + i * 9f));
                stationDebris[i].localScale = stationDebrisScales[i] * Mathf.Clamp01((5f - age) / 1.2f);
            }
        }

        bool PreserveCapturedBuoys()
        {
            if (Kind != SeaLootKind.Capture) return false;
            if (carryingPlayer != null) carryingPlayer.SetCarriedLoot(null);
            carryingPlayer = null; carrier.Value = null;
            captureRewardTaken.Value = true;
            return true;
        }

        void DestroyCaptureMeshes()
        {
            if (captureFlag != null) Destroy(captureFlag.Mesh);
            if (contestFlag != null) Destroy(contestFlag.Mesh);
            if (boundaryFlag != null) Destroy(boundaryFlag.Mesh);
            if (boundaryContestFlag != null) Destroy(boundaryContestFlag.Mesh);
        }
    }
}
