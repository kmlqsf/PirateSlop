using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using PirateSlop.Networking;
using PirateSlop.World;

namespace PirateSlop
{
    public sealed class WaterTestCapture : MonoBehaviour
    {
        string directory;
        Camera captureCamera;
        ShipController ship;
        bool driving, followShip, foamDrive;
        bool detailCapture, underwaterDetail, impactCapture;
        GameObject impactFixture;
        Vector3 impactFixturePosition, impactFixtureVelocity;
        int closeView;
        Vector3 detailXZ, detailDirection;
        float detailDepth;
        Vector3 foamPosition;
        float foamSpeed, foamYaw, foamTurn;
        Vector3 driveStart;
        float driveTime, waterlineOffset;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Initialize()
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-watercapture");
            if (index < 0 || index + 1 >= args.Length) return;
            var runner = new GameObject("Water Test Capture");
            DontDestroyOnLoad(runner);
            runner.AddComponent<WaterTestCapture>().directory = args[index + 1];
        }
        IEnumerator Start()
        {
            yield return null;
            Directory.CreateDirectory(directory);
            float timeout = Time.realtimeSinceStartup + 120f;
            while (ShipController.ActiveControllers.Count == 0 || Camera.main == null || ProceduralWorld.Instance == null || !ProceduralWorld.Instance.Ready)
            {
                if (Time.realtimeSinceStartup > timeout) { Debug.LogError("WATER_CAPTURE_START_TIMEOUT"); Application.Quit(2); yield break; }
                yield return null;
            }
            yield return new WaitForSecondsRealtime(4f);
            ship = ShipController.ActiveControllers[0];
            var surface = OceanSurface.Instance;
            Debug.Log("WATER_CAPTURE_READY scene=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name +
                " sea=" + surface.SeaLevel + " center=" + surface.WhirlpoolCenter + " radius=" + surface.WhirlpoolRadius + " depth=" + surface.WhirlpoolDepth);
            foreach (var material in Resources.FindObjectsOfTypeAll<Material>().Where(m => m.shader != null && m.shader.name == "Boat Attack/Water"))
                Debug.Log("WATER_CAPTURE_MATERIAL name=" + material.name + " supported=" + material.shader.isSupported + " instanced=" + material.enableInstancing + " keywords=" + string.Join(",", material.shaderKeywords));
            if (impactCapture || Array.IndexOf(Environment.GetCommandLineArgs(), "-waterimpactcapture") >= 0)
            {
                yield return CaptureImpacts();
                Debug.Log("WATER_IMPACT_CAPTURE_COMPLETE");
                if (!Application.isEditor) Application.Quit();
                yield break;
            }
            if (detailCapture || Array.IndexOf(Environment.GetCommandLineArgs(), "-waterdetailcapture") >= 0)
            {
                detailCapture = true;
                yield return CaptureDetails();
                Debug.Log("WATER_DETAIL_CAPTURE_COMPLETE");
                if (!Application.isEditor) Application.Quit();
                yield break;
            }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-foamcapture") >= 0)
            {
                yield return CaptureFoam();
                if (Array.IndexOf(Environment.GetCommandLineArgs(), "-buoyancycapture") >= 0) yield return CaptureBuoyancy();
                Debug.Log("WATER_FOAM_CAPTURE_COMPLETE");
                Application.Quit();
                yield break;
            }
            yield return Capture("01-player");
            var source = Camera.main;
            source.enabled = false;
            source.tag = "Untagged";
            captureCamera = new GameObject("Water Verification Camera").AddComponent<Camera>();
            captureCamera.CopyFrom(source);
            captureCamera.tag = "MainCamera";
            captureCamera.enabled = true;
            captureCamera.GetUniversalAdditionalCameraData().requiresColorTexture = true;
            captureCamera.GetUniversalAdditionalCameraData().requiresDepthTexture = true;
            captureCamera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            var p = ship.transform.position;
            Frame(p + new Vector3(35f, 13f, -37f), p + Vector3.up);
            yield return Capture("02-hull");
            Frame(p + new Vector3(0f, 8f, 20f), surface.WhirlpoolCenter + new Vector3(0f, -25f, 0f));
            yield return Capture("03-deck-vortex");
            Frame(surface.WhirlpoolCenter + new Vector3(-370f, 230f, -450f), surface.WhirlpoolCenter + new Vector3(0f, -45f, 0f));
            yield return Capture("04-vortex-overview");
            Frame(surface.WhirlpoolCenter + new Vector3(-135f, 25f, -205f), surface.WhirlpoolCenter + new Vector3(0f, -90f, 0f));
            yield return Capture("05-vortex-approach");
            var inside = surface.WhirlpoolCenter + new Vector3(-150f, 0f, -90f);
            inside.y = surface.Height(inside) + 12f;
            Frame(inside, surface.WhirlpoolCenter + new Vector3(0f, -surface.WhirlpoolDepth + 3f, 0f));
            yield return Capture("06-vortex-inside");
            driveStart = p + new Vector3(-300f, 0f, 100f);
            waterlineOffset = p.y - surface.Height(p);
            driveTime = Time.realtimeSinceStartup;
            driving = followShip = true;
            yield return new WaitForSecondsRealtime(12f);
            yield return Capture("07-moving-wake");
            yield return new WaitForSecondsRealtime(2f);
            yield return Capture("08-moving-wake-later");
            driving = followShip = false;
            var ocean = surface.HeightSource as BoatAttackOcean;
            var sky = TestSkyDayNight.Active;
            Frame(p + new Vector3(-60f, 26f, -75f), p + new Vector3(120f, 5f, 0f));
            if (ocean != null)
            {
                SessionController.Instance.SetTestOcean(0f, 1f, 1f);
                yield return Capture("09-zero-waves");
                SessionController.Instance.SetTestOcean(2f, 1f, 2f);
                yield return Capture("10-strong-waves");
                SessionController.Instance.SetTestOcean(1f, 1f, 1f);
            }
            if (sky != null)
            {
                sky.TargetBlend = .6f;
                yield return new WaitForSecondsRealtime(4f);
                yield return Capture("11-sunset");
                sky.TargetBlend = 1f;
                yield return new WaitForSecondsRealtime(4f);
                yield return Capture("12-night");
            }
            Debug.Log("WATER_CAPTURE_COMPLETE");
            Application.Quit();
        }
        void LateUpdate()
        {
            if (impactFixture != null)
            {
                impactFixtureVelocity += Physics.gravity * Time.deltaTime;
                impactFixturePosition += impactFixtureVelocity * Time.deltaTime;
                impactFixture.transform.position = impactFixturePosition;
            }
            if (driving && ship != null)
            {
                var state = ship.Capture();
                if (foamDrive)
                {
                    foamYaw += foamTurn * Time.unscaledDeltaTime;
                    foamPosition += Quaternion.Euler(0, foamYaw, 0) * Vector3.forward * (foamSpeed * Time.unscaledDeltaTime);
                }
                state.Position = foamDrive ? foamPosition : driveStart + Vector3.forward * ((Time.realtimeSinceStartup - driveTime) * 8f);
                state.Position.y = OceanSurface.Instance.Height(state.Position) + waterlineOffset;
                state.Yaw = foamDrive ? foamYaw : 0f;
                state.Pitch = state.Bank = state.WaveRoll = 0f;
                state.Speed = foamDrive ? foamSpeed : 8f;
                state.Sail = 1f;
                state.Controlling = false;
                ship.Restore(state, null);
            }
            if (followShip && captureCamera != null)
            {
                if (detailCapture) FrameDetails();
                else Frame(ship.transform.position + new Vector3(34f, 20f, -65f), ship.transform.position + new Vector3(0f, -1f, -28f));
            }
            if (underwaterDetail && captureCamera != null)
            {
                detailXZ.y = OceanSurface.Instance.Height(detailXZ) - detailDepth;
                Frame(detailXZ, detailXZ + detailDirection);
            }
        }
        IEnumerator CaptureImpacts()
        {
            var source = Camera.main;
            source.enabled = false;
            source.tag = "Untagged";
            captureCamera = new GameObject("Water Impact Verification Camera").AddComponent<Camera>();
            captureCamera.CopyFrom(source);
            captureCamera.tag = "MainCamera";
            var data = captureCamera.GetUniversalAdditionalCameraData();
            data.requiresColorTexture = data.requiresDepthTexture = data.renderPostProcessing = true;
            SessionController.Instance.SetTestOcean(0f, 1f, 1f);
            yield return new WaitForSecondsRealtime(2f);
            var ocean = OceanSurface.Instance;
            var basePoint = ship.transform.position + new Vector3(-45f, 0, 20f);
            for (int test = 0; test < 6; test++)
            {
                string label = new[] { "light", "heavy", "large", "person", "creature", "cannon" }[test];
                var point = basePoint + Vector3.forward * (test * 6f);
                point.y = ocean.Height(point);
                float radius = test == 2 ? .6f : test == 3 ? .3f : test == 4 ? .35f : .06f;
                float mass = test == 0 ? .1f : test == 3 ? 80f : test == 4 ? 4f : 8f;
                GameObject fixture;
                if (test == 3)
                {
                    fixture = new GameObject("Water impact person fixture");
                    fixture.transform.position = point + Vector3.up * 2f;
                    var controller = fixture.AddComponent<CharacterController>();
                    controller.height = 1.8f; controller.radius = .3f; controller.center = Vector3.up * .9f;
                    var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    Destroy(visual.GetComponent<Collider>());
                    visual.transform.SetParent(fixture.transform, false);
                    visual.transform.localPosition = Vector3.up * .9f;
                    visual.transform.localScale = new Vector3(.6f, .9f, .6f);
                    visual.GetComponent<Renderer>().sharedMaterial = Resources.Load<Material>("Underwater/Seabed");
                    WaterImpactBody.Ensure(fixture);
                    impactFixture = fixture;
                    impactFixturePosition = fixture.transform.position;
                    impactFixtureVelocity = new Vector3(.3f, -5f, 0);
                }
                else
                {
                    fixture = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    fixture.name = "Water impact " + label + " fixture";
                    fixture.GetComponent<Renderer>().sharedMaterial = Resources.Load<Material>("Underwater/Seabed");
                    fixture.transform.localScale = Vector3.one * radius * 2f;
                    fixture.transform.position = point + Vector3.up * (2f + radius);
                    if (test == 5)
                    {
                        fixture.transform.localScale = Vector3.one * .24f;
                        var shot = fixture.AddComponent<CannonShotDamage>();
                        shot.Velocity = new Vector3(35f, -10f, 0f);
                        shot.Radius = .12f; shot.Authoritative = false;
                    }
                    else
                    {
                        var rb = fixture.AddComponent<Rigidbody>();
                        rb.mass = mass; rb.linearDamping = 0f;
                        rb.linearVelocity = new Vector3(0, -5f, 0);
                        WaterImpactBody.Ensure(fixture);
                    }
                }
                Frame(point + new Vector3(-3f, 2f, -3f), point + Vector3.up * .5f);
                int before = WaterImpactPhysics.Instance.ImpactCount;
                float timeout = Time.realtimeSinceStartup + 3f;
                while (WaterImpactPhysics.Instance.ImpactCount == before && Time.realtimeSinceStartup < timeout) yield return null;
                if (WaterImpactPhysics.Instance.ImpactCount == before)
                    Debug.LogError("WATER_IMPACT_MISSING " + label);
                var impact = WaterImpactPhysics.Instance.LastImpact;
                point = impact.Position;
                Frame(point + new Vector3(-3f, 2f, -3f), point + Vector3.up * .5f + (test == 5 ? Vector3.right * 1.2f : Vector3.zero));
                Debug.Log("WATER_IMPACT_EVENT " + label + " drops=" + impact.Drops + " mass=" + impact.Mass + " radius=" + impact.Radius + " normalSpeed=" + impact.NormalSpeed + " drift=" + impact.Drift + " energy=" + impact.Energy + " kind=" + impact.Kind);
                foreach (float delay in new[] { .04f, .10f, .22f, .45f })
                {
                    yield return new WaitForSecondsRealtime(delay);
                    yield return Capture("impact-" + label + "-" + delay, true);
                }
                int after = WaterImpactPhysics.Instance.ImpactCount;
                yield return new WaitForSecondsRealtime(.6f);
                if (WaterImpactPhysics.Instance.ImpactCount != after) Debug.LogError("WATER_IMPACT_REPEATED " + label);
                impactFixture = null;
                if (fixture != null) Destroy(fixture);
                yield return new WaitForSecondsRealtime(2f);
            }
            SessionController.Instance.SetTestOcean(1f, 1f, 1f);
            var sprayPoint = ship.transform.TransformPoint(new Vector3(-7f, 0f, 16f));
            sprayPoint.y = ocean.Height(sprayPoint);
            Frame(sprayPoint + Vector3.left * 3f + Vector3.up * 2.2f - ship.transform.forward * 2f, sprayPoint + Vector3.up * .7f);
            int bowBefore = WaterBowSpray.Instance.BowBurstCount;
            float bowTimeout = Time.realtimeSinceStartup + 16f;
            while (WaterBowSpray.Instance.BowBurstCount == bowBefore && Time.realtimeSinceStartup < bowTimeout) yield return null;
            if (WaterBowSpray.Instance.BowBurstCount == bowBefore) Debug.LogError("WATER_BOW_IMPACT_MISSING");
            else
            {
                sprayPoint = WaterBowSpray.Instance.LastBowPosition;
                var outward = Vector3.ProjectOnPlane(WaterBowSpray.Instance.LastBowNormal, Vector3.up).normalized;
                captureCamera.fieldOfView = 60f;
                Frame(sprayPoint + outward * 7f + Vector3.up * 4f - ship.transform.forward * 1.5f, sprayPoint + Vector3.up * .7f);
                for (int frame = 0; frame < 3; frame++)
                {
                    yield return new WaitForSecondsRealtime(.10f);
                    yield return Capture("impact-bow-close-" + frame, true);
                }
            }
            var freeWater = ship.transform.position - ship.transform.right * 35f + ship.transform.forward * 30f;
            UnderwaterFrame(freeWater, new Vector3(.2f, 1f, .35f), 1.5f);
            yield return Capture("impact-underwater");
            underwaterDetail = false;
            yield return CaptureWhaleVisibility();
        }

