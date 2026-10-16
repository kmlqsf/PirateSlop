using System;
using System.Collections.Generic;
using PirateSlop.Networking;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;
using PirateSlop.Ships;

namespace PirateSlop.Performance
{
    public sealed class PerformanceIsolation : IDisposable
    {
        public static readonly string[] Stages = { "Baseline start", "No ship rendering", "No ship cloth and ropes", "No player presentation", "No idle world animation", "No water impact tracking", "No cloud rendering", "No water surface", "No shadows", "Baseline end" };
        public static readonly string[] ShipStages = { "Baseline start", "No batch colour", "No separate fixtures", "No ship skinned rendering", "No ship cloth updates", "No ship shadows", "No batch polling", "Half render scale", "Half scale no ships", "No ship rendering", "Baseline end" };
        public static bool SkipBatchPolling { get; private set; }
        readonly bool shipDetail;
        readonly List<Renderer> batchColour = new(), fixtures = new(), skinned = new();
        public float ExpectedScale { get; private set; }
        readonly float originalScale;
        readonly List<Renderer> shipRenderers = new(), playerRenderers = new();
        readonly List<Behaviour> shipCosmetics = new(), playerCosmetics = new(), worldCosmetics = new(), waterTracking = new();
        readonly List<ScriptableRendererFeature> clouds = new();
        readonly List<Action> undo = new();
        readonly UniversalRenderPipelineAsset pipeline;
        readonly WaterSystem.Water water;
        readonly bool telemetry;
        int stage = -1;

        public PerformanceIsolation(Camera camera, UniversalRenderPipelineAsset asset, bool detail = false)
        {
            pipeline = asset;
            shipDetail = detail; originalScale = ExpectedScale = asset.renderScale;
            telemetry = GameTelemetry.Visible;
            GameTelemetry.SetVisible(false);
            foreach (var renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (renderer.GetComponentInParent<NetworkPlayer>() != null) playerRenderers.Add(renderer);
                else if (renderer.GetComponentInParent<NetworkShip>() is { } ship && ship.TeamId.Value > 0) shipRenderers.Add(renderer);
            }
            foreach (var behaviour in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (!behaviour.enabled) continue;
                string type = behaviour.GetType().Name;
                bool onShip = behaviour.GetComponentInParent<NetworkShip>() is { } ship && ship.TeamId.Value > 0;
                if (type == "ShipV3ClothMotion" || type == "RiggingRopeVisual" && onShip) shipCosmetics.Add(behaviour);
                if (type == "WeaponArmRig" || type == "SwimPresentation") playerCosmetics.Add(behaviour);
                if (type == "SandyIslandFoliageWind" || type == "RiggingRopeVisual" && !onShip || behaviour is TentacleAnimator tentacle && !tentacle.IsAttacking) worldCosmetics.Add(behaviour);
                if (behaviour is WaterImpactBody) waterTracking.Add(behaviour);
            }
            foreach (var animator in UnityEngine.Object.FindObjectsByType<Animator>(FindObjectsSortMode.None))
                if (animator.enabled && animator.GetComponentInParent<NetworkPlayer>() != null) playerCosmetics.Add(animator);
            var selected = camera.GetUniversalAdditionalCameraData().scriptableRenderer;
            for (int i = 0; i < asset.rendererDataList.Length; i++)
                if (asset.rendererDataList[i] != null && asset.GetRenderer(i) == selected)
                    foreach (var feature in asset.rendererDataList[i].rendererFeatures)
                        if (feature != null && feature.isActive && (feature is VolumetricCloudsURP || feature is StormVolumeRendererFeature)) clouds.Add(feature);
            water = (OceanSurface.Instance.HeightSource as BoatAttackOcean)?.Water;
            if (shipDetail)
            {
                var sources = new HashSet<Renderer>();
                var proxies = new HashSet<Renderer>();
                foreach (var ship in NetworkShip.ActiveShips)
                {
                    if (ship == null || ship.TeamId.Value <= 0) continue;
                    foreach (var batch in ship.GetComponentsInChildren<ShipV3RenderBatch>(true))
                    {
                        batchColour.Add(batch.GetComponent<MeshRenderer>());
                        foreach (var source in batch.Sources) if (source != null) sources.Add(source);
                        if (batch.ShadowProxy != null) proxies.Add(batch.ShadowProxy);
                    }
                }
                var outputs = new HashSet<Renderer>(batchColour);
                foreach (var renderer in shipRenderers)
                {
                    if (renderer is SkinnedMeshRenderer) skinned.Add(renderer);
                    else if (!sources.Contains(renderer) && !outputs.Contains(renderer) && !proxies.Contains(renderer)) fixtures.Add(renderer);
                }
                PerformanceCapture.Event($"ShipIsolationInventory:batches={batchColour.Count};fixtures={fixtures.Count};skinned={skinned.Count};shadowProxies={proxies.Count};sourceRenderers={sources.Count}");
            }
            PerformanceCapture.Event($"IsolationInventory:shipRenderers={shipRenderers.Count};shipClothRopes={shipCosmetics.Count};playerRenderers={playerRenderers.Count};playerPresentation={playerCosmetics.Count};idleWorld={worldCosmetics.Count};waterTracking={waterTracking.Count};cloudFeatures={clouds.Count};waterSurface={water != null}");
        }

