namespace PirateSlop.Networking
{
    public interface IBotRosterPolicy
    {
        int TakeInitialCount(int capacity, int occupied);
        bool ReplaceOnHumanJoin { get; }
    }

    public sealed class InitialFillBotRosterPolicy : IBotRosterPolicy
    {
        readonly bool enabled;
        bool initialized;

        public InitialFillBotRosterPolicy(bool enabled) => this.enabled = enabled;

        public bool ReplaceOnHumanJoin => enabled;

        public int TakeInitialCount(int capacity, int occupied)
        {
            if (initialized) return 0;
            initialized = true;
            return enabled ? System.Math.Max(0, capacity - occupied) : 0;
        }
    }
}
