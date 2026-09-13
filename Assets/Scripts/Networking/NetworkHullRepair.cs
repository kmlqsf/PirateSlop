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
        Mesh surfaceHighlight;
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
                surfaceHighlight = new Mesh();
                surfaceHighlight.vertices = new[] { new Vector3(-.5f,0,-.5f),new Vector3(.5f,0,-.5f),new Vector3(.5f,0,.5f),new Vector3(-.5f,0,.5f) };
                surfaceHighlight.triangles = new[] { 0,2,1,0,3,2,1,2,0,2,3,0 };
                surfaceHighlight.RecalculateNormals();
            }
            var camera = motor.PlayerCamera;
            var ray = new Ray(camera.transform.position, camera.transform.forward);
            float nearest = 3.5f;
            foreach (var ship in NetworkShip.ActiveShips)
            {
                if (ship == null || ship.IsSinking || Vector3.Distance(ship.transform.position, transform.position) > 70f) continue;
                var destruction = ship.GetComponent<ShipDestruction>();
                if (destruction == null) continue;
                var mastGroups = new System.Collections.Generic.HashSet<string>();
                foreach (var section in destruction.Sections)
                {
                    if (section != null && destruction.Definition(section.SectionId).Type == ShipSectionType.Mast)
                    {
                        string group = destruction.Definition(section.SectionId).SourceGroup;
                        if (!mastGroups.Add(group) || !destruction.MastRepairPoint(section.SectionId, out var basePoint)) continue;
                        Graphics.DrawMesh(surfaceHighlight, Matrix4x4.TRS(basePoint, ship.transform.rotation * Quaternion.Euler(90,0,0), Vector3.one * 1.3f), highlight, 0, camera, 0, null, ShadowCastingMode.Off, false);
                        var bounds = new Bounds(basePoint, Vector3.one * 1.4f);
                        if (bounds.IntersectRay(ray, out float distance) && distance < nearest)
                        {
                            var point = ray.GetPoint(distance);
                            if (Reachable(point)) { nearest = distance; aimed = section; aimedFragment = -1; aimedPoint = point; }
                        }
                        continue;
                    }
                    if (section == null || section.RemovedFragments == 0) continue;
                    for (int i = 0; i < section.RepairCount; i++)
                    {
                        if ((section.RemovedFragments & (1UL << i)) == 0) continue;
                        var fragment = section.RepairTransform(i);
                        var filter = fragment.GetComponent<MeshFilter>();
                        if (filter == null || filter.sharedMesh == null) continue;
                        var mesh = filter.sharedMesh;
                        var bounds = section.RepairBounds(i);
                        Vector3 localEye = fragment.transform.InverseTransformPoint(ray.origin);
                        Vector3 closest = fragment.transform.TransformPoint(bounds.ClosestPoint(localEye));
                        if (Vector3.Distance(ray.origin, closest) > 12f) continue;
                        if (section.Fragments.Length == 0)
                            Graphics.DrawMesh(surfaceHighlight, fragment.localToWorldMatrix * Matrix4x4.TRS(bounds.center + Vector3.up * .015f, Quaternion.identity, bounds.size), highlight, 0, camera, 0, null, ShadowCastingMode.Off, false);
                        else for (int sub = 0; sub < mesh.subMeshCount; sub++)
                            Graphics.DrawMesh(mesh, fragment.transform.localToWorldMatrix, highlight, 0, camera, sub, null, ShadowCastingMode.Off, false);
                        var localRay = new Ray(localEye, fragment.transform.InverseTransformDirection(ray.direction));
                        if (!bounds.IntersectRay(localRay, out float distance)) continue;
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
            if (fragmentId == -1)
            {
                if (!destruction.MastRepairPoint(sectionId, out var basePoint)) return;
                Vector3 hitPoint = target.transform.TransformPoint(localPoint);
                if (Vector3.Distance(hitPoint, basePoint) > 1.3f || !Reachable(hitPoint)) return;
                if (target != lastShip || sectionId != lastSection || Time.time - lastStrikeAt > 3f) strikes = 0;
                lastShip = target; lastSection = sectionId; lastFragment = -1;
                nextStrike = Time.time + .5f; lastStrikeAt = Time.time;
                if (++strikes >= 10) { destruction.RepairMast(sectionId); strikes = 0; }
                StrikeObserversRpc(hitPoint);
                return;
            }
            if (section == null || fragmentId < 0 || fragmentId >= section.RepairCount || fragmentId >= 64 || (section.RemovedFragments & (1UL << fragmentId)) == 0) return;
            Vector3 point = target.transform.TransformPoint(localPoint);
            var fragment = section.RepairTransform(fragmentId).GetComponent<MeshFilter>();
            if (fragment == null || section.RepairBounds(fragmentId).SqrDistance(fragment.transform.InverseTransformPoint(point)) > .025f || !Reachable(point)) return;
            if (target != lastShip || sectionId != lastSection || fragmentId != lastFragment || Time.time - lastStrikeAt > 3f) strikes = 0;
            lastShip = target; lastSection = sectionId; lastFragment = fragmentId;
            nextStrike = Time.time + .5f; lastStrikeAt = Time.time;
            strikes++;
            if (strikes >= 3) { destruction.RepairNearby(sectionId, fragmentId, point); strikes = 0; }
            StrikeObserversRpc(point);
        }
        [ObserversRpc(RunLocally = true)]
        void StrikeObserversRpc(Vector3 point) { swingAt = Time.time; GameAudio.Play(SoundCue.BulletWood, point, .8f); }
        void OnGUI()
        {
            if (IsOwner && Available && motor.InputActive && !PlayerInventory.LootWindowOpen)
                PirateHudStyle.Panel(new Rect(Screen.width * .5f - 240, Screen.height - 155, 480, 32), aimed != null ? aimedFragment == -1 ? "ЛКМ — восстановить мачту целиком (10 ударов)" : "ЛКМ — починить до 3 осколков (3 удара)" : "Наведитесь на подсвеченную повреждённую часть");
        }
        void OnDestroy() { if (model != null) Destroy(model); if (highlight != null) Destroy(highlight); if (surfaceHighlight != null) Destroy(surfaceHighlight); }
    }
}

