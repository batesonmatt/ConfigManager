using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows;

namespace ConfigManager
{
    public class ConfigService
    {
        #region Events

        public delegate void ReportProgress(object sender, ProgressEventArgs e);
        public event ReportProgress? ReportProgressEvent;

        #endregion

        #region Properties

        #endregion

        #region Fields

        private readonly string _deployPath = @"\\vmservices\c$\Program Files (x86)\RCA\ProcessScheduler Service\Plugins";
        private readonly string _schedulerPath = GetHostPath(@"Code\ProcessScheduler\Scheduler.UI");
        private readonly string _releasePath = GetHostPath(@"Code\ProcessScheduler\Scheduler.UI\bin\Release\Plugins");
        private readonly string _debugPath = GetHostPath(@"Code\ProcessScheduler\Scheduler.UI\bin\Debug\Plugins");
        private readonly string _pluginPath = GetHostPath(@"Code\ProcessSchedulerPlugins");

        private readonly Dictionary<ConfigStatus, ConfigStatusArgs> _configStatusDictionary = new()
        {
            { ConfigStatus.LocalModified, new(1, "File has changes in local path", "Deploy") },
            { ConfigStatus.LiveModified, new(2, "File has changes in VMServices", "Grab") },
            { ConfigStatus.NotDeployed, new(3, "File has not been deployed to VMServices", "Deploy") },
            { ConfigStatus.LocalBuildModified, new(4, "File has changes in local path for one or more build output directories", "Deploy") },
            { ConfigStatus.BuildModified, new(5, "File has changes in one or more build output directories", "Deploy") },
            { ConfigStatus.BuildNotDeployed, new(6, "File has not been deployed to one or more build output directories", "Deploy") },
            { ConfigStatus.Good, new(7, "File is up to date in all paths") }
        };

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
        private readonly Dictionary<int, ConfigFileInfoArgs> _configFileDictionary;

        #endregion

        #region Constructors

