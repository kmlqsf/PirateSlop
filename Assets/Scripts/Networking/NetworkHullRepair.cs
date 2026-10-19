using FishNet.Object;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace PirateSlop.Networking
{
    public sealed class NetworkHullRepair : NetworkBehaviour
    {
        public const float StrikeInterval = .5f;
        public const int FragmentStrikes = 3, MastStrikes = 10;
        public const float PlankRepairSeconds = .8f;
        float UpgradeRepair => GetComponent<NetworkPlayer>().RepairUpgradeMultiplier;
        public GameObject MalletModel;
        public GameObject PlankModel;
        PlayerInventory inventory;
        AdvancedPlayerController motor;
        NetworkWeapon weapon;
        GameObject model;
        GameObject plankModel;
        public Transform HeldPlank => plankModel != null && plankModel.activeSelf ? plankModel.transform : null;
        public bool FirstPersonPlank => HeldPlank != null && IsOwner && !motor.IsThirdPerson;
        NetworkObject plankShip, localPlankShip;
        int plankSection, plankFragment, plankSlot, localPlankSection, localPlankFragment, localPlankSlot;
        Vector3 plankPoint;
        float plankStarted, plankHeartbeat, localPlankStarted, nextPlankSend;
        Material highlight;
        Mesh surfaceHighlight;
        ShipDamageSection aimed;
        Harpoon.HarpoonGun aimedHarpoon;
        Vector3 aimedHarpoonPoint;
        int aimedFragment = -1, lastSection, lastFragment, strikes;
        NetworkObject lastShip;
        Vector3 aimedPoint;
        float nextStrike, swingAt = -10f, lastStrikeAt;
        bool Available => IsSpawned && (inventory.MalletSelected || inventory.PlankSelected) && !inventory.HandsOccupied && !motor.IsDead && !motor.IsClimbing && !motor.LocomotionLocked && !GetComponent<CannonHands>().HasHeldBall && !weapon.LootHandsBusy && !inventory.Fishing.IsFishing && !inventory.Fishing.IsEating
#if UNITY_EDITOR || PIRATESLOP_UPPER_DESTRUCTION_V2
            && !weapon.DeveloperCannonEnabled
#endif
            ;
        bool PlankTargetAvailable => aimed != null && (aimedFragment >= 0
#if UNITY_EDITOR || PIRATESLOP_UPPER_DESTRUCTION_V2
            || aimedFragment == -1 && aimed.Owner.Upper != null && aimed.Owner.Upper.DirectMastAssembly(aimed.SectionId, out _)
            || aimedFragment == -2 && aimed.Owner.Upper != null && aimed.Owner.Upper.RepairableMechanism(aimed.SectionId, out _)
#endif
            );
        void Awake()
        {
            inventory = GetComponent<PlayerInventory>();
            motor = GetComponent<AdvancedPlayerController>();
            weapon = GetComponent<NetworkWeapon>();
        }
        void Update()
        {
            if (IsServerInitialized) TickPlankRepair();
            if (!IsOwner) return;
            if (!Available || !motor.InputActive || PlayerInventory.LootWindowOpen || inventory.ControlFocused)
            { CancelLocalPlank(); return; }
            if (inventory.PlankSelected)
            {
                if (!PlankTargetAvailable || Mouse.current == null || !Mouse.current.leftButton.isPressed)
                { CancelLocalPlank(); return; }
                var target = aimed.Owner.NetworkObject;
                if (localPlankShip != target || localPlankSection != aimed.SectionId || localPlankFragment != aimedFragment || localPlankSlot != inventory.SelectedSlot)
                {
                    CancelLocalPlank();
                    localPlankShip = target; localPlankSection = aimed.SectionId; localPlankFragment = aimedFragment; localPlankSlot = inventory.SelectedSlot;
                    localPlankStarted = Time.time; nextPlankSend = 0f;
                }
                if (Time.time >= nextPlankSend)
                {
                    nextPlankSend = Time.time + .05f;
                    HoldPlankServerRpc(target, aimed.SectionId, aimedFragment, target.transform.InverseTransformPoint(aimedPoint));
                }
                return;
            }
            CancelLocalPlank();
            if (aimedHarpoon != null && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                var ship = aimedHarpoon.GetComponentInParent<NetworkShip>();
                if (ship != null && ship.IsClientInitialized)
                    RepairHarpoonServerRpc(ship.NetworkObject, aimedHarpoonPoint);
                else
                {
                    aimedHarpoon.RepairStrike(gameObject);
                    StrikeObserversRpc(aimedHarpoonPoint);
                }
                return;
            }
            if (aimed != null && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                RepairServerRpc(aimed.Owner.NetworkObject, aimed.SectionId, aimedFragment, aimed.Owner.transform.InverseTransformPoint(aimedPoint));
        }
        void LateUpdate()
        {
            if (!IsClientInitialized) return;
            if (model == null && MalletModel != null)
            {
                model = Instantiate(MalletModel, transform);
                if (IsOwner) motor.ViewMotion.Register(model.transform);
                foreach (var collider in model.GetComponentsInChildren<Collider>()) Destroy(collider);
            }
            if (model != null)
            {
                model.SetActive((Available && inventory.MalletSelected || motor.SailPullLocked && inventory.MalletSelected && !motor.IsDead) && !inventory.ControlItemHidden);
                bool first = IsOwner && !motor.IsThirdPerson;
                var anchor = first ? motor.PlayerCamera.transform : transform;
                float swing = Mathf.Sin(Mathf.Clamp01((Time.time - swingAt) / .35f) * Mathf.PI);
                model.transform.SetPositionAndRotation(anchor.TransformPoint(first ? new Vector3(.32f, -.42f, .5f) : new Vector3(.35f, 1.05f, .4f)), anchor.rotation * Quaternion.Euler(-15f + swing * 75f, 0, -15f));
            }
            if (plankModel == null && PlankModel != null)
            {
                plankModel = Instantiate(PlankModel, transform);
                if (IsOwner) motor.ViewMotion.Register(plankModel.transform);
                foreach (var collider in plankModel.GetComponentsInChildren<Collider>()) Destroy(collider);
            }
            if (plankModel != null)
            {
                plankModel.SetActive(Available && inventory.PlankSelected && !inventory.ControlItemHidden);
                bool first = IsOwner && !motor.IsThirdPerson;
                var anchor = first ? motor.PlayerCamera.transform : transform;
                float press = localPlankShip != null ? Mathf.Clamp01((Time.time - localPlankStarted) / PlankRepairSeconds) : 0f;
                plankModel.transform.SetPositionAndRotation(anchor.TransformPoint(first ? new Vector3(.04f, -.32f, .6f + press * .04f) : new Vector3(0f, 1.05f, .48f)), anchor.rotation * Quaternion.Euler(first ? -50f : -35f, 0f, -10f));
            }
            aimed = null; aimedFragment = -1; aimedHarpoon = null;
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
            float nearest = 3.5f * UpgradeRepair;
            foreach (var ship in NetworkShip.ActiveShips)
            {
                if (ship == null || ship.IsSinking || Vector3.Distance(ship.transform.position, transform.position) > 70f) continue;
                var destruction = ship.GetComponent<ShipDestruction>();
                if (destruction == null) continue;
                var mastGroups = new System.Collections.Generic.HashSet<string>();
                foreach (var section in destruction.Sections)
                {
#if UNITY_EDITOR || PIRATESLOP_UPPER_DESTRUCTION_V2
                    if (section != null && destruction.UpperRepairTarget(section.SectionId, out int targetIndex, out var targetBounds, out int targetFragment))
                    {
                        var assembly = destruction.Upper.Assemblies[targetIndex];
                        if (!mastGroups.Add(assembly.Key)) continue;
                        var preview = assembly.RepairPreview;
                        if (preview != null && Vector3.Distance(ray.origin, ship.transform.TransformPoint(targetBounds.center)) <= 12f)
                            Graphics.DrawMesh(preview, ship.transform.localToWorldMatrix, highlight, 0, camera, 0, null, ShadowCastingMode.Off, false);
                        var localRay = new Ray(ship.transform.InverseTransformPoint(ray.origin), ship.transform.InverseTransformDirection(ray.direction));
                        if (targetBounds.IntersectRay(localRay, out float distance))
                        {
                            var point = ship.transform.TransformPoint(localRay.GetPoint(distance));
                            float worldDistance = Vector3.Distance(ray.origin, point);
                            if (worldDistance < nearest && ReachableAssembly(destruction, section.SectionId, point))
                            { nearest = worldDistance; aimed = section; aimedFragment = targetFragment; aimedPoint = point; }
                        }
                        continue;
                    }
                    if (section != null && destruction.Upper != null && destruction.Upper.MastAssembly(section.SectionId, out int collapsedIndex) && destruction.Upper.Stage(collapsedIndex) >= 3)
                        continue;
#endif
                    bool mastTarget = inventory.MalletSelected && section != null && destruction.Definition(section.SectionId).Type == ShipSectionType.Mast;
#if UNITY_EDITOR || PIRATESLOP_UPPER_DESTRUCTION_V2
                    int mastIndex = -1;
                    bool upperMast = section != null && destruction.Upper != null && destruction.Upper.DirectMastAssembly(section.SectionId, out mastIndex);
                    if (upperMast) mastTarget = (inventory.MalletSelected || inventory.PlankSelected) && mastGroups.Add(destruction.Upper.Assemblies[mastIndex].Key);
#endif
                    if (mastTarget && destruction.MastRepairPoint(section.SectionId, out var basePoint))
                    {
                        string group = destruction.MastGroupKey(section.SectionId);
                        if (
#if UNITY_EDITOR || PIRATESLOP_UPPER_DESTRUCTION_V2
                            upperMast ||
#endif
                            mastGroups.Add(group))
                        {
#if UNITY_EDITOR || PIRATESLOP_UPPER_DESTRUCTION_V2
                            if (upperMast)
                            {
                                var preview = destruction.MastRepairPreview(section.SectionId);
                                if (preview != null && Vector3.Distance(ray.origin, basePoint) <= 12f)
                                    Graphics.DrawMesh(preview, ship.transform.localToWorldMatrix, highlight, 0, camera, 0, null, ShadowCastingMode.Off, false);
                            }
                            else
#endif
                                Graphics.DrawMesh(surfaceHighlight, Matrix4x4.TRS(basePoint, ship.transform.rotation * Quaternion.Euler(90,0,0), Vector3.one * 1.3f), highlight, 0, camera, 0, null, ShadowCastingMode.Off, false);
                            var bounds = new Bounds(basePoint, Vector3.one * 1.4f);
                            if (bounds.IntersectRay(ray, out float distance) && distance < nearest)
                            {
                                var point = ray.GetPoint(distance);
                                if (Reachable(point)) { nearest = distance; aimed = section; aimedFragment = -1; aimedPoint = point; }
                            }
                        }
                        if (destruction.IsMastCollapsed(section.SectionId)) continue;
                    }
#if UNITY_EDITOR || PIRATESLOP_UPPER_DESTRUCTION_V2
                    if (upperMast && destruction.Upper.Stage(mastIndex) >= 3) continue;
#endif
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
            if (aimed == null && inventory.MalletSelected)
            {
                foreach (var hit in Physics.RaycastAll(ray, 3.5f * UpgradeRepair, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.transform.IsChildOf(transform)) continue;
                    var gun = hit.collider.GetComponentInParent<Harpoon.HarpoonGun>();
                    if (gun != null && gun.IsBroken && Reachable(hit.point))
                    {
                        aimedHarpoon = gun;
                        aimedHarpoonPoint = hit.point;
                        break;
                    }
                }
            }
        }
        bool Reachable(Vector3 point)
        {
            Vector3 origin = transform.position + Vector3.up * (motor.IsCrouched ? .8f : 1.5f);
            Vector3 delta = point - origin;
            return delta.magnitude <= 3.5f * UpgradeRepair && !FirearmTrace.Cast(gameObject, origin, point - delta.normalized * .08f, out _);
        }
#if UNITY_EDITOR || PIRATESLOP_UPPER_DESTRUCTION_V2
        bool ReachableAssembly(ShipDestruction owner, int id, Vector3 point)
        {
            Vector3 origin = transform.position + Vector3.up * (motor.IsCrouched ? .8f : 1.5f);
            Vector3 delta = point - origin;
            if (delta.magnitude > 3.5f * UpgradeRepair) return false;
            if (!FirearmTrace.Cast(gameObject, origin, point - delta.normalized * .08f, out var hit)) return true;
            if (hit.collider == null || !hit.collider.transform.IsChildOf(owner.transform)) return false;
            var section = Ships.ShipV3CollisionBatch.ResolveSection(hit.collider, hit.point);
            return section != null && owner.Upper.AssemblyFor(section.SectionId) == owner.Upper.AssemblyFor(id);
        }
#endif
        void CancelLocalPlank()
        {
            if (localPlankShip == null) return;
            localPlankShip = null;
            CancelPlankServerRpc();
        }
        [ServerRpc]
        void CancelPlankServerRpc() { TickPlankRepair(); plankShip = null; }
        bool ValidPlankTarget(NetworkObject target, int sectionId, int fragmentId, Vector3 localPoint, out ShipDestruction destruction)
        {
            destruction = target != null ? target.GetComponent<ShipDestruction>() : null;
            if (!Available || !inventory.PlankSelected || destruction == null || !destruction.IsSpawned || !float.IsFinite(localPoint.sqrMagnitude) || target.GetComponent<NetworkShip>() is not { IsSinking: false }) return false;
            var section = destruction.Section(sectionId);
#if UNITY_EDITOR || PIRATESLOP_UPPER_DESTRUCTION_V2
            if (fragmentId == -2 && destruction.Upper != null)
                return destruction.UpperRepairTarget(sectionId, out _, out var bounds, out int targetFragment) && targetFragment == -2 &&
                    bounds.SqrDistance(localPoint) <= .025f && destruction.UpperRepairFragment(sectionId, target.transform.TransformPoint(localPoint), out _, out _) &&
                    ReachableAssembly(destruction, sectionId, target.transform.TransformPoint(localPoint));
            if (fragmentId == -1 && destruction.Upper != null && destruction.Upper.DirectMastAssembly(sectionId, out _))
                return destruction.MastRepairPoint(sectionId, out var basePoint) && Vector3.Distance(target.transform.TransformPoint(localPoint), basePoint) <= 1.3f &&
                    destruction.MastRepairFragment(sectionId, out _, out _) && ReachableAssembly(destruction, sectionId, target.transform.TransformPoint(localPoint));
#endif
            if (section == null || fragmentId < 0 || fragmentId >= Mathf.Min(64, section.RepairCount) || (section.RemovedFragments & (1UL << fragmentId)) == 0) return false;
            Vector3 point = target.transform.TransformPoint(localPoint);
            var fragment = section.RepairTransform(fragmentId);
            return fragment != null && section.RepairBounds(fragmentId).SqrDistance(fragment.InverseTransformPoint(point)) <= .025f && Reachable(point);
        }
        [ServerRpc]
        void HoldPlankServerRpc(NetworkObject target, int sectionId, int fragmentId, Vector3 localPoint)
        {
            if (!ValidPlankTarget(target, sectionId, fragmentId, localPoint, out _)) { plankShip = null; return; }
            if (plankShip != target || plankSection != sectionId || plankFragment != fragmentId || plankSlot != inventory.SelectedSlot || Time.time - plankHeartbeat > .3f)
            {
                plankShip = target; plankSection = sectionId; plankFragment = fragmentId; plankSlot = inventory.SelectedSlot;
                plankStarted = Time.time;
            }
            plankHeartbeat = Time.time; plankPoint = localPoint;
        }
        void TickPlankRepair()
        {
            if (plankShip == null) return;
            if (Time.time - plankHeartbeat > .3f || inventory.SelectedSlot != plankSlot || !ValidPlankTarget(plankShip, plankSection, plankFragment, plankPoint, out var destruction))
            { plankShip = null; return; }
            if (Time.time - plankStarted < PlankRepairSeconds) return;
            Vector3 point = plankShip.transform.TransformPoint(plankPoint);
            int sectionId = plankSection, fragmentId = plankFragment;
#if UNITY_EDITOR || PIRATESLOP_UPPER_DESTRUCTION_V2
            if (fragmentId == -1 && !destruction.MastRepairFragment(sectionId, out sectionId, out fragmentId)) { plankShip = null; return; }
            if (fragmentId == -2 && !destruction.UpperRepairFragment(sectionId, point, out sectionId, out fragmentId)) { plankShip = null; return; }
#endif
            if (destruction.RepairFragment(sectionId, fragmentId))
            {
                weapon.ConsumePlank(plankSlot);
                StrikeObserversRpc(point);
            }
            plankShip = null;
        }
        [ServerRpc]
        void RepairServerRpc(NetworkObject target, int sectionId, int fragmentId, Vector3 localPoint) => TryRepair(target, sectionId, fragmentId, localPoint);
        [ServerRpc]
        void RepairHarpoonServerRpc(NetworkObject target, Vector3 point)
        {
            if (!IsServerInitialized || !Available || !inventory.MalletSelected || target == null || Time.time < nextStrike) return;
            var guns = target.GetComponentsInChildren<Harpoon.HarpoonGun>();
            foreach (var gun in guns)
            {
                if (gun != null && gun.IsBroken && Vector3.Distance(gun.transform.position, point) <= 3.8f && Reachable(point))
                {
                    nextStrike = Time.time + .45f / UpgradeRepair;
                    gun.RepairStrike(gameObject);
                    var ship = target.GetComponent<NetworkShip>();
                    if (ship != null && gun.MountIndex >= 0)
                        ship.HarpoonRepairStrike(gun.MountIndex, gun.CurrentHealth, gun.IsBroken, gun.RepairStrikes, point);
                    else
                        StrikeObserversRpc(point);
                    break;
                }
            }
        }
        public bool TryRepair(NetworkObject target, int sectionId, int fragmentId, Vector3 localPoint)
        {
            if (!IsServerInitialized || !Available || !inventory.MalletSelected || target == null || Time.time < nextStrike || !float.IsFinite(localPoint.sqrMagnitude)) return false;
            var destruction = target.GetComponent<ShipDestruction>();
            if (destruction == null || !destruction.IsSpawned || target.GetComponent<NetworkShip>().IsSinking) return false;
            var section = destruction.Section(sectionId);
#if UNITY_EDITOR || PIRATESLOP_UPPER_DESTRUCTION_V2
            if (fragmentId == -2 && destruction.Upper != null)
            {
                var mechanismPoint = target.transform.TransformPoint(localPoint);
                if (!destruction.UpperRepairTarget(sectionId, out _, out var bounds, out int kind) || kind != -2 || bounds.SqrDistance(localPoint) > .025f ||
                    !ReachableAssembly(destruction, sectionId, mechanismPoint) || !destruction.UpperRepairFragment(sectionId, mechanismPoint, out int repairId, out int repairBit)) return false;
                if (target != lastShip || repairId != lastSection || repairBit != lastFragment) strikes = 0;
                lastShip = target; lastSection = repairId; lastFragment = repairBit;
                nextStrike = Time.time + StrikeInterval / UpgradeRepair; lastStrikeAt = Time.time;
                if (++strikes >= FragmentStrikes)
                {
                    if (!destruction.RepairFragment(repairId, repairBit)) return false;
                    strikes = 0;
                }
                StrikeObserversRpc(mechanismPoint);
                return true;
            }
#endif
            if (fragmentId == -1)
            {
#if UNITY_EDITOR || PIRATESLOP_UPPER_DESTRUCTION_V2
                if (destruction.Upper != null && destruction.Upper.DirectMastAssembly(sectionId, out _))
                {
                    if (!destruction.MastRepairPoint(sectionId, out var mastBase)) return false;
                    var position = target.transform.TransformPoint(localPoint);
                    if (Vector3.Distance(position, mastBase) > 1.3f || !ReachableAssembly(destruction, sectionId, position)) return false;
                    if (!destruction.Upper.StrikeMastRepair(sectionId)) return false;
                    nextStrike = Time.time + StrikeInterval / UpgradeRepair; lastStrikeAt = Time.time;
                    StrikeObserversRpc(position);
                    return true;
                }
#endif
                if (!destruction.MastRepairPoint(sectionId, out var basePoint) || !destruction.MastRepairFragment(sectionId, out int repairSection, out int repairFragment)) return false;
                Vector3 hitPoint = target.transform.TransformPoint(localPoint);
                if (Vector3.Distance(hitPoint, basePoint) > 1.3f || !Reachable(hitPoint)) return false;
                if (target != lastShip || repairSection != lastSection || repairFragment != lastFragment) strikes = 0;
                lastShip = target; lastSection = repairSection; lastFragment = repairFragment;
                nextStrike = Time.time + StrikeInterval / UpgradeRepair; lastStrikeAt = Time.time;
                if (++strikes >= FragmentStrikes) { destruction.RepairFragment(repairSection, repairFragment); strikes = 0; }
                StrikeObserversRpc(hitPoint);
                return true;
            }
            if (section == null || fragmentId < 0 || fragmentId >= section.RepairCount || fragmentId >= 64 || (section.RemovedFragments & (1UL << fragmentId)) == 0) return false;
            Vector3 point = target.transform.TransformPoint(localPoint);
            var fragment = section.RepairTransform(fragmentId).GetComponent<MeshFilter>();
            if (fragment == null || section.RepairBounds(fragmentId).SqrDistance(fragment.transform.InverseTransformPoint(point)) > .025f || !Reachable(point)) return false;
            if (target != lastShip || sectionId != lastSection || fragmentId != lastFragment) strikes = 0;
            lastShip = target; lastSection = sectionId; lastFragment = fragmentId;
            nextStrike = Time.time + StrikeInterval / UpgradeRepair; lastStrikeAt = Time.time;
            strikes++;
            if (strikes >= FragmentStrikes) { destruction.RepairFragment(sectionId, fragmentId); strikes = 0; }
            StrikeObserversRpc(point);
            return true;
        }
        [ObserversRpc(RunLocally = true)]
        void StrikeObserversRpc(Vector3 point) { swingAt = Time.time; GameAudio.Play(SoundCue.BulletWood, point, .8f); }
        void OnGUI()
        {
            if (IsOwner && Available && motor.InputActive && !PlayerInventory.LootWindowOpen)
            {
                if (inventory.PlankSelected)
                {
                    string hint = PlankTargetAvailable ? "Зажмите ЛКМ на пробоине на 0,8 с · 1 доска за 1 блок" : "Наведитесь на подсвеченную пробоину";
#if UNITY_EDITOR || PIRATESLOP_UPPER_DESTRUCTION_V2
                    if (PlankTargetAvailable && aimedFragment == -1) hint = "Зажмите ЛКМ у основания · 1 доска за 1 блок мачты";
#endif
                    ContextPrompt.Offer(hint, aimed != null ? 50 : 5);
                    if (localPlankShip != null)
                    {
                        PirateHudStyle.RepairRing(new Vector2(Screen.width * .5f, Screen.height * .5f), (Time.time - localPlankStarted) / PlankRepairSeconds);
                    }
                }
                else if (aimedHarpoon != null)
                    ContextPrompt.Offer($"ЛКМ — починить гарпунную пушку ({3 - aimedHarpoon.RepairStrikes} удара)", 50);
                else
#if UNITY_EDITOR || PIRATESLOP_UPPER_DESTRUCTION_V2
                    if (aimed != null && aimedFragment == -1 && aimed.Owner.Upper != null && aimed.Owner.Upper.MastAssembly(aimed.SectionId, out int mastIndex))
                        ContextPrompt.Offer($"ЛКМ у основания — поднять мачту ({Experimental.UpperDestructionV2.MastRepairStrikes - aimed.Owner.Upper.MastRepairProgress(mastIndex)} ударов)", 50);
                    else
#endif
                    ContextPrompt.Offer(aimed != null ? "ЛКМ — починить 1 блок за 3 удара" : "Наведитесь на подсвеченную повреждённую часть", aimed != null ? 50 : 5);
            }
        }
        void OnDestroy() { if (model != null) Destroy(model); if (plankModel != null) Destroy(plankModel); if (highlight != null) Destroy(highlight); if (surfaceHighlight != null) Destroy(surfaceHighlight); }
    }
}

