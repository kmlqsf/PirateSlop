using FishNet.Object;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace PirateSlop.Networking
{
    public sealed class NetworkHullRepair : NetworkBehaviour
    {
        public GameObject MalletModel;
        PlayerInventory inventory;
        AdvancedPlayerController motor;
        NetworkWeapon weapon;
        GameObject model;
        Material highlight;
        ShipDamageSection aimed;
        int aimedFragment = -1, lastSection, lastFragment, strikes;
        NetworkObject lastShip;
        Vector3 aimedPoint;
        float nextStrike, swingAt = -10f, lastStrikeAt;
        bool Available => IsSpawned && inventory.MalletSelected && !inventory.HandsOccupied && !motor.IsDead && !motor.IsClimbing && !motor.LocomotionLocked && !GetComponent<CannonHands>().HasHeldBall && !weapon.LootHandsBusy && !inventory.Fishing.IsFishing && !inventory.Fishing.IsEating;
        void Awake()
        {
            inventory = GetComponent<PlayerInventory>();
            motor = GetComponent<AdvancedPlayerController>();
            weapon = GetComponent<NetworkWeapon>();
        }
        void Update()
        {
            if (!IsOwner || !Available || !motor.InputActive || PlayerInventory.LootWindowOpen) return;
            if (aimed != null && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                RepairServerRpc(aimed.Owner.NetworkObject, aimed.SectionId, aimedFragment, aimed.Owner.transform.InverseTransformPoint(aimedPoint));
        }
        void LateUpdate()
        {
            if (!IsClientInitialized) return;
            if (model == null && MalletModel != null)
            {
                model = Instantiate(MalletModel, transform);
                foreach (var collider in model.GetComponentsInChildren<Collider>()) Destroy(collider);
            }
            if (model != null)
            {
                model.SetActive(Available);
                bool first = IsOwner && !motor.IsThirdPerson;
                var anchor = first ? motor.PlayerCamera.transform : transform;
                float swing = Mathf.Sin(Mathf.Clamp01((Time.time - swingAt) / .35f) * Mathf.PI);
                model.transform.SetPositionAndRotation(anchor.TransformPoint(first ? new Vector3(.32f, -.42f, .5f) : new Vector3(.35f, 1.05f, .4f)), anchor.rotation * Quaternion.Euler(-15f + swing * 75f, 0, -15f));
            }
            aimed = null; aimedFragment = -1;
            if (!IsOwner || !Available || !motor.InputActive || PlayerInventory.LootWindowOpen) return;
            if (highlight == null)
            {
                highlight = new Material(inventory.PreviewMaterial);
                highlight.color = new Color(.2f, 1f, .55f, .4f);
                if (highlight.HasProperty("_BaseColor")) highlight.SetColor("_BaseColor", highlight.color);
            }
            var camera = motor.PlayerCamera;
            var ray = new Ray(camera.transform.position, camera.transform.forward);
            float nearest = 3.5f;
            foreach (var ship in NetworkShip.ActiveShips)
            {
                if (ship == null || ship.IsSinking || Vector3.Distance(ship.transform.position, transform.position) > 70f) continue;
                var destruction = ship.GetComponent<ShipDestruction>();
                if (destruction == null) continue;
                foreach (var section in destruction.Sections)
                {
                    if (section == null || section.RemovedFragments == 0 || destruction.Definition(section.SectionId).Type != ShipSectionType.Hull) continue;
                    for (int i = 0; i < section.Fragments.Length; i++)
                    {
                        if ((section.RemovedFragments & (1UL << i)) == 0) continue;
                        var fragment = section.Fragments[i];
                        var filter = fragment.GetComponent<MeshFilter>();
                        if (filter == null || filter.sharedMesh == null) continue;
                        var mesh = filter.sharedMesh;
                        Vector3 localEye = fragment.transform.InverseTransformPoint(ray.origin);
                        Vector3 closest = fragment.transform.TransformPoint(mesh.bounds.ClosestPoint(localEye));
                        if (Vector3.Distance(ray.origin, closest) > 12f) continue;
                        for (int sub = 0; sub < mesh.subMeshCount; sub++)
                            Graphics.DrawMesh(mesh, fragment.transform.localToWorldMatrix, highlight, 0, camera, sub, null, ShadowCastingMode.Off, false);
                        var localRay = new Ray(localEye, fragment.transform.InverseTransformDirection(ray.direction));
                        if (!mesh.bounds.IntersectRay(localRay, out float distance)) continue;
                        Vector3 point = fragment.transform.TransformPoint(localRay.GetPoint(distance));
                        float worldDistance = Vector3.Distance(ray.origin, point);
                        if (worldDistance >= nearest || !Reachable(point)) continue;
                        nearest = worldDistance; aimed = section; aimedFragment = i; aimedPoint = point;
                    }
                }
            }
        }
        bool Reachable(Vector3 point)
        {
            Vector3 origin = transform.position + Vector3.up * (motor.IsCrouched ? .8f : 1.5f);
            Vector3 delta = point - origin;
            return delta.magnitude <= 3.5f && !FirearmTrace.Cast(gameObject, origin, point - delta.normalized * .08f, out _);
        }
        [ServerRpc]
        void RepairServerRpc(NetworkObject target, int sectionId, int fragmentId, Vector3 localPoint)
        {
            if (!Available || target == null || Time.time < nextStrike || !float.IsFinite(localPoint.sqrMagnitude)) return;
            var destruction = target.GetComponent<ShipDestruction>();
            if (destruction == null || !destruction.IsSpawned || target.GetComponent<NetworkShip>().IsSinking) return;
            var section = destruction.Section(sectionId);
            if (section == null || destruction.Definition(sectionId).Type != ShipSectionType.Hull || fragmentId < 0 || fragmentId >= section.Fragments.Length || fragmentId >= 64 || (section.RemovedFragments & (1UL << fragmentId)) == 0) return;
            Vector3 point = target.transform.TransformPoint(localPoint);
            var fragment = section.Fragments[fragmentId].GetComponent<MeshFilter>();
            if (fragment == null || fragment.sharedMesh.bounds.SqrDistance(fragment.transform.InverseTransformPoint(point)) > .025f || !Reachable(point)) return;
            if (target != lastShip || sectionId != lastSection || fragmentId != lastFragment || Time.time - lastStrikeAt > 3f) strikes = 0;
            lastShip = target; lastSection = sectionId; lastFragment = fragmentId;
            nextStrike = Time.time + .5f; lastStrikeAt = Time.time;
            strikes++;
            if (strikes >= 3) { destruction.RepairFragment(sectionId, fragmentId); strikes = 0; }
            StrikeObserversRpc(point);
        }
        [ObserversRpc(RunLocally = true)]
        void StrikeObserversRpc(Vector3 point) { swingAt = Time.time; GameAudio.Play(SoundCue.BulletWood, point, .8f); }
        void OnGUI()
        {
            if (IsOwner && Available && motor.InputActive && !PlayerInventory.LootWindowOpen)
                PirateHudStyle.Panel(new Rect(Screen.width * .5f - 240, Screen.height - 155, 480, 32), aimed != null ? "ЛКМ — заделать пробоину (3 удара киянкой)" : "Наведитесь на подсвеченную пробоину корпуса");
        }
        void OnDestroy() { if (model != null) Destroy(model); if (highlight != null) Destroy(highlight); }
    }
}
