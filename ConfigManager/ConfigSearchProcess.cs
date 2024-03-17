using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows;

namespace ConfigManager
{
    internal class ConfigSearchProcess
    {
        #region Events

        public delegate void ReportProgress(object sender, ProgressEventArgs e);
        public event ReportProgress? ReportProgressEvent;

        #endregion

        #region Properties

        #endregion

        #region Fields

        private const string _configStatusSvnModified = "File has changes in SVN";
        private const string _configStatusLiveModified = "File has changes in VMServices";
        private const string _configStatusNotDeployed = "File has has not been deployed to VMServices";

        private readonly string _deployPath = @"\\vmservices\c$\Program Files (x86)\RCA\ProcessScheduler Service\Plugins";
        private readonly string _schedulerPath = GetHostPath(@"Code\ProcessScheduler\Scheduler.UI");
        private readonly string _debugPath = GetHostPath(@"Code\ProcessScheduler\Scheduler.UI\bin\Debug\Plugins");
        private readonly string _releasePath = GetHostPath(@"Code\ProcessScheduler\Scheduler.UI\bin\Release\Plugins");
        private readonly string _pluginPath = GetHostPath(@"Code\ProcessSchedulerPlugins");

        private const int READ_BYTES = sizeof(long);

        private readonly string[] _pluginDirectories =
        {
            "Adjuster",
            "AuditArchive",
            "AutoDialer2.0",
            "BasicDBMaint",
            "DBI_DailyActivityReport",
            "EDI_Archive",
            "Notes",
            "ProfitPal",
            "Reporter",
            "Uploader"
        };

        private readonly string[] _subDirectoryExclusions = { "bin", "obj", "My Project", ".svn" };

        private bool _cancelled;

        private readonly DataTable _configDataTable;

        #endregion

        #region Constructors

        public ConfigSearchProcess()
        {
            _cancelled = false;

            _configDataTable = new DataTable();
            _configDataTable.Columns.Add("Plugin", typeof(string));
            _configDataTable.Columns.Add("File", typeof(string));
            _configDataTable.Columns.Add("Modified (SVN)", typeof(DateTime));
            _configDataTable.Columns.Add("Modified (Live)", typeof(DateTime));
            _configDataTable.Columns.Add("Status", typeof(string));
        }

        #endregion

        #region Methods

        private static string GetHostPath(string path)
        {
            string result = string.Empty;
            string host;

            try
            {
                host = App.ActiveDirectoryUser.Host;

                if (!string.IsNullOrWhiteSpace(host))
                {
                    result = Path.Combine(@"\\", host, "c$", path);
                }
            }
            catch
            {
                result = string.Empty;
            }

            return result;
        }

