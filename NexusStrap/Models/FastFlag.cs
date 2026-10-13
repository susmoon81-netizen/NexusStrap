namespace NexusStrap.Models
{
    public class FastFlag
    {
        // public bool Enabled { get; set; }
        public string Preset { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string Value { get; set; } = null!;

        /// <summary>
        /// True when Roblox publishes this flag in its client settings for the current
        /// channel. NOTE: absence does not mean the flag was removed - the public settings
        /// endpoint only returns a subset, and plenty of perfectly working flags are gated
        /// out of it. Treat false as "we can't confirm this", never as "this is dead".
        /// </summary>
        public bool IsKnown { get; set; } = true;

        /// <summary>
        /// True when the value is the one Roblox already ships, so the flag does nothing.
        /// </summary>
        public bool IsDefault { get; set; }
    }
}