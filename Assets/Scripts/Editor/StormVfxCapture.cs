using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using PirateSlop.Networking;
using PirateSlop.World;

namespace PirateSlop.EditorTools
{
    [InitializeOnLoad]
    public static class StormVfxCapture
    {
        const string Folder = "Captures/VfxSkyWaterReview20261007";
        static Camera camera, source;
        static bool sourceEnabled, capturing;
        static readonly List<Camera> otherCameras = new();
        static readonly Dictionary<string,int> renderedCameras = new();
        static string pendingLabel;
        static Action pendingDone;
        static string sourceTag;
        static RenderTexture target;
        static double started, stageStarted, lastCapture;
        static int warmFrame, lastFrame, stage, mode, motionFrame, quietFrames;
        static ShipController ship;
        static NetworkShip network;
        static Rigidbody body;
        static bool shipEnabled, networkEnabled, moved,freezeChanged,oldFreeze;
        static float oldRadius;
        static Vector3 shipPosition, deckLocal, radial, farEye;
        static Quaternion shipRotation;
        static StormBillowController billows;
        static bool billowsEnabled;
        static readonly List<ScriptableRendererFeature> features = new();
        static readonly List<bool> featureStates = new();
        static readonly List<GameObject> markers = new();
        static readonly List<string> rows = new();
        static readonly FrameTiming[] timings = new FrameTiming[1];
        static ulong lastTimestamp;
        static ProfilerRecorder gpu, cpu;
        static bool profiling, profilerEnabled;
        static readonly Dictionary<ProfilerArea, bool> areas = new();
        static readonly float[] distances = { -50, -10, 0, 0, 0, 25, 300, 300 };
        static readonly string[] labels = { "near", "edge", "inwall-baseline-markers", "inwall-on-markers", "inwall-deck", "outside-near", "outside-lookback", "outside-away" };
        static StormVfxCapture()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += Changed;
            RenderPipelineManager.endCameraRendering += CameraRendered;
        }
        [MenuItem("PirateSlop/VFX/Review/Start Volume Isolation")]
        public static void StartVolumeIsolation() => Start(9);
        [MenuItem("PirateSlop/VFX/Review/Start Volume Angles")]
        public static void StartVolumeAngles() => Start(8);
        [MenuItem("PirateSlop/VFX/Review/Start Far")]
        public static void StartFar() => Start(0);
        [MenuItem("PirateSlop/VFX/Review/Start Route And Performance")]
        public static void StartRoute() => Start(1);
        [MenuItem("PirateSlop/VFX/Review/Start Lookback And Performance")]
        public static void StartLookback() => Start(3);
        [MenuItem("PirateSlop/VFX/Review/Start Near Costs")]
        public static void StartNearCosts() => Start(4);
        [MenuItem("PirateSlop/VFX/Review/Start Shrink Peek")]
        public static void StartShrinkPeek() => Start(7);
        [MenuItem("PirateSlop/VFX/Review/Start Local Motion Only")]
        public static void StartLocalMotionOnly() => Start(6);
        [MenuItem("PirateSlop/VFX/Review/Start Local Motion And Timing")]
        public static void StartLocalMotion() => Start(5);
        [MenuItem("PirateSlop/VFX/Review/Start Performance")]
        public static void StartPerformance() => Start(2);
        static void Start(int runMode)
        {
            if (SessionState.GetBool("StormBillowQAArmed", false)) return;
            SessionState.SetBool("StormBillowQAOwnPlay", !EditorApplication.isPlaying || SessionState.GetBool("StormBillowQAOwnPlay", false));
            SessionState.SetBool("StormBillowQAArmed", true);
            SessionState.SetBool("StormBillowQAJoined", false);
            SessionState.SetInt("StormBillowQAMode", runMode);
            started = EditorApplication.timeSinceStartup;
            if (!EditorApplication.isPlaying) { EditorApplication.isPaused=false; EditorApplication.EnterPlaymode(); }
        }
        [MenuItem("PirateSlop/VFX/Review/Cleanup")]
        public static void Cleanup()
        {
            SessionState.SetBool("StormBillowQAArmed", false);
            capturing = false;pendingLabel=null;pendingDone=null;
            Shader.SetGlobalFloat("_PirateStormFieldDiagnostic",0);
            if(freezeChanged&&StormVolumeController.Instance!=null){StormVolumeController.Instance.FreezeZoneValues=oldFreeze;StormVolumeController.Instance.ManualRadius=oldRadius;}freezeChanged=false;
            SetEffects(true);
            if (source != null) { source.enabled = sourceEnabled; source.tag = sourceTag; }
            if (moved && ship != null)
            {
                body.position = shipPosition; body.rotation = shipRotation;
                ship.transform.SetPositionAndRotation(shipPosition, shipRotation);
                ship.enabled = shipEnabled;
                if (network != null) network.enabled = networkEnabled;
                Physics.SyncTransforms();
            }
            moved = false;
            RemoveMarkers();
            if (camera != null) UnityEngine.Object.DestroyImmediate(camera.gameObject);
            if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
            if (profiling)
            {
                gpu.Dispose(); cpu.Dispose();
                foreach (var pair in areas) ProfilerDriver.SetAreaEnabled(pair.Key, pair.Value);
                ProfilerDriver.enabled = profilerEnabled;
                profiling = false; areas.Clear();
            }
            features.Clear(); featureStates.Clear();
            var gameView = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
            if (gameView != null)
                foreach (var view in Resources.FindObjectsOfTypeAll(gameView))
                    gameView.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.SetValue(view, 0);
            foreach(var other in otherCameras)if(other!=null)other.enabled=true;
            otherCameras.Clear();renderedCameras.Clear();
            source = camera = null; target = null; ship = null; network = null; body = null; billows = null;
            lastFrame = -1; started = 0;
        }
        static void Changed(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode) Cleanup();
            if (state == PlayModeStateChange.EnteredEditMode) SessionState.SetBool("StormBillowQAOwnPlay", false);
            if(state==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("StormBillowQAArmed",false) && SessionState.GetBool("StormBillowQAOwnPlay",false))EditorApplication.isPaused=false;
        }
        static void Tick()
        {
            if (!SessionState.GetBool("StormBillowQAArmed", false) || !EditorApplication.isPlaying) return;
            if (EditorApplication.isPaused)
            {
                if (SessionState.GetBool("StormBillowQAOwnPlay", false)) { EditorApplication.isPaused=false; started=EditorApplication.timeSinceStartup; }
                return;
            }
            try
            {
                if (started == 0) started = EditorApplication.timeSinceStartup;
                if (EditorApplication.timeSinceStartup - started > 90) throw new TimeoutException("Storm QA timeout");
                if (lastFrame == Time.frameCount) return;
                lastFrame = Time.frameCount;
                var session = SessionController.Instance;
                if (session != null && !SessionState.GetBool("StormBillowQAJoined", false))
                {
                    if (Time.time < 1.5f) return;
                    SessionState.SetBool("StormBillowQAJoined", true);
                    session.Begin(true, "127.0.0.1:" + session.Config.Port, true);
                }
                if (camera == null)
                {
                    if (ProceduralWorld.Instance == null || !ProceduralWorld.Instance.Ready || StormVolumeController.Instance == null || !StormVolumeController.Instance.Ready || Camera.main == null || ShipController.ActiveControllers.Count == 0) return;
                    Setup();
                }
                if (capturing) return;
                if(mode==9)
                {
                    if(EditorApplication.timeSinceStartup-stageStarted<1.6||Time.frameCount-warmFrame<60)return;
                    Request("dense3-rain-off",()=>{
                        if(++stage==1){File.WriteAllLines(Folder+"/volume8-bindings.csv",rows);File.WriteAllText(Folder+"/volume8-bindings-done.txt","complete");Cleanup();}
                        else SetupVolumeIsolation();
                    });return;
                }
                if(mode==8)
                {
                    if(stage==1)
                    {
                        double elapsed=EditorApplication.timeSinceStartup-stageStarted;
                        var tangent=Vector3.Cross(Vector3.up,radial);
                        camera.transform.SetPositionAndRotation(ship.transform.TransformPoint(deckLocal)+tangent*35*Mathf.Clamp01((float)elapsed/2.4f),Quaternion.LookRotation(radial*.65f+tangent*.76f+Vector3.up*.2f));
                        if(elapsed>=2.4){stage=2;SetupVolumeAngle();return;}
                        if(EditorApplication.timeSinceStartup-lastCapture>.2)
                        {
                            lastCapture=EditorApplication.timeSinceStartup;
                            Request("dense3-oblique-motion"+(motionFrame++).ToString("00"),()=>{});
                        }
                        return;
                    }
                    if(EditorApplication.timeSinceStartup-stageStarted<1.6||Time.frameCount-warmFrame<60)return;
                    Request("dense3-"+(stage==0?"oblique":"tangent"),()=>{
                        if(stage==2){File.WriteAllLines(Folder+"/dense3-local.csv",rows);File.WriteAllText(Folder+"/dense3-local-done.txt","complete");Cleanup();}
                        else{stage=1;lastCapture=EditorApplication.timeSinceStartup;motionFrame=0;stageStarted=EditorApplication.timeSinceStartup;}
                    });
                    return;
                }

                if (mode == 0 || (mode == 3 && stage == -1))
                {
                    if (Time.frameCount - warmFrame < 100) return;
                    if (Shader.GetGlobalVector("_StormLightning").w > .65f || Shader.GetGlobalVector("_StormLightningSecondary").w > .65f || Time.frameCount - warmFrame > 400)
                        Request("dense3-far-qhd", () => { if (mode == 3) { SetupDeck(); stage = 2; SetupRouteStage(); } else { File.WriteAllText(Folder + "/final-far-done.txt", "frame=" + Time.frameCount); Cleanup(); } });
                    return;
                }
                if ((mode == 1 || mode == 3) && stage < labels.Length)
                {
                    if (Time.frameCount - warmFrame < 45 || EditorApplication.timeSinceStartup-stageStarted < 1.6) return;
                    if(mode==3&&stage==4)
                    {
                        if(Shader.GetGlobalVector("_StormLightning").w>.001f||Shader.GetGlobalVector("_StormLightningSecondary").w>.001f){quietFrames=0;return;}
                        if(++quietFrames<5)return;
                    }
                    Request((mode == 3 ? "dense3-" : "final-") + labels[stage], () => { stage = mode == 3 ? (stage == 2 ? 3 : stage == 3 ? 4 : stage == 4 ? labels.Length : stage == 7 ? 6 : labels.Length) : stage + 1; SetupRouteStage(); });
                    return;
                }
                if ((mode == 1 || mode == 3 || mode == 5 || mode == 6) && stage == labels.Length)
                {
                    if(mode==3){File.WriteAllText(Folder+"/final-local-done.txt","captures complete");Cleanup();return;}
                    double elapsed = EditorApplication.timeSinceStartup - stageStarted;
                    MoveShip(mode==6?Mathf.Lerp(-40f,50f,Mathf.Clamp01((float)elapsed/3)):Mathf.Lerp(-15,15,Mathf.Clamp01((float)elapsed/3)));
                    camera.transform.SetPositionAndRotation(ship.transform.TransformPoint(deckLocal), Quaternion.LookRotation(radial - Vector3.up * .06f));
                    if (elapsed < 3.0)
                    {
                        if (EditorApplication.timeSinceStartup - lastCapture >= .08)
                        {
                            lastCapture = EditorApplication.timeSinceStartup;
                            Request("dense3-motion-" + (motionFrame++).ToString("00"), () => { });
                        }
                        return;
                    }
                    if(mode==6){File.WriteAllLines(Folder+"/dense3-motion.csv",rows);File.WriteAllText(Folder+"/dense3-motion-done.txt","complete");Cleanup();return;}
                    stage++; SetupPerformance(); return;
                }
                if(mode==7){ShrinkPeek();return;}
                Performance();
            }
            catch (Exception error) { Debug.LogException(error); Cleanup(); }
        }
        static void Setup()
        {
            mode = SessionState.GetInt("StormBillowQAMode", 0); stage = 0; motionFrame = 0;quietFrames=0;
            rows.Clear(); rows.Add("kind,label,frame,time,cameraX,cameraY,cameraZ,R,signedDistance,FOV,Primary,Secondary,frameMs,cpuMs,gpuMs,gpuTimestamp,gpuCounterMs,profileFrame,rainExposed,rainIntensity,nearMedium,nearCells,nearIdentity");
            source = Camera.main; sourceEnabled = source.enabled; sourceTag = source.tag;
            ship = ShipController.ActiveControllers[0]; body = ship.GetComponent<Rigidbody>(); network = ship.GetComponent<NetworkShip>();
            shipPosition = ship.transform.position; shipRotation = ship.transform.rotation;
            shipEnabled = ship.enabled; networkEnabled = network != null && network.enabled;
            var storm = StormVolumeController.Instance;
            billows = storm.GetComponent<StormBillowController>(); billowsEnabled = billows != null && billows.enabled;
            radial = shipPosition - storm.CurrentCenter; radial.y = 0;
            if (radial.sqrMagnitude < 1) radial = Vector3.forward;
            radial.Normalize();
            farEye = shipPosition + radial * 45 + Vector3.Cross(Vector3.up, radial) * 35; farEye.y = storm.WaterLevel + 18;
            if (Physics.CheckSphere(farEye, 1.5f, ~0, QueryTriggerInteraction.Ignore)) throw new InvalidOperationException("Far camera blocked");
            camera = new GameObject("Storm Billow Review Camera") { hideFlags = HideFlags.HideAndDontSave }.AddComponent<Camera>();
            camera.CopyFrom(source);
            var data = camera.GetUniversalAdditionalCameraData(); var original = source.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = original.renderPostProcessing; data.antialiasing = original.antialiasing;
            data.requiresColorTexture = true; data.requiresDepthTexture = true;
            camera.farClipPlane = Mathf.Max(6000, source.farClipPlane); camera.fieldOfView = 80;
            camera.tag = "MainCamera"; source.tag = "Untagged"; source.enabled = false;
            otherCameras.Clear();renderedCameras.Clear();
            foreach(var other in Resources.FindObjectsOfTypeAll<Camera>())
                if(other!=camera&&other!=source&&other.enabled&&other.gameObject.activeInHierarchy&&other.cameraType==CameraType.Game){otherCameras.Add(other);other.enabled=false;}
            var cull = camera.layerCullDistances; cull[1] = camera.farClipPlane; camera.layerCullDistances = cull;
            target = new RenderTexture(mode==6||mode==7?960:2560, mode==6||mode==7?540:1440, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { name = "Storm QHD Review" };
            target.Create(); camera.targetTexture = target; camera.enabled = true;
            Directory.CreateDirectory(Folder);
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline != null)
            {
                var so = new SerializedObject(pipeline); var list = so.FindProperty("m_RendererDataList");
                if (list != null)
                    for (int i = 0; i < list.arraySize; i++)
                    {
                        var renderer = list.GetArrayElementAtIndex(i).objectReferenceValue as ScriptableRendererData;
                        if (renderer == null) continue;
                        foreach (var feature in renderer.rendererFeatures)
                        {
                            if (feature == null || features.Contains(feature)) continue;
                            string name = feature.GetType().Name;
                            if (name == "VolumetricCloudsURP" || name == "StormVolumeRendererFeature" || name == "StormRainRendererFeature")
                            { features.Add(feature); featureStates.Add(feature.isActive); }
                        }
                    }
            }
            warmFrame = Time.frameCount;
            if(mode==9){SetupDeck();stage=0;SetupVolumeIsolation();}
            else if(mode==8){SetupDeck();stage=0;SetupVolumeAngle();}
            else if (mode == 0) { stage = -1; camera.transform.SetPositionAndRotation(farEye, Quaternion.LookRotation(radial + Vector3.up * .10f)); }
            else if(mode==3){SetupDeck();stage=4;SetupRouteStage();}
            else if(mode==7){SetupDeck();var zone=StormVolumeController.Instance;oldFreeze=zone.FreezeZoneValues;oldRadius=zone.ManualRadius;freezeChanged=true;zone.FreezeZoneValues=true;zone.ManualRadius=zone.CurrentRadius;stage=0;stageStarted=EditorApplication.timeSinceStartup;}
            else if(mode==5||mode==6){SetupDeck();stage=labels.Length;SetupRouteStage();}
            else if (mode == 1) { SetupDeck(); SetupRouteStage(); }
            else
            {
                if(mode==4)SetupDeck();
                SetupPerformance();
                if(SessionState.GetInt("StormBillowQAMode",0)==4)
                {
                    mode=4;stage=0;moved=true;ship.enabled=false;if(network!=null)network.enabled=false;
                    MoveShip(0);camera.transform.SetPositionAndRotation(ship.transform.TransformPoint(deckLocal),Quaternion.LookRotation(radial-Vector3.up*.06f));
                }
            }
        }
        static void SetupVolumeIsolation()
        {
            MoveShip(-60);
            Shader.SetGlobalFloat("_PirateStormFieldDiagnostic",1);
            var tangent=Vector3.Cross(Vector3.up,radial);
            camera.transform.SetPositionAndRotation(ship.transform.TransformPoint(deckLocal),Quaternion.LookRotation(radial*.65f+tangent*.76f+Vector3.up*.2f));
            SetEffects(true);
            foreach(var feature in features)
                if(feature!=null&&feature.GetType().Name=="StormRainRendererFeature")feature.SetActive(false);
            renderedCameras.Clear();stageStarted=EditorApplication.timeSinceStartup;warmFrame=Time.frameCount;
        }
        static void SetupVolumeAngle()
        {
            float[] sd={-60,-60,-30};
            MoveShip(sd[stage]);
            var tangent=Vector3.Cross(Vector3.up,radial);
            var eye=ship.transform.TransformPoint(deckLocal);
            var direction=radial+Vector3.up*.18f;
            if(stage==0)direction=radial*.65f+tangent*.76f+Vector3.up*.2f;
            if(stage==2)direction=radial*.05f+tangent+Vector3.up*.12f;
            if(stage==1)direction=-radial+Vector3.up*.65f;
            if(stage==3)direction=radial+Vector3.up*.06f;
            camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(direction));
            stageStarted=EditorApplication.timeSinceStartup;warmFrame=Time.frameCount;
        }
        static void ShrinkPeek()
        {
            var zone=StormVolumeController.Instance;
            double elapsed=EditorApplication.timeSinceStartup-stageStarted;
            zone.ManualRadius=stage==0?Mathf.Lerp(4000,3980,Mathf.Clamp01((float)elapsed/3)):250;
            MoveShip(75);var eye=ship.transform.TransformPoint(deckLocal)+Vector3.Cross(Vector3.up,radial)*6;
            camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(-radial+Vector3.up*.55f));
            if(elapsed<3){if(elapsed>.2&&EditorApplication.timeSinceStartup-lastCapture>.15){lastCapture=EditorApplication.timeSinceStartup;Request("final-shrink-"+(motionFrame++).ToString("00"),()=>{});}return;}
            if(stage==0){stage=1;stageStarted=EditorApplication.timeSinceStartup;return;}
            Request("final-shrink-small",()=>{File.WriteAllLines(Folder+"/final-shrink.csv",rows);File.WriteAllText(Folder+"/final-shrink-done.txt","complete");Cleanup();});
        }
        static void SetupDeck()
        {
            moved = true; ship.enabled = false; if (network != null) network.enabled = false;
            body.rotation = Quaternion.LookRotation(radial); ship.transform.rotation = body.rotation; Physics.SyncTransforms();
            bool found = false;
            foreach (float x in new[] { 4f, -4f, 3f, -3f })
            {
                var origin = ship.transform.TransformPoint(new Vector3(x, 45, -13));
                var hits = Physics.RaycastAll(origin, Vector3.down, 60, ~0, QueryTriggerInteraction.Ignore).OrderByDescending(h => h.point.y);
                foreach (var hit in hits)
                {
                    if (!hit.collider.transform.IsChildOf(ship.transform) || hit.normal.y < .6f || hit.point.y - shipPosition.y > 22) continue;
                    var eye = hit.point + Vector3.up * 1.8f;
                    if (Physics.CheckSphere(eye, .35f, ~0, QueryTriggerInteraction.Ignore) || Physics.Raycast(eye, radial, 3, ~0, QueryTriggerInteraction.Ignore)) continue;
                    deckLocal = ship.transform.InverseTransformPoint(eye); found = true; break;
                }
                if (found) break;
            }
            if (!found) throw new InvalidOperationException("No open actual deck camera anchor");
            File.WriteAllText(Folder + "/final-deck-anchor.txt", "local=" + deckLocal + "\noriginal=" + shipPosition + "\nrotation=" + shipRotation + "\nR=" + StormVolumeController.Instance.CurrentRadius);
        }
        static void MoveShip(float distance)
        {
            var storm = StormVolumeController.Instance;
            var position = storm.CurrentCenter + radial * (storm.CurrentRadius + distance);
            position.y = shipPosition.y;
            var eyeOffset = Quaternion.LookRotation(radial) * deckLocal; eyeOffset.y = 0;
            position -= radial * Vector3.Dot(eyeOffset, radial);
            body.position = position; body.rotation = Quaternion.LookRotation(radial);
            ship.transform.SetPositionAndRotation(body.position, body.rotation); Physics.SyncTransforms();
        }
        static void SetupRouteStage()
        {
            SetEffects(true); RemoveMarkers();
            if (stage >= labels.Length)
            {
                MoveShip(mode==6?-40f:-15); stageStarted = EditorApplication.timeSinceStartup; lastCapture = 0; return;
            }
            MoveShip(distances[stage]);
            var eye = ship.transform.TransformPoint(deckLocal);
            var direction = radial - Vector3.up * .06f;
            if (stage == 6) { eye += Vector3.Cross(Vector3.up, radial) * 6; direction = -radial + Vector3.up * .8f; }
            if (stage == 7) direction = radial + Vector3.up * .10f;
            if (Physics.CheckSphere(eye, .35f, ~0, QueryTriggerInteraction.Ignore) || Physics.Raycast(eye, direction.normalized, 1.5f, ~0, QueryTriggerInteraction.Ignore)) throw new InvalidOperationException("Route camera blocked: " + labels[stage]);
            camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(direction));
            if (stage == 2 || stage == 3) { SetupMarkers(); if (stage == 2) SetEffects(false); }
            warmFrame = Time.frameCount; stageStarted=EditorApplication.timeSinceStartup;
        }
        static void SetupMarkers()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) throw new InvalidOperationException("QA marker shader absent");
            var values = new[] { 5f, 10f, 20f, 25f, 35f };
            for (int index = 0; index < values.Length; index++)
                for (int side = 0; side < 2; side++)
                {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = "Storm QA marker " + values[index];
                    UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
                    go.hideFlags = HideFlags.HideAndDontSave; go.layer = 1;
                    var direction = (camera.transform.forward + camera.transform.right * ((index - 2) * .22f) + Vector3.up * .45f).normalized;
                    go.transform.SetPositionAndRotation(camera.transform.position + direction * values[index] + camera.transform.right * (side == 0 ? -.19f : .19f), camera.transform.rotation);
                    go.transform.localScale = Vector3.one * .35f;
                    var material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                    material.SetColor("_BaseColor", side == 0 ? Color.white : Color.black);
                    go.GetComponent<Renderer>().sharedMaterial = material; markers.Add(go);
                    var screen = camera.WorldToScreenPoint(go.transform.position);
                    File.AppendAllText(Folder + "/final-markers.txt", "stage=" + stage + ";distance=" + values[index] + ";color=" + side + ";pixel=" + screen + "\n");
                }
        }
        static void RemoveMarkers()
        {
            foreach (var marker in markers)
                if (marker != null) { var mat = marker.GetComponent<Renderer>().sharedMaterial; UnityEngine.Object.DestroyImmediate(marker); UnityEngine.Object.DestroyImmediate(mat); }
            markers.Clear();
        }
        static void SetEffects(bool on)
        {
            for (int i = 0; i < features.Count; i++) if (features[i] != null) features[i].SetActive(on && featureStates[i]);
            if (billows != null) billows.enabled = on && billowsEnabled;
        }
        static void SetupPerformance()
        {
            started=EditorApplication.timeSinceStartup;
            if (moved && ship != null)
            {
                body.position = shipPosition; body.rotation = shipRotation; ship.transform.SetPositionAndRotation(shipPosition,shipRotation);
                ship.enabled = shipEnabled; if(network != null) network.enabled = networkEnabled; moved = false; Physics.SyncTransforms();
            }
            SetEffects(true); RemoveMarkers();
            camera.transform.SetPositionAndRotation(farEye, Quaternion.LookRotation(radial + Vector3.up * .10f));
            camera.fieldOfView = 80;
            profilerEnabled = ProfilerDriver.enabled;
            foreach (ProfilerArea area in Enum.GetValues(typeof(ProfilerArea))) { areas[area] = ProfilerDriver.IsAreaEnabled(area); ProfilerDriver.SetAreaEnabled(area, area == ProfilerArea.GPU); }
            ProfilerDriver.enabled = true; profiling = true;
            gpu = ProfilerRecorder.StartNew(ProfilerCategory.Render, "GPU Frame Time", 1);
            cpu = ProfilerRecorder.StartNew(ProfilerCategory.Render, "CPU Main Thread Frame Time", 1);
            bool localOnly=SessionState.GetInt("StormBillowQAMode",0)==3||SessionState.GetInt("StormBillowQAMode",0)==5;
            stage = localOnly?2:0; mode = 2; stageStarted = EditorApplication.timeSinceStartup; lastTimestamp = 0;
            if(localOnly)
            {
                moved=true;ship.enabled=false;if(network!=null)network.enabled=false;
                MoveShip(0);camera.transform.SetPositionAndRotation(ship.transform.TransformPoint(deckLocal),Quaternion.LookRotation(radial-Vector3.up*.06f));
            }
            File.WriteAllText(Folder + "/final-performance-conditions.txt", "GPU="+SystemInfo.graphicsDeviceName+";API="+SystemInfo.graphicsDeviceType+";Editor QHD2560x1440 FOV80; GPU-only Profiler, no PNG during timing; near all-on / main-cloud+wall+rain-features+billows off / all-on-confirm; each 2s warm +3s samples. PBSky reflection environment remains enabled; not a cloud-free world baseline. Near phases controlled actual ship at same R4000, scripts frozen equally for all phases; shelter retained. Features=" + string.Join("|", features.Select(f => f.name + ":" + f.GetType().Name)) + "\n");
        }
        static void NearCosts()
        {
            double elapsed=EditorApplication.timeSinceStartup-stageStarted;
            MoveShip(0);camera.transform.SetPositionAndRotation(ship.transform.TransformPoint(deckLocal),Quaternion.LookRotation(radial-Vector3.up*.06f));
            FrameTimingManager.CaptureFrameTimings();
            string[] phases={ "near-all-on","near-wall-off","near-billows-off","near-main-off","near-rain-off" };
            if(elapsed>1.0&&elapsed<3.0)
            {
                uint count=FrameTimingManager.GetLatestTimings(1,timings);
                double ms=0;ulong stamp=0;
                if(count>0&&timings[0].frameStartTimestamp!=lastTimestamp&&timings[0].gpuFrameTime>0)
                { ms=timings[0].gpuFrameTime;stamp=timings[0].frameStartTimestamp;lastTimestamp=stamp; }
                AddRow("perf",phases[stage],cpu.Valid?cpu.LastValue*1e-6:0,ms,stamp);
            }
            if(elapsed<3.0)return;
            SetEffects(true);stage++;
            if(stage>=phases.Length)
            {
                File.WriteAllLines(Folder+"/near-cost-phases.csv",rows);
                File.WriteAllText(Folder+"/near-cost-done.txt","complete=true");
                Cleanup();return;
            }
            if(stage==2&&billows!=null)billows.enabled=false;
            foreach(var feature in features)
            {
                string type=feature.GetType().Name;
                if((stage==1&&type=="StormVolumeRendererFeature")||(stage==3&&type=="VolumetricCloudsURP")||(stage==4&&type=="StormRainRendererFeature"))feature.SetActive(false);
            }
            stageStarted=EditorApplication.timeSinceStartup;
        }
        static void Performance()
        {
            if(mode==4){NearCosts();return;}
            double elapsed=EditorApplication.timeSinceStartup-stageStarted;
            if(stage>=3)
            {
                MoveShip(0);
                camera.transform.SetPositionAndRotation(ship.transform.TransformPoint(deckLocal),Quaternion.LookRotation(radial-Vector3.up*.06f));
            }
            FrameTimingManager.CaptureFrameTimings();
            if(elapsed>2.0 && elapsed<5.0)
            {
                uint count=FrameTimingManager.GetLatestTimings(1,timings);
                double gpuMs=0; ulong timestamp=0;
                if(count>0 && timings[0].frameStartTimestamp!=lastTimestamp && timings[0].gpuFrameTime>0)
                {
                    gpuMs=timings[0].gpuFrameTime;
                    timestamp=timings[0].frameStartTimestamp;
                    lastTimestamp=timestamp;
                }
                string[] phases={"far-on","far-off","far-confirm","inwall-on","inwall-off","inwall-confirm"};
                AddRow("perf",phases[stage],cpu.Valid?cpu.LastValue*1e-6:0,gpuMs,timestamp);
            }
            if(elapsed<5.0)return;
            File.WriteAllLines(Folder+"/dense3-performance.csv",rows);
            stage++;
            if(stage>=6)
            {
                File.WriteAllLines(Folder+"/dense3-performance.csv",rows);
                File.WriteAllText(Folder+"/dense3-performance-done.txt","complete; all camera/features/Profiler state restored by cleanup");
                Cleanup();
                return;
            }
            SetEffects(stage!=1 && stage!=4);
            if(stage==3)
            {
                SetupDeck();
                MoveShip(0);
                camera.transform.SetPositionAndRotation(ship.transform.TransformPoint(deckLocal),Quaternion.LookRotation(radial-Vector3.up*.06f));
            }
            stageStarted=EditorApplication.timeSinceStartup;
        }
        static void AddRow(string kind, string label, double cpuMs = 0, double gpuMs = 0, ulong timestamp = 0)
        {
            var storm = StormVolumeController.Instance; var eye = camera.transform.position;
            float sd = Vector2.Distance(new Vector2(eye.x, eye.z), new Vector2(storm.CurrentCenter.x, storm.CurrentCenter.z)) - storm.CurrentRadius;
            rows.Add(string.Join(",", new object[] { kind, label, Time.frameCount, EditorApplication.timeSinceStartup.ToString("F6", System.Globalization.CultureInfo.InvariantCulture), eye.x, eye.y, eye.z, storm.CurrentRadius, sd, camera.fieldOfView, Shader.GetGlobalVector("_StormLightning").w, Shader.GetGlobalVector("_StormLightningSecondary").w, Time.unscaledDeltaTime * 1000, cpuMs, gpuMs, timestamp, profiling && gpu.Valid ? gpu.LastValue * 1e-6 : 0, profiling ? ProfilerDriver.lastFrameIndex : -1, StormRainController.Instance != null && StormRainController.Instance.Exposed ? 1 : 0, StormRainController.Instance != null ? StormRainController.Instance.Intensity : 0, Shader.GetGlobalFloat("_PirateStormNearMedium"),NearCells(),NearIdentity() }.Select(value => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture))));
        }
        static void DumpBinding(string label)
        {
            string text="Camera="+camera.name+";main="+Camera.main?.name+"\nVolume3D="+Shader.GetGlobalFloat("_PirateStormVolume3D")+"\nDiagnostic="+Shader.GetGlobalFloat("_PirateStormFieldDiagnostic")+"\nShape="+Shader.GetGlobalVector("_StormShape")+"\nBand="+Shader.GetGlobalVector("_StormBand");
            foreach(string id in new[]{"_StormVolumeDensity","_PirateStormNearNoise"})
            {
                var texture=Shader.GetGlobalTexture(id);
                text+="\n"+id+"="+(texture==null?"null":texture.name+";path="+AssetDatabase.GetAssetPath(texture)+";format="+texture.graphicsFormat+";sRGB="+texture.isDataSRGB);
            }
            foreach(var feature in features)
            {
                if(feature==null||feature.GetType().Name!="StormVolumeRendererFeature")continue;
                var material=(Material)feature.GetType().GetField("material",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(feature);
                text+="\nFeatureActive="+feature.isActive+";material="+material.name+";shader="+material.shader.name+";path="+AssetDatabase.GetAssetPath(material.shader)+";VolumeProperty="+material.HasProperty("_PirateStormVolume3D")+";keywords="+string.Join("|",material.shaderKeywords)+";shaderMessages="+ShaderUtil.GetShaderMessages(material.shader).Length;
            }
            File.WriteAllText(Folder+"/"+label+"-bindings.txt",text);
        }
        static int NearCells()=>billows==null?0:(int)typeof(StormBillowController).GetField("nearCells",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(billows);
        static string NearIdentity()
        {
            return Shader.GetGlobalFloat("_PirateStormVolume3D")>.5f?"volume3D-64":"legacy";
        }
        static void Request(string label, Action done)
        {
            capturing = true;pendingLabel=label;pendingDone=done;
        }
        static void CameraRendered(ScriptableRenderContext context,Camera rendered)
        {
            if(mode==9&&rendered!=null){renderedCameras.TryGetValue(rendered.name,out int count);renderedCameras[rendered.name]=count+1;}
            if(rendered!=camera||pendingLabel==null)return;
            string label=pendingLabel;Action done=pendingDone;pendingLabel=null;pendingDone=null;
            AddRow("image",label);
            if(mode==9)DumpBinding(label);
            if(mode==9)File.AppendAllText(Folder+"/volume3-camera-routing.txt",label+";main="+(Camera.main==null?"null":Camera.main.name)+";rendered="+string.Join("|",renderedCameras.Select(p=>p.Key+":"+p.Value))+";disabledForQA="+string.Join("|",otherCameras.Select(c=>c.name))+"\n");
            AsyncGPUReadback.Request(target, 0, TextureFormat.RGBA32, request =>
            {
                try
                {
                    if (request.hasError) throw new InvalidOperationException("Storm GPU readback failed");
                    var pixels = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
                    pixels.LoadRawTextureData(request.GetData<byte>()); pixels.Apply(false);
                    if (!SystemInfo.graphicsUVStartsAtTop)
                    {
                        var buffer = pixels.GetPixels32(); var flipped = new Color32[buffer.Length];
                        for (int y = 0; y < target.height; y++) Array.Copy(buffer, y * target.width, flipped, (target.height - 1 - y) * target.width, target.width);
                        pixels.SetPixels32(flipped); pixels.Apply(false);
                    }
                    File.WriteAllBytes(Folder + "/" + label + ".png", pixels.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(pixels);
                    File.WriteAllLines(Folder + "/final-route-live.csv", rows);
                    capturing = false; done();
                }
                catch (Exception error) { Debug.LogException(error); Cleanup(); }
            });
        }
    }
}
