using System.IO;
using UnityEditor;
using UnityEngine;

namespace PirateSlop.Editor
{
    public static class FrozenWaterPreview
    {
        public static string Capture(float age, float remaining, float waveTime, string output)
        {
            var preview = new PreviewRenderUtility();
            var group = new GameObject("FrozenWaterPreview");
            Material water = null;
            Mesh mesh = null;
            Texture2D image = null;
            RenderTexture copy = null;
            var previousTarget = RenderTexture.active;
            var previousMask = Shader.GetGlobalTexture("_FrozenWaterMask");
            var previousMapping = Shader.GetGlobalVector("_FrozenWaterMapping");
            try
            {
                preview.AddSingleGO(group);
                var source = Object.FindFirstObjectByType<OceanSurface>();
                var ocean = group.AddComponent<OceanSurface>();
                ocean.WhirlpoolRadius = 0;
                water = new Material(source.WaterMaterial);
                water.SetFloat("_UseWaveTime",1);
                water.SetFloat("_WaveTime",waveTime);
                water.SetVectorArray("_SwellWaves",new[] { new Vector4(.94f,.342f,1.1f,180),new Vector4(-.4f,.916f,.56f,120),new Vector4(.6f,-.8f,.36f,90),new Vector4(-.8f,-.6f,.24f,64) });
                water.SetFloat("_SeaChop",.4f);
                water.SetFloat("_WhirlpoolRadius",0);
                var vertices = new Vector3[81*81]; var uv = new Vector2[vertices.Length]; var triangles = new int[80*80*6];
                for(int z=0;z<=80;z++) for(int x=0;x<=80;x++) { int n=z*81+x; vertices[n]=new Vector3(x-40,0,z-35); uv[n]=new Vector2(x/80f,z/80f); }
                int t=0;
                for(int z=0;z<80;z++) for(int x=0;x<80;x++) { int n=z*81+x; triangles[t++]=n;triangles[t++]=n+81;triangles[t++]=n+1;triangles[t++]=n+1;triangles[t++]=n+81;triangles[t++]=n+82; }
                mesh=new Mesh(); mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles;mesh.RecalculateNormals();
                var sea=new GameObject("PreviewSea"); sea.transform.SetParent(group.transform,false);sea.AddComponent<MeshFilter>().sharedMesh=mesh;sea.AddComponent<MeshRenderer>().sharedMaterial=water;
                for(int z=0;z<7;z++)
                {
                    var tile=FrozenWaterTile.Create(new Vector3(z>=4?2:0,.12f,z*2),group.scene);
                    tile.transform.SetParent(group.transform,true);
                    tile.Evaluate(age,remaining,0);
                }
                var bridge=FrozenWaterTile.Create(new Vector3(2,.12f,6),group.scene);bridge.transform.SetParent(group.transform,true);bridge.Evaluate(age,remaining,0);
                foreach(var particles in group.GetComponentsInChildren<ParticleSystem>()) particles.Simulate(age,false,true,true);
                FrozenWaterSurface.Upload(ocean,Vector3.zero);
                preview.AddSingleGO(group);
                var cameraPosition=new Vector3(5.5f,4.5f,-5);
                preview.camera.transform.SetPositionAndRotation(cameraPosition,Quaternion.LookRotation(new Vector3(0,0,5)-cameraPosition));
                preview.camera.fieldOfView=55;preview.camera.nearClipPlane=.05f;preview.camera.farClipPlane=150;
                preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.48f,.61f,.7f);
                preview.lights[0].intensity=1.8f;preview.lights[0].transform.rotation=Quaternion.Euler(38,-45,0);
                preview.lights[1].intensity=.7f;preview.lights[1].transform.rotation=Quaternion.Euler(20,140,0);
                preview.BeginPreview(new Rect(0,0,1200,800),GUIStyle.none);preview.Render(true);
                copy=RenderTexture.GetTemporary(1200,800,0,RenderTextureFormat.ARGB32);
                Graphics.Blit(preview.EndPreview(),copy);RenderTexture.active=copy;
                image=new Texture2D(1200,800,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1200,800),0,0);image.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(output));File.WriteAllBytes(output,image.EncodeToPNG());
                return output;
            }
            finally
            {
                RenderTexture.active=previousTarget;
                if(copy!=null)RenderTexture.ReleaseTemporary(copy);
                if(image!=null)Object.DestroyImmediate(image);
                if(group!=null) foreach(var tile in group.GetComponentsInChildren<FrozenWaterTile>()) tile.Unregister();
                preview.Cleanup();
                if(mesh!=null)Object.DestroyImmediate(mesh);
                if(water!=null)Object.DestroyImmediate(water);
                Shader.SetGlobalTexture("_FrozenWaterMask",previousMask);
                Shader.SetGlobalVector("_FrozenWaterMapping",previousMapping);
            }
        }
    }
}
