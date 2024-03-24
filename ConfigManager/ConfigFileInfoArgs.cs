using System.Collections.Generic;
using System.IO;

namespace ConfigManager
{
    public class ConfigFileInfoArgs
    {
        #region Properties

        public ConfigDeployAction DeployAction { get; }
        public FileInfo PluginFile { get; } = null!;
        public FileInfo ReleaseFile { get; } = null!;
        public FileInfo DebugFile { get; } = null!;
        public FileInfo DeployFile { get; } = null!;

        #endregion

        #region Constructors

        public ConfigFileInfoArgs(
            ConfigDeployAction deployAction, FileInfo pluginFile, FileInfo releaseFile, FileInfo debugFile, FileInfo deployFile)
        {
            DeployAction = deployAction;
            PluginFile = pluginFile;
            ReleaseFile = releaseFile;
            DebugFile = debugFile;
            DeployFile = deployFile;
        }

        #endregion

        #region Methods

        public bool IsDeployed()
        {
            return DeployFile is not null && DeployFile.Exists;
        }

        public FileInfo GetDeployingFile()
        {
            return DeployAction switch
            {
                ConfigDeployAction.Plugin => PluginFile,
                ConfigDeployAction.Release => ReleaseFile,
                ConfigDeployAction.Debug => DebugFile,
                _ => PluginFile
            };
        }

        public IEnumerable<FileInfo> GetReplacingFiles()
        {
            if (DeployAction != ConfigDeployAction.Plugin)
            {
                yield return PluginFile;
            }

            if (DeployAction != ConfigDeployAction.Release)
            {
                yield return ReleaseFile;
            }

            if (DeployAction != ConfigDeployAction.Debug)
            {
                yield return DebugFile;
            }

            yield return DeployFile;
        }

        public IEnumerable<FileInfo> GetLocalFiles()
        {
            yield return PluginFile;
            yield return ReleaseFile;
            yield return DebugFile;
        }

        #endregion
    }
}
