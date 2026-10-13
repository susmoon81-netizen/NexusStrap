namespace NexusStrap.Models.SettingTasks
{
    public class ShortcutTask : BoolBaseTask
    {
        private string _shortcutPath;

        private string _exeFlags;

        public ShortcutTask(string name, string lnkFolder, string lnkName, string exeFlags = "") : base("Shortcut", name)
        {
            _shortcutPath = Path.Combine(lnkFolder, lnkName);
            _exeFlags = exeFlags;

            OriginalState = File.Exists(_shortcutPath);
        }

        public override bool NewState
        {
            get => base.NewState;
            set
            {
                base.NewState = value;
            }
        }

        public override void Execute()
        {
            if (NewState)
                Shortcut.Create(Paths.Application, _exeFlags, _shortcutPath);
            else if (File.Exists(_shortcutPath))
                File.Delete(_shortcutPath);

            OriginalState = NewState;

            // Keep the startup auto-repair in sync with the explicit choice:
            // toggled off means "deleted on purpose", so never auto-recreate.
            if (Name == "Shortcut.Desktop")
            {
                App.State.Prop.DesktopShortcutCreated = true;
                App.State.Save();
            }
            else if (Name == "Shortcut.StartMenu")
            {
                App.State.Prop.StartMenuShortcutCreated = true;
                App.State.Save();
            }
        }
    }
}