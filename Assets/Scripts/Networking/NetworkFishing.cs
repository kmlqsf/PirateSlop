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
        readonly SyncVar<Vector3> castPoint = new();
        readonly SyncVar<float> progress = new();
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
        public bool HasFish => CarryingCatch || (inventory != null && inventory.FishSelected);
        public bool IsFishing => stage.Value > 0 && stage.Value < 5;
        bool Available => !motor.IsDead && !motor.IsSwimming && !motor.IsClimbing && !motor.LocomotionLocked && !hands.HasHeldBall;
        void Awake()
        {
            motor = GetComponent<AdvancedPlayerController>(); inventory = GetComponent<PlayerInventory>();
            hands = GetComponent<CannonHands>(); controls = GetComponent<DirectShipControls>();
        }
        void Update()
        {
            if (IsServerInitialized) TickEating();
            if (IsServerInitialized) TickFishing();
            UpdateChewing();
            if (!IsOwner || !motor.InputActive || !Available) return;
            var mouse = Mouse.current; var keys = Keyboard.current;
            if (mouse == null || keys == null) return;
            if (IsEating) return;
            aimed = FindFish();
            if (aimed != null && !IsFishing && !CarryingCatch && keys.eKey.wasPressedThisFrame) { PickupServerRpc(aimed.NetworkObject); return; }
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
            foreach (var hit in Physics.RaycastAll(camera.transform.position, camera.transform.forward, closest, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform) && hit.distance < closest) { closest = hit.distance; result = hit.collider.GetComponentInParent<NetworkFish>(); }
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
            castPoint.Value = end; stage.Value = 1; progress.Value = 0;
            deadline = Time.time + .65f; nextCast = Time.time + 1f;
            SoundObserversRpc(SoundCue.FishingCast, start);
        }
        void TickFishing()
        {
            if (motor.IsDead) { stage.Value = 0; return; }
            if (!IsFishing) return;
            if (!Available || !inventory.RodSelected || Vector3.Distance(transform.position, castPoint.Value) > 30f) { ResetFishing(); return; }
            if (stage.Value == 1 && Time.time >= deadline)
            {
                stage.Value = 2; deadline = Time.time + Random.Range(BiteDelay.x, BiteDelay.y);
                SoundObserversRpc(SoundCue.Splash, castPoint.Value);
            }
            else if (stage.Value == 2 && Time.time >= deadline)
            {
                stage.Value = 3; deadline = Time.time + BiteWindow;
                SoundObserversRpc(SoundCue.FishingBite, castPoint.Value);
            }
            else if (stage.Value == 3 && Time.time >= deadline) ResetFishing();
            else if (stage.Value == 4)
            {
                if (Time.time >= deadline) { ResetFishing(); return; }
                bool pulling = reeling && Time.time - lastReel < .35f;
                progress.Value = Mathf.Clamp01(progress.Value + Time.deltaTime * (pulling ? 1f / ReelDuration : -.12f));
                if (pulling && Time.time >= nextReelSound) { nextReelSound = Time.time + .4f; SoundObserversRpc(SoundCue.FishingReel, transform.position); }
                if (progress.Value >= 1f) { stage.Value = GetComponent<NetworkWeapon>().AddItem(InventoryItem.Fish) ? (byte)0 : (byte)5; SoundObserversRpc(SoundCue.FishingCatch, transform.position); }
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
            { stage.Value = 4; deadline = Time.time + 10f; lastReel = Time.time; reeling = true; }
            else if (stage.Value == 1 || stage.Value == 2) ResetFishing();
        }
        [ServerRpc] void ReelServerRpc(bool held) { if (stage.Value == 4) { reeling = held; lastReel = Time.time; } }
        [ServerRpc]
        void DropServerRpc()
        {
            if (!CarryingCatch || !Available || IsEating) return;
            Vector3 origin = transform.position + transform.forward * .7f + Vector3.up;
            RaycastHit floor = default; float closest = 5f;
            foreach (var hit in Physics.RaycastAll(origin, Vector3.down, closest, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform) && hit.normal.y > .5f && hit.distance < closest) { floor = hit; closest = hit.distance; }
            if (floor.collider == null) return;
            var support = floor.collider.GetComponentInParent<NetworkShip>();
            var point = floor.point + floor.normal * .12f;
            var caught = Instantiate(FishPrefab, point, Quaternion.identity);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(caught.gameObject, gameObject.scene);
            caught.Place(support != null ? support.NetworkObject : null, point, Quaternion.FromToRotation(Vector3.up, floor.normal) * Quaternion.Euler(0, transform.eulerAngles.y, 90));
            ServerManager.Spawn(caught.NetworkObject);
            stage.Value = 0;
            SoundObserversRpc(SoundCue.FishDrop, point);
        }
        [ServerRpc]
        void PickupServerRpc(NetworkObject target)
        {
            if (!Available || IsEating || stage.Value != 0 || target == null || Vector3.Distance(transform.position + Vector3.up, target.transform.position) > 3.5f) return;
            var item = target.GetComponent<NetworkFish>();
            Vector3 origin = transform.position + Vector3.up * 1.5f;
            Vector3 delta = target.transform.position - origin;
            foreach (var hit in Physics.RaycastAll(origin, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform) && !hit.transform.IsChildOf(target.transform)) return;
            var equipment = GetComponent<NetworkWeapon>();
            if (item != null && equipment.CanAddItem(item.Item))
            {
                var kind = item.Item;
                if (item.Take()) { equipment.AddItem(kind); SoundObserversRpc(SoundCue.Pickup, transform.position); }
            }
        }
        [ServerRpc]
        void EatServerRpc()
        {
            if (!HasFish || !Available || IsEating) return;
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
                var go = new GameObject("FishingLine"); go.transform.SetParent(transform);
                line = go.AddComponent<LineRenderer>(); line.sharedMaterial = LineMaterial;
                line.startWidth = .008f; line.endWidth = .004f; line.positionCount = 16;
            }
            bool first = IsOwner && !motor.IsThirdPerson;
            Transform anchor = first ? motor.PlayerCamera.transform : transform;
            var hand = anchor.TransformPoint(first ? new Vector3(.28f, -.3f, .55f) : new Vector3(.35f, 1.15f, .45f));
            rod.SetActive(inventory.RodSelected && !HasFish && Available);
            fish.SetActive(HasFish && !motor.IsDead);
            rod.transform.SetPositionAndRotation(hand, anchor.rotation * Quaternion.Euler(stage.Value == 4 ? -15f + Mathf.Sin(Time.time * 18f) * 2f : -12f, -8f, 0));
            fish.transform.SetPositionAndRotation(hand, anchor.rotation * Quaternion.Euler(0, 90, Mathf.Sin(Time.time * 9f) * 4f));
            if (IsEating) fish.transform.position = Vector3.Lerp(hand, anchor.TransformPoint(first ? new Vector3(.05f, -.12f, .3f) : new Vector3(.1f, 1.55f, .25f)), .8f + Mathf.Sin(Time.time * 14f) * .1f);
            if (shownStage != stage.Value) { if (stage.Value == 1) castStarted = Time.time; shownStage = stage.Value; }
            line.enabled = IsFishing; bobber.SetActive(IsFishing);
            if (!IsFishing) return;
            Vector3 tip = rod.transform.TransformPoint(new Vector3(0, .15f, 1.7f));
            Vector3 end = castPoint.Value;
            if (OceanSurface.Instance != null) end.y = OceanSurface.Instance.Height(end) + .04f;
            if (stage.Value == 3) end.y -= .12f + Mathf.Sin(Time.time * 24f) * .09f;
            if (stage.Value == 4) end = Vector3.Lerp(end, hand, progress.Value);
            if (stage.Value == 1)
            {
                float t = Mathf.Clamp01((Time.time - castStarted) / .65f);
                end = Vector3.Lerp(hand, end, t) + Vector3.up * (4f * t * (1 - t));
            }
            bobber.transform.position = end;
            for (int i = 0; i < 16; i++)
            {
                float t = i / 15f;
                line.SetPosition(i, Vector3.Lerp(tip, end, t) - Vector3.up * (Mathf.Sin(t * Mathf.PI) * (stage.Value == 4 ? .05f : .3f)));
            }
        }
        void OnDestroy() { if (bobber != null) Destroy(bobber); }
        void OnGUI()
        {
            if (!IsOwner || !motor.InputActive || !Available) return;
            string hint = aimed != null && !IsFishing && !CarryingCatch ? "E — подобрать " + aimed.ItemName : HasFish ? (CarryingCatch ? "Инвентарь заполнен! " : "") + "G — выбросить рыбу · ПКМ — съесть (+25 HP)" : !inventory.RodSelected ? "" :
                stage.Value == 0 ? "ЛКМ — забросить в море" : stage.Value == 1 ? "Заброс…" : stage.Value == 2 ? "Ждите поклёвку… · ПКМ — убрать удочку" : stage.Value == 3 ? "КЛЮЁТ! Зажмите ЛКМ!" : "Держите ЛКМ — вытянуть рыбу: " + Mathf.RoundToInt(progress.Value * 100) + "%";
            if (hint.Length > 0) GUI.Box(new Rect(Screen.width * .5f - 300, Screen.height - 155, 600, 32), hint);
            if (IsEating) GUI.Box(new Rect(Screen.width * .5f - 300, Screen.height - 155, 600, 32), "Едим рыбу… " + Mathf.CeilToInt((1f - eatProgress.Value) * 3f) + " с");
        }
    }
}