        IEnumerator CaptureDetails()
        {
            var source = Camera.main;
            source.enabled = false;
            source.tag = "Untagged";
            captureCamera = new GameObject("Water Detail Verification Camera").AddComponent<Camera>();
            captureCamera.CopyFrom(source);
            captureCamera.tag = "MainCamera";
            var data = captureCamera.GetUniversalAdditionalCameraData();
            data.requiresColorTexture = data.requiresDepthTexture = data.renderPostProcessing = true;
            var p = ship.transform.position;
            foamPosition = p + new Vector3(-300f, 0, 100f);
            waterlineOffset = p.y - OceanSurface.Instance.Height(p);
            driving = foamDrive = followShip = true;
            foamSpeed = foamYaw = foamTurn = 0;
            closeView = 0;
            SessionController.Instance.SetTestOcean(0f, 1f, 1f);
            yield return new WaitForSecondsRealtime(4f);
            yield return Capture("detail-01-calm-side");
            SessionController.Instance.SetTestOcean(1f, 1f, 1f);
            for (int i = 0; i < 4; i++)
            {
                yield return new WaitForSecondsRealtime(.4f);
                yield return Capture("detail-0" + (i + 2) + "-wave-side");
            }
            foamSpeed = 8f;
            yield return new WaitForSecondsRealtime(5f);
            yield return Capture("detail-06-moving-side");
            closeView = 1;
            yield return Capture("detail-07-wake-close");
            yield return Capture("detail-08-wake-later");
            foamTurn = 18f;
            yield return new WaitForSecondsRealtime(3f);
            yield return Capture("detail-09-turn-close");
            foamSpeed = foamTurn = 0;
            yield return Capture("detail-10-stop-close");
            yield return new WaitForSecondsRealtime(5f);
            yield return Capture("detail-11-old-wake");
            SessionController.Instance.SetTestOcean(0f, 1f, 1f);
            yield return new WaitForSecondsRealtime(9f);
            yield return Capture("detail-12-dissipated");
                        closeView = 2;
            SessionController.Instance.SetTestOcean(0f, 1f, 1f);
            foreach (float speed in new[] { 0f, 2f, 5f, 8f, -4f })
            {
                foamSpeed = speed;
                yield return new WaitForSecondsRealtime(3f);
                yield return Capture("bow-speed-" + speed);
            }
            foamSpeed = 8f;
            SessionController.Instance.SetTestOcean(1.8f, 1f, 1f);
            yield return new WaitForSecondsRealtime(3f);
            for (int i = 0; i < 8; i++)
            {
                yield return Capture("bow-impact-" + i, true);
                yield return new WaitForSecondsRealtime(.15f);
            }
            
            for (int impact = 0; impact < 2; impact++)
            {
                var spray = WaterBowSpray.Instance;
                int before = spray != null ? spray.BurstCount : 0;
                float timeout = Time.realtimeSinceStartup + 12f;
                while (spray != null && spray.BurstCount == before && Time.realtimeSinceStartup < timeout) yield return null;
                for (int frame = 0; frame < 3; frame++)
                {
                    yield return new WaitForSecondsRealtime(.06f);
                    yield return Capture("bow-spray-" + impact + "-" + frame, true);
                }
            }
            closeView = 3;
            yield return Capture("bow-deck");
            var sky = TestSkyDayNight.Active;
            if (sky != null)
            {
                sky.TargetBlend = 1f;
                yield return new WaitForSecondsRealtime(4f);
                yield return Capture("bow-night");
                sky.TargetBlend = 0f;
                yield return new WaitForSecondsRealtime(4f);
            }
            closeView = 2;
            foamTurn = 18f;
            yield return new WaitForSecondsRealtime(3f);
            yield return Capture("bow-turn");
            foamSpeed = foamTurn = 0;
            yield return new WaitForSecondsRealtime(3f);
            yield return Capture("bow-stopped");
            SessionController.Instance.SetTestOcean(1f, 1f, 1f);
            driving = followShip = false;
            var beside = ship.transform.TransformPoint(new Vector3(-14f, 0, 0));
            UnderwaterFrame(beside, new Vector3(0, 1, .15f), 1.5f);
            yield return Capture("detail-13-under-up");
            UnderwaterFrame(beside, new Vector3(-1, .2f, .1f), 2f);
            yield return Capture("detail-14-under-grazing");
            UnderwaterFrame(beside, new Vector3(.1f, 1, .1f), 8f);
            yield return Capture("detail-15-under-deep");
            UnderwaterFrame(beside, ship.transform.position - beside + Vector3.up * 1.5f, 2f);
            yield return Capture("detail-16-under-hull");
            UnderwaterFrame(beside, new Vector3(-1, -.25f, .1f), -.3f);
            yield return Capture("detail-17-cross-above");
            UnderwaterFrame(beside, new Vector3(-1, .25f, .1f), .3f);
            yield return Capture("detail-18-cross-below");
            var cup = OceanSurface.Instance.WhirlpoolCenter + new Vector3(-120f, 0, -40f);
            UnderwaterFrame(cup, new Vector3(1, -.4f, .2f), -3f);
            yield return Capture("detail-19-cup-air");
            UnderwaterFrame(cup, new Vector3(.3f, 1, .2f), 2f);
            yield return Capture("detail-20-cup-under");
            yield return CaptureWhaleVisibility();
        }

