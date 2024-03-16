using ConfigManager.DataLayer;
using ConfigManager.Security;
using System.Windows;

namespace ConfigManager
{
    public partial class App : Application
    {
        #region Properties

        public static StoreDB StoreDB { get; } = new();
        public static ActiveDirectoryUser ActiveDirectoryUser { get; } = new();

        #endregion
    }
}
