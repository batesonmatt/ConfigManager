namespace ConfigManager
{
    public class ConfigStatusArgs
    {
        #region Properties

        public int Id { get; }
        public string Status { get; } = string.Empty;

        #endregion

        #region Constructors

        public ConfigStatusArgs(int id, string status)
        {
            Id = id;
            Status = string.IsNullOrWhiteSpace(status) ? string.Empty : status;
        }

        #endregion
    }
}
