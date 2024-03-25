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
            { ConfigStatus.LocalModified, new(1, "File has changes in local plugin directory (SVN)", "Deploy") },
            { ConfigStatus.LiveModified, new(2, "File has changes in VMServices", "Grab") },
            { ConfigStatus.NotDeployed, new(3, "File has not been deployed to VMServices", "Deploy") },
            { ConfigStatus.ReleaseModified, new(4, "File has changes in Release directory", "Deploy") },
            { ConfigStatus.DebugModified, new(5, "File has changes in Debug directory", "Deploy") },
            { ConfigStatus.BuildModified, new(6, "File has changes in build output directories", "Deploy") },
            { ConfigStatus.Good, new(7, "File is up to date in all directories") }
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
            _configDataTable.Columns.Add("Modified (SVN)", typeof(DateTime));
            _configDataTable.Columns.Add("Modified (Release)", typeof(DateTime));
            _configDataTable.Columns.Add("Modified (Debug)", typeof(DateTime));
            _configDataTable.Columns.Add("Modified (Live)", typeof(DateTime));
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

        private static object? GetValidatedDateTime(DateTime dateTime)
        {
            if (dateTime == DateTime.MinValue || dateTime == DateTime.MaxValue)
            {
                return null;
            }
            
            return dateTime;
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

            ConfigDeployAction deployAction = ConfigDeployAction.Plugin;

            Queue<PluginConfig> pluginConfigs;
            Queue<string> plugins;
            Queue<string> configs;

            ConfigStatusArgs? status = null;

            PluginConfig pluginConfig;
            string pluginFolder;
            string plugin;
            string config;

            bool pluginExists;
            bool deployExists;
            bool releaseExists;
            bool debugExists;

            int comparePluginDeploy;
            int comparePluginRelease;
            int comparePluginDebug;
            int compareDeployRelease;
            int compareDeployDebug;
            int compareReleaseDebug;

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
                            pluginExists = pluginFileInfo.Exists;

                            if (pluginExists && pluginFileInfo.FileNameContains(args.Search))
                            {
                                if (IsValidSubDirectory(pluginDirectory, pluginFileInfo.Directory))
                                {
                                    deployFileInfo = new(Path.Combine(deployDirectory.FullName, plugin, pluginFileInfo.Name));
                                    releaseFileInfo = new(Path.Combine(releaseDirectory.FullName, plugin, pluginFileInfo.Name));
                                    debugFileInfo = new(Path.Combine(debugDirectory.FullName, plugin, pluginFileInfo.Name));

                                    deployExists = deployFileInfo.Exists;
                                    releaseExists = releaseFileInfo.Exists;
                                    debugExists = debugFileInfo.Exists;

                                    pluginFileModified = pluginExists ? pluginFileInfo.LastWriteTime : DateTime.MinValue;
                                    deployFileModified = deployExists ? deployFileInfo.LastWriteTime : DateTime.MinValue;
                                    releaseFileModified = releaseExists ? releaseFileInfo.LastWriteTime : DateTime.MinValue;
                                    debugFileModified = debugExists ? debugFileInfo.LastWriteTime : DateTime.MinValue;

                                    if (minDateTime.LessThanOrEqualToAny(
                                        pluginFileModified, deployFileModified, releaseFileModified, debugFileModified))
                                    {
                                        comparePluginDeploy = pluginFileInfo.CompareModified(deployFileInfo);
                                        comparePluginRelease = pluginFileInfo.CompareModified(releaseFileInfo);
                                        comparePluginDebug = pluginFileInfo.CompareModified(debugFileInfo);
                                        compareDeployRelease = deployFileInfo.CompareModified(releaseFileInfo);
                                        compareDeployDebug = deployFileInfo.CompareModified(debugFileInfo);
                                        compareReleaseDebug = releaseFileInfo.CompareModified(debugFileInfo);

                                        if (status is null)
                                        {
                                            if (!deployExists)
                                            {
                                                if (pluginExists && releaseExists && debugExists)
                                                {
                                                    if (comparePluginRelease == 0 && comparePluginDebug == 0 && compareReleaseDebug == 0)
                                                    {
                                                        // All local copies exist and are equal, but neither local copy has been deployed
                                                        // to the live directory yet.
                                                        status = _configStatusDictionary[ConfigStatus.NotDeployed];

                                                        // When deploying, the local plugin file will replace the other copies.
                                                        deployAction = ConfigDeployAction.Plugin;
                                                    }
                                                }
                                            }
                                        }

                                        if (status is null)
                                        {
                                            if (pluginExists)
                                            {
                                                if (comparePluginDeploy >= 0 && comparePluginRelease >= 0 && comparePluginDebug >= 0)
                                                {
                                                    if (comparePluginDeploy == 1 || comparePluginRelease == 1 || comparePluginDebug == 1)
                                                    {
                                                        // Neither copy has changes earlier than the local plugin file, 
                                                        // but the plugin file has changes earlier than at least one other copy.
                                                        status = _configStatusDictionary[ConfigStatus.LocalModified];

                                                        // When deploying, the local plugin file will replace the other copies.
                                                        deployAction = ConfigDeployAction.Plugin;
                                                    }
                                                }
                                            }
                                        }

                                        if (status is null)
                                        {
                                            if (deployExists)
                                            {
                                                if (comparePluginDeploy <= 0 && compareDeployRelease >= 0 && compareDeployDebug >= 0)
                                                {
                                                    if (comparePluginDeploy == -1 || compareDeployRelease == 1 || compareDeployDebug == 1)
                                                    {
                                                        // Neither local copy has changes earlier than the deployed file, 
                                                        // but the deployed file has changes earlier than at least one other local copy.
                                                        status = _configStatusDictionary[ConfigStatus.LiveModified];

                                                        if (pluginExists && comparePluginRelease >= 0 && comparePluginDebug >= 0)
                                                        {
                                                            // When deploying, the local plugin file will replace the other copies.
                                                            deployAction = ConfigDeployAction.Plugin;
                                                        }
                                                        else if (releaseExists && comparePluginRelease <= 0 && compareReleaseDebug >= 0)
                                                        {
                                                            // When deploying, the release file will replace the other copies.
                                                            deployAction = ConfigDeployAction.Release;
                                                        }
                                                        else if (debugExists && comparePluginDebug <= 0 && compareReleaseDebug <= 0)
                                                        {
                                                            // When deploying, the debug file will replace the other copies.
                                                            deployAction = ConfigDeployAction.Debug;
                                                        }
                                                        else
                                                        {
                                                            // When deploying, the local plugin file will replace the other copies.
                                                            deployAction = ConfigDeployAction.Plugin;
                                                        }
                                                    }
                                                }
                                            }
                                        }

                                        if (status is null)
                                        {
                                            if (releaseExists)
                                            {
                                                if (comparePluginRelease == -1 && compareDeployRelease == -1 && compareReleaseDebug == 1)
                                                {
                                                    // The release file has changes earlier than all other copies.
                                                    status = _configStatusDictionary[ConfigStatus.ReleaseModified];

                                                    // When deploying, the release file will replace the other copies.
                                                    deployAction = ConfigDeployAction.Release;
                                                }
                                            }
                                        }
                                        
                                        if (status is null)
                                        {
                                            if (debugExists)
                                            {
                                                if (comparePluginDebug == -1 && compareDeployDebug == -1 && compareReleaseDebug == -1)
                                                {
                                                    // The debug file has changes earlier than all other copies.
                                                    status = _configStatusDictionary[ConfigStatus.DebugModified];

                                                    // When deploying, the debug file will replace the other copies.
                                                    deployAction = ConfigDeployAction.Debug;
                                                }
                                            }
                                        }
                                        
                                        if (status is null)
                                        {
                                            if (releaseExists && debugExists)
                                            {
                                                if (compareReleaseDebug == 0 && comparePluginRelease == -1 && compareDeployRelease == -1)
                                                {
                                                    // Both the release and debug files are equal and have changes earlier
                                                    // than the local plugin copy and the deployed copy.
                                                    status = _configStatusDictionary[ConfigStatus.BuildModified];

                                                    // When deploying, the release file will replace the other copies.
                                                    deployAction = ConfigDeployAction.Release;
                                                }
                                            }
                                        }
                                        
                                        if (status is null)
                                        {
                                            if (pluginExists && releaseExists && debugExists && deployExists)
                                            {
                                                if (comparePluginDeploy == 0 && comparePluginRelease == 0 && comparePluginDebug == 0)
                                                {
                                                    if (compareDeployRelease == 0 && compareDeployDebug == 0 && compareReleaseDebug == 0)
                                                    {
                                                        // All copies of the file exist and are equal.
                                                        status = _configStatusDictionary[ConfigStatus.Good];

                                                        // When deploying, the local plugin file will replace the other copies.
                                                        deployAction = ConfigDeployAction.Plugin;
                                                    }
                                                }
                                            }
                                        }

                                        if (status is not null)
                                        {
                                            _configDataTable.Rows.Add(
                                                id,
                                                plugin,
                                                pluginFileInfo.Name,
                                                GetValidatedDateTime(pluginFileModified),
                                                GetValidatedDateTime(releaseFileModified),
                                                GetValidatedDateTime(debugFileModified),
                                                GetValidatedDateTime(deployFileModified),
                                                status.Action,
                                                status.Id,
                                                status.Status);

                                            _configFileDictionary.Add(
                                                id, new(deployAction, pluginFileInfo, releaseFileInfo, debugFileInfo, deployFileInfo));

                                            id++;

                                            status = null;
                                        }
                                    }
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

        // Copy and overwrite config files with the latest local versions to all other necessary directories.
        public bool Deploy(int[] configIds)
        {
            bool success = true;
            FileInfo localFile;

            try
            {
                foreach (int id in configIds)
                {
                    if (_configFileDictionary.ContainsKey(id))
                    {
                        // Get the latest local file to deploy.
                        localFile = _configFileDictionary[id].GetDeployingFile();

                        if (localFile is not null && localFile.Exists)
                        {
                            // Load any changes made to the file.
                            localFile.Refresh();

                            foreach (FileInfo copyFile in _configFileDictionary[id].GetReplacingFiles())
                            {
                                // We don't need to check whether the actual file to be replaced exists.
                                if (copyFile is not null && copyFile.Directory is not null && copyFile.Directory.Exists)
                                {
                                    // Do not attempt to copy the file to the same path.
                                    if (localFile.FullName != copyFile.FullName)
                                    {
                                        /* Consider File.Replace to store backups. */

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

        // Save the edited config file and deploy the changes to all other necessary directories.
        public bool Deploy(ConfigEditorArgs configEditor)
        {
            bool success = true;
            FileInfo file;
            int id;

            try
            {
                id = configEditor.Id;

                if (_configFileDictionary.ContainsKey(id))
                {
                    file = new(configEditor.Path);

                    if (file is not null && file.Directory is not null && file.Directory.Exists)
                    {
                        // Save the file.
                        File.WriteAllText(file.FullName, configEditor.Content);
                        file.Refresh();

                        // Deploy changes.
                        success = Deploy(new int[] { id });
                    }
                }
            }
            catch (Exception ex)
            {
                success = false;

                string message = $"Could not save or deploy the config file.\n\nDetails:\n\n{ex.Message}";

                if (ex.StackTrace is not null)
                {
                    message += $"\n\n{ex.StackTrace}";
                }

                MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return success;
        }

        // Copy and overwrite config files from the deployed (live) directory to the local plugin, release, and debug directories.
        public bool Grab(int[] configIds)
        {
            bool success = true;
            FileInfo deployFile;

            try
            {
                foreach (int id in configIds)
                {
                    if (_configFileDictionary.ContainsKey(id))
                    {
                        // We can't grab the file if it has not yet been deployed (i.e., the file does not exist).
                        if (_configFileDictionary[id].IsDeployed())
                        {
                            deployFile = _configFileDictionary[id].DeployFile;

                            if (deployFile is not null && deployFile.Exists)
                            {
                                // Load any changes made to the file.
                                deployFile.Refresh();

                                foreach (FileInfo localFile in _configFileDictionary[id].GetLocalFiles())
                                {
                                    // We don't need to check whether the actual local file exists.
                                    if (localFile is not null && localFile.Directory is not null && localFile.Directory.Exists)
                                    {
                                        // Do not attempt to copy the file to the same path.
                                        if (deployFile.FullName != localFile.FullName)
                                        {
                                            /* Consider File.Replace to store backups. */

                                            // Overwrite the existing config file, if it exists.
                                            File.Copy(deployFile.FullName, localFile.FullName, overwrite: true);
                                            localFile.Refresh();

                                            if (localFile.LastWriteTime != deployFile.LastWriteTime)
                                            {
                                                localFile.LastWriteTime = deployFile.LastWriteTime;
                                                localFile.Refresh();
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

        public ConfigEditorArgs? GetConfigEditor(int configId)
        {
            string path;
            string content;
            ConfigEditorArgs? editor = null;
            FileInfo file;

            try
            {
                if (_configFileDictionary.ContainsKey(configId))
                {
                    file = _configFileDictionary[configId].GetDeployingFile();

                    if (file is not null)
                    {
                        // Load any changes made to the file.
                        file.Refresh();

                        if (file.Exists)
                        {
                            path = file.FullName;
                            content = File.ReadAllText(path);
                            editor = new(configId, path, content);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                editor = null;

                string message = $"Could not retrieve file.\n\nDetails:\n\n{ex.Message}";

                if (ex.StackTrace is not null)
                {
                    message += $"\n\n{ex.StackTrace}";
                }

                MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return editor;
        }

        #endregion
    }
}
