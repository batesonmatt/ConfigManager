using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Windows;
using System.Windows.Controls;

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
            configProgressBar.Value = 0;

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
                if (e.ProgressPercentage <= configProgressBar.Maximum)
                {
                    configProgressBar.Value = e.ProgressPercentage;
                }
                else
                {
                    configProgressBar.Value = configProgressBar.Maximum;
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
                configProgressBar.Value = 0;
                countLabel.Content = string.Empty;
                selectedLabel.Content = string.Empty;
                errorLabel.Content = string.Empty;
                configDataGrid.ItemsSource = null;
                configDataGrid.Items.Refresh();
            }
            catch { }
        }

        private int GetGoodRecordCount(IList items)
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

        private void Grab_Click(object sender, RoutedEventArgs e)
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

                    _configService.Grab(ids);

                    // Re-render the DataGrid.
                    RenderData();
                }
            }
            else
            {
                MessageBox.Show("No items are selected.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Deploy_Click(object sender, RoutedEventArgs e)
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

                    _configService.Deploy(ids);

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
            if (sender is not null && sender is DataGridRow row)
            {
                if (configDataGrid.SelectedItems is not null && configDataGrid.SelectedItems.Count > 1)
                {
                    configDataGrid.UnselectAll();
                    row.IsSelected = true;
                }
            }
        }
    }
}
