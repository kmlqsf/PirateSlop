using UnityEngine;
using UnityEngine.Rendering;
using PirateSlop.Networking;

namespace PirateSlop
{
    public static class StaffVfx
    {
        static Material additive, smoke, fireBody, iceBody, lightningMaterial, lightningHalo;
        static Mesh crystalMesh;
        static Texture2D softTexture;
        static readonly Material[] energyMaterials = new Material[3];
        static Material shellMaterial, groundMaterial;
        static Material EnergyMaterial(int mode)
        {
            if (energyMaterials[mode] == null)
            {
                energyMaterials[mode] = new Material(Resources.Load<Shader>("StaffEnergy")) { hideFlags = HideFlags.HideAndDontSave };
                energyMaterials[mode].SetFloat("_Mode", mode);
            }
            return energyMaterials[mode];
        }
        static Material GetMaterial(bool transparent = false)
        {
            if (softTexture == null)
            {
                softTexture = new Texture2D(64, 64, TextureFormat.RGBA32, false) { name = "StaffSoftParticle", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
                var pixels = new Color[4096];
                for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
                {
                    float r = new Vector2((x + .5f) / 32f - 1f, (y + .5f) / 32f - 1f).magnitude;
                    float a = Mathf.Pow(Mathf.Clamp01(1f - r * r), 2f);
                    pixels[y * 64 + x] = new Color(1, 1, 1, a);
                }
                softTexture.SetPixels(pixels); softTexture.Apply(false, true);
            }
            var material = transparent ? smoke : additive;
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = transparent ? "StaffSmoke" : "StaffGlow", hideFlags = HideFlags.HideAndDontSave };
            material.SetTexture("_BaseMap", softTexture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", transparent ? 0f : 2f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)(transparent ? BlendMode.OneMinusSrcAlpha : BlendMode.One));
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            if (transparent) smoke = material; else additive = material;
            return material;
        }

        static Material BodyMaterial(bool ice)
        {
            var material = ice ? iceBody : fireBody;
            if (material == null)
            {
                material = new Material(Resources.Load<Shader>("StaffCore")) { hideFlags = HideFlags.HideAndDontSave };
                material.SetFloat("_Ice", ice ? 1f : 0f);
                if (ice) iceBody = material; else fireBody = material;
            }
            return material;
        }

        static Transform Body(Transform root, bool ice, float radius)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Sphere); body.name = ice ? "IceCrystalBody" : "MoltenFireBody";
            if (Application.isPlaying) Object.Destroy(body.GetComponent<Collider>()); else Object.DestroyImmediate(body.GetComponent<Collider>());
            body.transform.SetParent(root, false); body.transform.localScale = Vector3.one * radius * 2.1f;
            if (ice)
            {
                if (crystalMesh == null)
                {
                    crystalMesh = new Mesh { name = "StaffIceCrystal", hideFlags = HideFlags.HideAndDontSave };
                    Vector3[] points = { Vector3.up * .7f, Vector3.down * .7f, Vector3.left * .5f, Vector3.forward * .5f, Vector3.right * .5f, Vector3.back * .5f };
                    int[] faces = { 0,3,2,0,4,3,0,5,4,0,2,5,1,2,3,1,3,4,1,4,5,1,5,2 };
                    var vertices = new Vector3[faces.Length]; var triangles = new int[faces.Length];
                    for (int i = 0; i < faces.Length; i++) { vertices[i] = points[faces[i]]; triangles[i] = i; }
                    crystalMesh.vertices = vertices; crystalMesh.triangles = triangles; crystalMesh.RecalculateNormals(); crystalMesh.RecalculateBounds();
                }
                body.GetComponent<MeshFilter>().sharedMesh = crystalMesh;
                body.transform.localScale *= 1.25f;
            }
            var renderer = body.GetComponent<Renderer>(); renderer.sharedMaterial = BodyMaterial(ice); renderer.shadowCastingMode = ShadowCastingMode.Off;
            return body.transform;
        }

