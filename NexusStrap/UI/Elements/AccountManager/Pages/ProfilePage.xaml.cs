using NexusStrap.UI.ViewModels.AccountManagers;

namespace NexusStrap.UI.Elements.AccountManagers.Pages
{
    /// <summary>
    /// Interaction logic for ProfilePage.xaml
    /// </summary>
    public partial class ProfilePage
    {
        private readonly ProfileViewModel _viewModel;

        public ProfilePage()
        {
            _viewModel = new ProfileViewModel();
            DataContext = _viewModel;
            InitializeComponent();
            Loaded += (_, _) => RefreshFromSelection();
        }

        private void RefreshFromSelection()
        {
            long userId = ProfileSelection.UserId
                ?? AccountsViewModel.GetActiveUserId()
                ?? 0;

            if (userId <= 0)
                return;

            // Avoid refetching when already showing this profile.
            if (userId == _viewModel.UserId && _viewModel.UserId != 0)
                return;

            _ = _viewModel.LoadAsync(userId);
        }
    }
}
