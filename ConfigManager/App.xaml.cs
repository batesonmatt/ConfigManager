using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace ConfigManager
{
    public partial class App : Application
    {
        public void ApplicationStart(object sender, StartupEventArgs e)
        {
            try
            {
                //Disable shutdown when the dialog closes
                Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;

                Login loginWindow = new();

                if (loginWindow.ShowDialog() == true)
                {
                    MainWindow mainWindow = new(/*dialog.Data*/);

                    //Re-enable normal shutdown mode.
                    Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
                    Current.MainWindow = mainWindow;
                    mainWindow.Show();
                }
                else
                {
                    Current.Shutdown(-1);
                }
            }
            catch (Exception ex)
            {
                string message = $"Could not load user data.\n\nDetails:\n\n{ex.Message}";

                if (ex.StackTrace is not null)
                {
                    message += $"\n\n{ex.StackTrace}";
                }

                MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
