using UnityEngine;
using PirateSlop.World;

namespace PirateSlop
{
    [DefaultExecutionOrder(1000)]
    public sealed class BRZoneGeometryDebug : MonoBehaviour
    {
        public BRZoneVisual Visual;
        public Camera Observer;
        public Material DebugMaterial;
        public bool OpaqueWall = true, LockView = true;
        public float CaptureDistance = 250;
        MeshRenderer wall;
        Material original;
        Vector3 center, nearest;
        float visualRadius, bottom, top, maximumRadiusError;
        public string Readout { get; private set; }

        void LateUpdate()
        {
            var zone=StormZone.Instance;
            if(Visual==null || Observer==null || zone==null) return;
            if(wall==null)
            {
                wall=Visual.transform.Find("ZoneWallRenderer").GetComponent<MeshRenderer>();
                original=wall.sharedMaterial;
            }
            wall.sharedMaterial=OpaqueWall?DebugMaterial:original;
            var vertices=wall.GetComponent<MeshFilter>().sharedMesh.vertices;
            center=wall.transform.position;
            var first=wall.transform.TransformPoint(vertices[0]);
            visualRadius=new Vector2(first.x-center.x,first.z-center.z).magnitude;
            maximumRadiusError=0;
            for(int i=0;i<vertices.Length;i+=9)
            {
                var p=wall.transform.TransformPoint(vertices[i]);
                maximumRadiusError=Mathf.Max(maximumRadiusError,Mathf.Abs(new Vector2(p.x-zone.Center.x,p.z-zone.Center.z).magnitude-zone.Radius));
            }
            if(LockView)
            {
                Observer.transform.position=zone.Center+new Vector3(0,9,zone.Radius-CaptureDistance);
                Observer.transform.rotation=Quaternion.identity;
            }
            Vector3 radial=Observer.transform.position-zone.Center; radial.y=0;
            radial=radial.sqrMagnitude>.001f?radial.normalized:Vector3.forward;
            nearest=zone.Center+radial*zone.Radius;
            bottom=OceanSurface.Instance!=null?OceanSurface.Instance.Height(nearest):0;
            top=bottom+Visual.WallHeight;
            nearest.y=Observer.transform.position.y;
            float angle=Vector3.Angle(Observer.transform.forward,(nearest-Observer.transform.position).normalized);
            Readout=$"DistanceToBorder = {zone.DistanceInside(Observer.transform.position):F2} m\nVisualWallRadius = {visualRadius:F2} m\nGameplayRadius = {zone.Radius:F2} m\nWallHeight = {Visual.WallHeight:F2} m\nCamera->NearestWallPoint = {Vector3.Distance(Observer.transform.position,nearest):F2} m\nFacing error = {angle:F2} deg | Preview = {Visual.Preview}\nMax vertex radius error = {maximumRadiusError:F4} m\nWater = {bottom:F2} m | Wall top = {top:F2} m";
        }

        void OnGUI()
        {
            if(string.IsNullOrEmpty(Readout)) return;
            var rect=new Rect(20,120,560,220);
            GUI.Box(rect,GUIContent.none);
            GUI.Label(new Rect(rect.x+12,rect.y+10,rect.width-24,rect.height-20),Readout,new GUIStyle(GUI.skin.label) { fontSize=20,normal={textColor=Color.white} });
        }

        void OnDrawGizmos()
        {
            var zone=StormZone.Instance;
            if(zone==null || Observer==null || Visual==null) return;
            Gizmos.color=Color.yellow; Gizmos.DrawSphere(zone.Center,8);
            Ring(zone.Center,zone.Radius,0,Color.yellow);
            Ring(zone.Center,zone.Radius,Visual.WallHeight,Color.magenta);
            Ring(zone.TargetCenter,zone.TargetRadius,0,Color.cyan);
            Gizmos.color=Color.green; Gizmos.DrawLine(Observer.transform.position,nearest); Gizmos.DrawSphere(nearest,4);
            Vector3 b=new Vector3(nearest.x,bottom,nearest.z),t=new Vector3(nearest.x,top,nearest.z);
            Gizmos.color=Color.white; Gizmos.DrawLine(b,t); Gizmos.DrawLine(b-Vector3.right*12,b+Vector3.right*12); Gizmos.DrawLine(t-Vector3.right*12,t+Vector3.right*12);
            Gizmos.matrix=Observer.transform.localToWorldMatrix; Gizmos.color=Color.green;
            Gizmos.DrawFrustum(Vector3.zero,Observer.fieldOfView,CaptureDistance+40,Observer.nearClipPlane,Observer.aspect); Gizmos.matrix=Matrix4x4.identity;
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(zone.Center+Vector3.up*20,$"GAMEPLAY / VISUAL CENTER\n{zone.Center}\nR = {zone.Radius:F1} m");
            UnityEditor.Handles.Label(t,$"TOP: {top:F1} m\nHEIGHT: {Visual.WallHeight:F1} m");
            UnityEditor.Handles.Label(b,$"BOTTOM / WATER: {bottom:F1} m");
            UnityEditor.Handles.Label(Observer.transform.position,$"CAMERA: {CaptureDistance:F0} m to nearest wall");
            foreach(var player in FindObjectsByType<Networking.NetworkPlayer>(FindObjectsSortMode.None))
                if(player.IsOwner) { Gizmos.color=Color.blue; Gizmos.DrawSphere(player.transform.position,6); UnityEditor.Handles.Label(player.transform.position+Vector3.up*15,"ACTUAL PLAYER"); }
            #endif
        }

        static void Ring(Vector3 center,float radius,float height,Color color)
        {
            Gizmos.color=color;
            for(int i=0;i<256;i++) { float a=i*Mathf.PI/128,b=(i+1)*Mathf.PI/128; Gizmos.DrawLine(center+new Vector3(Mathf.Cos(a)*radius,height,Mathf.Sin(a)*radius),center+new Vector3(Mathf.Cos(b)*radius,height,Mathf.Sin(b)*radius)); }
        }
        void OnDisable() { if(wall!=null && original!=null) wall.sharedMaterial=original; }
    }
}
