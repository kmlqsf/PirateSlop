using UnityEngine;

namespace PirateSlop
{
    public sealed class MenuBackdrop : MonoBehaviour
    {
        public Camera View;
        public Transform Ship;
        public Material Water;
        public GameObject Content;
        Vector3 shipRest, cameraRest;
        bool wasVisible;
        void Awake() { shipRest=Ship.localPosition; cameraRest=View.transform.localPosition; }
        void Update()
        {
            bool visible=View!=null && View.gameObject.activeInHierarchy;
            Content.SetActive(visible);
            if(visible!=wasVisible)
            {
                wasVisible=visible;
                foreach(var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if(light.type==LightType.Directional && light.enabled && ((light.cullingMask & (1<<30))!=0)==visible) { RenderSettings.sun=light; break; }
            }
            if(!visible) return;
            float t=Time.unscaledTime;
            Ship.localPosition=shipRest+Vector3.up*Mathf.Sin(t*.55f)*.25f;
            Ship.localRotation=Quaternion.Euler(Mathf.Sin(t*.4f)*.6f,-18f,Mathf.Sin(t*.65f)*.9f);
            View.transform.localPosition=cameraRest+new Vector3(Mathf.Sin(t*.08f)*2f,Mathf.Sin(t*.12f)*.5f,0);
            View.transform.LookAt(transform.TransformPoint(new Vector3(0,14,0)));
            if(Water!=null)
            {
                Water.SetVectorArray("_Waves",new[]{new Vector4(.94f,.342f,.65f,42f),new Vector4(-.4f,.9165f,.32f,23f),new Vector4(.6f,-.8f,.16f,11f),new Vector4(-.8f,-.6f,.07f,5f)});
                Water.SetFloat("_WaveTime",t); Water.SetFloat("_WaveScale",1f);
            }
            GameAudio.Ambience(.15f);
        }
    }
}
