using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Collections.ObjectModel;

using Wpf.Ui.Mvvm.Contracts;
using NexusStrap.UI.Elements.Dialogs;
using Microsoft.Win32;
using System.Windows.Media.Animation;
using System.Windows.Input;
using NexusStrap.UI.ViewModels.Dialogs;
using System.Windows.Media;

namespace NexusStrap.UI.Elements.Settings.Pages
{
    /// <summary>
    /// Interaction logic for FastFlagEditorPage.xaml
    /// </summary>
    public partial class FastFlagEditorPage
    {
        private readonly ObservableCollection<FastFlag> _fastFlagList = new();

        private bool _showPresets = true;
        private string _searchFilter = string.Empty;
        private string _lastSearch = string.Empty;
        private DateTime _lastSearchTime = DateTime.MinValue;
        private const int _debounceDelay = 70;

        private bool _catalogRequested = false;

        private bool LoadShowPresetColumnSetting()
        {
            return App.Settings.Prop.ShowPresetColumn;
        }

        private void FastFlagEditorPage_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (e.Key == Key.Z)
                {
                    App.FastFlags.Undo();
                    ReloadList();
                    e.Handled = true;
                }
                else if (e.Key == Key.Y)
                {
                    App.FastFlags.Redo();
                    ReloadList();
                    e.Handled = true;
                }
            }
        }

        public FastFlagEditorPage()
        {
            InitializeComponent();

            AdvancedSettingViewModel.ShowPresetColumnChanged += (_, _) =>
            {
                Dispatcher.Invoke(() =>
                {
                    PresetColumn.Visibility = LoadShowPresetColumnSetting()
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                });
            };

            AdvancedSettingViewModel.ShowFlagCountChanged += (_, _) =>
            {
                Dispatcher.Invoke(UpdateTotalFlagsCount);
            };

            SetDefaultStates();
            App.RichPresence?.SetPage("FastFlag Editor");
        }

        private void SetDefaultStates()
        {
            TogglePresetsButton.IsChecked = true;
        }

        public void ReloadList()
        {
            PresetColumn.Visibility = LoadShowPresetColumnSetting() ? Visibility.Visible : Visibility.Collapsed;

            bool catalogLoaded = FastFlagCatalog.IsLoaded;
            StatusColumn.Visibility = catalogLoaded ? Visibility.Visible : Visibility.Collapsed;

            _fastFlagList.Clear();

            var presetFlags = FastFlagManager.PresetFlags.Values;

            foreach (var pair in App.FastFlags.Prop.OrderBy(x => x.Key))
            {
                if (!_showPresets && presetFlags.Contains(pair.Key))
                    continue;

                if (!pair.Key.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase))
                    continue;

                string value = pair.Value?.ToString() ?? string.Empty;

                // Preset rows are stored under friendly names like "Rendering.RemoveGrass1",
                // which Roblox has never heard of. Resolve them to the real flag so the
                // status column reflects reality instead of calling every preset Unknown.
                string lookupName = FastFlagManager.PresetFlags.TryGetValue(pair.Key, out string? realFlag)
                    ? realFlag
                    : pair.Key;

                var entry = new FastFlag
                {
                    Name = pair.Key,
                    Value = value,
                    Preset = presetFlags.Contains(pair.Key)
                        ? "pack://application:,,,/Resources/Checkmark.ico" // lowkey need better icons because
                        : "pack://application:,,,/Resources/CrossMark.ico", // these ones are actually horrid
                    IsKnown = !catalogLoaded || FastFlagCatalog.Exists(lookupName) || FastFlagCatalog.InferKind(lookupName) != FlagValueKind.Unknown,
                    IsDefault = catalogLoaded && FastFlagCatalog.MatchesDefault(lookupName, value)
                };

                _fastFlagList.Add(entry);
            }

            if (DataGrid.ItemsSource is null)
                DataGrid.ItemsSource = _fastFlagList;

            UpdateTotalFlagsCount();
        }

        /// <summary>
        /// Pulls Roblox's live flag list in the background so the editor can tell the user
        /// which flags still exist, which values are just the shipped default, and what a
        /// flag expects to be given. Failing to load is fine - everything stays as it was.
        /// </summary>
        private async Task LoadFlagCatalogAsync()
        {
            const string LOG_IDENT = "FastFlagEditorPage::LoadFlagCatalogAsync";

            if (_catalogRequested)
                return;

            _catalogRequested = true;

            try
            {
                bool loaded = await FastFlagCatalog.LoadAsync(App.Settings.Prop.Channel);

                if (!loaded)
                {
                    App.Logger.WriteLine(LOG_IDENT, "Live flag list unavailable, skipping status column");
                    return;
                }

                Dispatcher.Invoke(ReloadList);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
            }
        }

        public string FlagCountText => $"Total flags: {_fastFlagList.Count}";

        public void UpdateTotalFlagsCount()
        {
            // Get current flags count from the DataGrid's ItemsSource, safely
            int count = 0;
            if (DataGrid.ItemsSource is IEnumerable<FastFlag> flags)
                count = flags.Count();

            // Update the TextBlock text with the count
            TotalFlagsTextBlock.Text = $"Total Flags: {count}";

            // Toggle visibility based on the user setting
            TotalFlagsTextBlock.Visibility = App.Settings.Prop.ShowFlagCount
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is INavigationWindow window)
                window.Navigate(typeof(FastFlagsPage));
        }

        private void ClearSearch(bool refresh = true)
        {
            SearchTextBox.Text = "";
            _searchFilter = "";

            if (refresh)
                ReloadList();
        }

        private async void ShowAddDialog()
        {
            App.RichPresence?.SetDialog("Add FastFlag");

            var dialog = new AddFastFlagDialog();
            dialog.ShowDialog();

            App.RichPresence?.ClearDialog();

            if (dialog.Result != MessageBoxResult.OK)
                return;

            if (dialog.Tabs.SelectedIndex == 0)
                AddSingle(dialog.FlagNameTextBox.Text.Trim(), dialog.FlagValueComboBox.Text);
            else if (dialog.Tabs.SelectedIndex == 1)
                _ = ImportJSON(dialog.JsonTextBox.Text);
        }

        private void AdvancedSettings_Click(object sender, RoutedEventArgs e)
        {
            App.RichPresence?.SetDialog("Advanced Settings");

            var dialog = new AdvancedSettingsDialog();
            dialog.Owner = Window.GetWindow(this);

            dialog.SettingsSaved += (_, _) =>
            {
                if (Window.GetWindow(this) is MainWindow mainWindow)
                {
                    mainWindow.SettingsSavedSnackbar.Show();
                }
            };

            dialog.ShowDialog();

            App.RichPresence?.ClearDialog();
        }

        private void ShowProfilesDialog()
        {
            App.RichPresence?.SetDialog("Profiles");

            var dialog = new FlagProfilesDialog();
            dialog.ShowDialog();

            App.RichPresence?.ClearDialog();

            if (dialog.Result != MessageBoxResult.OK)
                return;

            if (dialog.Tabs.SelectedIndex == 0)
                App.FastFlags.SaveProfile(dialog.SaveProfile.Text);
            else if (dialog.Tabs.SelectedIndex == 1)
            {
                if (dialog.LoadProfile.SelectedValue == null)
                    return;
                App.FastFlags.LoadProfile(dialog.LoadProfile.SelectedValue.ToString(), dialog.ClearFlags.IsChecked);
            }

            Thread.Sleep(1000);
            ReloadList();
        }

        private void AddSingle(string name, string value)
        {
            if (string.IsNullOrWhiteSpace(name))
                return;

            string? invalid = FastFlagCatalog.ValidateValue(name, value);

            if (invalid is not null)
            {
                Frontend.ShowMessageBox(invalid, MessageBoxImage.Warning);
                return;
            }

            FastFlag? entry;

            if (App.FastFlags.GetValue(name) is null)
            {
                entry = new FastFlag
                {
                    Name = name,
                    Value = value,
                    Preset = FastFlagManager.PresetFlags.Values.Contains(name)
                        ? "pack://application:,,,/Resources/Checkmark.ico"
                        : "pack://application:,,,/Resources/CrossMark.ico",
                    IsKnown = !FastFlagCatalog.IsLoaded || FastFlagCatalog.Exists(name) || FastFlagCatalog.InferKind(name) != FlagValueKind.Unknown,
                    IsDefault = FastFlagCatalog.IsLoaded && FastFlagCatalog.MatchesDefault(name, value)
                };

                if (!name.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase))
                    ClearSearch();

                App.FastFlags.SetValue(entry.Name, entry.Value);
                _fastFlagList.Add(entry);
            }
            else
            {
                Frontend.ShowMessageBox(Strings.Menu_FastFlagEditor_AlreadyExists, MessageBoxImage.Information);

                bool refresh = false;

                if (!_showPresets && FastFlagManager.PresetFlags.Values.Contains(name))
                {
                    TogglePresetsButton.IsChecked = true;
                    _showPresets = true;
                    refresh = true;
                }

                if (!name.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase))
                {
                    ClearSearch(false);
                    refresh = true;
                }

                if (refresh)
                    ReloadList();

                entry = _fastFlagList.FirstOrDefault(x => x.Name == name);
            }

            DataGrid.SelectedItem = entry;
            DataGrid.ScrollIntoView(entry);
            UpdateTotalFlagsCount();
        }

        private async Task ImportJSON(string json)
        {
            Dictionary<string, object>? list = null;

            json = json.Trim();

            if (!json.StartsWith('{'))
                json = '{' + json;

            if (!json.EndsWith('}'))
            {
                int lastIndex = json.LastIndexOf('}');

                if (lastIndex == -1)
                    json += '}';
                else
                    json = json.Substring(0, lastIndex + 1);
            }

            try
            {
                var options = new JsonSerializerOptions
                {
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                };

                list = JsonSerializer.Deserialize<Dictionary<string, object>>(json, options);

                if (list is null)
                    throw new Exception("JSON deserialization returned null");
            }
            catch (Exception ex)
            {
                Frontend.ShowMessageBox(
                    string.Format(Strings.Menu_FastFlagEditor_InvalidJSON, ex.Message),
                    MessageBoxImage.Error
                );

                ShowAddDialog();

                return;
            }

            var importable = new List<KeyValuePair<string, string>>();
            var invalid = new List<string>();

            foreach (var pair in list)
            {
                if (pair.Value is null)
                    continue;

                var val = pair.Value.ToString();

                if (val is null)
                    continue;

                string? problem = FastFlagCatalog.ValidateValue(pair.Key, val);

                if (problem is not null)
                    invalid.Add(problem);
                else
                    importable.Add(new KeyValuePair<string, string>(pair.Key, val));
            }

            // Validate before touching any state, so backing out here leaves the flag set alone.
            if (invalid.Count > 0)
            {
                string details = string.Join("\n", invalid.Take(10));

                if (invalid.Count > 10)
                    details += $"\n...and {invalid.Count - 10} more.";

                var result = Frontend.ShowMessageBox(
                    $"{invalid.Count} of the flags being imported have a value that doesn't match what Roblox expects for that flag name:\n\n{details}\n\nImport the other {importable.Count} flag(s) anyway?",
                    MessageBoxImage.Warning,
                    MessageBoxButton.YesNo);

                if (result != MessageBoxResult.Yes)
                {
                    ShowAddDialog();
                    return;
                }
            }

            App.FastFlags.suspendUndoSnapshot = true;
            App.FastFlags.SaveUndoSnapshot();

            var conflictingFlags = App.FastFlags.Prop.Where(x => importable.Any(p => p.Key == x.Key)).Select(x => x.Key);
            bool overwriteConflicting = false;

            if (conflictingFlags.Any())
            {
                int count = conflictingFlags.Count();

                string message = string.Format(
                    Strings.Menu_FastFlagEditor_ConflictingImport,
                    count,
                    string.Join(", ", conflictingFlags.Take(25))
                );

                if (count > 25)
                    message += "...";

                var result = Frontend.ShowMessageBox(message, MessageBoxImage.Question, MessageBoxButton.YesNo);

                overwriteConflicting = result == MessageBoxResult.Yes;
            }

            foreach (var pair in importable)
            {
                if (App.FastFlags.Prop.ContainsKey(pair.Key) && !overwriteConflicting)
                    continue;

                App.FastFlags.SetValue(pair.Key, pair.Value);
            }

            App.FastFlags.suspendUndoSnapshot = false;

            ClearSearch();
        }

        private void Editor_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);

                if (files.All(f => f.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
                                   f.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)))
                {
                    DragOverlay.Visibility = Visibility.Visible;
                    e.Effects = DragDropEffects.Copy;
                    e.Handled = true;
                    return;
                }
            }

            DragOverlay.Visibility = Visibility.Collapsed;
            e.Effects = DragDropEffects.None;
            e.Handled = true;
        }

        private void Editor_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);

                if (files.All(f => f.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
                                   f.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)))
                {
                    e.Effects = DragDropEffects.Copy;
                    e.Handled = true;
                    return;
                }
            }

            e.Effects = DragDropEffects.None;
            e.Handled = true;
        }

        private void Editor_DragLeave(object sender, DragEventArgs e)
        {
            DragOverlay.Visibility = Visibility.Collapsed;
            e.Handled = true;
        }

        private async void Editor_Drop(object sender, DragEventArgs e)
        {
            DragOverlay.Visibility = Visibility.Collapsed;

            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
                return;

            var files = (string[])e.Data.GetData(DataFormats.FileDrop);

            foreach (var file in files)
            {
                if (!file.EndsWith(".json", StringComparison.OrdinalIgnoreCase) &&
                    !file.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                {
                    Frontend.ShowMessageBox(
                        $"Invalid file type: {Path.GetFileName(file)}\nOnly .json and .txt are supported.",
                        MessageBoxImage.Error
                    );
                    continue;
                }

                try
                {
                    string content = File.ReadAllText(file);
                    await ImportJSON(content);
                }
                catch (Exception ex)
                {
                    Frontend.ShowMessageBox(
                        $"Failed to import \"{Path.GetFileName(file)}\": {ex.Message}",
                        MessageBoxImage.Error
                    );
                }
            }

            e.Handled = true;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            ReloadList();

            _ = LoadFlagCatalogAsync();
        }

        private void DataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.Row.DataContext is not FastFlag entry)
                return;

            if (e.EditingElement is not TextBox textbox)
                return;

            App.FastFlags.suspendUndoSnapshot = true;
            App.FastFlags.SaveUndoSnapshot();

            try
            {
                switch (e.Column.Header)
                {
                    case "Name":
                        string oldName = entry.Name;
                        string newName = textbox.Text;

                        if (newName == oldName)
                            break;

                        if (App.FastFlags.GetValue(newName) is not null)
                        {
                            Frontend.ShowMessageBox(Strings.Menu_FastFlagEditor_AlreadyExists, MessageBoxImage.Information);
                            e.Cancel = true;
                            textbox.Text = oldName;
                            break;
                        }

                        App.FastFlags.SetValue(oldName, null);
                        App.FastFlags.SetValue(newName, entry.Value);

                        if (!newName.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase))
                            ClearSearch();

                        entry.Name = newName;
                        entry.Preset = FastFlagManager.PresetFlags.Values.Contains(newName)
                            ? "pack://application:,,,/Resources/Checkmark.ico"
                            : "pack://application:,,,/Resources/CrossMark.ico";
                        break;

                    case "Value":
                        string newValue = textbox.Text;

                        string? invalid = FastFlagCatalog.ValidateValue(entry.Name, newValue);

                        if (invalid is not null)
                        {
                            Frontend.ShowMessageBox(invalid, MessageBoxImage.Warning);
                            e.Cancel = true;
                            textbox.Text = entry.Value;
                            break;
                        }

                        App.FastFlags.SetValue(entry.Name, newValue);
                        break;
                }
            }
            finally
            {
                App.FastFlags.suspendUndoSnapshot = false;
            }

            // Renaming or retyping a flag changes what Roblox thinks it is, so the status
            // column has to be rebuilt. Without the catalog there's nothing to recompute.
            if (FastFlagCatalog.IsLoaded)
                ReloadList();
            else
                UpdateTotalFlagsCount();
        }

        private void AddButton_Click(object sender, RoutedEventArgs e) => ShowAddDialog();

        private void FlagProfiles_Click(object sender, RoutedEventArgs e) => ShowProfilesDialog();

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            App.FastFlags.SaveUndoSnapshot();
            App.FastFlags.suspendUndoSnapshot = true;

            var tempList = new List<FastFlag>();

            foreach (FastFlag entry in DataGrid.SelectedItems)
                tempList.Add(entry);

            foreach (FastFlag entry in tempList)
            {
                _fastFlagList.Remove(entry);
                App.FastFlags.SetValue(entry.Name, null);
            }

            App.FastFlags.suspendUndoSnapshot = false;

            UpdateTotalFlagsCount();
        }

        private void ToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton button)
                return;

            DataGrid.Columns[0].Visibility = button.IsChecked ?? false ? Visibility.Visible : Visibility.Collapsed;

            _showPresets = button.IsChecked ?? true;
            ReloadList();
        }

        private void ExportJSONButton_Click(object sender, RoutedEventArgs e)
        {
            var flags = App.FastFlags.Prop;

            var groupedFlags = flags
                .GroupBy(kvp =>
                {
                    var match = Regex.Match(kvp.Key, @"^[A-Z]+[a-z]*");
                    return match.Success ? match.Value : "Other";
                })
                .OrderBy(g => g.Key);

            var formattedJson = new StringBuilder();
            formattedJson.AppendLine("{");

            int totalItems = flags.Count;
            int writtenItems = 0;
            int groupIndex = 0;

            foreach (var group in groupedFlags)
            {
                if (groupIndex > 0)
                    formattedJson.AppendLine();

                var sortedGroup = group
                    .OrderByDescending(kvp => kvp.Key.Length + (kvp.Value?.ToString()?.Length ?? 0));

                foreach (var kvp in sortedGroup)
                {
                    writtenItems++;
                    bool isLast = (writtenItems == totalItems);
                    string line = $"    \"{kvp.Key}\": \"{kvp.Value}\"";

                    if (!isLast)
                        line += ",";

                    formattedJson.AppendLine(line);
                }

                groupIndex++;
            }

            formattedJson.AppendLine("}");

            SaveJSONToFile(formattedJson.ToString());
        }

        private void CopyJSONButton_Click(object sender, RoutedEventArgs e)
        {
            var flags = App.FastFlags.Prop;

            var groupedFlags = flags
                .GroupBy(kvp =>
                {
                    var match = Regex.Match(kvp.Key, @"^[A-Z]+[a-z]*");
                    return match.Success ? match.Value : "Other";
                })
                .OrderBy(g => g.Key);

            var formattedJson = new StringBuilder();
            formattedJson.AppendLine("{");

            int totalItems = flags.Count;
            int writtenItems = 0;
            int groupIndex = 0;

            foreach (var group in groupedFlags)
            {
                if (groupIndex > 0)
                    formattedJson.AppendLine();

                var sortedGroup = group
                    .OrderByDescending(kvp => kvp.Key.Length + (kvp.Value?.ToString()?.Length ?? 0));

                foreach (var kvp in sortedGroup)
                {
                    writtenItems++;
                    bool isLast = (writtenItems == totalItems);
                    string line = $"    \"{kvp.Key}\": \"{kvp.Value}\"";

                    if (!isLast)
                        line += ",";

                    formattedJson.AppendLine(line);
                }

                groupIndex++;
            }

            formattedJson.AppendLine("}");
            Clipboard.SetText(formattedJson.ToString());
        }


        private void SaveJSONToFile(string json)
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json|Text files (*.txt)|*.txt",
                Title = "Save JSON or TXT File",
                FileName = "NexusStrapExport.json"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {

                    var filePath = saveFileDialog.FileName;
                    if (string.IsNullOrEmpty(Path.GetExtension(filePath)))
                    {
                        filePath += ".json";
                    }

                    File.WriteAllText(filePath, json);
                    Frontend.ShowMessageBox("JSON file saved successfully!", MessageBoxImage.Information);
                }
                catch (IOException ioEx)
                {
                    Frontend.ShowMessageBox($"Error saving file: {ioEx.Message}", MessageBoxImage.Error);
                }
                catch (UnauthorizedAccessException uaEx)
                {
                    Frontend.ShowMessageBox($"Permission error: {uaEx.Message}", MessageBoxImage.Error);
                }
                catch (Exception ex)
                {
                    Frontend.ShowMessageBox($"Unexpected error: {ex.Message}", MessageBoxImage.Error);
                }
            }
        }

        private void ShowDeleteAllFlagsConfirmation()
        {
            // Show a confirmation message box to the user
            if (Frontend.ShowMessageBox(
                "Are you sure you want to delete all flags?",
                MessageBoxImage.Warning,
                MessageBoxButton.YesNo) != MessageBoxResult.Yes)
            {
                return; // Exit if the user cancels the action
            }

            // Exit if there are no flags to delete
            if (!HasFlagsToDelete())
            {
                Frontend.ShowMessageBox(
                "There are no flags to delete.",
                MessageBoxImage.Information,
                MessageBoxButton.YesNo);
                return;
            }

            try
            {
                App.FastFlags.SaveUndoSnapshot();
                App.FastFlags.suspendUndoSnapshot = true;
                _fastFlagList.Clear();

                foreach (var key in App.FastFlags.Prop.Keys.ToList())
                {
                    App.FastFlags.SetValue(key, null);
                }
                App.FastFlags.suspendUndoSnapshot = false;

                ReloadList();
            }
            catch (Exception ex)
            {
                HandleError(ex);
            }
        }
        private bool HasFlagsToDelete()
        {
            return _fastFlagList.Any() || App.FastFlags.Prop.Any();
        }

        private void HandleError(Exception ex)
        {
            // Display and log the error message
            Frontend.ShowMessageBox($"An error occurred while deleting flags:\n{ex.Message}", MessageBoxImage.Error, MessageBoxButton.OK);
            LogError(ex); // Logging error in a centralized method
        }

        private void LogError(Exception ex)
        {
            // Detailed logging for developers
            Console.WriteLine(ex.ToString());
        }

        private void DeleteAllButton_Click(object sender, RoutedEventArgs e) => ShowDeleteAllFlagsConfirmation();

        private CancellationTokenSource? _searchCancellationTokenSource;

        private async void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is not TextBox textbox) return;

            string newSearch = textbox.Text.Trim();

            if (newSearch == _lastSearch && (DateTime.Now - _lastSearchTime).TotalMilliseconds < _debounceDelay)
                return;

            _searchCancellationTokenSource?.Cancel();
            _searchCancellationTokenSource = new CancellationTokenSource();

            _searchFilter = newSearch;
            _lastSearch = newSearch;
            _lastSearchTime = DateTime.Now;

            try
            {
                await Task.Delay(_debounceDelay, _searchCancellationTokenSource.Token);

                if (_searchCancellationTokenSource.Token.IsCancellationRequested)
                    return;

                Dispatcher.Invoke(() =>
                {
                    ReloadList();
                    ShowSearchSuggestion(newSearch);
                });
            }
            catch (TaskCanceledException)
            {
            }
        }


        private void ShowSearchSuggestion(string searchFilter)
        {
            if (string.IsNullOrWhiteSpace(searchFilter))
            {
                AnimateSuggestionVisibility(0);
                return;
            }

            var bestMatch = App.FastFlags.Prop.Keys
                .Where(flag => flag.Contains(searchFilter, StringComparison.OrdinalIgnoreCase))
                .OrderBy(flag => !flag.StartsWith(searchFilter, StringComparison.OrdinalIgnoreCase))
                .ThenBy(flag => flag.IndexOf(searchFilter, StringComparison.OrdinalIgnoreCase))
                .ThenBy(flag => flag.Length)
                .FirstOrDefault();

            // Nothing set locally? Fall back to Roblox's live list so the user can discover
            // flags they haven't used yet instead of staring at an empty result.
            if (string.IsNullOrEmpty(bestMatch) && FastFlagCatalog.IsLoaded)
            {
                bestMatch = FastFlagCatalog
                    .Search(searchFilter, 1)
                    .FirstOrDefault(flag => !App.FastFlags.Prop.ContainsKey(flag));
            }

            if (!string.IsNullOrEmpty(bestMatch))
            {
                SuggestionKeywordRun.Text = bestMatch;
                AnimateSuggestionVisibility(1);
            }
            else
            {
                AnimateSuggestionVisibility(0);
            }
        }

        private void SuggestionTextBlock_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var suggestion = SuggestionKeywordRun.Text;

            if (string.IsNullOrEmpty(suggestion))
                return;

            // A suggestion that isn't in the flag set yet is a flag the user went looking for
            // and couldn't find - add it, seeded with whatever value Roblox ships so they can
            // see what it is before changing it.
            if (FastFlagCatalog.IsLoaded && App.FastFlags.GetValue(suggestion) is null)
            {
                FastFlagCatalog.TryGetDefault(suggestion, out string defaultValue);

                AddSingle(suggestion, defaultValue);
                return;
            }

            SearchTextBox.Text = suggestion;
            SearchTextBox.CaretIndex = suggestion.Length;
        }

        private void AnimateSuggestionVisibility(double targetOpacity)
        {
            var opacityAnimation = new DoubleAnimation
            {
                To = targetOpacity,
                Duration = TimeSpan.FromMilliseconds(120),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };

            var translateAnimation = new DoubleAnimation
            {
                To = targetOpacity > 0 ? 0 : 10,
                Duration = TimeSpan.FromMilliseconds(120),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };

            opacityAnimation.Completed += (s, e) =>
            {
                if (targetOpacity == 0)
                {
                    SuggestionTextBlock.Visibility = Visibility.Collapsed;
                }
            };

            if (targetOpacity > 0)
                SuggestionTextBlock.Visibility = Visibility.Visible;

            SuggestionTextBlock.BeginAnimation(UIElement.OpacityProperty, opacityAnimation);
            SuggestionTranslateTransform.BeginAnimation(TranslateTransform.XProperty, translateAnimation);
        }
    }
}