        void Hide(List<Renderer> renderers)
        {
            foreach (var renderer in renderers)
            {
                bool previous = renderer.forceRenderingOff;
                renderer.forceRenderingOff = true;
                undo.Add(() => { if (renderer != null) renderer.forceRenderingOff = previous; });
            }
        }

        void Pause(List<Behaviour> behaviours)
        {
            foreach (var behaviour in behaviours)
            {
                if (behaviour == null) continue;
                bool previous = behaviour.enabled;
                behaviour.enabled = false;
                undo.Add(() => { if (behaviour != null) behaviour.enabled = previous; });
            }
        }

        public void Apply(int index)
        {
            if (stage == index) return;
            Restore(); stage = index;
            if (shipDetail)
            {
                if (index == 1) Hide(batchColour);
                if (index == 2) Hide(fixtures);
                if (index == 3) Hide(skinned);
                if (index == 4) Pause(shipCosmetics);
                if (index == 5)
                    foreach (var renderer in shipRenderers)
                    {
                        var previous = renderer.shadowCastingMode;
                        bool off = renderer.forceRenderingOff;
                        if (previous == ShadowCastingMode.ShadowsOnly) renderer.forceRenderingOff = true;
                        else renderer.shadowCastingMode = ShadowCastingMode.Off;
                        undo.Add(() => { if (renderer != null) { renderer.shadowCastingMode = previous; renderer.forceRenderingOff = off; } });
                    }
                if (index == 6) SkipBatchPolling = true;
                if (index == 7 || index == 8)
                {
                    ExpectedScale = Mathf.Max(.1f, originalScale * .5f);
                    pipeline.renderScale = ExpectedScale;
                    undo.Add(() => { if (pipeline != null) pipeline.renderScale = originalScale; });
                }
                if (index == 8 || index == 9) Hide(shipRenderers);
                PerformanceCapture.Event("ShipIsolation:" + ShipStages[index]);
                return;
            }
            if (index == 1) Hide(shipRenderers);
            if (index == 2) Pause(shipCosmetics);
            if (index == 3) { Pause(playerCosmetics); Hide(playerRenderers); }
            if (index == 4) Pause(worldCosmetics);
            if (index == 5) Pause(waterTracking);
            if (index == 6)
                foreach (var feature in clouds)
                {
                    feature.SetActive(false);
                    undo.Add(() => { if (feature != null) feature.SetActive(true); });
                }
            if (index == 7 && water != null)
            {
                bool previous = water.RenderingEnabled;
                water.RenderingEnabled = false;
                undo.Add(() => { if (water != null) water.RenderingEnabled = previous; });
            }
            if (index == 8)
            {
                float previous = pipeline.shadowDistance;
                pipeline.shadowDistance = 0;
                undo.Add(() => { if (pipeline != null) pipeline.shadowDistance = previous; });
            }
            PerformanceCapture.Event("Isolation:" + Stages[index]);
        }

        public void Restore()
        {
            for (int i = undo.Count - 1; i >= 0; i--) undo[i]();
            undo.Clear(); stage = -1;
            SkipBatchPolling = false; ExpectedScale = originalScale;
        }

        public void Dispose()
        {
            Restore();
            GameTelemetry.SetVisible(telemetry);
        }
    }
}
