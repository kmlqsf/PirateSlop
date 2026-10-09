using UnityEngine;

namespace PirateSlop
{
    [DefaultExecutionOrder(310)]
    public sealed class StormBillowController : MonoBehaviour
    {
        public Material Material;
        Texture3D density,nearNoise;
        StormVolumeController owner;
        bool published;
        int nearCells=64;
        static readonly int ModeId=Shader.PropertyToID("_PirateStormBillows");
        static readonly int VolumeId=Shader.PropertyToID("_PirateStormVolume3D");
        static readonly int DensityId=Shader.PropertyToID("_StormVolumeDensity");
        static readonly int NoiseId=Shader.PropertyToID("_PirateStormNearNoise");
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            Shader.SetGlobalFloat(ModeId,0);
            Shader.SetGlobalFloat(VolumeId,0);
            Shader.SetGlobalTexture(DensityId,null);
            Shader.SetGlobalTexture(NoiseId,null);
        }
        static float Hash(uint value)
        {
            value^=value>>16;value*=0x7feb352du;value^=value>>15;value*=0x846ca68bu;value^=value>>16;
            return (value&0x00ffffffu)/16777216f;
        }
        void Awake()
        {
            owner=GetComponent<StormVolumeController>();
            density=Resources.Load<Texture3D>("StormVolumeDensity");
            var data=new Color[32*32*32];
            for(int z=0;z<32;z++)for(int y=0;y<32;y++)for(int x=0;x<32;x++)
            {
                int i=x+32*(y+32*z);
                var n=new Vector3(Hash((uint)(((x+31)%32)+32*(y+32*z))+17471u)-Hash((uint)(((x+1)%32)+32*(y+32*z))+17471u),
                    Hash((uint)(x+32*((y+31)%32+32*z))+17471u)-Hash((uint)(x+32*((y+1)%32+32*z))+17471u),
                    Hash((uint)(x+32*(y+32*((z+31)%32)))+17471u)-Hash((uint)(x+32*(y+32*((z+1)%32)))+17471u)).normalized;
                data[i]=new Color(Hash((uint)i+17471u),n.x*.5f+.5f,n.y*.5f+.5f,n.z*.5f+.5f);
            }
            nearNoise=new Texture3D(32,32,32,TextureFormat.RGBA32,false)
            {
                name="Storm continuous density field",hideFlags=HideFlags.HideAndDontSave,
                filterMode=FilterMode.Trilinear,wrapMode=TextureWrapMode.Repeat
            };
            nearNoise.SetPixels(data);nearNoise.Apply(false,true);
        }
        void LateUpdate()
        {
            bool active=owner!=null && StormVolumeController.Instance==owner && owner.Ready && !owner.IsMenuPreview && density!=null;
            if(!active){Release();return;}
            Shader.SetGlobalFloat(ModeId,1);
            Shader.SetGlobalFloat(VolumeId,1);
            Shader.SetGlobalTexture(DensityId,density);
            Shader.SetGlobalTexture(NoiseId,nearNoise);
            published=true;
        }
        void Release()
        {
            if(!published)return;
            if(StormVolumeController.Instance==owner || StormVolumeController.Instance==null)
            {
                Shader.SetGlobalFloat(ModeId,0);Shader.SetGlobalFloat(VolumeId,0);
                Shader.SetGlobalTexture(DensityId,null);Shader.SetGlobalTexture(NoiseId,null);
            }
            published=false;
        }
        void OnDisable(){Release();}
        void OnDestroy(){Release();if(nearNoise!=null)Destroy(nearNoise);}
    }
}
