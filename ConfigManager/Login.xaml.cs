using System.Windows;

namespace ConfigManager
{
    public partial class Login : Window
    {
        public Login()
        {
            InitializeComponent();

            userTextBox.Text = App.ActiveDirectoryUser.Name;

            if (string.IsNullOrWhiteSpace(userTextBox.Text))
            {
                userTextBox.Focus();
            }
            else
            {
                passwordBox.Focus();
            }
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            okButton.IsEnabled = false;
            cancelButton.IsEnabled = false;
            userTextBox.IsEnabled = false;
            passwordBox.IsEnabled = false;

            if (App.StoreDB.ValidateOperator(userTextBox.Text, passwordBox.Password))
            {
                if (App.ActiveDirectoryUser.IsAdmin())
                {
                    DialogResult = true;
                }
            }

            if (DialogResult is null)
            {
                MessageBox.Show("Access Denied", "Access Denied", MessageBoxButton.OK, MessageBoxImage.Exclamation);

                okButton.IsEnabled = true;
                cancelButton.IsEnabled = true;
                userTextBox.IsEnabled = true;
                passwordBox.IsEnabled = true;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
