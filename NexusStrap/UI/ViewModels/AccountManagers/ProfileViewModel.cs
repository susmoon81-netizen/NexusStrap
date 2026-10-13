using NexusStrap.Integrations;
using NexusStrap.Models.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows;

namespace NexusStrap.UI.ViewModels.AccountManagers
{
    /// <summary>
    /// Shared selection: list pages set it, then navigate to the Profile page.
    /// </summary>
    public static class ProfileSelection
    {
        public static long? UserId { get; private set; }
        public static FriendInfo? Prefill { get; private set; }

        public static void Select(long userId, FriendInfo? prefill = null)
        {
            UserId = userId;
            Prefill = prefill is not null && prefill.Id == userId ? prefill : null;
        }

        public static void Clear()
        {
            UserId = null;
            Prefill = null;
        }
    }

    public partial class ProfileViewModel : ObservableObject
    {
        private const string LOG_IDENT = "ProfileViewModel";

        [ObservableProperty]
        private bool _isLoading = false;

        [ObservableProperty]
        private string _loadError = "";

        [ObservableProperty]
        private long _userId;

        [ObservableProperty]
        private string _avatarUrl = "";

        [ObservableProperty]
        private string _displayName = "—";

        [ObservableProperty]
        private string _username = "";

        [ObservableProperty]
        private string _presenceText = "—";

        [ObservableProperty]
        private string _presenceColor = "#808080";

        [ObservableProperty]
        private string _locationText = "";

        [ObservableProperty]
        private bool _isInGame = false;

        [ObservableProperty]
        private string _gameName = "";

        [ObservableProperty]
        private int _friendsCount;

        [ObservableProperty]
        private int _followersCount;

        [ObservableProperty]
        private int _followingCount;

        [ObservableProperty]
        private bool _hasCounts = false;

        public string UserIdText => UserId > 0 ? UserId.ToString() : "—";

        public ProfileViewModel()
        {
            if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(new DependencyObject()))
                return;
        }