        IEnumerator CaptureWhaleVisibility()
        {
            var poi = GameObject.Find("WhaleLootPOI(Clone)");
            if (poi == null) yield break;
            Renderer whale = null;
            foreach (var renderer in poi.GetComponentsInChildren<Renderer>())
                if (renderer.name == "Whale") { whale = renderer; break; }
            if (whale == null) yield break;
            var bounds = whale.bounds;
            var target = bounds.center;
            target.y = Mathf.Min(target.y, OceanSurface.Instance.SeaLevel - 3.3f);
            captureCamera.fieldOfView = 60f;
            underwaterDetail = false;
            foreach (float distance in new[] { 5f, 15f, 30f, 60f })
            {
                var eye = target + Vector3.right * (bounds.extents.x + distance);
                Frame(eye, target);
                yield return Capture("detail-21-poi-" + distance);
            }
        }

        void UnderwaterFrame(Vector3 position, Vector3 direction, float depth)
        {
            detailXZ = position;
            detailDirection = direction;
            detailDepth = depth;
            underwaterDetail = true;
        }

        void FrameDetails()
        {
            if (closeView == 0)
                Frame(ship.transform.TransformPoint(new Vector3(-7.5f, 6f, -1f)), ship.transform.TransformPoint(new Vector3(-9f, .1f, 4f)));
            else if (closeView == 2)
                Frame(ship.transform.TransformPoint(new Vector3(-8f, 5f, 16f)), ship.transform.TransformPoint(new Vector3(-2.5f, .1f, 18f)));
            else if (closeView == 3)
                Frame(ship.transform.TransformPoint(new Vector3(0, 10f, 13f)), ship.transform.TransformPoint(new Vector3(0, .1f, 25f)));
            else
                Frame(ship.transform.TransformPoint(new Vector3(-8.5f, 5f, -16f)), ship.transform.TransformPoint(new Vector3(-8f, 0, -29f)));
        }