        public ConfigService()
        {
            _cancelled = false;

            _configDataTable = new();
            _configDataTable.Columns.Add("ID", typeof(int));
            _configDataTable.Columns.Add("Plugin", typeof(string));
            _configDataTable.Columns.Add("File", typeof(string));
            _configDataTable.Columns.Add("Modified (Local)", typeof(DateTime));
            _configDataTable.Columns.Add("Modified (Live)", typeof(DateTime));
            _configDataTable.Columns.Add("Modified (Build)", typeof(DateTime));
            _configDataTable.Columns.Add("Recommend Action", typeof(string));
            _configDataTable.Columns.Add("StatusID", typeof(int));
            _configDataTable.Columns.Add("Status", typeof(string));

            _configFileDictionary = new();
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

        private static bool SearchFileName(FileInfo fileInfo, string search)
        {
            bool result;

            try
            {
                if (string.IsNullOrWhiteSpace(search) || search.Trim() == string.Empty)
                {
                    result = true;
                }
                else if (search.IndexOfAny(Path.GetInvalidFileNameChars()) < 0)
                {
                    result = Path.GetFileNameWithoutExtension(fileInfo.Name).Contains(search, StringComparison.OrdinalIgnoreCase);
                }
                else
                {
                    result = false;
                }
            }
            catch
            {
                result = false;
            }

            return result;
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

        public void Search(ConfigSearchArgs args)
        {
            if (!CheckPaths())
            {
                return;
            }

            DateTime minDateTime;

            DirectoryInfo pluginDirectory;
            DirectoryInfo deployDirectory;
            DirectoryInfo releaseDirectory;
            DirectoryInfo debugDirectory;

            FileInfo pluginFileInfo;
            FileInfo deployFileInfo;
            FileInfo releaseFileInfo;
            FileInfo debugFileInfo;

            DateTime pluginFileModified;
            DateTime deployFileModified;
            DateTime releaseFileModified;
            DateTime debugFileModified;

            Queue<PluginConfig> pluginConfigs;
            Queue<string> plugins;
            Queue<string> configs;

            ConfigStatusArgs status = null!;

            PluginConfig pluginConfig;
            string pluginFolder;
            string plugin;
            string config;

            bool deployExists;
            bool releaseExists;
            bool debugExists;

            int totalFiles;
            int filesProcessed = 0;
            double progress;

            int id = 1;

            try
            {
                args ??= new();

                pluginDirectory = new(_pluginPath);
                deployDirectory = new(_deployPath);
                releaseDirectory = new(_releasePath);
                debugDirectory = new(_debugPath);

                _configDataTable.Rows.Clear();
                _configFileDictionary.Clear();

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
                            if (SearchFileName(pluginFileInfo, args.Search))
                            {
                                if (IsValidSubDirectory(pluginDirectory, pluginFileInfo.Directory))
                                {
                                    deployFileInfo = new(Path.Combine(deployDirectory.FullName, plugin, pluginFileInfo.Name));
                                    releaseFileInfo = new(Path.Combine(releaseDirectory.FullName, plugin, pluginFileInfo.Name));
                                    debugFileInfo = new(Path.Combine(debugDirectory.FullName, plugin, pluginFileInfo.Name));

                                    deployExists = deployFileInfo.Exists;
                                    releaseExists = releaseFileInfo.Exists;
                                    debugExists = debugFileInfo.Exists;

                                    pluginFileModified = pluginFileInfo.LastWriteTime;

                                    /* Get all 4 file infos
                                     * Get all 4 file exists bools
                                     * Get all 4 file modified datetimes
                                     * Only process if at least 1 datetime is >= min datetime
                                     * 
                                     * Compare (Revise FilesEqual as CompareModified(file, other) extension method to return -1,0,1):
                                     * 1. comparePluginDeploy
                                     * 2. comparePluginRelease
                                     * 3. comparePluginDebug
                                     * 4. compareDeployRelease
                                     * 5. compareDeployDebug
                                     * 6. compareReleaseDebug
                                     * 
                                     * If !deployExists
                                     *      => ConfigStatus.NotDeployed (do not override)
                                     * 
                                     * If neither file is > plugin, and plugin is > at least one other file
                                     * If comparePluginDeploy >= 0 && comparePluginRelease >= 0 && comparePluginDebug >= 0
                                     *      If comparePluginDeploy == 1 || comparePluginRelease == 1 || comparePluginDebug == 1
                                     *          => ConfigStatus.LocalModified (do not override)
                                     * 
                                     * If deploy exists and neither file is > deploy, and deploy > at least one other file
                                     * If deployExists
                                     *      If comparePluginDeploy <= 0 && compareDeployRelease >= 0 && compareDeployDebug >= 0
                                     *          If comparePluginDeploy == -1 || compareDeployRelease == 1 || compareDeployDebug == 1
                                     *              => ConfigStatus.LiveModified (do not override)
                                     * 
                                     * If release exists and > all other files
                                     * If releaseExists
                                     *      If comparePluginRelease == -1 && compareDeployRelease == -1 && compareReleaseDebug == 1
                                     *          => ConfigStatus.ReleaseModified (do not override)
                                     * 
                                     * If debug exists and > all other files
                                     * If debugExists
                                     *      If comparePluginDebug == -1 && compareDeployDebug == -1 && compareReleaseDebug == -1
                                     *          => ConfigStatus.DebugModified (do not override)
                                     * 
                                     * If both release and debug exist, and release = debug, and both > plugin and deploy
                                     * If releaseExists && debugExists
                                     *      If compareReleaseDebug == 0 && comparePluginRelease == -1 && compareDeployRelease == -1
                                     *          => ConfigStatus.BuildModified (do not override)
                                     * 
                                     * If all exist and equal
                                     * If deployExists && releaseExists && debugExists
                                     *      If comparePluginDeploy == 0 && comparePluginRelease == 0 && comparePluginDebug == 0
                                     *          If compareDeployRelease == 0 && compareDeployDebug == 0 && compareReleaseDebug == 0
                                     *              => ConfigStatus.Good (do not override)
                                     */

                                    // Check the deployed (live) file.
                                    deployFile = Path.Combine(deployDirectory.FullName, plugin, pluginFileInfo.Name);

                                    if (File.Exists(deployFile))
                                    {
                                        deployFileInfo = new(deployFile);

                                        if (deployFileInfo is not null && deployFileInfo.Directory is not null)
                                        {
                                            deployFileModified = deployFileInfo.LastWriteTime;

                                            if (status is null)
                                            {
                                                if (pluginFileModified != deployFileModified)
                                                {
                                                    if (pluginFileModified >= minDateTime || deployFileModified >= minDateTime)
                                                    {
                                                        if (!FilesEqual(pluginFileInfo, deployFileInfo))
                                                        {
                                                            // Local file has recent changes that have not been deployed yet.
                                                            if (pluginFileModified > deployFileModified)
                                                            {
                                                                status = _configStatusDictionary[ConfigStatus.LocalModified];
                                                            }
                                                            // Deployed file has recent changes that should be grabbed.
                                                            else if (deployFileModified > pluginFileModified)
                                                            {
                                                                status = _configStatusDictionary[ConfigStatus.LiveModified];
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    else if (status is null)
                                    {
                                        // File has not been deployed yet.
                                        if (pluginFileModified >= minDateTime)
                                        {
                                            status = _configStatusDictionary[ConfigStatus.NotDeployed];
                                        }
                                    }

#warning "Code duplication"
                                    // If the deployed file exists and has no differences, check the config in the release output directory.
                                    releaseFile = Path.Combine(releaseDirectory.FullName, plugin, pluginFileInfo.Name);

                                    if (File.Exists(releaseFile))
                                    {
                                        releaseFileInfo = new(releaseFile);

                                        if (releaseFileInfo is not null && releaseFileInfo.Directory is not null)
                                        {
                                            releaseFileModified = releaseFileInfo.LastWriteTime;

                                            if (status is null)
                                            {
                                                if (pluginFileModified != releaseFileModified)
                                                {
                                                    if (pluginFileModified >= minDateTime || releaseFileModified >= minDateTime)
                                                    {
                                                        if (!FilesEqual(pluginFileInfo, releaseFileInfo))
                                                        {
                                                            // Local file has recent changes that have not been deployed to the release directory.
                                                            if (pluginFileModified > releaseFileModified)
                                                            {
                                                                status = _configStatusDictionary[ConfigStatus.LocalBuildModified];
                                                            }
                                                            // Release file has recent changes that should be deployed as the latest local version.
                                                            else if (releaseFileModified > pluginFileModified)
                                                            {
                                                                /* This should overwrite the status */
                                                                status = _configStatusDictionary[ConfigStatus.BuildModified];
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    else if (status is null)
                                    {
                                        // File has not been deployed to the release directory yet.
                                        if (pluginFileModified >= minDateTime)
                                        {
                                            status = _configStatusDictionary[ConfigStatus.BuildNotDeployed];
                                        }
                                    }

#warning "Code duplication"
                                    // If the release file exists and has no differences, check the config in the debug output directory.
                                    debugFile = Path.Combine(debugDirectory.FullName, plugin, pluginFileInfo.Name);

                                    if (File.Exists(releaseFile))
                                    {
                                        debugFileInfo = new(releaseFile);

                                        if (debugFileInfo is not null && debugFileInfo.Directory is not null)
                                        {
                                            debugFileModified = debugFileInfo.LastWriteTime;

                                            if (status is null)
                                            {
                                                if (pluginFileModified != debugFileModified)
                                                {
                                                    if (pluginFileModified >= minDateTime || debugFileModified >= minDateTime)
                                                    {
                                                        if (!FilesEqual(pluginFileInfo, debugFileInfo))
                                                        {
                                                            // Local file has recent changes that have not been deployed to the debug directory.
                                                            if (pluginFileModified > debugFileModified)
                                                            {
                                                                status = _configStatusDictionary[ConfigStatus.LocalBuildModified];
                                                            }
                                                            // Debug file has recent changes that should be deployed as the latest local version.
                                                            else if (debugFileModified > pluginFileModified)
                                                            {
                                                                /* This should overwrite the status */
                                                                status = _configStatusDictionary[ConfigStatus.BuildModified];
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    else if (status is null)
                                    {
                                        // File has not been deployed to the debug directory yet.
                                        if (pluginFileModified >= minDateTime)
                                        {
                                            status = _configStatusDictionary[ConfigStatus.BuildNotDeployed];
                                        }
                                    }

#warning "Code duplication"
                                    if (status is null)
                                    {
                                        /* Verify all datetimes are the same and >= min datetime */

                                        // The file is up to date in all paths.
                                        if (pluginFileModified >= minDateTime)
                                        {
                                            status = _configStatusDictionary[ConfigStatus.Good];
                                        }
                                    }
                                }
                            }
                        }
                    }

                    if (status is not null)
                    {
                        _configDataTable.Rows.Add(
                            id, plugin, pluginFileInfo.Name, pluginFileModified, deployFileModified, null, status.Action, status.Id, status.Status);

                        _configFileDictionary.Add(id, new(plugin, pluginFileInfo, deployFileInfo));
                    }

                    /* reset file values to null */

                    id++;
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

        // Copy and overwrite config files from the local plugin path to the live, release, and debug paths.
        public bool Deploy(int[] configIds)
        {
            bool success = true;
            FileInfo localFile;
            FileInfo copyFile;
            string plugin;

            string[] copyPaths;

            try
            {
                copyPaths = new string[] { _deployPath, _releasePath, _debugPath };

                foreach (int id in configIds)
                {
                    if (_configFileDictionary.ContainsKey(id))
                    {
                        localFile = _configFileDictionary[id].LocalFile;

                        if (localFile is not null && localFile.Exists)
                        {
                            // Get the plugin name for this config file.
                            plugin = _configFileDictionary[id].Plugin;

                            if (!string.IsNullOrWhiteSpace(plugin))
                            {
                                foreach (string copyPath in copyPaths)
                                {
                                    copyFile = new(Path.Combine(copyPath, plugin, localFile.Name));

                                    if (copyFile.Directory is not null && copyFile.Directory.Exists)
                                    {
                                        // Overwrite the existing config file, if it exists.
                                        File.Copy(localFile.FullName, copyFile.FullName, overwrite: true);
                                        copyFile.Refresh();

                                        if (copyFile.LastWriteTime != localFile.LastWriteTime)
                                        {
                                            copyFile.LastWriteTime = localFile.LastWriteTime;
                                            copyFile.Refresh();
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                success = false;

                string message = $"Could not deploy one or more config file(s).\n\nDetails:\n\n{ex.Message}";

                if (ex.StackTrace is not null)
                {
                    message += $"\n\n{ex.StackTrace}";
                }

                MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return success;
        }

        // Copy and overwrite config files from their deployed paths to the local plugin, release, and debug paths.
        public bool Grab(int[] configIds)
        {
            bool success = true;
            FileInfo deployFile;
            FileInfo localFile;
            FileInfo copyFile;
            string plugin;

            string[] copyPaths;

            try
            {
                copyPaths = new string[] { _releasePath, _debugPath };

                foreach (int id in configIds)
                {
                    if (_configFileDictionary.ContainsKey(id))
                    {
                        // We can't grab the file if it has not yet been deployed (i.e., the file does not exist).
                        if (_configFileDictionary[id].IsDeployed())
                        {
                            deployFile = _configFileDictionary[id].DeployFile;
                            localFile = _configFileDictionary[id].LocalFile;

                            if (deployFile is not null && deployFile.Exists && localFile is not null && localFile.Exists)
                            {
                                // When copying over to the local plugin path, use the saved path in the dictionary.
                                // Overwrite the existing local config file.
                                File.Copy(deployFile.FullName, localFile.FullName, overwrite: true);
                                localFile.Refresh();

                                if (localFile.LastWriteTime != deployFile.LastWriteTime)
                                {
                                    localFile.LastWriteTime = deployFile.LastWriteTime;
                                    localFile.Refresh();
                                }

                                // Get the plugin name for this config file.
                                plugin = _configFileDictionary[id].Plugin;

                                if (!string.IsNullOrWhiteSpace(plugin))
                                {
                                    foreach (string copyPath in copyPaths)
                                    {
                                        copyFile = new(Path.Combine(copyPath, plugin, deployFile.Name));

                                        if (copyFile.Directory is not null && copyFile.Directory.Exists)
                                        {
                                            // Do not attempt to copy the file to the same path.
                                            if (deployFile.FullName != copyFile.FullName)
                                            {
                                                // Overwrite the existing config file, if it exists.
                                                File.Copy(deployFile.FullName, copyFile.FullName, overwrite: true);
                                                copyFile.Refresh();

                                                if (copyFile.LastWriteTime != deployFile.LastWriteTime)
                                                {
                                                    copyFile.LastWriteTime = deployFile.LastWriteTime;
                                                    copyFile.Refresh();
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                success = false;

                string message = $"Could not grab one or more config file(s).\n\nDetails:\n\n{ex.Message}";

                if (ex.StackTrace is not null)
                {
                    message += $"\n\n{ex.StackTrace}";
                }

                MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return success;
        }

        #endregion
    }
}
