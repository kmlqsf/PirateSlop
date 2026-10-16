using System.Collections.Generic;
using FishNet.Object.Synchronizing;
using PirateSlop.Customization;
using PirateSlop.Networking;
using UnityEngine;

namespace PirateSlop.Ships
{
    public sealed class SailDamageSurface : System.IDisposable
    {
        static readonly int damageMapId = Shader.PropertyToID("_SailDamageMap");
        static readonly int stampSeedId = Shader.PropertyToID("_Seed");
        static readonly int sailShapeId = Shader.PropertyToID("_SailShape");
        static readonly int tearId = Shader.PropertyToID("_Tear");
        static readonly int sourceTexId = Shader.PropertyToID("_SourceTex");
        static readonly int sourceStepId = Shader.PropertyToID("_SourceStep");
        static readonly int sourceMipId = Shader.PropertyToID("_SourceMip");
        static readonly int reduceMinimumId = Shader.PropertyToID("_ReduceMinimum");
        const float StampExtent = 2.75f;
        readonly SkinnedMeshRenderer renderer;
        readonly ShipV3ClothMotion cloth;
        readonly Mesh baked;
        readonly List<Vector3> vertices;
        readonly Vector2[] uv;
        readonly int[] triangles;
        readonly List<SailPuncture> history = new();
        readonly Vector2 fabricSize;
        readonly Vector4 sailShape;
        RenderTexture mask, mipScratch;
        Material stampMaterial, ownedMaterial, boundMaterial, originalMaterial;
        int bakedFrame = -1;
        bool dirty;

        public SailDamageSurface(SkinnedMeshRenderer sail, int sailIndex)
        {
            renderer = sail;
            sailShape = new Vector4(sailIndex == 4 ? 1f : 0f, .37f + sailIndex * 1.731f, 0f, 0f);
            cloth = sail.GetComponent<ShipV3ClothMotion>();
            var mesh = sail.sharedMesh;
            if (mesh == null || !mesh.isReadable) return;
            uv = mesh.uv;
            triangles = mesh.triangles;
            vertices = new List<Vector3>(mesh.vertexCount);
            baked = new Mesh { name = "SailDamageTrace", hideFlags = HideFlags.HideAndDontSave };
            var source = mesh.vertices;
            var matrix = sail.transform.localToWorldMatrix;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                Vector2 first = uv[b] - uv[a], second = uv[c] - uv[a];
                float determinant = first.x * second.y - first.y * second.x;
                if (Mathf.Abs(determinant) < .0000001f) continue;
                Vector3 edge1 = matrix.MultiplyVector(source[b] - source[a]), edge2 = matrix.MultiplyVector(source[c] - source[a]);
                fabricSize.x = Mathf.Max(fabricSize.x, ((edge1 * second.y - edge2 * first.y) / determinant).magnitude);
                fabricSize.y = Mathf.Max(fabricSize.y, ((edge2 * first.x - edge1 * second.x) / determinant).magnitude);
            }
        }