        private bool CheckPaths()
        {
            bool success = true;

            try
            {
                if (string.IsNullOrWhiteSpace(App.ActiveDirectoryUser.Host))
                {
                    MessageBox.Show("Could not get the current host information.", "Error", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    success = false;
                }
                else if (!Directory.Exists(_pluginPath))
                {
                    MessageBox.Show($"Path does not exist:\n\n{_pluginPath}", "Error", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    success = false;
                }
                else if (!Directory.Exists(_deployPath))
                {
                    MessageBox.Show($"Path does not exist:\n\n{_deployPath}", "Error", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    return false;
                }
                else if (Directory.Exists(_schedulerPath))
                {
                    if (!Directory.Exists(_debugPath))
                    {
                        MessageBox.Show(
                            $"Path does not exist:\n\n{_debugPath}\n\nMake sure the project is built in Visual Studio.",
                            "Error", MessageBoxButton.OK, MessageBoxImage.Exclamation);

                        success = false;
                    }
                    else if (!Directory.Exists(_releasePath))
                    {
                        MessageBox.Show(
                            $"Path does not exist:\n\n{_releasePath}\n\nMake sure the project is built in Visual Studio.",
                            "Error", MessageBoxButton.OK, MessageBoxImage.Exclamation);

                        success = false;
                    }
                }
                else
                {
                    MessageBox.Show($"Path does not exist:\n\n{_schedulerPath}", "Error", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    success = false;
                }
            }
            catch (Exception ex)
            {
                string message = $"Could not determine status of required folders\n\nDetails:\n\n{ex.Message}";

                if (ex.StackTrace is not null)
                {
                    message += $"\n\n{ex.StackTrace}";
                }

                MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);

                success = false;
            }

            return success;
        }

        private bool IsValidSubDirectory(DirectoryInfo searchDirectory, DirectoryInfo subDirectory)
        {
            bool isValid = false;

            if (searchDirectory is not null && searchDirectory.Exists && subDirectory is not null && subDirectory.Exists)
            {
                Queue<DirectoryInfo> parents = new();
                parents.Enqueue(subDirectory);

                DirectoryInfo parent = subDirectory.Parent ?? subDirectory.Root;

                while (parent is not null &&
                    !parent.FullName.Equals(subDirectory.Root.FullName, StringComparison.OrdinalIgnoreCase) &&
                    !parent.FullName.Equals(searchDirectory.FullName, StringComparison.OrdinalIgnoreCase))
                {
                    parents.Enqueue(parent);
                    parent = parent.Parent ?? subDirectory.Root;
                }

                string directoryName;

                isValid = true;

                while (isValid && parents.Count > 0)
                {
                    directoryName = parents.Dequeue().Name;
                    isValid = !_subDirectoryExclusions.Contains(directoryName, StringComparer.OrdinalIgnoreCase);
                }
            }

            return isValid;
        }

        private static bool FilesEqual(FileInfo first, FileInfo second)
        {
            if (first is null || second is null)
            {
                return false;
            }

            if (!(first.Exists && second.Exists))
            {
                return false;
            }

            if (first.Length != second.Length)
            {
                return false;
            }

            if (first.FullName.Equals(second.FullName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            int iterations = (int)Math.Ceiling((double)first.Length / READ_BYTES);

            using FileStream stream1 = first.OpenRead();
            using FileStream stream2 = second.OpenRead();

            byte[] bytes1 = new byte[READ_BYTES];
            byte[] bytes2 = new byte[READ_BYTES];

            for (int i = 0; i < iterations; i++)
            {
                stream1.Read(bytes1, 0, READ_BYTES);
                stream2.Read(bytes2, 0, READ_BYTES);

                if (BitConverter.ToInt64(bytes1, 0) != BitConverter.ToInt64(bytes2, 0))
                {
                    return false;
                }
            }

            return true;
        }

        private void RaiseReportProgressEvent(int percentage)
        {
            ReportProgressEvent?.Invoke(this, new ProgressEventArgs(percentage));
        }

        private string[] GetPlugins(PluginType plugin)
        {
            return plugin switch
            {
                PluginType.Adjuster => new[] { _pluginDirectories[0] },
                PluginType.AuditArchive => new[] { _pluginDirectories[1] },
                PluginType.AutoDialer => new[] { _pluginDirectories[2] },
                PluginType.BasicDBMaint => new[] { _pluginDirectories[3] },
                PluginType.DBI_DailyActivityReport => new[] { _pluginDirectories[4] },
                PluginType.EDI_Archive => new[] { _pluginDirectories[5] },
                PluginType.Notes => new[] { _pluginDirectories[6] },
                PluginType.ProfitPal => new[] { _pluginDirectories[7] },
                PluginType.Reporter => new[] { _pluginDirectories[8] },
                PluginType.Uploader => new[] { _pluginDirectories[9] },
                _ => _pluginDirectories
            };
        }

        private static DateTime GetMinDateTime(DateTime start, DateRangeType dateRange)
        {
            DateTime result;

            try
            {
                result = dateRange switch
                {
                    DateRangeType.Today => start.Date,
                    DateRangeType.Week => start.AddDays(-7),
                    DateRangeType.Month => start.AddMonths(-1),
                    DateRangeType.Year => start.AddYears(-1),
                    _ => DateTime.MinValue
                };
            }
            catch
            {
                result = DateTime.MinValue;
            }

            return result;
        }

        public int GetCount()
        {
            if (_configDataTable is not null)
            {
                if (_configDataTable.Rows is not null)
                {
                    return _configDataTable.Rows.Count;
                }
            }

            return 0;
        }

        public DataView GetDataView()
        {
            return _configDataTable?.AsDataView() ?? new();
        }

        public void Cancel(object sender, CancellationEventArgs e)
        {
            _cancelled = e.Cancelled;
        }

        public void Run(ConfigSearchArgs args)
        {
            if (!CheckPaths())
            {
                return;
            }

            DateTime minDateTime;

            DirectoryInfo pluginDirectory;
            DirectoryInfo deployDirectory;

            FileInfo pluginFileInfo;
            FileInfo deployFileInfo;

            DateTime pluginFileModified;
            DateTime deployFileModified;

            Queue<PluginConfig> pluginConfigs;
            Queue<string> plugins;
            Queue<string> configs;

            PluginConfig pluginConfig;
            string plugin;
            string config;

            string pluginFolder;
            string deployFile;

            int totalFiles;
            int filesProcessed = 0;
            double progress;

            try
            {
                args ??= new();

                pluginDirectory = new(_pluginPath);
                deployDirectory = new(_deployPath);

                _configDataTable.Rows.Clear();

                minDateTime = GetMinDateTime(DateTime.Now, args.DateRange);

                pluginConfigs = new();
                plugins = new(GetPlugins(args.Plugin));

                // Get all config files so we have a predetermined count for reporting progress.
                while (plugins.Count > 0 && !_cancelled)
                {
                    plugin = plugins.Dequeue();
                    pluginFolder = Path.Combine(pluginDirectory.FullName, plugin);

                    if (Directory.Exists(pluginFolder))
                    {
                        configs = new(Directory.GetFiles(pluginFolder, "*.xml", SearchOption.AllDirectories));

                        while (configs.Count > 0 && !_cancelled)
                        {
                            config = configs.Dequeue();
                            pluginConfigs.Enqueue(new(plugin, config));
                        }
                    }
                }

                totalFiles = pluginConfigs.Count;

                // Begin search.
                while (pluginConfigs.Count > 0 && !_cancelled)
                {
                    pluginConfig = pluginConfigs.Dequeue();
                    plugin = pluginConfig.PluginName;
                    pluginFolder = Path.Combine(pluginDirectory.FullName, plugin);

                    if (Directory.Exists(pluginFolder))
                    {
                        config = pluginConfig.ConfigFile;
                        pluginFileInfo = new(config);

                        if (pluginFileInfo is not null && pluginFileInfo.Directory is not null)
                        {
                            pluginFileModified = pluginFileInfo.LastWriteTime;

                            if (IsValidSubDirectory(pluginDirectory, pluginFileInfo.Directory))
                            {
                                deployFile = Path.Combine(deployDirectory.FullName, plugin, pluginFileInfo.Name);

                                if (File.Exists(deployFile))
                                {
                                    deployFileInfo = new(deployFile);

                                    if (deployFileInfo is not null && deployFileInfo.Directory is not null)
                                    {
                                        deployFileModified = deployFileInfo.LastWriteTime;

                                        if (pluginFileModified != deployFileModified)
                                        {
                                            if (pluginFileModified >= minDateTime || deployFileModified >= minDateTime)
                                            {
                                                if (!FilesEqual(pluginFileInfo, deployFileInfo))
                                                {
                                                    if (pluginFileModified > deployFileModified)
                                                    {
                                                        _configDataTable.Rows.Add(
                                                            plugin, pluginFileInfo.Name, pluginFileModified, deployFileModified, _configStatusSvnModified);
                                                    }
                                                    else if (deployFileModified > pluginFileModified)
                                                    {
                                                        _configDataTable.Rows.Add(
                                                            plugin, pluginFileInfo.Name, pluginFileModified, deployFileModified, _configStatusLiveModified);
                                                    }

                                                    /* save pluginFileInfo and deployFileInfo */
                                                }
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    if (pluginFileModified >= minDateTime)
                                    {
                                        _configDataTable.Rows.Add(
                                            plugin, pluginFileInfo.Name, pluginFileModified, null, _configStatusNotDeployed);
                                    }

                                    /* save pluginFileInfo */
                                }
                            }
                        }
                    }

                    filesProcessed++;

                    // Calculate progress percentage.
                    progress = ((filesProcessed + 1.0) / totalFiles) * 100.0;

                    // Report progress.
                    RaiseReportProgressEvent((int)progress);
                }

                if (_cancelled)
                {
                    _configDataTable.RejectChanges();
                }
                else
                {
                    _configDataTable.AcceptChanges();
                }
            }
            catch (Exception ex)
            {
                string message = $"Could not get config data.\n\nDetails:\n\n{ex.Message}";

                if (ex.StackTrace is not null)
                {
                    message += $"\n\n{ex.StackTrace}";
                }

                MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _cancelled = false;
            }
        }

        #endregion
    }
}
