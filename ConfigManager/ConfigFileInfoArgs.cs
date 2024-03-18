using System.IO;

namespace ConfigManager
{
    public class ConfigFileInfoArgs
    {
        #region Properties

        public FileInfo LocalFile { get; } = null!;
        public FileInfo LiveFile { get; } = null!;

        #endregion

        #region Constructors

        public ConfigFileInfoArgs(FileInfo localFile)
        {
            LocalFile = localFile;
        }

        public ConfigFileInfoArgs(FileInfo localFile, FileInfo liveFile)
        {
            LocalFile = localFile;
            LiveFile = liveFile;
        }

        #endregion
    }
}