        public void Puncture(SyncList<SailPuncture> holes, byte index, Vector3 start, Vector3 end, float diameter, ushort seed)
        {
            if (baked == null || renderer == null || !renderer.enabled || renderer.forceRenderingOff || !renderer.gameObject.activeInHierarchy) return;
            Vector3 travel = end - start;
            float distance = travel.magnitude;
            var bounds = renderer.bounds;
            bounds.Expand(diameter);
            if (!bounds.Contains(start) && (!bounds.IntersectRay(new Ray(start, travel / distance), out float entry) || entry > distance)) return;
            if (bakedFrame != Time.frameCount)
            {
                cloth?.PrepareSailRaycast();
                renderer.BakeMesh(baked, false);
                baked.GetVertices(vertices);
                bakedFrame = Time.frameCount;
            }
            var matrix = renderer.transform.localToWorldMatrix;
            Vector3 origin = renderer.transform.InverseTransformPoint(start);
            Vector3 direction = renderer.transform.InverseTransformVector(travel);
            Vector3 flight = travel / distance;
            Vector3 right = Vector3.Cross(flight, Mathf.Abs(flight.y) < .95f ? Vector3.up : Vector3.right).normalized;
            Vector3 up = Vector3.Cross(right, flight);
            float rotation = seed * .01745329252f;
            Vector3 rotatedRight = right * Mathf.Cos(rotation) + up * Mathf.Sin(rotation);
            Vector3 rotatedUp = up * Mathf.Cos(rotation) - right * Mathf.Sin(rotation);
            float radius = diameter * .5f;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                Vector3 edge1 = vertices[b] - vertices[a], edge2 = vertices[c] - vertices[a];
                Vector3 cross = Vector3.Cross(direction, edge2);
                float determinant = Vector3.Dot(edge1, cross);
                if (Mathf.Abs(determinant) < .000000000001f) continue;
                Vector3 offset = origin - vertices[a];
                float first = Vector3.Dot(offset, cross) / determinant;
                if (first < 0f || first > 1f) continue;
                Vector3 side = Vector3.Cross(offset, edge1);
                float second = Vector3.Dot(direction, side) / determinant;
                if (second < 0f || first + second > 1f) continue;
                float fraction = Vector3.Dot(edge2, side) / determinant;
                if (fraction < 0f || fraction > 1f) continue;
                Vector3 world1 = matrix.MultiplyVector(edge1), world2 = matrix.MultiplyVector(edge2);
                Vector3 normal = Vector3.Cross(world1, world2).normalized;
                float incidence = Vector3.Dot(normal, flight);
                incidence = Mathf.Sign(incidence) * Mathf.Max(.15f, Mathf.Abs(incidence));
                Vector3 holeRight = (rotatedRight - flight * (Vector3.Dot(normal, rotatedRight) / incidence)) * radius;
                Vector3 holeUp = (rotatedUp - flight * (Vector3.Dot(normal, rotatedUp) / incidence)) * radius;
                Vector2 uv1 = uv[b] - uv[a], uv2 = uv[c] - uv[a];
                var hole = new SailPuncture { Sail = index, Seed = seed, UV = uv[a] + uv1 * first + uv2 * second,
                    AxisU = ToUV(holeRight, world1, world2, uv1, uv2), AxisV = ToUV(holeUp, world1, world2, uv1, uv2) };
                bool covered = false;
                for (int previous = holes.Count - 1; previous >= Mathf.Max(0, holes.Count - 64); previous--)
                    if (holes[previous].Sail == index && Covers(holes[previous], hole)) { covered = true; break; }
                if (!covered) holes.Add(hole);
            }
        }

        static Vector2 ToUV(Vector3 vector, Vector3 edge1, Vector3 edge2, Vector2 uv1, Vector2 uv2)
        {
            float a = Vector3.Dot(edge1, edge1), b = Vector3.Dot(edge1, edge2), c = Vector3.Dot(edge2, edge2);
            float determinant = a * c - b * b;
            if (determinant < .000000000001f) return Vector2.zero;
            float first = Vector3.Dot(vector, edge1), second = Vector3.Dot(vector, edge2);
            return uv1 * ((first * c - second * b) / determinant) + uv2 * ((second * a - first * b) / determinant);
        }

        static bool Covers(SailPuncture existing, SailPuncture incoming)
        {
            if ((existing.UV - incoming.UV).sqrMagnitude < .0000000001f
                && (existing.AxisU - incoming.AxisU).sqrMagnitude < .0000000001f
                && (existing.AxisV - incoming.AxisV).sqrMagnitude < .0000000001f) return true;
            float determinant = existing.AxisU.x * existing.AxisV.y - existing.AxisU.y * existing.AxisV.x;
            if (Mathf.Abs(determinant) < .000000000001f) return false;
            Vector2 Coordinates(Vector2 delta) => new((delta.x * existing.AxisV.y - delta.y * existing.AxisV.x) / determinant,
                (existing.AxisU.x * delta.y - existing.AxisU.y * delta.x) / determinant);
            float extent = Coordinates(incoming.AxisU).magnitude + Coordinates(incoming.AxisV).magnitude;
            return Coordinates(incoming.UV - existing.UV).magnitude + extent * StampExtent < .65f;
        }

        public void Stamp(SailPuncture hole)
        {
            history.Add(hole);
            if (!EnsureMask()) return;
            Draw(hole);
        }

        bool EnsureMask()
        {
            if (renderer == null || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return false;
            if (mask == null)
            {
                var shader = Resources.Load<Shader>("SailDamageStamp");
                if (shader == null) return false;
                stampMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                stampMaterial.SetVector(sailShapeId, sailShape);
                float longest = Mathf.Max(.01f, fabricSize.x, fabricSize.y);
                int width = Mathf.Clamp(Mathf.NextPowerOfTwo(Mathf.CeilToInt(4096f * fabricSize.x / longest)), 256, 4096);
                int height = Mathf.Clamp(Mathf.NextPowerOfTwo(Mathf.CeilToInt(4096f * fabricSize.y / longest)), 256, 4096);
                mask = new RenderTexture(width, height, 0, RenderTextureFormat.R8, RenderTextureReadWrite.Linear)
                {
                    name = "SailDamage_" + renderer.name, hideFlags = HideFlags.HideAndDontSave,
                    useMipMap = true, autoGenerateMips = false, filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Clamp
                };
                mask.Create();
                Clear();
                var customizer = renderer.GetComponentInParent<SailCustomizer>();
                bool customized = customizer != null && customizer.PartRenderers != null && System.Array.IndexOf(customizer.PartRenderers, renderer) >= 0;
                boundMaterial = renderer.sharedMaterial;
                if (!customized)
                {
                    originalMaterial = boundMaterial;
                    ownedMaterial = new Material(boundMaterial) { hideFlags = HideFlags.HideAndDontSave };
                    ownedMaterial.shader = Shader.Find("PirateSlop/Sail");
                    renderer.sharedMaterial = ownedMaterial;
                    boundMaterial = ownedMaterial;
                }
                boundMaterial.SetTexture(damageMapId, mask);
            }
            else if (!mask.IsCreated())
            {
                mask.Create();
                Clear();
                foreach (var hole in history) Draw(hole);
            }
            return true;
        }

        void Clear()
        {
            var previous = RenderTexture.active;
            RenderTexture.active = mask;
            GL.Clear(false, true, Color.clear);
            RenderTexture.active = previous;
        }

        void Draw(SailPuncture hole)
        {
            float radius = Mathf.Min(Vector2.Scale(hole.AxisU, fabricSize).magnitude, Vector2.Scale(hole.AxisV, fabricSize).magnitude);
            float tear = radius < .03f ? Mathf.Lerp(1.4f, .7f, Mathf.Clamp01(radius / .03f)) : 0f;
            Vector2 texelU = Vector2.Scale(hole.AxisU, new Vector2(mask.width, mask.height));
            Vector2 texelV = Vector2.Scale(hole.AxisV, new Vector2(mask.width, mask.height));
            float u = texelU.sqrMagnitude, v = texelV.sqrMagnitude, skew = Vector2.Dot(texelU, texelV);
            float minimumRadius = Mathf.Sqrt(Mathf.Max(0f, (u + v - Mathf.Sqrt((u - v) * (u - v) + 4f * skew * skew)) * .5f));
            if (minimumRadius > 0f && minimumRadius < 1.2f)
            {
                hole.AxisU *= 1.2f / minimumRadius;
                hole.AxisV *= 1.2f / minimumRadius;
            }
            var previous = RenderTexture.active;
            bool srgb = GL.sRGBWrite;
            RenderTexture.active = mask;
            GL.sRGBWrite = false;
            GL.PushMatrix();
            try
            {
                GL.LoadOrtho();
                stampMaterial.SetFloat(stampSeedId, hole.Seed);
                stampMaterial.SetFloat(tearId, tear);
                stampMaterial.SetPass(0);
                GL.Begin(GL.QUADS);
                Vertex(hole, -StampExtent, -StampExtent);
                Vertex(hole, StampExtent, -StampExtent);
                Vertex(hole, StampExtent, StampExtent);
                Vertex(hole, -StampExtent, StampExtent);
                GL.End();
            }
            finally
            {
                GL.PopMatrix();
                GL.sRGBWrite = srgb;
                RenderTexture.active = previous;
            }
            dirty = true;
        }

        static void Vertex(SailPuncture hole, float x, float y)
        {
            Vector2 point = hole.UV + hole.AxisU * x + hole.AxisV * y;
            GL.TexCoord2(x, y);
            GL.Vertex3(point.x, point.y, 0f);
        }

        public void Flush()
        {
            if (mask == null || !EnsureMask() || !dirty) return;
            if (mipScratch == null)
            {
                mipScratch = new RenderTexture(Mathf.Max(1, mask.width / 2), Mathf.Max(1, mask.height / 2), 0, RenderTextureFormat.R8, RenderTextureReadWrite.Linear)
                {
                    name = "SailDamageMips_" + renderer.name, hideFlags = HideFlags.HideAndDontSave,
                    useMipMap = true, autoGenerateMips = false, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp
                };
            }
            if (!mipScratch.IsCreated()) mipScratch.Create();
            var previous = RenderTexture.active;
            bool srgb = GL.sRGBWrite;
            GL.sRGBWrite = false;
            GL.PushMatrix();
            try
            {
                GL.LoadOrtho();
                stampMaterial.SetTexture(sourceTexId, mask);
                int width = mask.width, height = mask.height;
                for (int level = 1; width > 1 || height > 1; level++)
                {
                    Graphics.SetRenderTarget(mipScratch, level - 1);
                    stampMaterial.SetVector(sourceStepId, new Vector4(.5f / width, .5f / height, 0f, 0f));
                    stampMaterial.SetFloat(sourceMipId, level - 1);
                    stampMaterial.SetFloat(reduceMinimumId, level > 4 ? 1f : 0f);
                    stampMaterial.SetPass(1);
                    GL.Begin(GL.QUADS);
                    GL.TexCoord2(0f, 0f); GL.Vertex3(0f, 0f, 0f);
                    GL.TexCoord2(1f, 0f); GL.Vertex3(1f, 0f, 0f);
                    GL.TexCoord2(1f, 1f); GL.Vertex3(1f, 1f, 0f);
                    GL.TexCoord2(0f, 1f); GL.Vertex3(0f, 1f, 0f);
                    GL.End();
                    Graphics.CopyTexture(mipScratch, 0, level - 1, mask, 0, level);
                    width = Mathf.Max(1, width / 2);
                    height = Mathf.Max(1, height / 2);
                }
            }
            finally
            {
                GL.PopMatrix();
                GL.sRGBWrite = srgb;
                RenderTexture.active = previous;
            }
            dirty = false;
        }

        public void Dispose()
        {
            if (boundMaterial != null) boundMaterial.SetTexture(damageMapId, Texture2D.blackTexture);
            if (ownedMaterial != null && renderer != null && renderer.sharedMaterial == ownedMaterial) renderer.sharedMaterial = originalMaterial;
            if (mask != null) { mask.Release(); Object.Destroy(mask); }
            if (mipScratch != null) { mipScratch.Release(); Object.Destroy(mipScratch); }
            if (stampMaterial != null) Object.Destroy(stampMaterial);
            if (ownedMaterial != null) Object.Destroy(ownedMaterial);
            if (baked != null) Object.Destroy(baked);
        }
    }
}
