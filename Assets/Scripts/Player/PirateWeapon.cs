using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using PirateSlop.Networking;

namespace PirateSlop
{
    public interface IWeaponTarget { void ReceiveWeaponHit(float damage, GameObject attacker); }

    [DefaultExecutionOrder(30)]
    public sealed class PirateWeapon : MonoBehaviour
    {
        public Transform WorldPivot, ViewPivot, WorldMuzzle, ViewMuzzle;
        public Material EffectMaterial;
        AdvancedPlayerController motor;
        CannonHands hands;
        NetworkWeapon network;
        bool loaded = true, reloading;
        float reloadUntil, nextAttack, recoil, stab, lift;
        Quaternion worldRest, viewRest;
        Vector3 worldPosition, viewPosition;
        Renderer[] viewRenderers;
        Renderer[] worldRenderers;
        public bool Loaded => loaded;
        public bool Reloading => reloading;
        bool Networked => network != null && (network.IsClientInitialized || network.IsServerInitialized);
        void Awake()
        {
            motor = GetComponent<AdvancedPlayerController>(); hands = GetComponent<CannonHands>(); network = GetComponent<NetworkWeapon>();
            worldRest = WorldPivot.localRotation; viewRest = ViewPivot.localRotation;
            worldPosition = WorldPivot.localPosition; viewPosition = ViewPivot.localPosition;
            viewRenderers = ViewPivot.GetComponentsInChildren<Renderer>(true);
            worldRenderers = WorldPivot.GetComponentsInChildren<Renderer>(true);
        }
        void OnEnable() { RenderPipelineManager.beginCameraRendering += BeforeCamera; RenderPipelineManager.endCameraRendering += AfterCamera; }
        void OnDisable() { RenderPipelineManager.beginCameraRendering -= BeforeCamera; RenderPipelineManager.endCameraRendering -= AfterCamera; }
        void BeforeCamera(ScriptableRenderContext context, Camera camera)
        {
            if (viewRenderers == null) return;
            bool show = camera == motor.PlayerCamera && motor.InputActive && !motor.IsThirdPerson && !motor.LocomotionLocked && (hands == null || !hands.HasHeldBall);
            foreach(var r in viewRenderers) if(r != null) r.forceRenderingOff = !show;
            bool hideWorld = camera == motor.PlayerCamera && !motor.IsThirdPerson;
            foreach(var r in worldRenderers) if(r != null) r.forceRenderingOff = hideWorld;
        }
        void AfterCamera(ScriptableRenderContext context, Camera camera)
        {
            if(viewRenderers != null) foreach(var r in viewRenderers) if(r != null) r.forceRenderingOff = true;
            if(worldRenderers != null) foreach(var r in worldRenderers) if(r != null) r.forceRenderingOff = false;
        }
        void Update()
        {
            if (!Networked) TickAuthority();
            if (!motor.InputActive || motor.LocomotionLocked || (hands != null && hands.HasHeldBall)) return;
            var mouse = Mouse.current; var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame) Request(1);
            if (mouse == null) return;
            if (mouse.rightButton.wasPressedThisFrame) Request(2);
            else if (mouse.leftButton.wasPressedThisFrame && (hands == null || !hands.CanPickUpBall())) Request(0);
        }
        void Request(byte action)
        {
            Vector3 direction = motor.PlayerCamera.transform.forward;
            Vector3 eyeOffset = motor.PlayerCamera.transform.position - transform.position;
            if (Networked) { if (network.IsOwner) network.Request(action, direction, eyeOffset); }
            else Act(action, direction, eyeOffset);
        }
        public void TickAuthority()
        {
            if (reloading && (motor.LocomotionLocked || (hands != null && hands.HasHeldBall))) reloading = false;
            if (reloading && Time.time >= reloadUntil) { reloading = false; loaded = true; }
        }
        public bool Act(byte action, Vector3 direction, Vector3 eyeOffset)
        {
            if (motor.LocomotionLocked || (hands != null && hands.HasHeldBall) || !float.IsFinite(direction.sqrMagnitude) || direction.sqrMagnitude < .5f) return false;
            if (action == 1)
            {
                if (loaded || reloading || Time.time < nextAttack) return false;
                reloading = true; reloadUntil = Time.time + 3f; return true;
            }
            if (action > 2 || Time.time < nextAttack || (action == 0 && (!loaded || reloading))) return false;
            if (action == 0) { loaded = false; nextAttack = Time.time + .25f; }
            else { reloading = false; nextAttack = Time.time + .65f; }
            direction.Normalize();
            Vector3 origin = transform.position + Vector3.up * (motor.IsCrouched ? .75f : 1.65f);
            if (float.IsFinite(eyeOffset.sqrMagnitude) && eyeOffset.sqrMagnitude <= 16f)
                origin = transform.position + eyeOffset;
            if (action == 0)
            {
                // Use the first-person muzzle offset with the server-accepted aim, not
                // the remote camera's unsynchronised world orientation.
                Vector3 muzzleOffset = motor.PlayerCamera.transform.InverseTransformPoint(ViewMuzzle.position);
                Vector3 start = origin + Quaternion.LookRotation(direction) * muzzleOffset;
                foreach(var hit in Physics.RaycastAll(origin,(start-origin).normalized,(start-origin).magnitude,~0,QueryTriggerInteraction.Ignore))
                    if(!hit.transform.IsChildOf(transform) && (hit.point-origin).sqrMagnitude < (start-origin).sqrMagnitude) start = hit.point;
                float aimDistance = 100f;
                foreach(var hit in Physics.RaycastAll(origin,direction,aimDistance,~0,QueryTriggerInteraction.Ignore))
                    if(!hit.transform.IsChildOf(transform)) aimDistance = Mathf.Min(aimDistance,hit.distance);
                // With no target, zero at normal pistol range instead of lobbing toward the sky.
                if (aimDistance >= 100f) aimDistance = 20f;
                Vector3 target = origin + direction * aimDistance;
                float flightTime = Mathf.Max(.01f,Vector3.Distance(start,target) / 45f);
                Vector3 velocity = (target-start) / flightTime + Vector3.up * (3f * flightTime);
                SpawnBullet(start,velocity,true);
                if (Networked && network.IsServerInitialized) network.PublishShot(start,velocity);
                return true;
            }
            float range = action == 0 ? 100f : 1.7f;
            Vector3 end = origin + direction * range; RaycastHit nearest = default; float closest = range;
            foreach(var hit in Physics.RaycastAll(origin, direction, range, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform) && hit.distance < closest) { nearest = hit; closest = hit.distance; end = hit.point; }
            if (nearest.collider != null)
            {
                float damage = action == 2 ? 25f : Mathf.Lerp(45f,25f,Mathf.InverseLerp(15f,35f,closest));
                foreach(var component in nearest.collider.GetComponentsInParent<MonoBehaviour>())
                    if(component is IWeaponTarget target) { target.ReceiveWeaponHit(damage,gameObject); break; }
            }
            if (Networked && network.IsServerInitialized) network.PublishAttack(action,end);
            else ShowAttack(action,end);
            return true;
        }
        public void SetState(bool hasRound, bool isReloading) { loaded = hasRound; reloading = isReloading; }
        public void ShowAttack(byte action, Vector3 end)
        {
            if(action == 2) stab = 1;
        }
        public void SpawnBullet(Vector3 origin, Vector3 velocity, bool authoritative)
        {
            recoil = 1;
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere); go.name = "PistolBullet";
            go.GetComponent<Collider>().enabled = false; Destroy(go.GetComponent<Collider>());
            go.transform.localScale = Vector3.one * .035f; go.transform.position = origin;
            var renderer = go.GetComponent<Renderer>(); renderer.sharedMaterial = EffectMaterial;
            var color = new MaterialPropertyBlock(); color.SetColor("_BaseColor",new Color(.16f,.14f,.11f)); renderer.SetPropertyBlock(color);
            Vector3 visibleStart = motor.InputActive && !motor.IsThirdPerson ? ViewMuzzle.position : WorldMuzzle.position;
            go.AddComponent<PistolBullet>().Initialize(gameObject,origin,velocity,visibleStart-origin,authoritative);
        }
        void LateUpdate()
        {
            lift = Mathf.MoveTowards(lift,reloading ? 1 : 0,Time.deltaTime * 5);
            recoil = Mathf.MoveTowards(recoil,0,Time.deltaTime * 7); stab = Mathf.MoveTowards(stab,0,Time.deltaTime * 4);
            var tilt = Quaternion.Euler(-65 * lift - 12 * recoil,0,0);
            WorldPivot.localRotation = worldRest * tilt; ViewPivot.localRotation = viewRest * tilt;
            WorldPivot.localPosition = worldPosition + worldRest * Vector3.forward * (.13f * Mathf.Sin(stab * Mathf.PI));
            ViewPivot.localPosition = viewPosition + Vector3.forward * (.16f * Mathf.Sin(stab * Mathf.PI) - .035f * recoil);
        }
        void OnGUI()
        {
            if (!motor.InputActive || motor.LocomotionLocked || (hands != null && hands.HasHeldBall)) return;
            GUI.Label(new Rect(Screen.width-290,Screen.height-65,280,55),reloading ? "Перезарядка…" : (loaded ? "Пистолет: 1 / ∞" : "Пистолет: 0 / ∞ — R") + "\nЛКМ — выстрел · ПКМ — нож");
        }
    }
}
