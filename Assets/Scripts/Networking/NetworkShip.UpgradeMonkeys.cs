using System.Collections.Generic;
using FishNet.Object.Synchronizing;
using PirateSlop.Ships;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkShip
    {
        readonly SyncList<ShipMonkeyPose> extraMonkeyPoses = new();
        readonly SyncList<bool> extraMonkeyVisible = new();
        readonly List<ShipMonkey> extraMonkeys = new();
        float nextExtraMonkeyPublish;
        public bool AddUpgradeMonkey()
        {
            if (!IsServerInitialized || !IsSpawned || IsSinking || monkey == null || monkey.Visual == null) return false;
            var extra = CreateUpgradeMonkey();
            if (extra == null) return false;
            extra.Begin();
            extraMonkeyPoses.Add(extra.Capture());
            extraMonkeyVisible.Add(true);
            return true;
        }
        ShipMonkey CreateUpgradeMonkey()
        {
            if (monkey == null || monkey.Visual == null) return null;
            var holder = new GameObject("UpgradeShipMonkey");
            holder.transform.SetParent(transform, false);
            var extra = holder.AddComponent<ShipMonkey>();
            foreach (var field in typeof(ShipMonkey).GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public))
                if (!field.IsInitOnly) field.SetValue(extra, field.GetValue(monkey));
            extra.Visual = Instantiate(monkey.Visual.gameObject, holder.transform).transform;
            extra.Visual.gameObject.SetActive(true);
            extra.Animator = extra.Visual.GetComponentInChildren<Animator>();
            foreach (var hitbox in extra.Visual.GetComponentsInChildren<ShipMonkeyHitbox>()) hitbox.Monkey = extra;
            var choices = new List<int>();
            for (int i = 0; i < extra.Nodes.Length; i++)
                if (extra.Nodes[i].Available && extra.Nodes[i].Surface == ShipMonkeySurface.Deck) choices.Add(i);
            if (choices.Count > 0) extra.StartNode = choices[Random.Range(0, choices.Count)];
            extraMonkeys.Add(extra);
            return extra;
        }
        void TickUpgradeMonkeys()
        {
            if (monkey == null) return;
            if (!IsServerInitialized)
            {
                if (!IsClientInitialized) return;
                while (extraMonkeys.Count < extraMonkeyPoses.Count) if (CreateUpgradeMonkey() == null) return;
                for (int i = 0; i < extraMonkeys.Count && i < extraMonkeyPoses.Count; i++)
                {
                    if (i < extraMonkeyVisible.Count && !extraMonkeyVisible[i]) { extraMonkeys[i].Visual.gameObject.SetActive(false); continue; }
                    extraMonkeys[i].Receive(extraMonkeyPoses[i]); extraMonkeys[i].PresentRemote();
                }
                return;
            }
            foreach (var extra in extraMonkeys)
                if (!IsSinking) extra.Simulate(Time.deltaTime); else extra.StopActivities();
            if (Time.unscaledTime < nextExtraMonkeyPublish) return;
            nextExtraMonkeyPublish = Time.unscaledTime + .1f;
            for (int i = 0; i < extraMonkeys.Count; i++)
            {
                extraMonkeyPoses[i] = extraMonkeys[i].Capture();
                extraMonkeyVisible[i] = extraMonkeys[i].Visual.gameObject.activeSelf;
            }
        }
    }
}
