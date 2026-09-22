using UnityEngine;
using PirateSlop.World;
namespace PirateSlop
{
    [DefaultExecutionOrder(500)]
    public sealed class StormCrownDebug : MonoBehaviour
    {
        public Camera Observer;
        public float Distance=250,Yaw;
        public bool FromCenter;
        void LateUpdate()
        {
            var zone=StormZone.Instance;if(zone==null||Observer==null)return;
            Observer.transform.position=zone.Center+new Vector3(0,9,FromCenter?0:zone.Radius-Distance);
            Observer.transform.rotation=Quaternion.Euler(0,Yaw,0);
        }
        void OnGUI()
        {
            var z=StormZone.Instance;var c=Object.FindFirstObjectByType<StormCrown>();if(z==null||c==null)return;
            GUI.Box(new Rect(20,120,460,125),"");GUI.Label(new Rect(32,130,440,110),$"Gameplay radius: {z.Radius:F1} m\nDistance to border: {(FromCenter?z.Radius:Distance):F1} m\nCrown height: {c.Height:F0} m | top inset: {c.Inset(1,z.Radius):F0} m\nView yaw: {Yaw:F0} deg | particles: {c.ActiveParticles}",new GUIStyle(GUI.skin.label){fontSize=19});
        }
    }
}
