using UnityEngine;

namespace PirateSlop
{
    public sealed class FirearmPrediction
    {
        PistolBullet[] tracers;
        int[] generations;
        public void Fire(GameObject shooter,FirearmDefinition definition,Vector3 eye,Vector3 muzzle,Vector3 forward,bool aimed,int seed,Material material)
        {
            var shots=FirearmCombat.Resolve(shooter,definition,eye,muzzle,forward,aimed,seed,false);
            tracers=new PistolBullet[shots.Length];generations=new int[shots.Length];
            for(int i=0;i<shots.Length;i++)
            {
                tracers[i]=PistolBullet.Spawn(shots[i].Start,shots[i],material,definition.TracerWidth,shots.Length==1 || i%4==0,definition.TracerSpeed,false);
                if(tracers[i]!=null) generations[i]=tracers[i].Generation;
            }
        }
        public void Confirm(FirearmShot[] shots)
        {
            for(int i=0;i<shots.Length;i++)
            {
                if(tracers!=null && i<tracers.Length && tracers[i]!=null && tracers[i].Generation==generations[i]) tracers[i].Confirm(shots[i]);
                else FirearmImpact.Present(shots[i],shots.Length==1 || i%4==0);
            }
            tracers=null;generations=null;
        }
    }
}
