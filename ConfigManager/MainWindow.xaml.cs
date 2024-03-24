using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ConfigManager
{
    public partial class MainWindow : Window
    {
        #region Events

        public delegate void CancelProcess(object sender, CancellationEventArgs e);
        public event CancelProcess? CancelProcessEvent;

        #endregion

        #region Fields

        private readonly ConfigService _configService;
        private readonly BackgroundWorker _configWorker;
        private int _selectedGoodRecords;
        private ConfigEditorArgs? _configEditor;
        
        #endregion

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            _configService = new ConfigService();
            _configWorker = (BackgroundWorker)FindResource("configBackgroundWorker");

            _configService.ReportProgressEvent += BackgroundWorker_ReportProgress;
            CancelProcessEvent += _configService.Cancel;

            hostLabel.Content = $"Host: {App.ActiveDirectoryUser.Host}";

            _selectedGoodRecords = 0;
        }

        private PluginType GetPluginType()
        {
            return pluginComboBox.SelectedIndex switch
            {
                1 => PluginType.Adjuster,
                2 => PluginType.AuditArchive,
                3 => PluginType.AutoDialer,
                4 => PluginType.BasicDBMaint,
                5 => PluginType.DBI_DailyActivityReport,
                6 => PluginType.EDI_Archive,
                7 => PluginType.Notes,
                8 => PluginType.ProfitPal,
                9 => PluginType.Reporter,
                10 => PluginType.Uploader,
                _ => PluginType.All
            };
        }

        private DateRangeType GetDateRangeType()
        {
            return dateComboBox.SelectedIndex switch
            {
                1 => DateRangeType.Today,
                2 => DateRangeType.Week,
                3 => DateRangeType.Month,
                4 => DateRangeType.Year,
                _ => DateRangeType.AllTime
            };
        }

        private void RenderData()
        {
            pluginComboBox.IsEnabled = false;
            dateComboBox.IsEnabled = false;
            submitButton.IsEnabled = false;
            cancelButton.IsEnabled = true;

            Reset();

            if (!_configWorker.IsBusy)
            {
                ConfigSearchArgs args = new(GetPluginType(), GetDateRangeType(), searchBox.Text);

                // Fire the DoWork event.
                _configWorker.RunWorkerAsync(args);
            }
        }

        private void SubmitButton_Click(object sender, RoutedEventArgs e)
        {
            RenderData();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_configWorker.WorkerSupportsCancellation)
            {
                MessageBox.Show("Cancellation is not supported.", "Oops", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }

            cancelButton.IsEnabled = false;

            _configWorker.CancelAsync();

            // Reset so we don't have a partially filled ProgressBar
            configSearchProgressBar.Value = 0;

            CancelProcessEvent?.Invoke(this, new CancellationEventArgs(cancelled: true));
        }

        private void ConfigDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                _selectedGoodRecords += GetGoodRecordCount(e.AddedItems);
            }
            
            if (e.RemovedItems.Count > 0)
            {
                _selectedGoodRecords -= GetGoodRecordCount(e.RemovedItems);
            }

            grabButton.IsEnabled = configDataGrid.SelectedIndex >= 0 && _selectedGoodRecords == 0;
            deployButton.IsEnabled = configDataGrid.SelectedIndex >= 0 && _selectedGoodRecords == 0;
            clearSelectionButton.IsEnabled = configDataGrid.SelectedIndex >= 0;

            int selected = configDataGrid.SelectedItems.Count;

            if (selected > 0)
            {
                selectedLabel.Content = $"{selected} items selected";
            }
            else
            {
                selectedLabel.Content = string.Empty;
            }

            if (_selectedGoodRecords > 0)
            {
                errorLabel.Content = "There are files selected that have no changes to deploy/grab";
            }
            else
            {
                errorLabel.Content = string.Empty;
            }
        }

        private void ClearSelectionButton_Click(object sender, RoutedEventArgs e)
        {
            configDataGrid.UnselectAll();
            selectedLabel.Content = string.Empty;
            errorLabel.Content = string.Empty;
            _selectedGoodRecords = 0;
        }

        private void BackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            if (e.Argument is ConfigSearchArgs and not null)
            {
                ConfigSearchArgs args = (ConfigSearchArgs)e.Argument ?? new();

                _configService.Search(args);
            }

            // Return a Cancel value if the event was cancelled.
            if (_configWorker.CancellationPending)
            {
                e.Cancel = true;
                return;
            }

            // Set the Result value.
            e.Result = true;
        }

        private void BackgroundWorker_ReportProgress(object sender, ProgressEventArgs e)
        {
            if (_configWorker.WorkerReportsProgress)
            {
                _configWorker.ReportProgress(e.Percentage);
            }
        }

        private void BackgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            // Update progress bar if percentage changed.
            if (e.ProgressPercentage > 0)
            {
                if (e.ProgressPercentage <= configSearchProgressBar.Maximum)
                {
                    configSearchProgressBar.Value = e.ProgressPercentage;
                }
                else
                {
                    configSearchProgressBar.Value = configSearchProgressBar.Maximum;
                }
            }
        }

        private void BackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Cancelled)
            {
                MessageBox.Show("The search was cancelled.", "Cancelled", MessageBoxButton.OK, MessageBoxImage.Information);

                Reset();
            }
            else if (e.Error is not null)
            {
                MessageBox.Show($"The process encountered an error: \n{e.Error.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);

                Reset();
            }
            else
            {
                // Populate the DataGrid.
                if (e.Result?.GetType() == typeof(bool))
                {
                    bool result = (bool)e.Result;

                    if (result)
                    {
                        configDataGrid.ItemsSource = _configService.GetDataView();
                        countLabel.Content = $"{_configService.GetCount()} results";
                    }
                }
            }

            pluginComboBox.IsEnabled = true;
            dateComboBox.IsEnabled = true;
            submitButton.IsEnabled = true;
            cancelButton.IsEnabled = false;
        }

        private void Reset()
        {
            try
            {
                configSearchProgressBar.Value = 0;
                countLabel.Content = string.Empty;
                selectedLabel.Content = string.Empty;
                errorLabel.Content = string.Empty;
                configDataGrid.ItemsSource = null;
                configDataGrid.Items.Refresh();
            }
            catch { }
        }

        private void ShowIndeterminateProgress()
        {
            if (mainGrid.IsVisible)
            {
                configWorkProgressBar.IsIndeterminate = true;
                configWorkProgressBar.Visibility = Visibility.Visible;
                configSearchProgressBar.Visibility = Visibility.Hidden;
            }
            else if (editorGrid.IsVisible)
            {
                editorProgressBar.IsIndeterminate = true;
                editorProgressBar.Visibility = Visibility.Visible;
            }
        }

        private void HideIndeterminateProgress()
        {
            if (mainGrid.IsVisible)
            {
                configWorkProgressBar.IsIndeterminate = false;
                configWorkProgressBar.Visibility = Visibility.Hidden;
                configSearchProgressBar.Visibility = Visibility.Visible;
            }
            else if (editorGrid.IsVisible)
            {
                editorProgressBar.IsIndeterminate = false;
                editorProgressBar.Visibility = Visibility.Hidden;
            }
        }

        private static int GetGoodRecordCount(IList items)
        {
            int count = 0;
            DataRowView? row;

            try
            {
                if (items is not null && items.Count > 0)
                {
                    for (int i = 0; i < items.Count; i++)
                    {
                        row = items[i] as DataRowView;

                        if (row is not null && row["StatusId"] is int id)
                        {
                            if (id == 7)
                            {
                                count++;
                            }
                        }
                    }
                }
            }
            catch
            { }

            return count;
        }

        private int GetSelectedId()
        {
            object item;
            int configId = -1;
            DataRowView? row;

            try
            {
                item = configDataGrid.SelectedItem;
                row = item as DataRowView;

                if (row is not null && row["Id"] is int id)
                {
                    configId = id;
                }
            }
            catch
            {
                configId = -1;
            }

            return configId;
        }

        private int[] GetSelectedIds()
        {
            IList items;
            List<int> configIds;
            int[] result;
            DataRowView? row;

            try
            {
                items = configDataGrid.SelectedItems;
                configIds = new();

                if (items is not null && items.Count > 0)
                {
                    for (int i = 0; i < items.Count; i++)
                    {
                        row = items[i] as DataRowView;

                        if (row is not null && row["Id"] is int id)
                        {
                            configIds.Add(id);
                        }
                    }
                }

                result = configIds.ToArray();
            }
            catch
            {
                result = Array.Empty<int>();
            }

            return result;
        }

        private async void Grab_Click(object sender, RoutedEventArgs e)
        {
            int[] ids = GetSelectedIds();

            if (ids is not null && ids.Length > 0)
            {
                MessageBoxResult result = MessageBox.Show(
                    $"You are about to grab {ids.Length} config file(s).\n\nDo you wish to proceed?\n\n", "Warning",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    grabButton.IsEnabled = false;
                    deployButton.IsEnabled = false;

                    ShowIndeterminateProgress();

                    _ = await Task.Run(() => _configService.Grab(ids));

                    HideIndeterminateProgress();

                    // Re-render the DataGrid.
                    RenderData();
                }
            }
            else
            {
                MessageBox.Show("No items are selected.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void Deploy_Click(object sender, RoutedEventArgs e)
        {
            int[] ids = GetSelectedIds();

            if (ids is not null && ids.Length > 0)
            {
                MessageBoxResult result = MessageBox.Show(
                    $"You are about to deploy {ids.Length} config file(s).\n\nDo you wish to proceed?\n\n", "Warning", 
                    MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    grabButton.IsEnabled = false;
                    deployButton.IsEnabled = false;

                    ShowIndeterminateProgress();

                    _ = await Task.Run(() => _configService.Deploy(ids));

                    HideIndeterminateProgress();

                    // Re-render the DataGrid.
                    RenderData();
                }
            }
            else
            {
                MessageBox.Show("No items are selected.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DataGridRow_PreviewMouseRightButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Ensure only one item is selected when right-clicking a row in the datagrid.
            if (sender is not null && sender is DataGridRow row)
            {
                if (configDataGrid.SelectedItems is not null && configDataGrid.SelectedItems.Count > 1)
                {
                    configDataGrid.UnselectAll();
                    row.IsSelected = true;
                    e.Handled = true;
                }
            }
        }

        private void ShowEditor()
        {
            mainGrid.IsEnabled = false;
            mainGrid.Visibility = Visibility.Hidden;

            editorGrid.IsEnabled = true;
            editorGrid.Visibility = Visibility.Visible;
        }

        private void HideEditor()
        {
            editorPathTextBox.Clear();
            editorTextBox.Clear();

            editorGrid.IsEnabled = false;
            editorGrid.Visibility = Visibility.Hidden;

            mainGrid.IsEnabled = true;
            mainGrid.Visibility = Visibility.Visible;
        }

        private void Edit_Click(object sender, RoutedEventArgs e)
        {
            // There should be a single row selected.
            if (configDataGrid.SelectedItem is null)
            {
                return;
            }

            int id = GetSelectedId();

            if (id != -1)
            {
                _configEditor = _configService.GetConfigEditor(id);

                if (_configEditor is not null)
                {
                    ShowEditor();

                    editorPathTextBox.Text = _configEditor.Path;
                    editorTextBox.Text = _configEditor.Content;

                    editorTextBox.Focus();
                }
            }
        }

        private async void EditorSubmit_Click(object sender, RoutedEventArgs e)
        {
            if (_configEditor is null)
            {
                return;
            }

            MessageBoxResult result = MessageBox.Show(
                    $"Deploying this config file will overwite the local and live copies.\n\nDo you wish to proceed?\n\n", "Warning",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                editorTextBox.IsEnabled = false;
                editorCancelButton.IsEnabled = false;
                editorSubmitButton.IsEnabled = false;

                ShowIndeterminateProgress();

                _configEditor.Overwrite(editorTextBox.Text);

                bool success = await Task.Run(() => _configService.Deploy(_configEditor));

                HideIndeterminateProgress();

                editorTextBox.IsEnabled = true;
                editorCancelButton.IsEnabled = true;
                editorSubmitButton.IsEnabled = true;

                if (success)
                {
                    HideEditor();
                    RenderData();
                }
            }
        }

        private void EditorCancel_Click(object sender, RoutedEventArgs e)
        {
            HideEditor();
        }

        private void EditorTextBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers != ModifierKeys.Control)
            {
                return;
            }

            double fontSize = editorTextBox.FontSize;

            fontSize += e.Delta > 0 ? 1 : -1;

            editorTextBox.FontSize = double.Clamp(fontSize, 10, 48);

            e.Handled = true;
        }

        private void WrapText_Checked(object sender, RoutedEventArgs e)
        {
            editorTextBox.TextWrapping = TextWrapping.Wrap;
        }

        private void WrapText_Unhecked(object sender, RoutedEventArgs e)
        {
            editorTextBox.TextWrapping = TextWrapping.NoWrap;
        }

        private void SpellCheck_Checked(object sender, RoutedEventArgs e)
        {
            editorTextBox.SpellCheck.IsEnabled = true;
        }

        private void SpellCheck_Unchecked(object sender, RoutedEventArgs e)
        {
            editorTextBox.SpellCheck.IsEnabled = false;

            // Force clear the underlines remnants.
            editorTextBox.TextDecorations.Clear();
        }
    }
}
