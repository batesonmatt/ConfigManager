using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ConfigManager
{
    public partial class Login : Window
    {
        public Login()
        {
            InitializeComponent();

            userTextBox.Text = GetCurrentUserName();

            if (string.IsNullOrWhiteSpace(userTextBox.Text))
            {
                userTextBox.Focus();
            }
            else
            {
                passwordBox.Focus();
            }
        }

        private static string GetCurrentUserName()
        {
            string username;

            try
            {
                username = Environment.UserName;
            }
            catch
            {
                username = string.Empty;
            }

            return username;
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
