#if UNITY_EDITOR || PIRATESLOP_UPPER_DESTRUCTION_V2
using System.Collections.Generic;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkWeapon
    {
        readonly Dictionary<int, Queue<double>> upperDeadlines = new();
        double incomingUpperDeadline;
        public bool AddUpperItem(InventoryItem item, double deadline)
        {
            incomingUpperDeadline = deadline;
            try { return AddItem(item); }
            finally { incomingUpperDeadline = 0d; }
        }
        void RecordUpperItem(int slot, InventoryItem item)
        {
            if (item != InventoryItem.Cannon && item != InventoryItem.MortarKit) return;
            if (!upperDeadlines.TryGetValue(slot, out var queue)) upperDeadlines[slot] = queue = new Queue<double>();
            queue.Enqueue(incomingUpperDeadline);
        }
        public double UpperDeadline(int slot, int offset = 0)
        {
            if (!upperDeadlines.TryGetValue(slot, out var queue)) return 0d;
            foreach (var value in queue) if (offset-- == 0) return value;
            return 0d;
        }
        void ConsumeUpperItem(int slot, int amount)
        {
            if (!upperDeadlines.TryGetValue(slot, out var queue)) return;
            while (amount-- > 0 && queue.Count > 0) queue.Dequeue();
            if (queue.Count == 0) upperDeadlines.Remove(slot);
        }
    }

    public sealed partial class NetworkFish
    {
        public double UpperDisabledUntil { get; set; }
    }
}
#endif
