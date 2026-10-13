using System.IO;
using System.Runtime.InteropServices;
using System.Windows;

namespace NexusStrap.Utility
{
    internal static class Shortcut
    {
        // Silent — shortcuts are best-effort, no popup on first install

        [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
        private static extern void SHChangeNotify(int eventId, int flags, IntPtr item1, IntPtr item2);

        private const int SHCNE_ASSOCCHANGED = 0x08000000;
        private const int SHCNF_IDLIST = 0x0000;

        public static void Create(string exePath, string exeArgs, string lnkPath, bool showErrorDialog = true)
        {
            const string LOG_IDENT = "Shortcut::Create";

            bool created = false;

            try
            {
                if (File.Exists(lnkPath))
                    return;

                ShellLink.Shortcut.CreateShortcut(exePath, exeArgs, exePath, 0).WriteToFile(lnkPath);
                created = true;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Failed to create a shortcut for {lnkPath}!");
                App.Logger.WriteException(LOG_IDENT, ex);
                // Silent — log only, no warning dialog (fixes first-install popup)
            }

            if (created)
            {
                try { SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero); }
                catch { }
            }
        }
    }
}
