namespace ConfigManager
{
    public class ConfigEditorArgs
    {
        #region Properties

        public int Id { get; }
        public string Path { get; } = string.Empty;
        public string Content => _content;

        #endregion

        #region Fields

        private string _content;

        #endregion

        #region Constructors

        public ConfigEditorArgs(int id, string path, string content)
        {
            Id = id;
            Path = string.IsNullOrWhiteSpace(path) ? string.Empty : path;
            _content = string.IsNullOrWhiteSpace(content) ? string.Empty : content;
        }

        #endregion

        #region Methods

        public void Overwrite(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                _content = string.Empty;
            }
            else
            {
                _content = content;
            }
        }

        #endregion
    }
}
