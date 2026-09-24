using UnityEngine;

namespace PirateSlop
{
    public sealed class MenuBackdrop : MonoBehaviour
    {
        public Camera View;
        public Transform Ship;
        public Material Water;
        public GameObject Content;
        public Vector3 FocusPoint = new(0, 14, 0);
        public float FramingRadius = 28f;
        Vector3 shipRest;
        Quaternion shipRotation;
        Material runtimeWater;
        bool wasVisible;
        void Awake()
        {
            shipRest = Ship.localPosition;
            shipRotation = Ship.localRotation;
            if (Water != null)
            {
                runtimeWater = Instantiate(Water);
                foreach (var renderer in Content.GetComponentsInChildren<Renderer>(true))
                    if (renderer.sharedMaterial == Water) renderer.sharedMaterial = runtimeWater;
            }
            PositionView(0f);
        }
        public void PositionView(float time)
        {
            if (View == null) return;
            float tangent = Mathf.Tan(View.fieldOfView * Mathf.Deg2Rad * .5f);
            float distance = Mathf.Max(FramingRadius / (tangent * .8f), FramingRadius / (tangent * Mathf.Max(.6f, View.aspect) * .48f));
            Vector3 focus = transform.TransformPoint(FocusPoint);
            Vector3 offset = new Vector3(.8f, .18f, -1f).normalized * distance;
            View.transform.position = focus + offset + new Vector3(Mathf.Sin(time * .08f) * 1.2f, Mathf.Sin(time * .12f) * .35f, 0);
            Vector3 right = Vector3.Cross(Vector3.up, -offset.normalized).normalized;
            View.transform.LookAt(focus - right * (distance * tangent * View.aspect * .34f));
        }
        void Update()
        {
            bool visible=View!=null && View.gameObject.activeInHierarchy;
            Content.SetActive(visible);
            if(visible!=wasVisible)
            {
                wasVisible=visible;
                foreach(var light in FindObjectsByType<Light>(FindObjectsInactive.Exclude))
                    if(light.type==LightType.Directional && light.enabled && ((light.cullingMask & (1<<30))!=0)==visible) { RenderSettings.sun=light; break; }
            }
            if(!visible) return;
            float t=Time.unscaledTime;
            Ship.localPosition=shipRest+Vector3.up*Mathf.Sin(t*.55f)*.25f;
            Ship.localRotation=shipRotation * Quaternion.Euler(Mathf.Sin(t*.4f)*.6f,0f,Mathf.Sin(t*.65f)*.9f);
            PositionView(t);
            if(runtimeWater!=null)
            {
                runtimeWater.SetFloat("_UseWaveTime",1f);
                runtimeWater.SetFloat("_WaveTime",t);
            }
            GameAudio.Ambience(.15f);
        }
        void OnDestroy() { if (runtimeWater != null) Destroy(runtimeWater); }
    }
}