        public async Task LoadAsync(long userId)
        {
            if (userId <= 0)
            {
                LoadError = "Select a friend or an account to view their profile.";
                return;
            }

            UserId = userId;
            OnPropertyChanged(nameof(UserIdText));
            IsLoading = true;
            LoadError = "";

            try
            {
                // Instant paint from the friends list prefill, then real data.
                if (ProfileSelection.Prefill is { } pre && pre.Id == userId)
                {
                    DisplayName = pre.DisplayName;
                    AvatarUrl = pre.AvatarUrl ?? "";
                    PresenceText = PresenceLabel(pre.PresenceType);
                    PresenceColor = StatusColor(pre.PresenceType);
                    LocationText = pre.LastLocation;
                    IsInGame = pre.PresenceType == 2;
                    GameName = pre.PlayingGameName;
                }

                var detailsTask = UserDetails.Fetch(userId);
                var countsTask = GetCountsAsync(userId);
                var presenceTask = FetchPresenceAsync(userId);

                var details = await detailsTask;
                DisplayName = details.Data.DisplayName;
                Username = $"@{details.Data.Name}";
                if (!string.IsNullOrEmpty(details.Thumbnail.ImageUrl))
                    AvatarUrl = details.Thumbnail.ImageUrl;

                var (friends, followers, following) = await countsTask;
                FriendsCount = friends;
                FollowersCount = followers;
                FollowingCount = following;
                HasCounts = true;

                var presence = await presenceTask;
                if (presence is not null)
                {
                    PresenceText = PresenceLabel(presence.UserPresenceType);
                    PresenceColor = StatusColor(presence.UserPresenceType);
                    IsInGame = presence.UserPresenceType == 2;

                    if (presence.UserPresenceType == 2)
                    {
                        GameName = await ResolveGameNameAsync(presence);
                        LocationText = GameName;
                    }
                    else
                    {
                        GameName = "";
                        LocationText = presence.LastLocation ?? "";
                    }
                }
                else
                {
                    PresenceText = "Unknown";
                    PresenceColor = "#808080";
                    IsInGame = false;
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
                LoadError = $"Couldn't load this profile: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task Refresh()
        {
            ProfileSelection.Select(UserId);
            await LoadAsync(UserId);
        }

        [RelayCommand]
        private async Task JoinUser()
        {
            var account = AccountManager.Shared.ActiveAccount;
            if (account is null)
            {
                Frontend.ShowMessageBox("Please select an account first.", MessageBoxImage.Warning);
                return;
            }

            var presence = await FetchPresenceAsync(UserId);
            if (presence is null || presence.UserPresenceType != 2)
            {
                Frontend.ShowMessageBox($"{DisplayName} is not currently in a game.", MessageBoxImage.Information);
                return;
            }

            long? placeId = presence.PlaceId ?? presence.RootPlaceId;
            string? gameInstanceId = presence.GameId;

            if (!placeId.HasValue || placeId.Value == 0 || string.IsNullOrEmpty(gameInstanceId))
            {
                Frontend.ShowMessageBox($"Unable to get server information for {DisplayName}'s game.", MessageBoxImage.Warning);
                return;
            }

            try
            {
                await AccountManager.Shared.LaunchAccountAsync(account, placeId.Value, gameInstanceId);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException($"{LOG_IDENT}::JoinUser", ex);
                Frontend.ShowMessageBox($"Failed to join {DisplayName}'s game: {ex.Message}", MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void OpenRobloxPage()
        {
            if (UserId <= 0) return;

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = $"https://www.roblox.com/users/{UserId}/profile",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                App.Logger.WriteException($"{LOG_IDENT}::OpenRobloxPage", ex);
            }
        }

        [RelayCommand]
        private void CopyId()
        {
            if (UserId <= 0) return;

            try { Clipboard.SetText(UserId.ToString()); } catch { }
        }

        private async Task<Models.APIs.Roblox.UserPresence?> FetchPresenceAsync(long userId)
        {
            try
            {
                long? activeId = AccountsViewModel.GetActiveUserId();
                if (activeId is null) return null;

                string? cookie = AccountManager.Shared.GetRoblosecurityForUser(activeId.Value);
                if (string.IsNullOrEmpty(cookie)) return null;

                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                var json = JsonSerializer.Serialize(new { userIds = new[] { userId } });
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                client.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", $".ROBLOSECURITY={cookie}");

                using var response = await client.PostAsync("https://presence.roblox.com/v1/presence/users", content);
                if (!response.IsSuccessStatusCode) return null;

                var body = await response.Content.ReadAsStringAsync();
                var data = JsonSerializer.Deserialize<Models.APIs.Roblox.PresenceResponse>(body)?.UserPresences;
                return data?.FirstOrDefault(p => p.UserId == userId);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException($"{LOG_IDENT}::FetchPresence", ex);
                return null;
            }
        }

        private static async Task<(int friends, int followers, int following)> GetCountsAsync(long userId)
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

                var friendsTask = client.GetAsync($"https://friends.roblox.com/v1/users/{userId}/friends/count");
                var followersTask = client.GetAsync($"https://friends.roblox.com/v1/users/{userId}/followers/count");
                var followingTask = client.GetAsync($"https://friends.roblox.com/v1/users/{userId}/followings/count");

                await Task.WhenAll(friendsTask, followersTask, followingTask);

                async Task<int> ParseCount(Task<HttpResponseMessage> task)
                {
                    try
                    {
                        using var response = await task;
                        if (!response.IsSuccessStatusCode) return 0;
                        var content = await response.Content.ReadAsStringAsync();
                        return JsonSerializer.Deserialize<JsonElement>(content).GetProperty("count").GetInt32();
                    }
                    catch { return 0; }
                }

                return (await ParseCount(friendsTask), await ParseCount(followersTask), await ParseCount(followingTask));
            }
            catch (Exception ex)
            {
                App.Logger.WriteException($"{LOG_IDENT}::GetCounts", ex);
                return (0, 0, 0);
            }
        }

        private static async Task<string> ResolveGameNameAsync(Models.APIs.Roblox.UserPresence presence)
        {
            try
            {
                if (presence.UniverseId is long universeId && universeId != 0)
                {
                    await UniverseDetails.FetchSingle(universeId);
                    var details = UniverseDetails.LoadFromCache(universeId);
                    if (!string.IsNullOrWhiteSpace(details?.Data?.Name))
                        return details.Data.Name;
                }
            }
            catch { }

            return presence.LastLocation ?? "In Game";
        }

        private static string PresenceLabel(int type) => type switch
        {
            1 => "On Website",
            2 => "In Game",
            3 => "In Studio",
            _ => "Offline"
        };

        private static string StatusColor(int type) => type switch
        {
            1 => "#00A2FF",
            2 => "#02B757",
            3 => "#ffa500",
            _ => "#808080"
        };
    }
}