        static Gradient Fade(Color color)
        {
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(color, 0), new GradientColorKey(color * .65f, 1) }, new[] { new GradientAlphaKey(0f, 0), new GradientAlphaKey(color.a, .12f), new GradientAlphaKey(color.a * .7f, .5f), new GradientAlphaKey(0f, 1) });
            return gradient;
        }

        static ParticleSystem Particles(Transform parent, string name, Color color, float size, float lifetime, float speed, float rate, int burst, bool transparent = false, bool streak = false)
        {
            var root = new GameObject(name); root.transform.SetParent(parent, false);
            var particles = root.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = burst == 0;
            main.duration = .2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * .7f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * .4f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * .6f, size);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 160;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var emission = particles.emission; emission.rateOverTime = rate;
            if (burst > 0) emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)burst) });
            var shape = particles.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = .08f;
            var colors = particles.colorOverLifetime; colors.enabled = true; colors.color = Fade(Color.white);
            var sizes = particles.sizeOverLifetime; sizes.enabled = true;
            sizes.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, .35f), new Keyframe(.2f, 1f), new Keyframe(1, transparent ? 2f : 0f)));
            var renderer = particles.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = GetMaterial(transparent);
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            if (name == "Flame" || name == "FireBurst") { main.startSize3D = true; main.startSizeX = size; main.startSizeY = size * 2.2f; main.startSizeZ = size; }
            if (name == "Flame" || name == "FireBurst") renderer.sharedMaterial = EnergyMaterial(0);
            else if (transparent) renderer.sharedMaterial = EnergyMaterial(name.Contains("Smoke") ? 2 : 1);
            if (streak) { renderer.renderMode = ParticleSystemRenderMode.Stretch; renderer.lengthScale = 3f; renderer.velocityScale = .08f; }
            particles.Play();
            return particles;
        }

        static void Glow(Transform root, Color color, float size, float lifetime)
        {
            Particles(root, "Glow", new Color(color.r * 2f, color.g * 2f, color.b * 2f, .8f), size, lifetime, 0f, 0f, 1);
        }

        static Light Light(Transform root, Color color, float range, float intensity)
        {
            var light = root.gameObject.AddComponent<Light>(); light.type = LightType.Point; light.color = color;
            light.range = range; light.intensity = intensity; light.shadows = LightShadows.None;
            return light;
        }

        public static void Flight(Transform parent, StaffSettings settings)
        {
            foreach (var renderer in parent.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
            var root = new GameObject("StaffFlightVfx"); root.transform.SetParent(parent, false);
            bool fire = settings.Item == InventoryItem.FireStaff;
            Color color = settings.Color;
            var body = Body(root.transform, !fire, settings.ProjectileRadius);
            if (fire)
            {
                var shell = new GameObject("FlameEnvelope"); shell.transform.SetParent(root.transform, false);
                shell.transform.localScale = body.localScale * 1.2f;
                shell.AddComponent<MeshFilter>().sharedMesh = body.GetComponent<MeshFilter>().sharedMesh;
                if (shellMaterial == null) shellMaterial = new Material(Resources.Load<Shader>("StaffFlameShell")) { hideFlags = HideFlags.HideAndDontSave };
                var shellRenderer = shell.AddComponent<MeshRenderer>(); shellRenderer.sharedMaterial = shellMaterial; shellRenderer.shadowCastingMode = ShadowCastingMode.Off;
            }
            if (!fire) for (int i = 0; i < 3; i++)
            {
                var shard = Body(body, true, settings.ProjectileRadius * .22f);
                float angle = i * Mathf.PI * 2f / 3f;
                shard.localPosition = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), .15f) * .62f;
                shard.localScale = Vector3.one * .22f;
            }
            var core = Particles(root.transform, "CoreHalo", new Color(1f, .55f, .12f, .35f), settings.ProjectileRadius * 2.3f, .12f, .04f, 18f, 0);
            var coreMain = core.main; coreMain.simulationSpace = ParticleSystemSimulationSpace.Local;
            if (fire)
            {
                var corona = Particles(root.transform, "Flame", new Color(1f, .8f, .55f, .8f), .19f, .24f, .35f, 90f, 0);
                var coronaMain = corona.main; coronaMain.simulationSpace = ParticleSystemSimulationSpace.Local;
                var coronaShape = corona.shape; coronaShape.radius = settings.ProjectileRadius * .65f;
                var turbulence = corona.noise; turbulence.enabled = true; turbulence.strength = .08f; turbulence.frequency = 3f; turbulence.scrollSpeed = 1.3f;
            }
            Particles(root.transform, fire ? "Flame" : "Frost", fire ? new Color(1f, .8f, .6f, .85f) : color, fire ? .32f : .16f, .4f, .35f, 65f, 0, false, !fire);
            var sparks = Particles(root.transform, fire ? "Embers" : "IceShards", fire ? new Color(1.8f, .6f, .08f) : new Color(.6f, 1.4f, 2f), .045f, .7f, 1.2f, 24f, 0, false, true);
            var noise = sparks.noise; noise.enabled = true; noise.strength = .3f; noise.frequency = .7f; noise.scrollSpeed = .5f;
            if (fire)
            {
                var smokeParticles = Particles(root.transform, "Smoke", new Color(.14f, .11f, .09f, .32f), .35f, 1.1f, .3f, 16f, 0, true);
                var velocity = smokeParticles.velocityOverLifetime; velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.World; velocity.y = .6f;
            }
            var trail = root.AddComponent<TrailRenderer>(); trail.sharedMaterial = GetMaterial(); trail.time = fire ? .22f : .45f;
            trail.startWidth = settings.ProjectileRadius * 1.6f; trail.endWidth = 0f; trail.minVertexDistance = .06f;
            trail.colorGradient = Fade(fire ? new Color(1.4f, .48f, .035f, .65f) : color); trail.shadowCastingMode = ShadowCastingMode.Off;
            var effect = root.AddComponent<StaffVfxEffect>(); effect.Follow = parent; effect.Flight = true; effect.Body = body;
            effect.GlowLight = Light(root.transform, color, 3f, fire ? 1.6f : 1.1f);
        }

        public static void Cast(Vector3 point, Color color)
        {
            if (Application.isBatchMode) return;
            var root = new GameObject("StaffCastVfx"); root.transform.position = point;
            Glow(root.transform, color, .65f, .2f);
            Particles(root.transform, "CastSparks", color, .045f, .3f, 1.5f, 0f, 12, false, true);
            Object.Destroy(root, .5f);
        }

        public static GameObject Charge(Transform parent, StaffSettings settings)
        {
            Color color = settings.Color;
            var root = new GameObject("StaffChargeVfx"); root.transform.SetParent(parent, false);
            var core = Particles(root.transform, "ChargingCore", color * 1.8f, .45f, .2f, .08f, 22f, 0);
            var main = core.main; main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var sparks = Particles(root.transform, "ChargingSparks", color, .04f, .5f, .25f, 18f, 0, false, true);
            var shape = sparks.shape; shape.radius = .22f;
            var sparksMain = sparks.main; sparksMain.simulationSpace = ParticleSystemSimulationSpace.Local;
            var swirl = sparks.velocityOverLifetime; swirl.enabled = true; swirl.space = ParticleSystemSimulationSpace.Local; swirl.orbitalY = 7f; swirl.radial = -.3f;
            var haze = Particles(root.transform, settings.Item == InventoryItem.FireStaff ? "Flame" : "ChargeMist", new Color(color.r, color.g, color.b, .45f), .19f, .35f, .15f, 20f, 0, settings.Item != InventoryItem.FireStaff);
            var hazeMain = haze.main; hazeMain.simulationSpace = ParticleSystemSimulationSpace.Local;
            if (settings.Item == InventoryItem.LightningStaff)
            {
                for (int i = 0; i < 3; i++)
                {
                    float angle = i * Mathf.PI * 2f / 3f;
                    Vector3 from = new Vector3(Mathf.Cos(angle), .6f, Mathf.Sin(angle)) * .18f;
                    Bolt(root.transform, color, from, -from, 9, .012f);
                }
            }
            return root;
        }

        public static void UpdateCharge(GameObject root, float progress)
        {
            if (root == null) return;
            var core = root.transform.Find("ChargingCore").GetComponent<ParticleSystem>();
            var main = core.main; main.startSize = Mathf.Lerp(.15f, .5f, progress * progress);
            var sparks = root.transform.Find("ChargingSparks").GetComponent<ParticleSystem>();
            var emission = sparks.emission; emission.rateOverTime = Mathf.Lerp(8f, 42f, progress * progress);
        }

        public static void Held(Transform parent, StaffSettings settings)
        {
            var root = new GameObject("StaffHeldVfx"); root.transform.SetParent(parent, false);
            root.AddComponent<StaffHeldPresentation>().Configure(parent, settings, GetMaterial());
            bool fire = settings.Item == InventoryItem.FireStaff;
            bool ice = settings.Item == InventoryItem.IceStaff;
            var particles = Particles(root.transform, fire ? "HeldEmbers" : ice ? "HeldIceGlints" : "HeldElectricMotes",
                fire ? new Color(1f, .36f, .04f, .65f) : ice ? new Color(.4f, .8f, 1f, .45f) : new Color(.65f, .38f, 1f, .4f),
                fire ? .013f : .012f, fire ? .85f : .65f, .025f, fire ? 5f : 3f, 0);
            var main = particles.main; main.simulationSpace = ParticleSystemSimulationSpace.Local; main.maxParticles = 10;
            var shape = particles.shape; shape.radius = .065f; shape.scale = new Vector3(1f, 1.2f, .6f);
            var velocity = particles.velocityOverLifetime; velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.y = fire ? .1f : ice ? -.035f : .025f;
            var noise = particles.noise; noise.enabled = true; noise.strength = .015f; noise.frequency = 2f; noise.scrollSpeed = .3f;
            if (fire || ice)
            {
                var veil = Particles(root.transform, fire ? "Flame" : "HeldColdMist",
                    fire ? new Color(1f, .36f, .07f, .2f) : new Color(.42f, .72f, .85f, .13f),
                    fire ? .038f : .095f, fire ? .5f : 1.1f, .015f, fire ? 8f : 4f, 0, ice);
                var veilMain = veil.main; veilMain.simulationSpace = ParticleSystemSimulationSpace.Local; veilMain.maxParticles = 10;
                var veilShape = veil.shape; veilShape.radius = .06f; veilShape.scale = new Vector3(1f, .7f, .55f);
                var drift = veil.velocityOverLifetime; drift.enabled = true; drift.space = ParticleSystemSimulationSpace.Local; drift.y = fire ? .055f : -.045f;
                var curl = veil.noise; curl.enabled = true; curl.strength = .018f; curl.frequency = 3f; curl.scrollSpeed = .25f;
            }
        }

        public static GameObject Telegraph(Color color, float radius)
        {
            var root = new GameObject("LightningCastRadius");
            var surface = GameObject.CreatePrimitive(PrimitiveType.Quad); surface.name = "LightningGroundPattern";
            if (Application.isPlaying) Object.Destroy(surface.GetComponent<Collider>()); else Object.DestroyImmediate(surface.GetComponent<Collider>());
            surface.transform.SetParent(root.transform, false); surface.transform.localPosition = Vector3.up * .045f;
            surface.transform.localRotation = Quaternion.Euler(90, 0, 0); surface.transform.localScale = Vector3.one * radius * 2.14f;
            if (groundMaterial == null) groundMaterial = new Material(Resources.Load<Shader>("StaffGround")) { hideFlags = HideFlags.HideAndDontSave };
            surface.GetComponent<Renderer>().sharedMaterial = groundMaterial;
            var ring = Ring(root.transform, color * 1.5f, radius);
            ring.transform.localScale = Vector3.one;
            ring.startWidth = ring.endWidth = .045f;
            var center = Particles(root.transform, "LightningTargetSparks", color, .05f, .45f, .4f, 18f, 0, false, true);
            var main = center.main; main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var inner = Ring(root.transform, new Color(color.r, color.g, color.b, .45f), radius * .82f); inner.transform.localScale = Vector3.one;
            inner.startWidth = inner.endWidth = .035f;
            var shape = center.shape; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = radius; shape.radiusThickness = .015f;
            var drift = center.velocityOverLifetime; drift.enabled = true; drift.space = ParticleSystemSimulationSpace.Local; drift.y = .18f;
            return root;
        }

        public static GameObject IceBeam()
        {
            var root = new GameObject("ContinuousIceBeam");
            var line = root.AddComponent<LineRenderer>(); line.positionCount = 2; line.enabled = false;
            var seed = Body(root.transform, true, .025f); seed.gameObject.SetActive(false);
            var stream = Particles(root.transform, "FrostStream", new Color(.75f, .9f, 1f, .7f), .095f, 1f, 45f, 175f, 0);
            var shape = stream.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = .8f; shape.radius = .09f;
            var main = stream.main; main.startSpeed = 45f; main.maxParticles = 200; main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var renderer = stream.GetComponent<ParticleSystemRenderer>(); renderer.renderMode = ParticleSystemRenderMode.Mesh; renderer.mesh = crystalMesh; renderer.sharedMaterial = BodyMaterial(true);
            var rotation = stream.rotationOverLifetime; rotation.enabled = true; rotation.separateAxes = true; rotation.x = 3f; rotation.y = 5f; rotation.z = 4f;
            var fog = Particles(root.transform, "BeamMist", new Color(.7f, .83f, .88f, .52f), 1.15f, 1f, 45f, 145f, 0, true);
            var fogMain = fog.main; fogMain.startSpeed = 45f; fogMain.maxParticles = 240; fogMain.simulationSpace = ParticleSystemSimulationSpace.Local;
            var fogShape = fog.shape; fogShape.shapeType = ParticleSystemShapeType.Cone; fogShape.angle = .7f; fogShape.radius = .1f;
            var noise = fog.noise; noise.enabled = true; noise.strength = .24f; noise.frequency = 1.8f; noise.scrollSpeed = .4f;
            var spiral = fog.velocityOverLifetime; spiral.enabled = true; spiral.space = ParticleSystemSimulationSpace.Local; spiral.orbitalZ = 4f;
            var inner = Particles(root.transform, "BeamSpindrift", new Color(.86f, .96f, 1f, .5f), .38f, 1f, 45f, 110f, 0, true);
            var innerMain = inner.main; innerMain.startSpeed = 45f; innerMain.simulationSpace = ParticleSystemSimulationSpace.Local; innerMain.maxParticles = 110;
            var innerShape = inner.shape; innerShape.shapeType = ParticleSystemShapeType.Cone; innerShape.angle = .2f; innerShape.radius = .035f;
            var innerNoise = inner.noise; innerNoise.enabled = true; innerNoise.strength = .09f; innerNoise.frequency = 4f; innerNoise.scrollSpeed = .7f;
            var contact = new GameObject("Contact"); contact.transform.SetParent(root.transform, false);
            var contactIce = Particles(contact.transform, "IceContact", new Color(.6f, .9f, 1.2f, .65f), .09f, .45f, 2f, 42f, 0, false, true);
            var contactIceMain = contactIce.main; contactIceMain.simulationSpace = ParticleSystemSimulationSpace.Local;
            var contactMist = Particles(contact.transform, "ContactMist", new Color(.5f, .8f, 1f, .22f), .55f, .6f, .7f, 28f, 0, true);
            var contactMistMain = contactMist.main; contactMistMain.simulationSpace = ParticleSystemSimulationSpace.Local;
            var snow = Particles(contact.transform, "ContactIceChips", new Color(.85f, .95f, 1f), .07f, .4f, 1.7f, 32f, 0);
            var snowMain = snow.main; snowMain.simulationSpace = ParticleSystemSimulationSpace.Local; snowMain.gravityModifier = .35f; snowMain.maxParticles = 24;
            var snowRenderer = snow.GetComponent<ParticleSystemRenderer>(); snowRenderer.renderMode = ParticleSystemRenderMode.Mesh; snowRenderer.mesh = crystalMesh; snowRenderer.sharedMaterial = BodyMaterial(true);
            root.AddComponent<StaffIceBeamPresentation>().Initialize(stream, fog, inner);
            return root;
        }

        public static GameObject FireBreath()
        {
            var root = new GameObject("SkullFireBreath");
            var fire = Particles(root.transform, "Flame", new Color(2.5f, .65f, .08f, .85f), 2.3f, 2f, 45f, 95f, 0);
            var smoke = Particles(root.transform, "BreathSmoke", new Color(.18f, .12f, .1f, .3f), 4f, 2f, 45f, 30f, 0, true);
            var embers = Particles(root.transform, "BreathEmbers", new Color(3f, 1f, .15f, .8f), .16f, 2f, 45f, 65f, 0, false, true);
            foreach (var stream in new[] { fire, smoke, embers })
            {
                var main = stream.main; main.simulationSpace = ParticleSystemSimulationSpace.Local; main.startSpeed = 45f; main.maxParticles = 300;
                var shape = stream.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 1.3f; shape.radius = 1.6f;
                var noise = stream.noise; noise.enabled = true; noise.strength = stream == smoke ? 1.2f : .45f; noise.frequency = .65f; noise.scrollSpeed = .6f;
                var velocity = stream.velocityOverLifetime; velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.Local; velocity.orbitalZ = .9f;
            }
            var contact = new GameObject("Contact"); contact.transform.SetParent(root.transform, false);
            var hit = Particles(contact.transform, "Flame", new Color(2.4f, .8f, .1f, .7f), 3.5f, .45f, 5f, 35f, 0);
            var hitMain = hit.main; hitMain.simulationSpace = ParticleSystemSimulationSpace.Local;
            root.AddComponent<StaffIceBeamPresentation>().Initialize(fire, smoke, embers);
            Light(root.transform, new Color(1f, .32f, .05f), 45f, 5f);
            return root;
        }

        public static void UpdateFireBreath(GameObject root, Vector3 start, Vector3 end, bool hit)
        {
            var delta = end - start;
            root.transform.SetPositionAndRotation(start, delta.sqrMagnitude > .001f ? Quaternion.LookRotation(delta) : Quaternion.identity);
            root.GetComponent<StaffIceBeamPresentation>().SetLength(delta.magnitude);
            var contact = root.transform.Find("Contact"); contact.position = end; contact.gameObject.SetActive(hit);
        }

        public static void UpdateIceBeam(GameObject root, Vector3 start, Vector3 end, bool hit)
        {
            Vector3 delta = end - start;
            root.transform.SetPositionAndRotation(start, delta.sqrMagnitude > .001f ? Quaternion.LookRotation(delta) : Quaternion.identity);
            var line = root.GetComponent<LineRenderer>(); line.SetPosition(0, start); line.SetPosition(1, end);
            root.GetComponent<StaffIceBeamPresentation>().SetLength(delta.magnitude);
            var contact = root.transform.Find("Contact"); contact.position = end; contact.gameObject.SetActive(hit);
        }

        public static void FrozenWaterMist(Transform parent)
        {
            if (Application.isBatchMode) return;
            var mist = Particles(parent, "IceSurfaceSpindrift", new Color(.65f,.84f,.9f,.15f), .25f, .9f, .015f, 5f, 0, true);
            var main = mist.main; main.simulationSpace = ParticleSystemSimulationSpace.Local; main.maxParticles = 10;
            var shape = mist.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(1.75f,.04f,1.75f);
            mist.transform.localPosition = Vector3.up * .06f;
            var drift = mist.velocityOverLifetime; drift.enabled = true; drift.space = ParticleSystemSimulationSpace.Local; drift.x = .07f; drift.y = .025f;
            var noise = mist.noise; noise.enabled = true; noise.strength = .025f; noise.frequency = 2f; noise.scrollSpeed = .4f;
        }

        public static GameObject Frost(Transform parent)
        {
            var root = new GameObject("StaffFrostStatus"); root.transform.SetParent(parent, false); root.transform.localPosition = Vector3.up * .9f;
            var mist = Particles(root.transform, "ColdMist", new Color(.65f, .82f, .9f, .2f), .28f, .65f, .12f, 14f, 0, true);
            var mistMain = mist.main; mistMain.simulationSpace = ParticleSystemSimulationSpace.Local; mistMain.maxParticles = 24;
            var shape = mist.shape; shape.radius = .45f;
            var sparks = Particles(root.transform, "FrostCrystals", new Color(.8f, .95f, 1f, .6f), .045f, .6f, .1f, 10f, 0);
            var crystalMain = sparks.main; crystalMain.simulationSpace = ParticleSystemSimulationSpace.Local; crystalMain.maxParticles = 16;
            var crystalShape = sparks.shape; crystalShape.radius = .5f;
            if (crystalMesh == null) { var seed = Body(root.transform, true, .01f); seed.gameObject.SetActive(false); }
            var renderer = sparks.GetComponent<ParticleSystemRenderer>(); renderer.renderMode = ParticleSystemRenderMode.Mesh; renderer.mesh = crystalMesh; renderer.sharedMaterial = BodyMaterial(true);
            return root;
        }

        public static void FinishStatus(GameObject root)
        {
            if (root == null) return;
            root.transform.SetParent(null, true);
            foreach (var line in root.GetComponentsInChildren<LineRenderer>()) line.enabled = false;
            foreach (var particles in root.GetComponentsInChildren<ParticleSystem>()) particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Object.Destroy(root, 1f);
        }

        public static void Impact(Vector3 point, StaffSettings settings, bool hit = true, Transform anchor = null, Vector3 localPoint = default, float radiusMultiplier = 1f)
        {
            if (Application.isBatchMode) return;
            var root = new GameObject("StaffImpactVfx"); root.transform.position = point;
            Color color = settings.Color;
            float radius = hit ? settings.BlastRadius * radiusMultiplier : .5f;
            bool lightning = settings.Item == InventoryItem.LightningStaff;
            bool fire = settings.Item == InventoryItem.FireStaff;
            var effect = root.AddComponent<StaffVfxEffect>(); effect.Duration = fire ? 2.2f : 1.5f;
            effect.SurfaceAnchor = anchor; effect.SurfaceLocalPoint = localPoint;
            if (anchor != null) root.transform.position = anchor.TransformPoint(localPoint);
            effect.GlowLight = Light(root.transform, fire ? new Color(1f, .47f, .12f) : new Color(.75f, .8f, 1f), radius * 3f, lightning ? 7f : 4f);
            Glow(root.transform, fire ? new Color(2.6f, 1.3f, .4f) : new Color(2f, 2.2f, 3f), radius * 1.3f, .12f);
            var burst = Particles(root.transform, fire ? "FireBurst" : "ColdFlash", fire ? new Color(1f, .75f, .45f) : new Color(.85f, .9f, 1f, .65f), fire ? .8f : .18f, fire ? .7f : .3f, radius * 2.2f, 0, fire ? 48 : 60, false, !fire);
            var burstVelocity = burst.velocityOverLifetime; burstVelocity.enabled = true; burstVelocity.speedModifier = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 1), new Keyframe(.3f, .3f), new Keyframe(1, .06f)));
            var debris = Particles(root.transform, fire ? "EmberBurst" : "ShardBurst", fire ? new Color(2f, .7f, .1f) : new Color(.7f, 1.5f, 2f), .065f, .9f, radius * 3f, 0, 35, false, true);
            var main = debris.main; main.gravityModifier = fire ? .15f : .5f;
            if (fire && hit)
            {
                var cloud = Particles(root.transform, "SmokePuff", new Color(.085f, .075f, .068f, .55f), 1.4f, 1.9f, radius * .8f, 0, 18, true);
                var velocity = cloud.velocityOverLifetime; velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.World; velocity.y = .7f;
                var turbulence = cloud.noise; turbulence.enabled = true; turbulence.strength = .35f; turbulence.frequency = .9f; turbulence.scrollSpeed = .6f;
                var embers = Particles(root.transform, "LingeringEmbers", new Color(1.8f, .55f, .03f), .025f, 1.7f, radius * 2f, 0, 60, false, true);
                var emberMain = embers.main; emberMain.gravityModifier = .3f;
            }
            if (hit) effect.Ring = Ring(root.transform, color, radius);
            if (lightning && hit)
            {
                effect.Lightning = true;
                effect.Bolts = new LineRenderer[13];
                effect.Bolts[0] = Bolt(root.transform, color, new Vector3(1.8f, 32f, -.8f), Vector3.zero, 65, .3f);
                for (int i = 1; i <= 6; i++)
                {
                    Vector3 origin = effect.Bolts[0].GetPosition(4 + i * 7);
                    Vector3 end = origin + new Vector3((i % 2 == 0 ? 1 : -1) * Random.Range(2f, 5f), -Random.Range(2f, 5f), Random.Range(-2f, 2f));
                    effect.Bolts[i] = Bolt(root.transform, color, origin, end, 17, .1f);
                    Vector3 twig = effect.Bolts[i].GetPosition(8);
                    Bolt(root.transform, color, twig, twig + new Vector3(Random.Range(-2f, 2f), -1.4f, Random.Range(-1f, 1f)), 9, .035f);
                }
                for (int i = 7; i < 13; i++)
                {
                    float angle = (i - 7) * Mathf.PI / 3f;
                    Vector3 end = new Vector3(Mathf.Cos(angle) * radius, .09f, Mathf.Sin(angle) * radius);
                    effect.Bolts[i] = Bolt(root.transform, color, Vector3.up * .1f, end, 25, .1f);
                }
                Glow(root.transform, new Color(.7f, .8f, 1f), radius * 2.8f, .3f);
                var layers = new System.Collections.Generic.List<LineRenderer>();
                foreach (var line in root.GetComponentsInChildren<LineRenderer>()) if (line.gameObject.name == "LightningBranch" || line.gameObject.name == "LightningAura") layers.Add(line);
                effect.Bolts = layers.ToArray();
                var vapor = Particles(root.transform, "IonVapor", new Color(.65f, .69f, .8f, .38f), .85f, 1.1f, radius * 1.2f, 0, 18, true);
                var lift = vapor.velocityOverLifetime; lift.enabled = true; lift.y = .8f;
            }
            if (anchor != null) foreach (var particles in root.GetComponentsInChildren<ParticleSystem>())
            {
                var particleMain = particles.main; particleMain.simulationSpace = ParticleSystemSimulationSpace.Local;
            }
        }

        static LineRenderer Ring(Transform root, Color color, float radius)
        {
            var go = new GameObject("ShockRing"); go.transform.SetParent(root, false);
            var ring = go.AddComponent<LineRenderer>(); ring.sharedMaterial = GetMaterial(); ring.useWorldSpace = false;
            ring.loop = true; ring.positionCount = 65; ring.startWidth = ring.endWidth = .06f;
            ring.startColor = ring.endColor = color;
            for (int i = 0; i < 65; i++) { float angle = i * Mathf.PI * 2f / 65f; ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, .06f, Mathf.Sin(angle) * radius)); }
            ring.transform.localScale = Vector3.one * .05f;
            return ring;
        }

        static LineRenderer Bolt(Transform root, Color color, Vector3 start, Vector3 end, int segments, float width)
        {
            var go = new GameObject("LightningBranch"); go.transform.SetParent(root, false);
            var bolt = go.AddComponent<LineRenderer>(); if (lightningMaterial == null) lightningMaterial = new Material(Resources.Load<Shader>("StaffLightning")) { hideFlags = HideFlags.HideAndDontSave };
            bolt.sharedMaterial = lightningMaterial; bolt.useWorldSpace = false;
            bolt.numCornerVertices = 1; bolt.numCapVertices = 2;
            bolt.textureMode = LineTextureMode.Stretch;
            bolt.shadowCastingMode = ShadowCastingMode.Off;
            bolt.positionCount = segments; bolt.startWidth = width; bolt.endWidth = width * .55f;
            bolt.widthCurve = new AnimationCurve(new Keyframe(0, .7f), new Keyframe(.2f, 1f), new Keyframe(.7f, .65f), new Keyframe(1, start.y > 20f ? .9f : .08f));
            bolt.widthMultiplier = width;
            bolt.startColor = bolt.endColor = new Color(color.r, color.g, color.b, 1f);
            float seed = Random.Range(0f, 1000f);
            Vector3 axis = (end - start).normalized;
            Vector3 across = Vector3.Cross(axis, Mathf.Abs(axis.y) > .9f ? Vector3.forward : Vector3.up).normalized;
            Vector3 depth = Vector3.Cross(axis, across);
            for (int i = 0; i < segments; i++)
            {
                float t = i / (float)(segments - 1), envelope = Mathf.Sin(t * Mathf.PI);
                float amplitude = start.y > 20f ? .9f : .28f;
                float bend = (Mathf.PerlinNoise(seed, t * 7f) - .5f) * 2f;
                float jagged = (Random.value - .5f) * .2f;
                bolt.SetPosition(i, Vector3.Lerp(start, end, t) + envelope * amplitude * (across * (bend + jagged) + depth * (Mathf.PerlinNoise(seed + 17f, t * 11f) - .5f)));
            }
            if (lightningHalo == null) { lightningHalo = new Material(lightningMaterial) { hideFlags = HideFlags.HideAndDontSave }; lightningHalo.SetFloat("_CoreStrength", 0f); lightningHalo.SetFloat("_GlowStrength", .18f); }
            var haloRoot = new GameObject("LightningAura"); haloRoot.transform.SetParent(root, false);
            var halo = haloRoot.AddComponent<LineRenderer>(); halo.sharedMaterial = lightningHalo; halo.useWorldSpace = false;
            halo.positionCount = segments; var positions = new Vector3[segments]; bolt.GetPositions(positions);
            var smooth = (Vector3[])positions.Clone();
            for (int i = 1; i < segments - 1; i++) smooth[i] = positions[i] * .5f + (positions[i - 1] + positions[i + 1]) * .25f;
            halo.SetPositions(smooth);
            halo.startWidth = width * 3f; halo.endWidth = width * 1.8f; halo.widthCurve = bolt.widthCurve; halo.startColor = halo.endColor = new Color(color.r, color.g, color.b, .3f);
            halo.widthMultiplier = width * 3f;
            halo.numCornerVertices = 6; halo.numCapVertices = 6; halo.shadowCastingMode = ShadowCastingMode.Off;
            return bolt;
        }
    }
}
