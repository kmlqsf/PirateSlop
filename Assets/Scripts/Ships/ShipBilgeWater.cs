using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PirateSlop.Ships
{
    [DefaultExecutionOrder(80)]
    public sealed class ShipBilgeWater : MonoBehaviour
    {
        public Material WaterMaterial, LeakMaterial;
        public int MaximumLeakEffects = 32;
        ShipFlooding flooding;
        ShipWaterInterior interior;
        Mesh waterMesh;
        MeshRenderer waterRenderer;
        Vector3[] vertices;
        Vector2[] uv;
        Color[] profile;
        readonly float[] volumes = new float[33];
        readonly List<MeshRenderer> jets = new();
        readonly List<ShipBreach> leaks = new();
        readonly List<ShipBilgeLeakGeometry.Surface> leakSurfaces = new();
        readonly ShipBilgeLeakGeometry.Builder leakBuilder = new();
        Texture2D oceanNormals, oceanFoam;
        MaterialPropertyBlock waterProperties, jetProperties;
        Vector2 slope, slopeVelocity, surfaceSlope;
        float surfaceHeight, zMin, zMax;
        int revision = -1;
        int leakSignature = int.MinValue;
        const int Rows = 97, Columns = 9;

        void Awake()
        {
            flooding = GetComponent<ShipFlooding>();
            interior = GetComponent<ShipWaterInterior>();
            if (flooding == null || !flooding.UseBilgeFlooding || interior == null || interior.WidthProfile == null || WaterMaterial == null || LeakMaterial == null) { enabled = false; return; }
            waterProperties = new MaterialPropertyBlock();
            jetProperties = new MaterialPropertyBlock();
            profile = interior.WidthProfile.GetPixels();
            zMin = Mathf.Max(-20f, interior.ProfileBounds.x);
            zMax = Mathf.Min(20f, interior.ProfileBounds.y);
            BuildVolumes();
            var water = flooding.WaterVisual;
            if (water == null)
            {
                water = new GameObject("BilgeWater").transform;
                water.SetParent(transform, false);
                flooding.WaterVisual = water;
            }
            water.localPosition = Vector3.zero;
            water.localRotation = Quaternion.identity;
            water.localScale = Vector3.one;
            water.gameObject.SetActive(true);
            var filter = water.GetComponent<MeshFilter>();
            if (filter == null) filter = water.gameObject.AddComponent<MeshFilter>();
            waterRenderer = water.GetComponent<MeshRenderer>();
            if (waterRenderer == null) waterRenderer = water.gameObject.AddComponent<MeshRenderer>();
            waterRenderer.sharedMaterial = WaterMaterial;
            waterRenderer.enabled = false;
            waterRenderer.shadowCastingMode = ShadowCastingMode.Off;
            waterRenderer.receiveShadows = false;
            waterMesh = new Mesh { name = "BilgeWaterSurface" };
            waterMesh.MarkDynamic();
            filter.sharedMesh = waterMesh;
            vertices = new Vector3[Rows * Columns];
            uv = new Vector2[vertices.Length];
            var triangles = new int[(Rows - 1) * (Columns - 1) * 6];
            int at = 0;
            for (int row = 0; row < Rows - 1; row++)
                for (int col = 0; col < Columns - 1; col++)
                {
                    int a = row * Columns + col, b = a + 1, c = a + Columns, d = c + 1;
                    triangles[at++] = a; triangles[at++] = c; triangles[at++] = b;
                    triangles[at++] = b; triangles[at++] = c; triangles[at++] = d;
                }
            waterMesh.vertices = vertices;
            waterMesh.triangles = triangles;
            jets.Capacity = Mathf.Max(jets.Capacity, MaximumLeakEffects);
        }

        float Width(float z, float height)
        {
            var bounds = interior.ProfileBounds;
            float x = Mathf.Clamp01((z - bounds.x) / (bounds.y - bounds.x)) * (interior.WidthProfile.width - 1);
            float y = Mathf.Clamp01((height - bounds.z) / (bounds.w - bounds.z)) * (interior.WidthProfile.height - 1);
            int ix = (int)x, iy = (int)y, nx = Mathf.Min(ix + 1, interior.WidthProfile.width - 1), ny = Mathf.Min(iy + 1, interior.WidthProfile.height - 1);
            int w = interior.WidthProfile.width;
            return Mathf.Max(0f, Mathf.Lerp(Mathf.Lerp(profile[iy * w + ix].r, profile[iy * w + nx].r, x - ix), Mathf.Lerp(profile[ny * w + ix].r, profile[ny * w + nx].r, x - ix), y - iy) - .015f);
        }

        void BuildVolumes()
        {
            float previous = 0f, total = 0f;
            for (int level = 0; level < volumes.Length; level++)
            {
                float height = Mathf.Lerp(flooding.EmptyHeight, flooding.FullHeight, level / 32f);
                float area = 0f;
                for (int row = 0; row < Rows; row++)
                    area += Width(Mathf.Lerp(zMin, zMax, row / (float)(Rows - 1)), height) * 2f * (row == 0 || row == Rows - 1 ? .5f : 1f) * (zMax - zMin) / (Rows - 1);
                if (level > 0) total += (previous + area) * .5f * (flooding.FullHeight - flooding.EmptyHeight) / 32f;
                volumes[level] = total;
                previous = area;
            }
            for (int i = 0; i < volumes.Length; i++) volumes[i] /= Mathf.Max(.001f, total);
        }

        float VolumeHeight(float amount)
        {
            for (int i = 1; i < volumes.Length; i++)
                if (amount <= volumes[i])
                    return Mathf.Lerp(flooding.EmptyHeight, flooding.FullHeight, (i - 1 + Mathf.InverseLerp(volumes[i - 1], volumes[i], amount)) / 32f);
            return flooding.FullHeight;
        }

        public float SurfaceHeight(Vector3 local) => surfaceHeight + surfaceSlope.x * local.x + surfaceSlope.y * (local.z - (zMin + zMax) * .5f);
        public Vector4 SurfacePlane => new Vector4(surfaceSlope.x, surfaceSlope.y, SurfaceHeight(Vector3.zero), flooding.Level > .0001f ? 1f : 0f);

        void LateUpdate()
        {
            if (waterMesh == null) return;
            UpdateOceanStyle();
            float dt = Mathf.Min(.05f, Time.deltaTime);
            var gravity = transform.InverseTransformDirection(Vector3.up);
            var target = new Vector2(-gravity.x, -gravity.z) / Mathf.Max(.35f, gravity.y);
            slopeVelocity += ((target - slope) * 12f - slopeVelocity * 3f) * dt;
            slope += slopeVelocity * dt;
            surfaceHeight = VolumeHeight(flooding.Level);
            float clearance = Mathf.Max(0f, Mathf.Min(surfaceHeight - flooding.EmptyHeight, flooding.FullHeight - surfaceHeight) - .008f);
            float extent = Mathf.Abs(slope.x) * 6.5f + Mathf.Abs(slope.y) * (zMax - zMin) * .5f;
            surfaceSlope = slope * Mathf.Min(1f, clearance / Mathf.Max(.001f, extent));
            waterRenderer.enabled = flooding.Level > .0001f;
            if (waterRenderer.enabled)
            {
                for (int row = 0; row < Rows; row++)
                {
                    float z = Mathf.Lerp(zMin, zMax, row / (float)(Rows - 1));
                    float middleY = SurfaceHeight(new Vector3(0f, 0f, z));
                    float width = Width(z, middleY);
                    for (int col = 0; col < Columns; col++)
                    {
                        float x = Mathf.Lerp(-width, width, col / (float)(Columns - 1));
                        float y = SurfaceHeight(new Vector3(x, 0f, z));
                        float actualWidth = Width(z, y);
                        x = Mathf.Lerp(-actualWidth, actualWidth, col / (float)(Columns - 1));
                        int index = row * Columns + col;
                        vertices[index] = new Vector3(x, SurfaceHeight(new Vector3(x, 0f, z)), z);
                        uv[index] = new Vector2(x, z);
                    }
                }
                waterMesh.vertices = vertices;
                waterMesh.uv = uv;
                waterMesh.RecalculateBounds();
                waterProperties.SetTexture("_WidthProfile", interior.WidthProfile);
                waterProperties.SetVector("_ProfileBounds", interior.ProfileBounds);
                waterProperties.SetFloat("_BilgeTime", flooding.LeakClock);
                waterProperties.SetFloat("_Slosh", Mathf.Clamp01(slopeVelocity.magnitude * 4f));
                waterProperties.SetVector("_BilgeSlope", new Vector4(surfaceSlope.x, surfaceSlope.y, 0f, 0f));
                waterRenderer.SetPropertyBlock(waterProperties);
            }
            UpdateLeaks();
        }

        void UpdateOceanStyle()
        {
            var ocean = OceanSurface.Instance != null ? OceanSurface.Instance.HeightSource as BoatAttackOcean : null;
            var water = ocean != null ? ocean.Water : null;
            if (water == null) return;
            if (oceanNormals == null || oceanFoam == null)
            {
                var resources = WaterSystem.Settings.ProjectSettings.Instance.resources;
                oceanNormals = resources.DetailNormalMap;
                oceanFoam = resources.FoamMap;
            }
            Texture reflection = water.settings.cubemapTexture;
            bool sky = water.dynamicSkyReflection && RenderSettings.defaultReflectionMode == UnityEngine.Rendering.DefaultReflectionMode.Custom && RenderSettings.customReflectionTexture != null && RenderSettings.customReflectionTexture.dimension == TextureDimension.Cube;
            if (sky) reflection = RenderSettings.customReflectionTexture;
            var lighting = new Vector4(water.dynamicSkyReflection ? RenderSettings.reflectionIntensity : 1f, water.foamLightingMultiplier, water.scatteringLightingMultiplier, water.dynamicSkyReflection ? 1f : 0f);
            Apply(waterProperties);
            Apply(jetProperties);
            void Apply(MaterialPropertyBlock properties)
            {
                properties.SetTexture("_SurfaceNormals", oceanNormals);
                properties.SetTexture("_FoamMap", oceanFoam);
                if (reflection != null) properties.SetTexture("_CubemapTexture", reflection);
                properties.SetColor("_AbsorptionColor", water.settings.absorptionColor);
                properties.SetColor("_ScatteringColor", water.settings.scatteringColor);
                properties.SetFloat("_MaxDepth", Mathf.Max(.01f, water.settings.waterMaxVisibility));
                properties.SetFloat("_BoatAttack_Water_MicroWaveIntensity", water.settings.microWaveIntensity);
                properties.SetVector("_BoatAttack_Lighting", lighting);
            }
        }

        void UpdateLeaks()
        {
            if (revision != flooding.BreachRevision)
            {
                revision = flooding.BreachRevision;
                leaks.Clear();
                int signature = 0;
                foreach (var breach in flooding.Breaches)
                {
                    if (breach.SurfaceMesh == null || breach.SurfaceMesh.vertexCount == 0) continue;
                    leaks.Add(breach);
                    signature ^= unchecked((breach.SectionId * 64 + breach.FragmentIndex) * 397 ^ (breach.SurfaceMesh != null ? breach.SurfaceMesh.GetEntityId().GetHashCode() : breach.LocalPoint.GetHashCode()));
                }
                if (signature != leakSignature)
                {
                    leakSignature = signature;
                    var next = leakBuilder.Build(leaks, leakSurfaces);
                    foreach (var surface in leakSurfaces)
                        if (surface.Mesh != null && !next.Exists(candidate => candidate.Mesh == surface.Mesh)) Destroy(surface.Mesh);
                    leakSurfaces.Clear();
                    leakSurfaces.AddRange(next);
                    while (jets.Count < leakSurfaces.Count)
                    {
                        var jet = new GameObject("BilgeLeak_" + jets.Count);
                        jet.transform.SetParent(transform, false);
                        jet.AddComponent<MeshFilter>();
                        var renderer = jet.AddComponent<MeshRenderer>();
                        renderer.sharedMaterial = LeakMaterial;
                        renderer.shadowCastingMode = ShadowCastingMode.Off;
                        renderer.receiveShadows = false;
                        jets.Add(renderer);
                    }
                    for (int i = 0; i < leakSurfaces.Count; i++) jets[i].GetComponent<MeshFilter>().sharedMesh = leakSurfaces[i].Mesh;
                }
            }
            for (int i = 0; i < jets.Count; i++)
            {
                bool visible = i < leakSurfaces.Count;
                if (jets[i].gameObject.activeSelf != visible) jets[i].gameObject.SetActive(visible);
                if (!visible) continue;
                jetProperties.SetFloat("_BilgeTime", flooding.LeakClock);
                jetProperties.SetVector("_SurfacePlane", SurfacePlane);
                jetProperties.SetFloat("_Seed", leakSurfaces[i].Seed);
                var oceanPlane = flooding.OceanPlane(leakSurfaces[i].LowerPoint);
                float minimumHead = float.MaxValue;
                foreach (var point in leakSurfaces[i].Outline) minimumHead = Mathf.Min(minimumHead, ShipFlooding.OceanHead(oceanPlane, point));
                float pressure = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, .05f, minimumHead));
                jetProperties.SetVector("_OceanPlane", oceanPlane);
                jetProperties.SetVector("_FlowState", new Vector4(pressure, transform.up.y, 0f, 0f));
                jetProperties.SetVector("_LocalGravity", transform.InverseTransformDirection(Physics.gravity));
                jetProperties.SetVector("_HoldBounds", new Vector4(flooding.EmptyHeight, flooding.FullHeight, 0f, 0f));
                jets[i].SetPropertyBlock(jetProperties);
            }
        }

        void OnDestroy()
        {
            if (waterMesh != null) Destroy(waterMesh);
            foreach (var surface in leakSurfaces) if (surface.Mesh != null) Destroy(surface.Mesh);
        }
    }
}
