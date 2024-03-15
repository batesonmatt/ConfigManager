namespace ConfigManager
{
    public class ConfigSearchArgs
    {
        #region Properties

        public PluginType Plugin { get; }

        public DateRangeType DateRange { get; }

        #endregion

        #region Constructors

        public ConfigSearchArgs()
        {
            Plugin = PluginType.All;
            DateRange = DateRangeType.AllTime;
        }

        public ConfigSearchArgs(PluginType plugin, DateRangeType dateRange)
        {
            Plugin = plugin;
            DateRange = dateRange;
        }

        #endregion
    }
}
