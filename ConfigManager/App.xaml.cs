using System;
using System.Windows;

namespace ConfigManager
{
    public partial class App : Application
    {
        public void ApplicationStart(object sender, StartupEventArgs e)
        {
            try
            {
                // Disable shutdown when the dialog closes
                Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;

                Login loginWindow = new();

                if (loginWindow.ShowDialog() == false)
                {
                    Current.Shutdown(-1);
                }

                if (string.IsNullOrWhiteSpace(ActiveDirectoryUser.Host))
                {
                    MessageBox.Show("Could not get the current host information.", "Error", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    Current.Shutdown(-1);
                }

                MainWindow mainWindow = new();

                // Re-enable normal shutdown mode.
                Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
                Current.MainWindow = mainWindow;
                mainWindow.Show();
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
