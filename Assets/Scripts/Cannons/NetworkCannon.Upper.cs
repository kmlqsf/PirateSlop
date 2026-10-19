#if UNITY_EDITOR || PIRATESLOP_UPPER_DESTRUCTION_V2
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkCannon
    {
        public void NotifyUpperCancelled(int index)
        {
            if (!IsServerInitialized || index < 0 || index >= placements.Count) return;
            var value = placements[index];
            if (value.Fuse < 0f && !value.FireQueued && !value.Occupied) return;
            value.Fuse = -1f; value.FireQueued = false; value.Occupied = false;
            placements[index] = value;
        }

        void ReleaseBoardingForUpper(int index)
        {
            for (int i = 0; i < cables.Count; i++)
            {
                var cable = cables[i];
                if (cable.Cannon != index) continue;
                cable.Target = null; cable.Hits = 2; cables[i] = cable;
            }
            boardingFlights.Remove(index);
        }
        public bool RemoveUpperCannon(int index)
        {
            var cannon = Cannon(index);
            if (!IsServerInitialized || cannon == null) return false;
            cannon.CancelUpperFire();
            if (HasBoarding(index)) ReleaseBoardingForUpper(index);
            if (cannon.IsLoaded)
            {
                if (Crate.SpecialSupplyPrefab == null) return false;
                var ball = Instantiate(Crate.SpecialSupplyPrefab, cannon.Muzzle.position, Quaternion.identity);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(ball.gameObject, gameObject.scene);
                ball.SetAmmoItem(cannon.LoadedAmmo);
                ball.Place(null, ball.transform.position, ball.transform.rotation);
                ServerManager.Spawn(ball.NetworkObject);
                ball.Kick(GetComponent<ShipController>().CannonPointVelocity(cannon.Muzzle.position));
                cannon.ResetSupply();
            }
            var placement = placements[index];
            placement.Removed = true; placement.Loaded = false; placement.FireQueued = false; placement.Fuse = -1f;
            placements[index] = placement;
            cannon.gameObject.SetActive(false);
            return true;
        }
    }
}
#endif
