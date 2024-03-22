namespace ConfigManager
{
    public class ConfigStatusArgs
    {
        #region Properties

        public int Id { get; }
        public string Status { get; } = string.Empty;
        public string Action { get; } = string.Empty;

        #endregion

        #region Constructors

        public ConfigStatusArgs(int id, string status)
            : this(id, status, string.Empty)
        { }

        public ConfigStatusArgs(int id, string status, string action)
        {
            Id = id;
            Status = string.IsNullOrWhiteSpace(status) ? string.Empty : status;
            Action = string.IsNullOrWhiteSpace(action) ? string.Empty : action;
        }

        #endregion
    }
}
