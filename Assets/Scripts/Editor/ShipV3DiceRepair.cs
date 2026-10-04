using System;
using System.Linq;
using PirateSlop.Ships;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace PirateSlop.EditorTools
{
    public static class ShipV3DiceRepair
    {
        const string Folder = "Assets/Models/Ships/ShipV3/DiceProps/";

        public static void Configure(GameObject root)
        {
            var features = root.GetComponent<ShipV3Features>();
            var table = features.DiceTable;
            var barrel = root.GetComponentsInChildren<MeshFilter>(true).Single(m => m.name == "V17_Dice_Game_Barrel");
            var wood = barrel.GetComponent<MeshRenderer>().sharedMaterials[0];
            var mapping = root.transform.worldToLocalMatrix * barrel.transform.localToWorldMatrix;
            var points = barrel.sharedMesh.vertices.Select(mapping.MultiplyPoint3x4).ToArray();
            float lidHeight = BarrelLidHeight(root, barrel);
            var barrelMesh = UnityEngine.Object.Instantiate(barrel.sharedMesh);
            for (int sub = 0; sub < barrelMesh.subMeshCount; sub++)
            {
                var faces = barrelMesh.GetTriangles(sub); var kept = new System.Collections.Generic.List<int>();
                for (int triangle = 0; triangle < faces.Length; triangle += 3)
                    if (points[faces[triangle]].y <= lidHeight + .014f && points[faces[triangle + 1]].y <= lidHeight + .014f && points[faces[triangle + 2]].y <= lidHeight + .014f)
                    { kept.Add(faces[triangle]); kept.Add(faces[triangle + 1]); kept.Add(faces[triangle + 2]); }
                barrelMesh.SetTriangles(kept, sub);
            }
            barrel.sharedMesh = SaveMesh(barrelMesh, Folder + "DiceBarrelWithoutHandle.asset");
            foreach (var collider in barrel.GetComponents<MeshCollider>()) collider.sharedMesh = barrel.sharedMesh;
            Vector3 tablePoint = root.transform.InverseTransformPoint(table.position); tablePoint.y = lidHeight - .003f; table.position = root.transform.TransformPoint(tablePoint);
            var floor = table.GetComponent<BoxCollider>(); floor.center = new Vector3(0,-.025f,0); floor.size = new Vector3(features.DiceRadius * 2f,.05f,features.DiceRadius * 2f);
            foreach (var child in table.Cast<Transform>().Where(t => t.name.StartsWith("DiceRim_", StringComparison.Ordinal) || t.name.StartsWith("DiceSector", StringComparison.Ordinal) || t.name == "DiceCandle").ToArray())
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            foreach (var model in root.GetComponentsInChildren<MeshFilter>(true).Where(m => m.name.Contains("Dice") && m.name.Contains("Handle")).ToArray()) model.gameObject.SetActive(false);
            var mug = Geometry(Folder + "MedievalBeerMug/MedievalBeerMug.fbx", "MugGeometry", .23f, 1.18f);
            var mugMaterial = Material(Folder + "MedievalBeerMug/Mug.mat", Folder + "MedievalBeerMug/Mug_Base_Color.png", Folder + "MedievalBeerMug/Mug_Normal_DirectX.png", .3f);
            mugMaterial.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "MedievalBeerMug/Mug_Metallic.png")); mugMaterial.SetFloat("_Metallic", 1); mugMaterial.EnableKeyword("_METALLICSPECGLOSSMAP");
            float radius = features.DiceRadius;
            Vector3 center = root.transform.InverseTransformPoint(table.position);
            for (int index = 0; index < features.DiceSlots.Length; index++)
            {
                var slot = features.DiceSlots[index];
                foreach (var child in slot.Cup.transform.Cast<Transform>().Where(t => t.name == "CupBottom" || t.name.StartsWith("CupWall_", StringComparison.Ordinal)).ToArray()) UnityEngine.Object.DestroyImmediate(child.gameObject);
                Vector3 axis = slot.RestCup - center; axis.y = 0; axis.Normalize();
                slot.Cup.transform.SetPositionAndRotation(table.position + root.transform.TransformDirection(axis * .37f + Vector3.up * .004f), root.transform.rotation * Quaternion.LookRotation(axis, Vector3.up));
                slot.Cup.transform.localScale = Vector3.one;
                var parentScale = slot.Cup.transform.parent.lossyScale;
                slot.Cup.transform.localScale = new Vector3(1f / parentScale.x, 1f / parentScale.y, 1f / parentScale.z);
                slot.Cup.GetComponent<MeshFilter>().sharedMesh = mug;
                slot.Cup.GetComponent<MeshRenderer>().sharedMaterials = new[] { mugMaterial };
                foreach (var collider in slot.Cup.GetComponents<Collider>()) UnityEngine.Object.DestroyImmediate(collider);
                var cupCollider = slot.Cup.gameObject.AddComponent<BoxCollider>(); cupCollider.center = mug.bounds.center; cupCollider.size = mug.bounds.size; cupCollider.isTrigger = true;
                slot.Cup.isKinematic = true; slot.Cup.useGravity = false;
                slot.RestCup = root.transform.InverseTransformPoint(slot.Cup.transform.position);
                slot.RestRotation = Quaternion.Inverse(root.transform.rotation) * slot.Cup.transform.rotation;
                slot.CupHeight = .23f;
                Vector3 sideways = Vector3.Cross(Vector3.up, axis);
                for (int i = 0; i < slot.Dice.Length; i++)
                {
                    var die = slot.Dice[i];
                    var contact = die.GetComponent<ShipV3DiceContact>();
                    if (contact == null) contact = die.gameObject.AddComponent<ShipV3DiceContact>();
                    contact.Ship = features; contact.Slot = index;
                    die.transform.position = table.position + root.transform.TransformDirection(axis * (.21f + i / 2 * .055f) + sideways * ((i % 2 - .5f) * .055f) + Vector3.up * .035f);
                    die.isKinematic = true; die.solverIterations = 12; die.solverVelocityIterations = 4; die.linearDamping = .6f; die.angularDamping = .8f;
                }
                float angle = Mathf.Atan2(axis.z, axis.x);
                var top = new GameObject("DiceSectorTop_" + index); top.transform.SetParent(table, false);
                var sector = new Mesh { name = "DiceSectorTop_" + index };
                var vertices = new Vector3[25]; var uv = new Vector2[25]; var triangles = new int[23 * 3];
                vertices[0] = Vector3.up * .004f; uv[0] = Vector2.one * .5f;
                for (int step = 0; step < 24; step++)
                {
                    float arc = angle - Mathf.PI / 3f + step / 23f * Mathf.PI * 2f / 3f;
                    vertices[step + 1] = new Vector3(Mathf.Cos(arc) * radius, .004f, Mathf.Sin(arc) * radius);
                    uv[step + 1] = new Vector2(vertices[step + 1].x, vertices[step + 1].z) * 1.5f + Vector2.one * .5f;
                    if (step < 23) { triangles[step * 3] = 0; triangles[step * 3 + 1] = step + 2; triangles[step * 3 + 2] = step + 1; }
                }
                sector.vertices = vertices; sector.uv = uv; sector.triangles = triangles; sector.RecalculateNormals(); sector.RecalculateBounds();
                sector = SaveMesh(sector, Folder + "Sector" + index + ".asset");
                top.AddComponent<MeshFilter>().sharedMesh = sector; top.AddComponent<MeshRenderer>().sharedMaterial = wood;
                Vector3 radial = new Vector3(Mathf.Cos(angle + Mathf.PI / 3f), 0, Mathf.Sin(angle + Mathf.PI / 3f));
                float centerGap = .105f;
                Cube(table, "DiceSectorDivider_" + index, radial * ((radius + centerGap) * .5f) + Vector3.up * .055f, Quaternion.LookRotation(radial), new Vector3(.027f, .11f, radius - centerGap), wood);
            }
            for (int index = 0; index < 24; index++)
            {
                float angle = index * Mathf.PI * 2f / 24;
                Vector3 radial = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                Cube(table, "DiceRim_" + index, radial * radius + Vector3.up * .055f, Quaternion.LookRotation(radial), new Vector3(radius * .267f, .11f, .025f), wood);
            }
            ConfigureCandle(features, table);
            ConfigurePresentation(root);
            SeatTable(root);
        }

        public static void ConfigurePresentation(GameObject root)
        {
            var features = root.GetComponent<ShipV3Features>();
            foreach (var slot in features.DiceSlots)
            {
                slot.CupVisual = ConfigureVisual(slot.Cup, "DiceCupVisual");
                slot.Cup.interpolation = RigidbodyInterpolation.None;
                slot.DiceVisuals = new Transform[slot.Dice.Length];
                for (int i = 0; i < slot.Dice.Length; i++)
                {
                    slot.Dice[i].interpolation = RigidbodyInterpolation.None;
                    slot.DiceVisuals[i] = ConfigureVisual(slot.Dice[i], "DiceVisual");
                }
            }
            var table = features.DiceTable;
            foreach (var child in table.Cast<Transform>().Where(t => t.name.StartsWith("DiceZoneNumber_", StringComparison.Ordinal)).ToArray()) UnityEngine.Object.DestroyImmediate(child.gameObject);
            var gold = ZoneMaterial("ZoneNumberGold", new Color(.66f, .43f, .16f), .65f, .32f);
            var outline = ZoneMaterial("ZoneNumberOutline", new Color(.055f, .025f, .012f), 0, .16f);
            var paths = new[]
            {
                new[] {
                    new[] { new Vector2(-.022f,.031f), new Vector2(0,.047f), new Vector2(0,-.047f) },
                    new[] { new Vector2(-.027f,-.047f), new Vector2(.027f,-.047f) } },
                new[] {
                    new[] { new Vector2(-.03f,.026f), new Vector2(-.027f,.039f), new Vector2(-.013f,.047f), new Vector2(.009f,.047f), new Vector2(.026f,.037f), new Vector2(.03f,.022f), new Vector2(.025f,.008f), new Vector2(-.029f,-.043f), new Vector2(-.029f,-.047f), new Vector2(.029f,-.047f), new Vector2(.029f,-.035f) } },
                new[] {
                    new[] { new Vector2(-.029f,.036f), new Vector2(-.015f,.047f), new Vector2(.008f,.047f), new Vector2(.026f,.036f), new Vector2(.029f,.022f), new Vector2(.021f,.007f), new Vector2(0,0), new Vector2(.022f,-.006f), new Vector2(.031f,-.024f), new Vector2(.026f,-.039f), new Vector2(.009f,-.049f), new Vector2(-.015f,-.047f), new Vector2(-.031f,-.035f) } }
            };
            Vector3 center = root.transform.InverseTransformPoint(table.position);
            for (int i = 0; i < features.DiceSlots.Length; i++)
            {
                Vector3 axis = features.DiceSlots[i].RestCup - center; axis.y = 0; axis.Normalize();
                var mark = new GameObject("DiceZoneNumber_" + (i + 1)); mark.transform.SetParent(table, false);
                mark.transform.localPosition = axis * (features.DiceRadius * .55f) + Vector3.up * .0055f;
                mark.transform.localRotation = Quaternion.LookRotation(-axis, Vector3.up);
                var shadow = mark.AddComponent<MeshFilter>(); shadow.sharedMesh = SaveMesh(NumberMesh(paths[i], .016f), Folder + "ZoneNumberOutline" + (i + 1) + ".asset");
                var shadowRenderer = mark.AddComponent<MeshRenderer>(); shadowRenderer.sharedMaterial = outline; shadowRenderer.shadowCastingMode = ShadowCastingMode.Off;
                var face = new GameObject("GoldInlay"); face.transform.SetParent(mark.transform, false); face.transform.localPosition = Vector3.up * .0007f;
                face.AddComponent<MeshFilter>().sharedMesh = SaveMesh(NumberMesh(paths[i], .01f), Folder + "ZoneNumber" + (i + 1) + ".asset");
                var faceRenderer = face.AddComponent<MeshRenderer>(); faceRenderer.sharedMaterial = gold; faceRenderer.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        static Transform ConfigureVisual(Rigidbody body, string name)
        {
            var visual = body.transform.Find(name);
            if (visual == null)
            {
                var go = new GameObject(name); visual = go.transform; visual.SetParent(body.transform, false);
                go.AddComponent<MeshFilter>(); go.AddComponent<MeshRenderer>();
            }
            visual.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity); visual.localScale = Vector3.one;
            var source = body.GetComponent<MeshRenderer>();
            visual.GetComponent<MeshFilter>().sharedMesh = body.GetComponent<MeshFilter>().sharedMesh;
            var renderer = visual.GetComponent<MeshRenderer>();
            renderer.sharedMaterials = source.sharedMaterials; renderer.shadowCastingMode = source.shadowCastingMode;
            renderer.receiveShadows = source.receiveShadows; renderer.enabled = true;
            source.enabled = false; source.forceRenderingOff = true;
            return visual;
        }

        static float BarrelLidHeight(GameObject root, MeshFilter barrel)
        {
            var mapping = root.transform.worldToLocalMatrix * barrel.transform.localToWorldMatrix;
            var points = barrel.sharedMesh.vertices.Select(mapping.MultiplyPoint3x4).ToArray();
            var surfaces = new System.Collections.Generic.Dictionary<int, float>();
            for (int sub = 0; sub < barrel.sharedMesh.subMeshCount; sub++)
            {
                var faces = barrel.sharedMesh.GetTriangles(sub);
                for (int triangle = 0; triangle < faces.Length; triangle += 3)
                {
                    Vector3 a = points[faces[triangle]], b = points[faces[triangle + 1]], c = points[faces[triangle + 2]];
                    Vector3 normal = Vector3.Cross(b - a, c - a);
                    if (normal.normalized.y < .8f) continue;
                    int key = Mathf.RoundToInt((a.y + b.y + c.y) / 3f * 1000f);
                    surfaces.TryGetValue(key, out float area); surfaces[key] = area + normal.magnitude;
                }
            }
            return surfaces.Where(s => s.Key / 1000f > points.Min(v => v.y) + .5f).OrderByDescending(s => s.Value).First().Key / 1000f;
        }

        public static void SeatTable(GameObject root)
        {
            var features = root.GetComponent<ShipV3Features>();
            var barrel = root.GetComponentsInChildren<MeshFilter>(true).Single(m => m.name == "V17_Dice_Game_Barrel");
            var table = features.DiceTable;
            Vector3 point = root.transform.InverseTransformPoint(table.position);
            point.y = BarrelLidHeight(root, barrel) - .003f;
            Vector3 desired = root.transform.TransformPoint(point), delta = desired - table.position;
            table.position = desired;
            foreach (var slot in features.DiceSlots)
            {
                slot.Cup.transform.position += delta;
                var cupPoint = root.transform.InverseTransformPoint(slot.Cup.transform.position);
                cupPoint.y = point.y + .004f;
                slot.Cup.transform.position = root.transform.TransformPoint(cupPoint);
                slot.RestCup = cupPoint;
                foreach (var die in slot.Dice)
                {
                    die.transform.position += delta;
                    var collider = die.GetComponent<BoxCollider>();
                    var bottom = Enumerable.Range(0, 8).Select(i => root.transform.InverseTransformPoint(die.transform.TransformPoint(collider.center + Vector3.Scale(collider.size * .5f, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)))).y).Min();
                    die.transform.position += root.transform.TransformVector(Vector3.up * (point.y + .004f - bottom));
                }
            }
            foreach (var batch in root.GetComponentsInChildren<ShipV3RenderBatch>(true).Where(b => b.Sources.Any(s => s != null && s.transform.IsChildOf(table))))
            {
                var mesh = ShipV3RenderBatch.BuildMesh(batch.Sources, batch.SharedMaterial, batch.BatchAnchor, true);
                batch.CachedMesh = SaveMesh(mesh, AssetDatabase.GetAssetPath(batch.CachedMesh));
                batch.GetComponent<MeshFilter>().sharedMesh = batch.CachedMesh;
                if (batch.CachedShadowMesh != null)
                {
                    batch.CachedShadowMesh = SaveMesh(UnityEngine.Object.Instantiate(batch.CachedMesh), AssetDatabase.GetAssetPath(batch.CachedShadowMesh));
                    if (batch.ShadowProxy != null) batch.ShadowProxy.GetComponent<MeshFilter>().sharedMesh = batch.CachedShadowMesh;
                }
            }
        }

        static Material ZoneMaterial(string name, Color color, float metallic, float smoothness)
        {
            string path = Folder + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
            material.SetColor("_BaseColor", color); material.SetFloat("_Metallic", metallic); material.SetFloat("_Smoothness", smoothness);
            material.enableInstancing = true; EditorUtility.SetDirty(material);
            return material;
        }

        static Mesh NumberMesh(Vector2[][] paths, float width)
        {
            var vertices = new System.Collections.Generic.List<Vector3>();
            var triangles = new System.Collections.Generic.List<int>();
            foreach (var path in paths)
            {
                for (int i = 0; i + 1 < path.Length; i++)
                {
                    Vector2 direction = (path[i + 1] - path[i]).normalized;
                    Vector2 normal = new Vector2(-direction.y, direction.x) * width * .5f;
                    int start = vertices.Count;
                    foreach (var point in new[] { path[i] + normal, path[i + 1] + normal, path[i + 1] - normal, path[i] - normal }) vertices.Add(new Vector3(point.x, 0, point.y));
                    triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
                }
                foreach (var point in path)
                {
                    int start = vertices.Count; vertices.Add(new Vector3(point.x, 0, point.y));
                    for (int i = 0; i <= 10; i++)
                    {
                        float angle = i * Mathf.PI * .2f;
                        vertices.Add(new Vector3(point.x + Mathf.Cos(angle) * width * .5f, 0, point.y + Mathf.Sin(angle) * width * .5f));
                        if (i < 10) triangles.AddRange(new[] { start, start + i + 2, start + i + 1 });
                    }
                }
            }
            var mesh = new Mesh { name = "DiceZoneNumber" }; mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        static void Cube(Transform parent, string name, Vector3 position, Quaternion rotation, Vector3 scale, Material material)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube); cube.name = name; cube.transform.SetParent(parent, false);
            cube.transform.localPosition = position; cube.transform.localRotation = rotation; cube.transform.localScale = scale;
            cube.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        static Mesh Geometry(string path, string name, float height, float width, float axisRotation = 0f)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            if (!importer.isReadable || importer.materialImportMode != ModelImporterMaterialImportMode.None)
            {
                importer.isReadable = true; importer.importAnimation = false; importer.importCameras = false; importer.importLights = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.None; importer.SaveAndReimport();
            }
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var filters = source.GetComponentsInChildren<MeshFilter>();
            var orientation = Matrix4x4.Rotate(Quaternion.Euler(axisRotation, 0, 0));
            var points = filters.SelectMany(f => f.sharedMesh.vertices.Select(v => orientation.MultiplyPoint3x4(source.transform.InverseTransformPoint(f.transform.TransformPoint(v))))).ToArray();
            var bounds = new Bounds(points[0], Vector3.zero); foreach (var point in points) bounds.Encapsulate(point);
            float factor = height / bounds.size.y;
            Vector3 scale = new Vector3(factor * width, factor, factor * width);
            var normalize = Matrix4x4.TRS(-Vector3.Scale(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z), scale), Quaternion.identity, scale);
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.CombineMeshes(filters.Select(f => new CombineInstance { mesh = f.sharedMesh, transform = normalize * orientation * source.transform.worldToLocalMatrix * f.transform.localToWorldMatrix }).ToArray(), true, true);
            mesh.RecalculateBounds();
            return SaveMesh(mesh, Folder + name + ".asset");
        }

        static Mesh SaveMesh(Mesh mesh, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
            EditorUtility.CopySerialized(mesh, existing); EditorUtility.SetDirty(existing); UnityEngine.Object.DestroyImmediate(mesh); return existing;
        }

        static Material Material(string path, string albedo, string normal, float smoothness)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
            var normalImporter = (TextureImporter)AssetImporter.GetAtPath(normal);
            if (normalImporter.textureType != TextureImporterType.NormalMap) { normalImporter.textureType = TextureImporterType.NormalMap; normalImporter.maxTextureSize = 1024; normalImporter.SaveAndReimport(); }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(albedo));
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normal)); material.EnableKeyword("_NORMALMAP");
            material.SetColor("_BaseColor", Color.white); material.SetFloat("_Smoothness", smoothness); material.SetFloat("_Metallic", 0); material.enableInstancing = true; EditorUtility.SetDirty(material);
            return material;
        }

        static void ConfigureCandle(ShipV3Features features, Transform table)
        {
            var candle = new GameObject("DiceCandle"); candle.transform.SetParent(table, false);
            var mesh = Geometry(Folder + "WoodenCandlestick/WoodenCandlestick.fbx", "CandleGeometry", .21f, 1f, -90f);
            candle.AddComponent<MeshFilter>().sharedMesh = mesh;
            candle.AddComponent<MeshRenderer>().sharedMaterial = Material(Folder + "WoodenCandlestick/Candle.mat", Folder + "WoodenCandlestick/wooden_candlestick_diff_1k.jpg", Folder + "WoodenCandlestick/wooden_candlestick_nor_gl_1k.jpg", .2f);
            var collider = candle.AddComponent<CapsuleCollider>(); collider.center = mesh.bounds.center; collider.height = mesh.bounds.size.y; collider.radius = Mathf.Max(mesh.bounds.extents.x, mesh.bounds.extents.z);
            var target = candle.AddComponent<ShipV3InteractionTarget>(); target.Ship = features; target.Kind = ShipV3TargetKind.Candle;
            features.DiceCandle = candle.transform;
            var flame = new GameObject("CandleFlame"); flame.transform.SetParent(candle.transform, false);
            var wick = mesh.vertices.Where(v => v.y > mesh.bounds.max.y - .003f).ToArray();
            flame.transform.localPosition = new Vector3(wick.Average(v => v.x), mesh.bounds.max.y, wick.Average(v => v.z)); flame.transform.localRotation = Quaternion.Euler(-90, 0, 0);
            var particles = flame.AddComponent<ParticleSystem>(); particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main; main.duration = 1; main.loop = true; main.prewarm = true; main.simulationSpace = ParticleSystemSimulationSpace.Local; main.scalingMode = ParticleSystemScalingMode.Local;
            main.maxParticles = 16; main.startLifetime = new ParticleSystem.MinMaxCurve(.22f, .3f); main.startSpeed = new ParticleSystem.MinMaxCurve(.025f, .045f);
            main.startSize3D = true; main.startSizeX = .012f; main.startSizeY = .028f; main.startSizeZ = .012f;
            var emission = particles.emission; emission.rateOverTime = 28;
            var shape = particles.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.radius = .001f; shape.angle = 2;
            var size = particles.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0,.7f), new Keyframe(.25f,1), new Keyframe(.8f,.75f),new Keyframe(1,0)));
            var colors = particles.colorOverLifetime; colors.enabled = true; var gradient = new Gradient();
            gradient.SetKeys(new[] {new GradientColorKey(Color.white,0),new GradientColorKey(new Color(1,.65f,.25f),1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.75f,.12f),new GradientAlphaKey(.65f,.75f),new GradientAlphaKey(0,1)}); colors.color = gradient;
            var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "Flame.mat");
            if (material == null)
            {
                var texture = new Texture2D(64,128,TextureFormat.RGBA32,false); texture.name = "CandleFlame"; texture.wrapMode = TextureWrapMode.Clamp;
                for (int y = 0; y < 128; y++) for (int x = 0; x < 64; x++)
                {
                    float v = (y + .5f) / 128f, u = (x + .5f) / 64f - .5f;
                    float width = .30f * Mathf.Pow(1f - v, .65f) + .015f;
                    float edge = Mathf.Abs(u) / width;
                    float alpha = Mathf.Clamp01((1f - edge) * 3f) * Mathf.Clamp01(v * 12f) * Mathf.Clamp01((1f - v) * 8f);
                    texture.SetPixel(x,y,Color.Lerp(new Color(1,.3f,.025f,alpha),new Color(1,1,.7f,alpha),Mathf.Clamp01((1-edge)*2f)));
                }
                texture.Apply(); AssetDatabase.CreateAsset(texture,Folder + "FlameTexture.asset");
                material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")); material.SetTexture("_BaseMap",texture); material.SetColor("_BaseColor",new Color(1.8f,1.45f,.85f,1));
                material.SetFloat("_Surface",1); material.SetFloat("_Blend",0); material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha); material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha); material.SetFloat("_ZWrite",0); material.SetFloat("_Cull",0); material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.renderQueue = 3000;
                AssetDatabase.CreateAsset(material,Folder + "Flame.mat");
            }
            var renderer = particles.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = material; renderer.renderMode = ParticleSystemRenderMode.Billboard; renderer.alignment = ParticleSystemRenderSpace.View; renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            var glow = new GameObject("CandleLight"); glow.transform.SetParent(candle.transform,false); glow.transform.localPosition = flame.transform.localPosition + Vector3.up * .015f;
            var light = glow.AddComponent<Light>(); light.type = LightType.Point; light.color = new Color(1,.64f,.32f); light.intensity = 1.1f; light.range = 1.2f; light.shadows = LightShadows.None;
            features.CandleLight = light; features.CandleFlame = particles;
        }
    }
}
