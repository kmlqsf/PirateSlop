using UnityEngine;

namespace PirateSlop.World
{
    public static class SeabedTerrain
    {
        public const int Resolution = 192;
        static float Sample(WorldLayout layout, float x, float z)
        {
            float seed = (layout.Seed & 65535) * .013f;
            float relief = (Mathf.PerlinNoise(x * .009f + seed, z * .009f + seed) - .5f) * 12f
                + (Mathf.PerlinNoise(x * .035f + seed, z * .035f - seed) - .5f) * 2f;
            float influence = 1f;
            foreach (var location in layout.Locations)
            {
                float distance = Vector2.Distance(new Vector2(x,z), new Vector2(location.Position.x,location.Position.z));
                influence = Mathf.Min(influence, Mathf.SmoothStep(0,1,Mathf.InverseLerp(location.Radius * 1.45f, location.Radius * 1.8f + 20f,distance)));
            }
            return layout.SeaLevel - layout.Depth + relief * influence;
        }
        public static float Height(WorldLayout layout, Vector3 point)
        {
            float extent = layout.Radius + 200f, step = extent * 2f / Resolution;
            float gx = Mathf.Clamp((point.x + extent) / step,0,Resolution-.0001f), gz = Mathf.Clamp((point.z + extent) / step,0,Resolution-.0001f);
            int ix = Mathf.FloorToInt(gx), iz = Mathf.FloorToInt(gz);
            float x = ix * step - extent, z = iz * step - extent, u = gx-ix, v = gz-iz;
            float a=Sample(layout,x,z), b=Sample(layout,x+step,z), c=Sample(layout,x,z+step), d=Sample(layout,x+step,z+step);
            return u+v<=1f ? a+u*(b-a)+v*(c-a) : d+(1f-u)*(c-d)+(1f-v)*(b-d);
        }
        public static Mesh Build(WorldLayout layout)
        {
            int n=Resolution;
            var vertices=new Vector3[(n+1)*(n+1)]; var triangles=new int[n*n*6]; var uv=new Vector2[vertices.Length];
            float extent=layout.Radius+200f;
            for(int z=0;z<=n;z++) for(int x=0;x<=n;x++)
            {
                int i=z*(n+1)+x;
                float px=Mathf.Lerp(-extent,extent,x/(float)n), pz=Mathf.Lerp(-extent,extent,z/(float)n);
                vertices[i]=new Vector3(px,Sample(layout,px,pz),pz); uv[i]=new Vector2(px,pz)*.1f;
                if(x==n || z==n)continue;
                int t=(z*n+x)*6;
                triangles[t]=i; triangles[t+1]=i+n+1; triangles[t+2]=i+1;
                triangles[t+3]=i+1; triangles[t+4]=i+n+1; triangles[t+5]=i+n+2;
            }
            var mesh=new Mesh {name="SandySeabed",vertices=vertices,triangles=triangles,uv=uv};
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
    }
}
