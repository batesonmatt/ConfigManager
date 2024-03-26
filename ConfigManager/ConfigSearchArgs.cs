namespace ConfigManager
{
    public class ConfigSearchArgs
    {
        #region Properties

        public string Search { get; } = string.Empty;
        public FileSearchMethod SearchMethod { get; }
        public PluginType Plugin { get; }
        public DateRangeType DateRange { get; }

        #endregion

        #region Constructors

        public ConfigSearchArgs()
        {
            SearchMethod = FileSearchMethod.None;
            Plugin = PluginType.All;
            DateRange = DateRangeType.AllTime;
        }

        public ConfigSearchArgs(string search, FileSearchMethod searchMethod, PluginType plugin, DateRangeType dateRange)
        {
            Search = string.IsNullOrWhiteSpace(search) ? string.Empty : search.Trim();
            SearchMethod = searchMethod;
            Plugin = plugin;
            DateRange = dateRange;
            
        }

        #endregion
    }
}
