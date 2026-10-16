using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PirateSlop.World
{
    [DefaultExecutionOrder(70)]
    [RequireComponent(typeof(LineRenderer))]
    public sealed class RiggingRopeVisual : MonoBehaviour
    {
        public float TilesPerMeter = 3;
        public float LengthOffset;
        LineRenderer line;
        Mesh mesh;
        MeshRenderer output;
        readonly List<Vector3> points = new();
        readonly TubeBuffers buffers = new();
        static readonly Vector2[] circle = CreateCircle();
        static Vector2[] CreateCircle()
        {
            var result = new Vector2[13];
            for (int i = 0; i < result.Length; i++) { float angle = i * Mathf.PI * 2 / 12; result[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)); }
            return result;
        }
        sealed class TubeBuffers
        {
            public readonly List<Vector3> Rounded=new(),Path=new(),Vertices=new(),Normals=new(),Directions=new();
            public readonly List<Vector4> Tangents=new();
            public readonly List<Vector2> UV=new();
            public readonly List<int> Triangles=new();
            public int PathCount;
            public bool Closed;
        }
        Vector3[] previous;
        float previousRadius, previousOffset, previousTiles;
        float nextVisualUpdate;
        void Awake()
        {
            line = GetComponent<LineRenderer>();
            var child = new GameObject("TexturedRiggingRope");
            child.layer = gameObject.layer; child.transform.SetParent(transform,false);
            mesh = new Mesh { name = "RiggingRopeTube", indexFormat = IndexFormat.UInt32 }; mesh.MarkDynamic();
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            output = child.AddComponent<MeshRenderer>(); output.shadowCastingMode = ShadowCastingMode.On;
        }
        void LateUpdate()
        {
            output.enabled = line != null && line.enabled;
            if (!output.enabled) return;
            line.forceRenderingOff = true; output.sharedMaterial = line.sharedMaterial;
            if (previous != null && !PirateSlop.Performance.WorldVisualBudget.Due(output, ref nextVisualUpdate)) return;
            int count = line.positionCount; float radius = line.widthMultiplier*.5f;
            bool changed = previous == null || previous.Length != count || !Mathf.Approximately(radius,previousRadius) || !Mathf.Approximately(LengthOffset,previousOffset) || !Mathf.Approximately(TilesPerMeter,previousTiles);
            if (previous == null || previous.Length != count) previous = new Vector3[count];
            points.Clear();
            for (int i=0;i<count;i++)
            {
                var point = line.GetPosition(i);
                if (line.useWorldSpace) point = transform.InverseTransformPoint(point);
                changed |= (point-previous[i]).sqrMagnitude > .00000001f;
                points.Add(point); previous[i] = point;
            }
            if (!changed) return;
            previousRadius = radius; previousOffset = LengthOffset; previousTiles = TilesPerMeter; BuildTube(mesh,points,radius,TilesPerMeter,buffers,LengthOffset);
        }
        public static Mesh CreateTube(IReadOnlyList<Vector3> points,float radius)
        {
            var mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
            BuildTube(mesh,points,radius,3,new TubeBuffers()); return mesh;
        }
        static void BuildTube(Mesh mesh,IReadOnlyList<Vector3> source,float radius,float tiles,TubeBuffers buffers,float offset = 0)
        {
            if (source.Count < 2) { mesh.Clear(); return; }
            bool closed = (source[0]-source[source.Count-1]).sqrMagnitude < .000001f;
            int count = closed ? source.Count-1 : source.Count;
            var rounded = buffers.Rounded;rounded.Clear();
            for (int i=0;i<count;i++)
            {
                if (!closed && (i==0 || i==count-1)) { rounded.Add(source[i]); continue; }
                var point = source[i]; var before = source[(i+count-1)%count]; var after = source[(i+1)%count];
                float cut = Mathf.Min(radius*2.5f,Vector3.Distance(before,point)*.28f,Vector3.Distance(point,after)*.28f);
                var entry = point+(before-point).normalized*cut; var exit = point+(after-point).normalized*cut;
                for (int j=0;j<=4;j++)
                {
                    float t=j/4f;
                    rounded.Add((1-t)*(1-t)*entry+2*(1-t)*t*point+t*t*exit);
                }
            }
            if (closed) rounded.Add(rounded[0]);
            var path = buffers.Path;path.Clear();path.Add(rounded[0]);
            for (int i=1;i<rounded.Count;i++)
            {
                var a=rounded[i-1];var b=rounded[i];float length=Vector3.Distance(a,b);
                if (length < .00001f) continue;
                int steps=Mathf.Max(1,Mathf.CeilToInt(length/.35f));
                if (steps <= 3)
                    for(int j=1;j<=steps;j++) path.Add(Vector3.Lerp(a,b,j/(float)steps));
                else
                {
                    path.Add(Vector3.Lerp(a,b,1f/steps));
                    path.Add(Vector3.Lerp(a,b,(steps-1f)/steps));
                    path.Add(b);
                }
            }
            if(path.Count<2){mesh.Clear();return;}
            const int sides=12,ring=sides+1;
            int surfaceCount=path.Count*ring;
            int vertexCount=surfaceCount+(closed?0:2*ring+2);
            bool topologyChanged = mesh.vertexCount != vertexCount || buffers.PathCount != path.Count || buffers.Closed != closed;
            if(mesh.vertexCount!=vertexCount)mesh.Clear();
            buffers.PathCount = path.Count; buffers.Closed = closed;
            var vertices=buffers.Vertices;var normals=buffers.Normals;var tangents=buffers.Tangents;var uv=buffers.UV;var triangles=buffers.Triangles;
            vertices.Clear();normals.Clear();tangents.Clear();uv.Clear();if(topologyChanged)triangles.Clear();
            Vector3 across=Vector3.right;float distance=0;
            var directions=buffers.Directions;directions.Clear();
            for(int i=0;i<path.Count;i++)
            {
                if(i>0)distance+=Vector3.Distance(path[i-1],path[i]);
                var before=path[i>0?i-1:closed?path.Count-2:0];
                var after=path[i<path.Count-1?i+1:closed?1:path.Count-1];
                var direction=(after-before).normalized;directions.Add(direction);
                across=Vector3.ProjectOnPlane(across,direction).normalized;
                if(across.sqrMagnitude<.01f)across=Vector3.Cross(direction,Mathf.Abs(direction.y)<.9f?Vector3.up:Vector3.forward).normalized;
                var other=Vector3.Cross(direction,across).normalized;
                for(int side=0;side<=sides;side++)
                {
                    var normal=across*circle[side].x+other*circle[side].y;
                    vertices.Add(path[i]+normal*radius);normals.Add(normal);tangents.Add(new Vector4(direction.x,direction.y,direction.z,-1));uv.Add(new Vector2((distance+offset)*tiles,side/(float)sides));
                    if(i==0||side==sides||!topologyChanged)continue;
                    int a=(i-1)*ring+side,b=i*ring+side;
                    triangles.Add(a);triangles.Add(a+1);triangles.Add(b);triangles.Add(a+1);triangles.Add(b+1);triangles.Add(b);
                }
            }
            if(!closed)
                for(int end=0;end<2;end++)
                {
                    int index=end==0?0:path.Count-1,baseIndex=vertices.Count;var normal=directions[index]*(end==0?-1:1);
                    vertices.Add(path[index]);normals.Add(normal);tangents.Add(new Vector4(1,0,0,1));uv.Add(new Vector2(.5f,.5f));
                    for(int side=0;side<=sides;side++)
                    {
                        vertices.Add(vertices[index*ring+side]);normals.Add(normal);tangents.Add(new Vector4(1,0,0,1));uv.Add(new Vector2(.5f,.5f));
                        if(side==sides||!topologyChanged)continue;
                        triangles.Add(baseIndex);triangles.Add(baseIndex+side+(end==0?2:1));triangles.Add(baseIndex+side+(end==0?1:2));
                    }
                }
            mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetTangents(tangents);mesh.SetUVs(0,uv);if(topologyChanged)mesh.SetTriangles(triangles,0,false);mesh.RecalculateBounds();
        }
        void OnDestroy()
        {
            if(line!=null)line.forceRenderingOff=false;
            if(mesh!=null)Destroy(mesh);
        }
    }
}
