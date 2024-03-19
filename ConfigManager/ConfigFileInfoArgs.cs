using System.IO;

namespace ConfigManager
{
    public class ConfigFileInfoArgs
    {
        #region Properties

        public string Plugin { get; } = string.Empty;
        public FileInfo LocalFile { get; } = null!;
        public FileInfo DeployFile { get; } = null!;

        #endregion

        #region Constructors

        public ConfigFileInfoArgs(string plugin, FileInfo localFile)
        {
            Plugin = string.IsNullOrWhiteSpace(plugin) ? string.Empty : plugin;
            LocalFile = localFile;
        }

        public ConfigFileInfoArgs(string plugin, FileInfo localFile, FileInfo deployFile)
        {
            Plugin = string.IsNullOrWhiteSpace(plugin) ? string.Empty : plugin;
            LocalFile = localFile;
            DeployFile = deployFile;
        }

        #endregion

        #region Methods

        public bool IsDeployed()
        {
            return DeployFile is not null && DeployFile.Exists;
        }

        #endregion
    }
}
