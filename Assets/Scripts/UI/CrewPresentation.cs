using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using PirateSlop.Networking;

namespace PirateSlop
{
    [DefaultExecutionOrder(1000)]
    public sealed class CrewPresentation : MonoBehaviour
    {
        const float Range = 50f;
        readonly Dictionary<NetworkPlayer, Plate> plates = new();
        readonly List<NetworkPlayer> expired = new();
        readonly RaycastHit[] hits = new RaycastHit[64];
        NetworkPlayer owner;
        Canvas canvas;
        RectTransform canvasRect;
        GameObject root;
        Font font;
        float sampleAt, cleanupAt, respawnAt = -10f;
        bool wasDead;
        int waterLayer;
        sealed class Plate
        {
            public RectTransform Root;
            public CanvasGroup Group;
            public Text Name;
            public Transform Graphics;
            public Transform HeadBone;
            public CharacterController Controller;
            public Image[] Voice;
            public string LastName;
            public bool Visible;
            public float Width;
        }
        void Awake()
        {
            owner=GetComponent<NetworkPlayer>();
            waterLayer=LayerMask.NameToLayer("Water");
        }
        void LateUpdate()
        {
            if(owner==null || !owner.IsOwner || !owner.IsClientInitialized || owner.Motor==null)
            {
                if(canvas!=null) canvas.enabled=false;
                return;
            }
            bool dead=owner.Motor.IsDead;
            if(wasDead && !dead) respawnAt=Time.unscaledTime;
            wasDead=dead;
            var camera=owner.Motor.PlayerCamera;
            bool show=!dead && camera!=null && camera.isActiveAndEnabled && !SessionController.MenuOpen && !RoguelikeUpgradeUI.WindowOpen && !PlayerInventory.LootWindowOpen && !DeveloperMenu.IsOpen;
            if(!show) { if(canvas!=null) canvas.enabled=false; return; }
            if(canvas==null) BuildCanvas();
            canvas.enabled=true;
            float now=Time.unscaledTime;
            bool sample=now>=sampleAt;
            if(sample) sampleAt=now+.1f;
            if(now>=cleanupAt)
            {
                cleanupAt=now+.5f; expired.Clear();
                foreach(var pair in plates)
                    if(pair.Key==null || !pair.Key.IsClientInitialized || !pair.Key.isActiveAndEnabled) expired.Add(pair.Key);
                foreach(var member in expired) { Destroy(plates[member].Root.gameObject); plates.Remove(member); }
            }
            foreach(var member in NetworkPlayer.Active)
            {
                if(member==null || !member.IsClientInitialized || member.Motor==null || member.Motor.IsDead || member.Eliminated.Value || member.IsLoadTestParticipant || member==owner && !owner.Motor.IsThirdPerson)
                {
                    if(member!=null && plates.TryGetValue(member,out var hidden)) { hidden.Visible=false; hidden.Group.alpha=0; }
                    continue;
                }
                if(!plates.TryGetValue(member,out var plate))
                {
                    if((member.transform.position-camera.transform.position).sqrMagnitude>Range*Range) continue;
                    plate=BuildPlate(member); plates.Add(member,plate);
                }
                Vector3 head=HeadPosition(member,plate);
                Vector3 anchor=head+Vector3.up*.32f;
                Vector3 screen=camera.WorldToScreenPoint(anchor);
                float distance=Vector3.Distance(camera.transform.position,head);
                bool inFrame=screen.z>camera.nearClipPlane && screen.x>16 && screen.x<Screen.width-16 && screen.y>16 && screen.y<Screen.height-16 && distance<Range;
                if(!inFrame) { plate.Visible=false; plate.Group.alpha=0; continue; }
                if(sample) plate.Visible=Unobstructed(camera.transform.position,head,member);
                float fade=1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(35f,Range,distance));
                float target=plate.Visible?fade*.88f:0f;
                plate.Group.alpha=Mathf.MoveTowards(plate.Group.alpha,target,Time.unscaledDeltaTime*(target>plate.Group.alpha?6f:12f));
                if(plate.Group.alpha<=0f) continue;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect,screen,null,out var position);
                float scale=Mathf.Lerp(1f,.84f,Mathf.InverseLerp(8,Range,distance));
                plate.Root.localScale=Vector3.one*scale;
                float halfWidth=plate.Width*.5f*scale;
                if(position.x-halfWidth<canvasRect.rect.xMin+8 || position.x+halfWidth>canvasRect.rect.xMax-8 || position.y>canvasRect.rect.yMax-20)
                {
                    plate.Group.alpha=0;
                    continue;
                }
                plate.Root.anchoredPosition=position;
                string name=member.DisplayName;
                if(plate.LastName!=name) FitName(plate,name);
                bool ally=member.TeamId.Value==owner.TeamId.Value;
                Color accent=ally?new Color(.72f,.83f,.77f):new Color(.88f,.87f,.82f);
                plate.Name.color=accent;
                bool talking=member.Voice!=null && member.Voice.SpeechDetected;
                for(int i=0;i<plate.Voice.Length;i++)
                {
                    var bar=plate.Voice[i];
                    bar.gameObject.SetActive(talking);
                    if(talking)
                    {
                        bar.color=accent;
                        var rect=(RectTransform)bar.transform;
                        rect.sizeDelta=new Vector2(2,4+Mathf.Abs(Mathf.Sin(now*10+i*1.4f))*8);
                    }
                }
            }
        }
        Vector3 HeadPosition(NetworkPlayer member,Plate plate)
        {
            if(plate.HeadBone!=null) return plate.HeadBone.position+Vector3.up*.07f;
            var cc=plate.Controller;
            var source=plate.Graphics!=null?plate.Graphics.transform:member.transform;
            return cc!=null?source.TransformPoint(cc.center)+Vector3.up*cc.height*.4f:source.position+Vector3.up*1.65f;
        }
        bool Unobstructed(Vector3 origin,Vector3 head,NetworkPlayer member)
        {
            Vector3 delta=head-origin;
            float distance=delta.magnitude;
            if(distance<.01f) return true;
            int count=Physics.RaycastNonAlloc(origin,delta/distance,hits,distance,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Collide);
            if(count==hits.Length) return false;
            for(int i=0;i<count;i++)
            {
                var hit=hits[i];
                if(hit.collider==null || hit.collider.gameObject.layer==waterLayer || !PlayerHitbox.IsTarget(hit.collider)) continue;
                if(hit.transform.IsChildOf(owner.transform) || hit.transform.IsChildOf(member.transform)) continue;
                if(hit.distance<distance-.08f) return false;
            }
            return true;
        }
        void BuildCanvas()
        {
            font=Font.CreateDynamicFontFromOSFont(new[]{"Segoe UI","Arial","Segoe UI Symbol","Microsoft YaHei","Yu Gothic"},22);
            root=new GameObject("PlayerNameplates",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,gameObject.scene);
            canvas=root.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.sortingOrder=12;
            canvasRect=root.GetComponent<RectTransform>();
            var scaler=root.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920,1080); scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; scaler.matchWidthOrHeight=.5f;
            Canvas.ForceUpdateCanvases();
        }
        Plate BuildPlate(NetworkPlayer member)
        {
            var p=new Plate();
            p.Root=Rect("Nameplate_"+member.ParticipantId.Value,canvasRect,new Vector2(160,24));
            p.Group=p.Root.gameObject.AddComponent<CanvasGroup>(); p.Group.alpha=0; p.Group.interactable=false; p.Group.blocksRaycasts=false;
            var text=Rect("SteamName",p.Root,new Vector2(160,24));
            p.Name=text.gameObject.AddComponent<Text>(); p.Name.font=font; p.Name.fontSize=16; p.Name.fontStyle=FontStyle.Normal; p.Name.alignment=TextAnchor.MiddleCenter;
            p.Name.color=new Color(.88f,.87f,.82f); p.Name.supportRichText=false; p.Name.raycastTarget=false;
            p.Name.horizontalOverflow=HorizontalWrapMode.Overflow; p.Name.verticalOverflow=VerticalWrapMode.Overflow;
            var shadow=text.gameObject.AddComponent<Shadow>(); shadow.effectColor=new Color(.015f,.022f,.024f,.8f); shadow.effectDistance=new Vector2(.65f,-1f); shadow.useGraphicAlpha=true;
            var outline=text.gameObject.AddComponent<Outline>(); outline.effectColor=new Color(.015f,.022f,.024f,.42f); outline.effectDistance=new Vector2(.4f,-.4f); outline.useGraphicAlpha=true;
            p.Voice=new Image[3];
            for(int i=0;i<3;i++)
            {
                p.Voice[i]=Rect("Voice"+i,p.Root,new Vector2(2,6)).gameObject.AddComponent<Image>();
                p.Voice[i].raycastTarget=false;
                p.Voice[i].rectTransform.anchoredPosition=new Vector2((i-1)*4,15);
                p.Voice[i].gameObject.SetActive(false);
            }
            p.Controller=member.GetComponent<CharacterController>();
            var graphics=member.transform.Find("PlayerGraphics");
            p.Graphics=graphics;
            foreach(var animator in member.GetComponentsInChildren<Animator>(true))
                if(animator.isHuman && animator.avatar!=null && animator.avatar.isValid) { p.HeadBone=animator.GetBoneTransform(HumanBodyBones.Head); if(p.HeadBone!=null) break; }
            if(p.HeadBone==null && graphics!=null)
                foreach(var bone in graphics.GetComponentsInChildren<Transform>(true))
                    if(bone.name=="mixamorig:Head" || bone.name=="mixamorig_Head" || bone.name=="Head" || bone.name=="Bip001 Head") { p.HeadBone=bone; break; }
            FitName(p,member.DisplayName);
            return p;
        }
        void FitName(Plate plate,string name)
        {
            plate.LastName=name;
            plate.Name.text=name;
            int[] elements=StringInfo.ParseCombiningCharacters(name);
            int end=elements.Length;
            while(plate.Name.preferredWidth>232 && end>1)
            {
                end--;
                plate.Name.text=name.Substring(0,elements[end])+"…";
            }
            plate.Width=Mathf.Clamp(plate.Name.preferredWidth+8,40,240);
            plate.Root.sizeDelta=new Vector2(plate.Width,24);
            plate.Name.rectTransform.sizeDelta=plate.Root.sizeDelta;
            plate.Name.rectTransform.anchoredPosition=Vector2.zero;
        }
        RectTransform Rect(string name,Transform parent,Vector2 size)
        {
            var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent,false);
            rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f); rect.sizeDelta=size; return rect;
        }
        void OnGUI()
        {
            if(owner==null || !owner.IsOwner || SessionController.MenuOpen) return;
            float elapsed=Time.unscaledTime-respawnAt;
            if(elapsed<.65f) PirateHudStyle.Fill(new Rect(0,0,Screen.width,Screen.height),new Color(.04f,.08f,.1f,.7f*(1f-elapsed/.65f)));
        }
        void OnDisable() { if(canvas!=null) canvas.enabled=false; }
        void OnDestroy()
        {
            if(root!=null) Destroy(root);
            if(font!=null) Destroy(font);
        }
    }
}