        IEnumerator CaptureFoam()
        {
            var source = Camera.main;
            source.enabled = false;
            source.tag = "Untagged";
            captureCamera = new GameObject("Foam Verification Camera").AddComponent<Camera>();
            captureCamera.CopyFrom(source);
            captureCamera.tag = "MainCamera";
            var data = captureCamera.GetUniversalAdditionalCameraData();
            data.requiresColorTexture = data.requiresDepthTexture = data.renderPostProcessing = true;
            var p = ship.transform.position;
            foamPosition = p + new Vector3(-300f, 0, 100f);
            waterlineOffset = p.y - OceanSurface.Instance.Height(p);
            driving = foamDrive = true;
            foamSpeed = foamYaw = foamTurn = 0;
            SessionController.Instance.SetTestOcean(0f, 1f, 1f);
            Frame(foamPosition + new Vector3(27f, 23f, -42f), foamPosition);
            yield return new WaitForSecondsRealtime(4f);
            yield return Capture("foam-01-calm");
            yield return new WaitForSecondsRealtime(1f);
            yield return Capture("foam-02-calm-later");
            SessionController.Instance.SetTestOcean(1f, 1f, 1f);
            for (int i = 0; i < 4; i++)
            {
                yield return new WaitForSecondsRealtime(.5f);
                yield return Capture("foam-0" + (i + 3) + "-wave-hit");
            }
            SessionController.Instance.SetTestOcean(0f, 1f, 1f);
            yield return new WaitForSecondsRealtime(4f);
            yield return Capture("foam-07-calm-recovered");
            foamSpeed = 8f;
            followShip = true;
            yield return new WaitForSecondsRealtime(7f);
            yield return Capture("foam-08-straight");
            foamTurn = 18f;
            yield return new WaitForSecondsRealtime(3f);
            yield return Capture("foam-09-turn");
            yield return new WaitForSecondsRealtime(2f);
            yield return Capture("foam-10-turn-later");
            foamSpeed = foamTurn = 0;
            yield return Capture("foam-11-stopped");
            followShip = false;
            Frame(foamPosition + new Vector3(0, 80f, -35f), foamPosition + new Vector3(0, 0, -15f));
            yield return Capture("foam-12-stopped-overview");
            var returnPosition = captureCamera.transform.position;
            var returnRotation = captureCamera.transform.rotation;
            Frame(foamPosition + new Vector3(700f, 80f, 0), foamPosition + new Vector3(700f, 0, 0));
            yield return Capture("foam-13-camera-away");
            captureCamera.transform.SetPositionAndRotation(returnPosition, returnRotation);
            yield return Capture("foam-14-camera-return");
            yield return new WaitForSecondsRealtime(8f);
            yield return Capture("foam-15-dissipated");
            SessionController.Instance.SetTestOcean(1f, 1f, 1f);
        }

