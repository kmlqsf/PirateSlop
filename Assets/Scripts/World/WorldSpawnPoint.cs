using UnityEngine;

namespace PirateSlop.World
{
    public sealed class WorldSpawnPoint : MonoBehaviour
    {
        public string Id, Tag;
        void OnDrawGizmosSelected() { Gizmos.color = Tag == "ship_spawn" ? Color.cyan : Color.yellow; Gizmos.DrawWireSphere(transform.position, 1); }
    }
}
