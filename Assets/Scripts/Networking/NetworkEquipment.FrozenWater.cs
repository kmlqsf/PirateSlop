using System.Collections.Generic;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkEquipment
    {
        public struct FrozenCell
        {
            public Vector2Int Key;
            public Vector3 Point;
            public uint Until;
        }

        readonly SyncList<FrozenCell> frozenWater = new();
        readonly Dictionary<Vector2Int, FrozenWaterTile> frozenTiles = new();
        readonly List<Vector2Int> removedFrozenTiles = new();
        Vector3? lastFrozenContact;
        Vector2Int? lastFrozenKey;
        const float FrozenCellSize = 2f;

        void FreezeWater(Vector3 point, float seconds)
        {
            if (!IsServerInitialized || OceanSurface.Instance == null) return;
            if (lastFrozenContact.HasValue && Vector3.Distance(lastFrozenContact.Value, point) <= 4f)
            {
                int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(lastFrozenContact.Value, point) / .7f));
                for (int i = 1; i <= steps; i++) FreezeWaterCell(Vector3.Lerp(lastFrozenContact.Value, point, i / (float)steps), seconds);
            }
            else { lastFrozenKey = null; FreezeWaterCell(point, seconds); }
            lastFrozenContact = point;
        }

        void FreezeWaterCell(Vector3 point, float seconds, bool connect = true)
        {
            var key = new Vector2Int(Mathf.RoundToInt(point.x / FrozenCellSize), Mathf.RoundToInt(point.z / FrozenCellSize));
            if (connect && lastFrozenKey.HasValue && Mathf.Abs(lastFrozenKey.Value.x-key.x)==1 && Mathf.Abs(lastFrozenKey.Value.y-key.y)==1)
                FreezeWaterCell(new Vector3(key.x*FrozenCellSize,point.y,lastFrozenKey.Value.y*FrozenCellSize),seconds,false);
            lastFrozenKey = key;
            uint until = TimeManager.Tick + (uint)Mathf.CeilToInt(seconds / (float)TimeManager.TickDelta);
            for (int i = 0; i < frozenWater.Count; i++)
            {
                var cell = frozenWater[i];
                if (cell.Key != key) continue;
                cell.Until = until;
                frozenWater[i] = cell;
                return;
            }
            if (frozenWater.Count >= 128) return;
            var center = new Vector3(key.x * FrozenCellSize, point.y, key.y * FrozenCellSize);
            center.y = OceanSurface.Instance.SeaLevel + OceanSurface.Instance.GetWhirlpoolHeight(center) + .12f;
            foreach (var neighbor in frozenWater)
                if (Mathf.Abs(neighbor.Key.x - key.x) <= 1 && Mathf.Abs(neighbor.Key.y - key.y) <= 1) { center.y = neighbor.Point.y; break; }
            frozenWater.Add(new FrozenCell { Key = key, Point = center, Until = until });
        }

        void UpdateFrozenWater()
        {
            if (TimeManager == null) return;
            if (IsServerInitialized)
                for (int i = frozenWater.Count - 1; i >= 0; i--)
                    if (unchecked((int)(frozenWater[i].Until - TimeManager.Tick)) <= 0) frozenWater.RemoveAt(i);
            removedFrozenTiles.Clear();
            foreach (var tile in frozenTiles) removedFrozenTiles.Add(tile.Key);
            foreach (var cell in frozenWater)
            {
                float remaining = unchecked((int)(cell.Until - TimeManager.Tick)) * (float)TimeManager.TickDelta;
                if (remaining <= 0) continue;
                removedFrozenTiles.Remove(cell.Key);
                if (!frozenTiles.TryGetValue(cell.Key, out var tile) || tile == null)
                {
                    tile = FrozenWaterTile.Create(cell.Point, gameObject.scene);
                    frozenTiles[cell.Key] = tile;
                }
                tile.SetRemaining(remaining);
            }
            foreach (var key in removedFrozenTiles)
            {
                if (frozenTiles[key] != null) frozenTiles[key].Remove();
                frozenTiles.Remove(key);
            }
        }

        void ClearFrozenWater()
        {
            foreach (var tile in frozenTiles.Values) if (tile != null) tile.Remove();
            frozenTiles.Clear();
            lastFrozenContact = null;
            lastFrozenKey = null;
        }
    }
}