        IEnumerator CaptureBuoyancy()
        {
            driving = foamDrive = followShip = false;
            var state = ship.Capture();
            state.Speed = state.Sail = state.Rudder = 0;
            state.Pitch = state.WaveRoll = state.Bank = 0;
            state.Yaw = 0;
            ship.Restore(state, null);
            ship.SetAnchored(true);
            var support = new ShipBuoyancy(18f, 5.5f, ship.HullFootprint);
            Frame(ship.transform.position + new Vector3(38f, 10f, 0), ship.transform.position + Vector3.up * 2f);
            yield return new WaitForSecondsRealtime(4f);
            for (int i = 0; i < 8; i++)
            {
                var actual = ship.Capture();
                var target = support.Evaluate(actual.Position, actual.Yaw, OceanSurface.Instance.Height);
                Debug.Log("WATER_BUOYANCY " + i + " target=" + target + " actualHeight=" + actual.Position.y + " pitch=" + actual.Pitch + " roll=" + actual.WaveRoll);
                yield return Capture("buoyancy-0" + (i + 1) + "-wave");
                yield return new WaitForSecondsRealtime(.25f);
            }
            SessionController.Instance.SetTestOcean(0f, 1f, 1f);
            yield return new WaitForSecondsRealtime(4f);
            var flat = ship.Capture();
            Debug.Log("WATER_BUOYANCY_FLAT height=" + flat.Position.y + " pitch=" + flat.Pitch + " roll=" + flat.WaveRoll);
            yield return Capture("buoyancy-09-flat");
            SessionController.Instance.SetTestOcean(1f, 1f, 1f);
        }

