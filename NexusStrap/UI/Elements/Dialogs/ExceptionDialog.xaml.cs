using System.Media;
using System.Windows;
using System.Windows.Interop;

using Windows.Win32;
using Windows.Win32.Foundation;

namespace NexusStrap.UI.Elements.Dialogs
{
    // hmm... do i use MVVM for this?
    // this is entirely static, so i think im fine without it, and this way is just so much more efficient

    /// <summary>
    /// Interaction logic for ExceptionDialog.xaml
    /// </summary>
    public partial class ExceptionDialog
    {
        const int MAX_GITHUB_URL_LENGTH = 8192;

        public ExceptionDialog(Exception exception)
        {
            InitializeComponent();

            App.RichPresence?.SetDialog("Exception");

            AddException(exception);

            if (!App.Logger.Initialized)
                LocateLogFileButton.Content = Strings.Dialog_Exception_CopyLogContents;

            // There is no issue tracker anymore, so "Report" hands off to Discord and the
            // log travels with the user's clipboard rather than a pre-filled issue body.
            string issueUrl = App.DiscordInviteLink;

            string helpMessage = String.Format(Strings.Dialog_Exception_Info_2_Alt, App.ProjectWikiLink);

            HelpMessageMDTextBlock.MarkdownText = helpMessage;
            VersionText.Text = String.Format(Strings.Dialog_Exception_Version, App.Version);

            ReportExceptionButton.Click += (_, _) =>
            {
                try { Clipboard.SetDataObject(App.Logger.AsDocument); } catch { }
                Utilities.ShellExecute(issueUrl);
            };

            LocateLogFileButton.Click += delegate
            {
                if (App.Logger.Initialized && !String.IsNullOrEmpty(App.Logger.FileLocation))
                    Utilities.ShellExecute(App.Logger.FileLocation);
                else
                    Clipboard.SetDataObject(App.Logger.AsDocument);
            };

            CloseButton.Click += delegate
            {
                Close();
            };

            SystemSounds.Hand.Play();

            Loaded += delegate
            {
                IntPtr hWnd = new WindowInteropHelper(this).Handle;
                PInvoke.FlashWindow((HWND)hWnd, true);
            };
        }

        private void AddException(Exception exception, bool inner = false)
        {
            if (!inner)
                ErrorRichTextBox.Selection.Text = $"{exception.GetType()}: {exception.Message}";

            if (exception.InnerException is null)
                return;

            ErrorRichTextBox.Selection.Text += $"\n\n[Inner Exception]\n{exception.InnerException.GetType()}: {exception.InnerException.Message}";

            AddException(exception.InnerException, true);
        }
    }
}
