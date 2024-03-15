﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

namespace ConfigManager
{
    public partial class MainWindow : Window
    {
        #region Events

        public delegate void CancelProcess(object sender, CancellationEventArgs e);
        public event CancelProcess? CancelProcessEvent;

        #endregion

        #region Fields

        private ConfigSearchProcess _searchProcess;
        private BackgroundWorker _configWorker;
        
        #endregion

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            _searchProcess = new ConfigSearchProcess();
            _configWorker = (BackgroundWorker)FindResource("configBackgroundWorker");

            _searchProcess.ReportProgressEvent += BackgroundWorker_ReportProgress;
            CancelProcessEvent += _searchProcess.Cancel;
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

        private void SubmitButton_Click(object sender, RoutedEventArgs e)
        {
            pluginComboBox.IsEnabled = false;
            dateComboBox.IsEnabled = false;
            submitButton.IsEnabled = false;
            cancelButton.IsEnabled = true;

            configProgressBar.Value = 0;

            if (!_configWorker.IsBusy)
            {
                ConfigSearchArgs args = new(GetPluginType(), GetDateRangeType());

                // Fire the DoWork event.
                _configWorker.RunWorkerAsync(args);
            }
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
            grabButton.IsEnabled = configDataGrid.SelectedIndex >= 0;
            deployButton.IsEnabled = configDataGrid.SelectedIndex >= 0;
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
        }

        private void ClearSelectionButton_Click(object sender, RoutedEventArgs e)
        {
            configDataGrid.UnselectAll();
            selectedLabel.Content = string.Empty;
        }

        private void BackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            if (e.Argument is ConfigSearchArgs and not null)
            {
                ConfigSearchArgs args = (ConfigSearchArgs)e.Argument ?? new();

                _searchProcess.Run(args);
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
                if ((configProgressBar.Value + e.ProgressPercentage) <= configProgressBar.Maximum)
                {
                    configProgressBar.Value += e.ProgressPercentage;
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
            }
            else if (e.Error is not null)
            {
                MessageBox.Show($"The process encountered an error: \n{e.Error.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                // Populate the DataGrid.
                if (e.Result?.GetType() == typeof(bool))
                {
                    bool result = (bool)e.Result;

                    if (result)
                    {
                        configDataGrid.ItemsSource = _searchProcess.GetDataView();
                        countLabel.Content = $"{_searchProcess.GetCount()} results";
                    }
                }
            }

            pluginComboBox.IsEnabled = true;
            dateComboBox.IsEnabled = true;
            submitButton.IsEnabled = true;
            cancelButton.IsEnabled = false;
        }
    }
}
