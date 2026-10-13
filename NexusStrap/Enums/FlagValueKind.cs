namespace NexusStrap.Enums
{
    /// <summary>
    /// The value shape a FastFlag is expected to hold, inferred from its name prefix
    /// (FInt/DFInt, FFlag/DFFlag, FString/DFString).
    /// </summary>
    public enum FlagValueKind
    {
        Unknown,
        Bool,
        Int,
        String
    }
}