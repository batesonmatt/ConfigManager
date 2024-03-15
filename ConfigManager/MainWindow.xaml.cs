using System;
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
            /* add cancel handler */
        }

        private string[] GetSearchPlugins()
        {
            return pluginComboBox.SelectedIndex switch
            {
                1 => new string[] { _pluginDirectories[0] },
                2 => new string[] { _pluginDirectories[1] },
                3 => new string[] { _pluginDirectories[2] },
                4 => new string[] { _pluginDirectories[3] },
                5 => new string[] { _pluginDirectories[4] },
                6 => new string[] { _pluginDirectories[5] },
                7 => new string[] { _pluginDirectories[6] },
                8 => new string[] { _pluginDirectories[7] },
                9 => new string[] { _pluginDirectories[8] },
                10 => new string[] { _pluginDirectories[9] },
                _ => _pluginDirectories
            };
        }

        private void SubmitButton_Click(object sender, RoutedEventArgs e)
        {
            pluginComboBox.IsEnabled = false;
            dateComboBox.IsEnabled = false;
            submitButton.IsEnabled = false;
            cancelButton.IsEnabled = true;

            if (!_configWorker.IsBusy)
            {
                /* Consider passing argument for the plugin type and date range. */

                // Fire the DoWork event.
                _configWorker.RunWorkerAsync(/*argument*/);
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
        }

        private void ClearSelectionButton_Click(object sender, RoutedEventArgs e)
        {
            configDataGrid.UnselectAll();
        }

        private void BackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            /* Get argument type from e.Argument */

            

            // Return a Cancel value if the event was cancelled.
            if (_configWorker.CancellationPending)
            {
                e.Cancel = true;
                return;
            }

            // Set the Result value.
            /* e.Result = true; */
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
                /* Handle e.Result */
            }

            pluginComboBox.IsEnabled = true;
            dateComboBox.IsEnabled = true;
            submitButton.IsEnabled = true;
            cancelButton.IsEnabled = false;
        }
    }
}
