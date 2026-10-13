namespace NexusStrap.Models.Persistable
{
    public class State
    {
        public bool TestModeWarningShown { get; set; } = false;

        public bool IgnoreOutdatedChannel { get; set; } = false;

        public bool PromptWebView2Install { get; set; } = true;

        public string? LastPage { get; set; } = null!;

        public bool HasSeenGuide { get; set; } = false;

        public bool ForceReinstall { get; set; } = false;

        // Startup shortcut repair bookkeeping: true once a shortcut has been
        // handled (created, or found existing). A missing-but-handled shortcut
        // was deleted by the user on purpose and must NOT be recreated.
        public bool DesktopShortcutCreated { get; set; } = false;

        public bool StartMenuShortcutCreated { get; set; } = false;

        public bool StudioShortcutCreated { get; set; } = false;

        public WindowState SettingsWindow { get; set; } = new();

        public List<ModConfig> Mods { get; set; } = new();
    }
}