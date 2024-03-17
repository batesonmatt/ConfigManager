using System.Windows;

namespace ConfigManager
{
    public partial class Login : Window
    {
        public Login()
        {
            InitializeComponent();

            userTextBox.Text = App.ActiveDirectoryUser.GetFullDomainUserName();
            passwordBox.Focus();
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            okButton.IsEnabled = false;
            cancelButton.IsEnabled = false;
            //userTextBox.IsEnabled = false;
            passwordBox.IsEnabled = false;

            if (App.ActiveDirectoryUser.IsAdmin())
            {
                if (App.StoreDB.ValidateOperator(App.ActiveDirectoryUser.Name, passwordBox.Password))
                {
                    DialogResult = true;
                }
                else
                {
                    MessageBox.Show("Invalid credentials", "Access Denied", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                }
            }
            else
            {
                MessageBox.Show("Invalid domain user", "Access Denied", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            }

            if (DialogResult is null)
            {
                okButton.IsEnabled = true;
                cancelButton.IsEnabled = true;
                //userTextBox.IsEnabled = true;
                passwordBox.IsEnabled = true;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
