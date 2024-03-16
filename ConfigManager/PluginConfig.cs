namespace ConfigManager
{
    public class PluginConfig
    {
        #region Properties

        public string PluginName { get; }
        public string ConfigFile {  get; }

        #endregion

        #region Constructors

        public PluginConfig(string pluginName, string configFile)
        {
            PluginName = string.IsNullOrWhiteSpace(pluginName) ? string.Empty : pluginName;
            ConfigFile = string.IsNullOrWhiteSpace(configFile) ? string.Empty : configFile;
        }

        #endregion
    }
}
