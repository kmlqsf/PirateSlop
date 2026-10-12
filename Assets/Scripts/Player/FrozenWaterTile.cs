using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PirateSlop
{
    public sealed class FrozenWaterTile : MonoBehaviour
    {
        static readonly HashSet<FrozenWaterTile> active = new();
        static Material material;
        Renderer surface;
        BoxCollider support;
        MaterialPropertyBlock properties;

        public static FrozenWaterTile Create(Vector3 point, Scene scene)
        {
            var root = new GameObject("FrozenWaterPath");
            root.AddComponent<MeshFilter>().sharedMesh = IceMesh();
            root.AddComponent<MeshRenderer>();
            var collider = root.AddComponent<BoxCollider>(); collider.center = Vector3.down * .09f; collider.size = new Vector3(2f, .18f, 2f);
            root.name = "FrozenWaterPath";
            SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.position = point;

            root.layer = LayerMask.NameToLayer("WorldStatic");
            var tile = root.AddComponent<FrozenWaterTile>();
            tile.surface = root.GetComponent<Renderer>();
            tile.support = root.GetComponent<BoxCollider>();
            tile.properties = new MaterialPropertyBlock();
            tile.createdAt = Time.time;
            tile.surface.sharedMaterial = IceMaterial();
            active.Add(tile); FrozenWaterSurface.Add(tile);
            StaffVfx.FrozenWaterMist(root.transform);
            return tile;
        }

        static Material IceMaterial()
        {
            if (material == null) material = new Material(Resources.Load<Shader>("FrozenWaterIce")) { name = "FrozenWaterIce", hideFlags = HideFlags.HideAndDontSave };
            return material;
        }

        static Mesh iceMesh;
        static Mesh IceMesh()
        {
            if (iceMesh != null) return iceMesh;
            var corners = new[] { new Vector2(- .98f,-1f), new Vector2( .98f,-1f), new Vector2(1f,- .98f), new Vector2(1f, .98f), new Vector2( .98f,1f), new Vector2(- .98f,1f), new Vector2(-1f, .98f), new Vector2(-1f,- .98f) };
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            for (int i = 0; i < 8; i++)
            {
                var a = corners[i]; var b = corners[(i+1)%8];
                int n=vertices.Count;
                vertices.Add(Vector3.zero); vertices.Add(new Vector3(b.x,0,b.y)); vertices.Add(new Vector3(a.x,0,a.y));
                vertices.Add(new Vector3(a.x,0,a.y)); vertices.Add(new Vector3(b.x,0,b.y)); vertices.Add(new Vector3(a.x*.92f,-.18f,a.y*.92f));
                vertices.Add(new Vector3(b.x,0,b.y)); vertices.Add(new Vector3(b.x*.92f,-.18f,b.y*.92f)); vertices.Add(new Vector3(a.x*.92f,-.18f,a.y*.92f));
                for(int j=0;j<9;j++) triangles.Add(n+j);
            }
            iceMesh=new Mesh { name="FrozenWaterBeveledSurface", hideFlags=HideFlags.HideAndDontSave };
            iceMesh.SetVertices(vertices); iceMesh.SetTriangles(triangles,0); iceMesh.RecalculateNormals(); iceMesh.RecalculateBounds();
            return iceMesh;
        }

        float createdAt, retireAt;
        public float Formation { get; private set; } = 1f;
        bool removing;
        public void SetRemaining(float seconds)
        {
            Evaluate(Time.time-createdAt, seconds, 0);
        }

        public void Evaluate(float age, float seconds, float collapse)
        {
            Formation = Mathf.SmoothStep(.15f,1f,Mathf.Clamp01(age/.35f));
            properties.SetFloat("_Formation", Formation);
            properties.SetFloat("_Thaw", Mathf.Max((1-Mathf.Clamp01(seconds/1.15f))*.45f,collapse));
            surface.SetPropertyBlock(properties);
        }

        void Update()
        {
            if (!removing) return;
            Evaluate(Time.time-createdAt,0,Mathf.Clamp01(1-(retireAt-Time.time)/.45f));
            if(Time.time>=retireAt) Destroy(gameObject);
        }

        public static bool Supports(Vector3 feet, Scene scene)
        {
            foreach (var tile in active)
            {
                if (tile == null || tile.gameObject.scene != scene || tile.support == null || !tile.support.enabled) continue;
                var bounds = tile.support.bounds;
                if (Mathf.Abs(feet.x - bounds.center.x) <= bounds.extents.x && Mathf.Abs(feet.z - bounds.center.z) <= bounds.extents.z && feet.y >= bounds.max.y - .25f && feet.y <= bounds.max.y + .6f) return true;
            }
            return false;
        }

        void OnEnable() { active.Add(this); FrozenWaterSurface.Add(this); }
        public void Unregister() { active.Remove(this); FrozenWaterSurface.Remove(this); }
        void OnDisable() => Unregister();
        public void Remove()
        {
            if (support != null) support.enabled = false;
            active.Remove(this);
            removing = true; retireAt = Time.time + .45f;
        }
    }
}
