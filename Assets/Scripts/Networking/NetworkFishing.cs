using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PirateSlop.Networking
{
    [DefaultExecutionOrder(25)]
    public sealed class NetworkFishing : NetworkBehaviour
    {
        public GameObject RodModel, FishModel, FloatModel;
        public NetworkFish FishPrefab;
        public Material LineMaterial;
        public Vector2 BiteDelay = new(4f, 12f);
        public float BiteWindow = 3f, ReelDuration = 2.5f, HealAmount = 25f;
        readonly SyncVar<byte> stage = new();
        readonly SyncVar<InventoryItem> catchItem = new(InventoryItem.Fish);
        GameObject catchVisual;
        InventoryItem shownCatch = InventoryItem.None;
        readonly SyncVar<Vector3> castPoint = new();
        readonly SyncVar<NetworkObject> castShip = new();
        readonly SyncVar<Vector3> castLocalPoint = new();
        Vector3 CastPosition
        {
            get
            {
                Vector3 point = castShip.Value != null ? castShip.Value.transform.TransformPoint(castLocalPoint.Value) : castPoint.Value;
                if (OceanSurface.Instance != null) point.y = OceanSurface.Instance.Height(point);
                return point;
            }
        }
        readonly SyncVar<float> progress = new();
        readonly SyncVar<bool> pickingUp = new();
        NetworkObject pickupTarget;
        float pickupUntil;
        const float MinimumPickupTime = 1.9f;
        public bool IsPickingUp => pickingUp.Value;
        readonly SyncVar<bool> eating = new();
        readonly SyncVar<float> eatProgress = new();
        float eatUntil;
        int eatSlot;
        bool eatCatch;
        AudioSource chewing;
        public bool IsEating => eating.Value;
        AdvancedPlayerController motor;
        PlayerInventory inventory;
        CannonHands hands;
        DirectShipControls controls;
        GameObject rod, fish, bobber;
        LineRenderer line;
        float deadline, lastReel, nextCast, nextInput, nextReelSound, castStarted;
        bool reeling;
        byte shownStage;
        NetworkFish aimed;
        public bool CarryingCatch => stage.Value == 5;
        public bool HasFish => (CarryingCatch && catchItem.Value == InventoryItem.Fish) || (inventory != null && inventory.FishSelected);
        public bool IsFishing => stage.Value > 0 && stage.Value < 5;
        bool Available => !motor.IsDead && !motor.IsSwimming && !motor.IsClimbing && !motor.LocomotionLocked && !hands.HasHeldBall && !(GetComponent<NetworkWeapon>()?.LootHandsBusy ?? false);
        void Awake()
        {
            motor = GetComponent<AdvancedPlayerController>(); inventory = GetComponent<PlayerInventory>();
            hands = GetComponent<CannonHands>(); controls = GetComponent<DirectShipControls>();
        }
        void Update()
        {
            if (IsServerInitialized) TickPickup();
            motor.PickupLocked = IsPickingUp;
            if (IsServerInitialized) TickEating();
            if (IsServerInitialized) TickFishing();
            UpdateChewing();
            if (!IsOwner || !motor.InputActive || !Available) return;
            var mouse = Mouse.current; var keys = Keyboard.current;
            if (mouse == null || keys == null) return;
            if (IsEating) return;
            if (IsPickingUp) return;
            aimed = FindFish();
            if (aimed != null && !aimed.Available) aimed = null;
            if (aimed != null && !IsFishing && !CarryingCatch && keys.eKey.wasPressedThisFrame) { PickupServerRpc(aimed.NetworkObject); return; }
            if (CarryingCatch)
            {
                if (keys.gKey.wasPressedThisFrame) DropServerRpc();
                else if (mouse.leftButton.wasPressedThisFrame) ThrowCatchServerRpc(motor.AimDirection);
                else if (mouse.rightButton.wasPressedThisFrame && HasFish) EatServerRpc();
                return;
            }
            if (HasFish)
            {
                if (keys.gKey.wasPressedThisFrame && CarryingCatch) DropServerRpc();
                else if (mouse.rightButton.wasPressedThisFrame) EatServerRpc();
                return;
            }
            if (!inventory.RodSelected || controls.BlocksPrimary) return;
            if (mouse.rightButton.wasPressedThisFrame) { CancelServerRpc(); return; }
            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (stage.Value == 0) CastServerRpc(motor.PlayerCamera.transform.forward);
                else PullServerRpc();
            }
            if (stage.Value == 4 && Time.unscaledTime >= nextInput)
            {
                nextInput = Time.unscaledTime + .1f;
                ReelServerRpc(mouse.leftButton.isPressed);
            }
        }
        NetworkFish FindFish()
        {
            float closest = 3f; NetworkFish result = null;
            var camera = motor.PlayerCamera;
            foreach (var hit in Physics.RaycastAll(camera.transform.position, camera.transform.forward, closest, ~0, QueryTriggerInteraction.Collide))
            {
                if (hit.collider.isTrigger && hit.collider.name != "SwordfishRecovery") continue;
                if (!hit.transform.IsChildOf(transform) && hit.distance < closest) { closest = hit.distance; result = hit.collider.GetComponentInParent<NetworkFish>(); }
            }
            return result;
        }
        [ServerRpc]
        void CastServerRpc(Vector3 aim)
        {
            if (!Available || !inventory.RodSelected || stage.Value != 0 || Time.time < nextCast || !float.IsFinite(aim.sqrMagnitude) || aim.sqrMagnitude < .5f || OceanSurface.Instance == null) return;
            aim.Normalize();
            Vector3 horizontal = Vector3.ProjectOnPlane(aim, Vector3.up).normalized;
            if (horizontal.sqrMagnitude < .5f) return;
            Vector3 start = transform.position + Vector3.up * 1.5f;
            Vector3 end = start + horizontal * Mathf.Lerp(8f, 18f, Mathf.InverseLerp(-.8f, .4f, aim.y));
            end.y = OceanSurface.Instance.Height(end);
            var world = World.ProceduralWorld.Instance;
            if (world != null && world.Ready && world.GroundHeight(end) > end.y - 1f) return;
            Vector3 previous = start;
            for (int i = 1; i <= 16; i++)
            {
                float t = i / 16f;
                Vector3 point = Vector3.Lerp(start, end, t) + Vector3.up * (4f * t * (1f - t));
                foreach (var hit in Physics.RaycastAll(previous, (point - previous).normalized, Vector3.Distance(previous, point), ~0, QueryTriggerInteraction.Ignore))
                    if (!hit.transform.IsChildOf(transform)) return;
                previous = point;
            }
            var support = GetComponent<ShipDeckPassenger>().Ship;
            castShip.Value = support != null ? support.GetComponent<NetworkObject>() : null;
            castLocalPoint.Value = support != null ? support.transform.InverseTransformPoint(end) : end;
            castPoint.Value = end; stage.Value = 1; progress.Value = 0;
            deadline = Time.time + .65f; nextCast = Time.time + 1f;
            SoundObserversRpc(SoundCue.FishingCast, start);
        }
        float catchThrowUntil, catchWindupAt = -10f;
        Vector3 catchThrowDirection;
        void TickFishing()
        {
            if (motor.IsDead) { stage.Value = 0; catchThrowUntil = 0; return; }
            if (catchThrowUntil > 0f)
            {
                if (!CarryingCatch || !Available || IsEating) catchThrowUntil = 0f;
                else if (Time.time >= catchThrowUntil)
                {
                    catchThrowUntil = 0f;
                    if (GetComponent<NetworkWeapon>().ThrowFish(catchItem.Value,catchThrowDirection,true)) stage.Value = 0;
                }
                return;
            }
            if (CarryingCatch && !IsEating && catchThrowUntil <= 0f && GetComponent<NetworkWeapon>().AddItem(catchItem.Value)) { stage.Value = 0; return; }
            if (!IsFishing) return;
            if (!Available || !inventory.RodSelected || Vector3.Distance(transform.position, CastPosition) > 30f) { ResetFishing(); return; }
            if (stage.Value == 1 && Time.time >= deadline)
            {
                stage.Value = 2; deadline = Time.time + Random.Range(BiteDelay.x, BiteDelay.y);
                SoundObserversRpc(SoundCue.Splash, CastPosition);
            }
            else if (stage.Value == 2 && Time.time >= deadline)
            {
                stage.Value = 3; deadline = Time.time + BiteWindow;
                SoundObserversRpc(SoundCue.FishingBite, CastPosition);
            }
            else if (stage.Value == 3 && Time.time >= deadline) ResetFishing();
            else if (stage.Value == 4)
            {
                if (Time.time >= deadline) { ResetFishing(); return; }
                bool pulling = reeling && Time.time - lastReel < .35f;
                progress.Value = Mathf.Clamp01(progress.Value + Time.deltaTime * (pulling ? 1f / ReelDuration : -.12f));
                if (pulling && Time.time >= nextReelSound) { nextReelSound = Time.time + .4f; SoundObserversRpc(SoundCue.FishingReel, transform.position); }
                if (progress.Value >= 1f) { stage.Value = GetComponent<NetworkWeapon>().AddItem(catchItem.Value) ? (byte)0 : (byte)5; SoundObserversRpc(SoundCue.FishingCatch, transform.position); }
            }
        }
        void ResetFishing()
        {
            if (IsFishing) SoundObserversRpc(SoundCue.FishingEscape, transform.position);
            stage.Value = 0; progress.Value = 0; reeling = false;
        }
        [ServerRpc] void CancelServerRpc() { if (IsFishing) ResetFishing(); }
        [ServerRpc]
        void PullServerRpc()
        {
            if (!Available || !inventory.RodSelected) return;
            if (stage.Value == 3 && Time.time < deadline)
            { catchItem.Value = RollCatch(); stage.Value = 4; deadline = Time.time + 10f; lastReel = Time.time; reeling = true; }
            else if (stage.Value == 1 || stage.Value == 2) ResetFishing();
        }
        [ServerRpc] void ReelServerRpc(bool held) { if (stage.Value == 4) { reeling = held; lastReel = Time.time; } }
        InventoryItem RollCatch()
        {
            float roll = Random.value;
            if (roll < .8f) return InventoryItem.Fish;
            if (roll < .875f) return InventoryItem.Pufferfish;
            if (roll < .95f) return InventoryItem.Swordfish;
            var items = SessionController.Instance.Config.Loot.Items;
            var drops = GetComponent<NetworkWeapon>().DropPrefabs;
            var candidates = new System.Collections.Generic.List<InventoryItem>();
            foreach (var entry in items)
            {
                if (entry == null || entry.Weight <= 0 || entry.Item == InventoryItem.None) continue;
                int index = CannonAmmo.IsBall(entry.Item) ? (int)InventoryItem.Cannonball : (int)entry.Item;
                if (index >= 0 && index < drops.Length && drops[index] != null && !candidates.Contains(entry.Item)) candidates.Add(entry.Item);
            }
            return candidates.Count > 0 ? candidates[Random.Range(0, candidates.Count)] : InventoryItem.Fish;
        }
        [ServerRpc]
        void ThrowCatchServerRpc(Vector3 direction)
        {
            if (!CarryingCatch || !Available || IsEating || catchThrowUntil > 0f || !float.IsFinite(direction.sqrMagnitude) || direction.sqrMagnitude < .5f) return;
            if (catchItem.Value == InventoryItem.Swordfish)
            { catchThrowDirection = direction.normalized; catchThrowUntil = Time.time + .3f; CatchWindupObserversRpc(); }
            else if (GetComponent<NetworkWeapon>().ThrowFish(catchItem.Value, direction, true)) stage.Value = 0;
        }
        [ServerRpc]
        void DropServerRpc()
        {
            if (!CarryingCatch || !Available || IsEating || catchThrowUntil > 0f) return;
            var drops = GetComponent<NetworkWeapon>().DropPrefabs;
            int index = CannonAmmo.IsBall(catchItem.Value) ? (int)InventoryItem.Cannonball : (int)catchItem.Value;
            if (index < 0 || index >= drops.Length || drops[index] == null) return;
            if (!LootPlacement.Find(transform, drops[index], catchItem.Value, out var point, out var orientation, out var support)) return;
            var caught = Instantiate(drops[index], point, orientation);
            if (CannonAmmo.IsBall(catchItem.Value)) caught.SetAmmoItem(catchItem.Value);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(caught.gameObject, gameObject.scene);
            caught.Place(support != null ? support.NetworkObject : null, point, orientation);
            ServerManager.Spawn(caught.NetworkObject);
            stage.Value = 0;
            SoundObserversRpc(SoundCue.FishDrop, point);
        }
        [ServerRpc]
        void PickupServerRpc(NetworkObject target)
        {
            if (!Available || IsEating || IsPickingUp || stage.Value != 0 || !CanPickupTarget(target)) return;
            pickupTarget = target;
            pickupUntil = Time.time + MinimumPickupTime;
            pickingUp.Value = true;
        }
        public void FinishPickup() { if (IsOwner && IsPickingUp) FinishPickupServerRpc(); }
        [ServerRpc]
        void FinishPickupServerRpc()
        {
            if (!IsPickingUp || Time.time < pickupUntil) return;
            bool valid = !motor.IsDead && !motor.IsSwimming && !motor.IsClimbing && stage.Value == 0 && !IsEating;
            var target = pickupTarget;
            pickupTarget = null;
            pickingUp.Value = false;
            motor.PickupLocked = false;
            if (!valid || !CanPickupTarget(target)) return;
            var item = target.GetComponent<NetworkFish>();
            var kind = item.CurrentItem;
            if (item.Take() && GetComponent<NetworkWeapon>().AddItem(kind)) SoundObserversRpc(SoundCue.Pickup, transform.position);
        }
        bool CanPickupTarget(NetworkObject target)
        {
            if (target == null || !target.IsSpawned || Vector3.Distance(transform.position + Vector3.up, target.transform.position) > 3.5f) return false;
            var item = target.GetComponent<NetworkFish>();
            if (item == null || !item.Available || !GetComponent<NetworkWeapon>().CanAddItem(item.CurrentItem)) return false;
            var loose = target.GetComponent<NetworkLooseCannonball>();
            if (loose != null && loose.IsHeld) return false;
            Vector3 origin = transform.position + Vector3.up * 1.5f;
            Vector3 delta = target.transform.position - origin;
            foreach (var hit in Physics.RaycastAll(origin, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform) && !hit.transform.IsChildOf(target.transform)) return false;
            return true;
        }
        void TickPickup()
        {
            if (!IsPickingUp) return;
            bool valid = !motor.IsDead && !motor.IsSwimming && !motor.IsClimbing && stage.Value == 0 && !IsEating;
            if (!valid || Time.time >= pickupUntil + 3f)
            {
                pickupTarget = null;
                pickingUp.Value = false;
                motor.PickupLocked = false;
            }
        }
        void OnDisable() { if (motor != null) motor.PickupLocked = false; }
        [ServerRpc]
        void EatServerRpc()
        {
            if (!HasFish || !Available || IsEating || catchThrowUntil > 0f) return;
            eatCatch = CarryingCatch; eatSlot = inventory.SelectedSlot;
            eatUntil = Time.time + 3f; eatProgress.Value = 0; eating.Value = true;
        }
        void TickEating()
        {
            if (!IsEating) return;
            if (!Available || inventory.SelectedSlot != eatSlot || (!eatCatch && !inventory.FishSelected) || (eatCatch && !CarryingCatch))
            { eating.Value = false; return; }
            eatProgress.Value = Mathf.Clamp01(1f - (eatUntil - Time.time) / 3f);
            if (Time.time < eatUntil) return;
            if (eatCatch) { stage.Value = 0; GetComponent<CombatHealth>().Heal(HealAmount); }
            else GetComponent<NetworkWeapon>().EatSelectedFish(HealAmount);
            eating.Value = false;
        }
        void UpdateChewing()
        {
            if (!IsClientInitialized) return;
            if (IsEating && chewing == null)
            {
                var bank = Resources.Load<GameAudioBank>("GameAudioBank");
                var entry = bank == null ? null : System.Array.Find(bank.Entries, e => e.Cue == SoundCue.FishEat);
                if (entry == null || entry.Clips.Length == 0) return;
                chewing = gameObject.AddComponent<AudioSource>(); chewing.playOnAwake = false;
                chewing.clip = entry.Clips[0]; chewing.loop = true; chewing.spatialBlend = IsOwner ? 0f : 1f;
                chewing.volume = entry.Volume * bank.Master * bank.Effects; chewing.minDistance = 1f; chewing.maxDistance = 12f;
            }
            if (chewing == null) return;
            if (IsEating && !chewing.isPlaying) chewing.Play();
            else if (!IsEating && chewing.isPlaying) chewing.Stop();
        }
        [ObserversRpc(RunLocally = true)]
        void SoundObserversRpc(SoundCue cue, Vector3 point) => GameAudio.Play(cue, point, 1f, IsOwner && cue == SoundCue.FishingBite);
        void LateUpdate()
        {
            if (!IsClientInitialized) return;
            if (rod == null)
            {
                rod = Instantiate(RodModel, transform); fish = Instantiate(FishModel, transform); bobber = Instantiate(FloatModel);
                if (IsOwner) { motor.ViewMotion.Register(rod.transform); motor.ViewMotion.Register(fish.transform); }
                var go = new GameObject("FishingLine"); go.transform.SetParent(transform);
                line = go.AddComponent<LineRenderer>(); line.sharedMaterial = LineMaterial;
                line.startWidth = .008f; line.endWidth = .004f; line.positionCount = 16;
            }
            bool first = IsOwner && !motor.IsThirdPerson;
            Transform anchor = first ? motor.PlayerCamera.transform : transform;
            var hand = anchor.TransformPoint(first ? new Vector3(.28f, -.3f, .55f) : new Vector3(.35f, 1.15f, .45f));
            rod.SetActive(inventory.RodSelected && !CarryingCatch && !HasFish && Available);
            fish.SetActive(HasFish && !CarryingCatch && !motor.IsDead);
            rod.transform.SetPositionAndRotation(hand, anchor.rotation * Quaternion.Euler(stage.Value == 3 ? -18f + Mathf.Sin(Time.time * 24f) * 5f : stage.Value == 4 ? -15f + Mathf.Sin(Time.time * 18f) * 2f : -12f, -8f, 0));
            fish.transform.SetPositionAndRotation(hand, anchor.rotation * Quaternion.Euler(0, 90, Mathf.Sin(Time.time * 9f) * 4f));
            if (IsEating) fish.transform.position = Vector3.Lerp(hand, anchor.TransformPoint(first ? new Vector3(.05f, -.12f, .3f) : new Vector3(.1f, 1.55f, .25f)), .8f + Mathf.Sin(Time.time * 14f) * .1f);
            if (shownStage != stage.Value) { if (stage.Value == 1) castStarted = Time.time; shownStage = stage.Value; }
            line.enabled = IsFishing; bobber.SetActive(IsFishing);
            float windup = Mathf.Sin(Mathf.Clamp01((Time.time - catchWindupAt) / .3f) * Mathf.PI);
            UpdateCatchVisual(hand + anchor.TransformVector(new Vector3(.06f,.09f,-.18f) * windup), anchor.rotation * Quaternion.Euler(-22f * windup,0,0));
            if (!IsFishing) return;
            Vector3 tip = rod.transform.TransformPoint(new Vector3(0, .15f, 1.7f));
            Vector3 end = CastPosition;
            if (OceanSurface.Instance != null) end.y = OceanSurface.Instance.Height(end) + .04f;
            if (stage.Value == 3) end.y -= .12f + Mathf.Sin(Time.time * 24f) * .09f;
            if (stage.Value == 4) end = Vector3.Lerp(end, hand, progress.Value);
            if (stage.Value == 1)
            {
                float t = Mathf.Clamp01((Time.time - castStarted) / .65f);
                end = Vector3.Lerp(hand, end, t) + Vector3.up * (4f * t * (1 - t));
            }
            bobber.transform.position = end;
            if (stage.Value == 4 && catchVisual != null) catchVisual.transform.position = end - Vector3.up * .22f;
            for (int i = 0; i < 16; i++)
            {
                float t = i / 15f;
                line.SetPosition(i, Vector3.Lerp(tip, end, t) - Vector3.up * (Mathf.Sin(t * Mathf.PI) * (stage.Value == 4 ? .05f : .3f)));
            }
        }
        [ObserversRpc(RunLocally = true)]
        void CatchWindupObserversRpc() => catchWindupAt = Time.time;
        void UpdateCatchVisual(Vector3 hand, Quaternion orientation)
        {
            bool visible = (stage.Value == 4 || CarryingCatch) && !motor.IsDead;
            if (visible && (shownCatch != catchItem.Value || catchVisual == null))
            {
                if (catchVisual != null) Destroy(catchVisual);
                shownCatch = catchItem.Value;
                catchVisual = new GameObject("ReeledCatch");
                var drops = GetComponent<NetworkWeapon>().DropPrefabs;
                int index = CannonAmmo.IsBall(shownCatch) ? (int)InventoryItem.Cannonball : (int)shownCatch;
                if (index >= 0 && index < drops.Length && drops[index] != null)
                {
                    var source = drops[index].transform;
                    foreach (var filter in source.GetComponentsInChildren<MeshFilter>(true))
                    {
                        var renderer = filter.GetComponent<MeshRenderer>();
                        if (renderer == null) continue;
                        var visual = new GameObject(filter.name);
                        visual.transform.SetParent(catchVisual.transform, false);
                        visual.transform.localPosition = source.InverseTransformPoint(filter.transform.position);
                        visual.transform.localRotation = Quaternion.Inverse(source.rotation) * filter.transform.rotation;
                        visual.transform.localScale = filter.transform.lossyScale;
                        visual.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                        visual.AddComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
                    }
                }
            }
            if (catchVisual == null) return;
            catchVisual.SetActive(visible);
            catchVisual.transform.SetPositionAndRotation(hand, orientation * Quaternion.Euler(0, 90, 0));
            if (IsEating && CarryingCatch) catchVisual.transform.position = fish.transform.position;
        }
        void OnDestroy() { if (bobber != null) Destroy(bobber); if (catchVisual != null) Destroy(catchVisual); }
        void OnGUI()
        {
            if (!IsOwner || !motor.InputActive || !Available) return;
            string hint = aimed != null && !IsFishing && !CarryingCatch ? (inventory.CanFitItem(aimed.CurrentItem) ? "E — подобрать " + aimed.ItemName : inventory.CannotFitHint(aimed.CurrentItem)) : HasFish ? (CarryingCatch ? "Инвентарь заполнен! " : "") + "G — выбросить рыбу · ПКМ — съесть (+25 HP)" : !inventory.RodSelected ? "" :
                stage.Value == 0 ? "ЛКМ — забросить в море" : stage.Value == 1 ? "Заброс…" : stage.Value == 2 ? "Ждите поклёвку… · ПКМ — убрать удочку" : stage.Value == 3 ? "КЛЮЁТ! Зажмите ЛКМ!" : "Держите ЛКМ — вытянуть рыбу: " + Mathf.RoundToInt(progress.Value * 100) + "%";
            if (stage.Value == 4) hint = "ЛКМ — вытянуть: " + InventoryIcons.ItemName(catchItem.Value) + " · " + Mathf.RoundToInt(progress.Value * 100) + "%";
            if (CarryingCatch) hint = "Инвентарь заполнен: " + InventoryIcons.ItemName(catchItem.Value) + " · G — положить" + (catchItem.Value == InventoryItem.Fish ? " · ПКМ — съесть" : catchItem.Value == InventoryItem.Pufferfish || catchItem.Value == InventoryItem.Swordfish ? " · ЛКМ — бросить" : "");
            else if (inventory.EquipmentAt(inventory.SelectedSlot) == InventoryItem.Pufferfish || inventory.EquipmentAt(inventory.SelectedSlot) == InventoryItem.Swordfish) hint = InventoryIcons.ItemName(inventory.EquipmentAt(inventory.SelectedSlot)) + " · ЛКМ — бросить · G — положить";
            if (!IsEating && hint.Length > 0) ContextPrompt.Offer(hint, IsFishing || CarryingCatch ? 55 : aimed != null ? 45 : 5);
            if (IsEating) ContextPrompt.Offer("Едим рыбу… " + Mathf.CeilToInt((1f - eatProgress.Value) * 3f) + " с", 45);
            if (IsEating || stage.Value == 4)
                PirateHudStyle.Bar(new Rect(Screen.width * .5f - 290, Screen.height - 126, 580, 6), IsEating ? eatProgress.Value : progress.Value, PirateHudStyle.Gold);
        }
    }
}