        void Frame(Vector3 position, Vector3 target)
        {
            captureCamera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position, Vector3.up));
        }
        IEnumerator Capture(string name, bool immediate = false)
        {
            if (!immediate) yield return new WaitForSecondsRealtime(.6f);
            yield return new WaitForEndOfFrame();
            if (WaterShipFoam.Instance != null)
            {
                var foam = WaterShipFoam.Instance;
                Debug.Log("WATER_FOAM_STATS " + name + " contacts=" + foam.ActiveContactCount + " wakes=" + foam.ActiveWakeCount + " bursts=" + foam.ContactBursts + " bowPackets=" + foam.BowPackets + " bowSpeed=" + foam.MaximumBowSpeed + " position=" + ship.transform.position);
            }
            if (WaterBowSpray.Instance != null) Debug.Log("WATER_SPRAY_STATS " + name + " drops=" + WaterBowSpray.Instance.ActiveCount + " bursts=" + WaterBowSpray.Instance.BurstCount);
            string path = Path.Combine(directory, name + ".png");
            var target = RenderTexture.GetTemporary(1280, 720, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active;
            var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(Camera.main, new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = target });
                Debug.Log("WATER_MEDIUM " + name + " state=" + Shader.GetGlobalVector("_BoatAttack_CameraWater") + " pass=" + Shader.GetGlobalFloat("_BoatAttack_UnderwaterPass"));
                var sky = TestSkyDayNight.Active;
                if (sky != null)
                {
                    sky.Environment.profile.TryGet<PhysicallyBasedSky>(out var settings);
                    var stacked = UnityEngine.Rendering.VolumeManager.instance.stack?.GetComponent<PhysicallyBasedSky>();
                    Debug.Log("WATER_CAPTURE_SKY " + name + " blend=" + sky.CurrentBlend + " profileEV=" + settings?.exposure.value + " stackEV=" + stacked?.exposure.value + " moon=" + stacked?.moonBody.value + " intensity=" + Shader.GetGlobalFloat("_IntensityMultiplier") + " skybox=" + RenderSettings.skybox?.shader.name);
                }
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                Destroy(pixels);
            }
            if (!immediate) yield return new WaitForSecondsRealtime(1f);
            Debug.Log("WATER_CAPTURE_FRAME " + path + " camera=" + Camera.main.transform.position);
        }
    }
}
