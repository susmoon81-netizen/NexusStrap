using NexusStrap.Enums;
using NexusStrap.RobloxInterfaces;

namespace NexusStrap.Models
{
    /// <summary>
    /// A snapshot of Roblox's live client flag list, used to answer three questions the
    /// editor can't answer on its own: does this flag still exist, what type does it want,
    /// and is this value just the default Roblox already ships.
    /// </summary>
    /// <remarks>
    /// Fetched once from clientsettingscdn and cached for the process lifetime. Everything
    /// here degrades to a no-op when the fetch failed, so the editor has to keep working
    /// offline - <see cref="IsLoaded"/> tells callers whether the answers can be trusted.
    /// </remarks>
    public static class FastFlagCatalog
    {
        private static readonly SemaphoreSlim _loadLock = new(1, 1);

        private static Dictionary<string, string>? _flags;

        /// <summary>
        /// False when the live list is unavailable. Callers must not present anything as
        /// "unknown" or "default" until this is true.
        /// </summary>
        public static bool IsLoaded => _flags is not null;

        /// <summary>
        /// Names Roblox currently publishes. Empty until the catalog loads. Note this is a
        /// subset: most gameplay flags never appear here, which is why absence proves nothing.
        /// </summary>
        public static IReadOnlyCollection<string> Names =>
            _flags is null
                ? Array.Empty<string>()
                : _flags.Keys;

        public static async Task<bool> LoadAsync(string channel = Deployment.DefaultChannel)
        {
            if (_flags is not null)
                return true;

            await _loadLock.WaitAsync();

            try
            {
                if (_flags is not null)
                    return true;

                var fetched = await ApplicationSettings
                    .GetSettings("PCDesktopClient", channel)
                    .GetAllFlagsAsync();

                if (fetched.Count == 0)
                {
                    App.Logger.WriteLine("FastFlagCatalog::LoadAsync", "Roblox returned an empty flag list");
                    return false;
                }

                _flags = fetched;

                App.Logger.WriteLine("FastFlagCatalog::LoadAsync", $"Loaded {fetched.Count} live flags for channel '{channel}'");

                return true;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("FastFlagCatalog::LoadAsync", ex);
                return false;
            }
            finally
            {
                _loadLock.Release();
            }
        }

        /// <summary>
        /// True when Roblox still ships this flag. Always false while <see cref="IsLoaded"/>
        /// is false, so callers should check that first rather than reporting unknown flags.
        /// </summary>
        public static bool Exists(string name) =>
            _flags is not null && _flags.ContainsKey(name);

        public static bool TryGetDefault(string name, out string defaultValue)
        {
            if (_flags is not null && _flags.TryGetValue(name, out string? value))
            {
                defaultValue = value ?? string.Empty;
                return true;
            }

            defaultValue = string.Empty;
            return false;
        }

        /// <summary>
        /// True when the value is what Roblox already ships, which means setting it is a
        /// no-op and it can be pruned without changing behaviour.
        /// </summary>
        public static bool MatchesDefault(string name, string value) =>
            TryGetDefault(name, out string defaultValue) &&
            string.Equals(defaultValue, value, StringComparison.OrdinalIgnoreCase);

        public static FlagValueKind InferKind(string name)
        {
            if (name.StartsWith("FInt", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("DFInt", StringComparison.OrdinalIgnoreCase))
                return FlagValueKind.Int;

            if (name.StartsWith("FFlag", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("DFFlag", StringComparison.OrdinalIgnoreCase))
                return FlagValueKind.Bool;

            if (name.StartsWith("FString", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("DFString", StringComparison.OrdinalIgnoreCase))
                return FlagValueKind.String;

            return FlagValueKind.Unknown;
        }

        /// <summary>
        /// Returns null when the value is safe to write, otherwise a human readable reason.
        /// Only flags whose kind can be inferred are checked, so anything unfamiliar or
        /// simply unknown to us passes through untouched rather than being wrongly blocked.
        /// </summary>
        public static string? ValidateValue(string name, string value)
        {
            switch (InferKind(name))
            {
                case FlagValueKind.Bool:
                    return bool.TryParse(value, out _)
                        ? null
                        : $"'{name}' is a boolean flag, so it needs True or False - not '{value}'.";

                case FlagValueKind.Int:
                    return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
                        ? null
                        : $"'{name}' is an integer flag, so it needs a whole number - not '{value}'.";

                default:
                    return null;
            }
        }

        /// <summary>
        /// Live flag names matching a partial query, ranked starts-with first, then earliest
        /// match position, then shortest - same ordering the editor already uses for its own
        /// flags so suggestions read consistently.
        /// </summary>
        public static IEnumerable<string> Search(string query, int limit = 1)
        {
            if (_flags is null || string.IsNullOrWhiteSpace(query))
                yield break;

            foreach (string name in _flags.Keys
                .Where(name => name.Contains(query, StringComparison.OrdinalIgnoreCase))
                .OrderBy(name => !name.StartsWith(query, StringComparison.OrdinalIgnoreCase))
                .ThenBy(name => name.IndexOf(query, StringComparison.OrdinalIgnoreCase))
                .ThenBy(name => name.Length)
                .Take(limit))
            {
                yield return name;
            }
        }
    }
}