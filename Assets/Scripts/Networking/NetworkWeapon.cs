using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed class NetworkWeapon : NetworkBehaviour
    {
        readonly SyncVar<bool> loaded = new(true);
        readonly SyncVar<bool> reloading = new(false);
        PirateWeapon weapon;
        void Awake() => weapon = GetComponent<PirateWeapon>();
        public void Request(byte action, Vector3 direction) => ActionServerRpc(action,direction);
        [ServerRpc] void ActionServerRpc(byte action, Vector3 direction)
        { weapon.TickAuthority(); weapon.Act(action,direction); loaded.Value = weapon.Loaded; reloading.Value = weapon.Reloading; }
        void Update()
        {
            if (IsServerInitialized) { weapon.TickAuthority(); loaded.Value = weapon.Loaded; reloading.Value = weapon.Reloading; }
            else if (IsClientInitialized) weapon.SetState(loaded.Value,reloading.Value);
        }
        public void PublishAttack(byte action,Vector3 end) => AttackObserversRpc(action,end);
        public void PublishShot(Vector3 origin,Vector3 velocity) => ShotObserversRpc(origin,velocity);
        [ObserversRpc] void ShotObserversRpc(Vector3 origin,Vector3 velocity)
        { if(!IsServerInitialized)weapon.SpawnBullet(origin,velocity,false); }
        [ObserversRpc] void AttackObserversRpc(byte action,Vector3 end) => weapon.ShowAttack(action,end);
    }
}
