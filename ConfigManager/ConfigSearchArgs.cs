namespace ConfigManager
{
    public class ConfigSearchArgs
    {
        #region Properties

        public string Search { get; } = string.Empty;
        public PluginType Plugin { get; }
        public DateRangeType DateRange { get; }

        #endregion

        #region Constructors

        public ConfigSearchArgs()
        {
            Plugin = PluginType.All;
            DateRange = DateRangeType.AllTime;
        }

        public ConfigSearchArgs(PluginType plugin, DateRangeType dateRange, string search)
        {
            Plugin = plugin;
            DateRange = dateRange;
            Search = string.IsNullOrWhiteSpace(search) ? string.Empty : search;
        }

        #endregion
    }
}
