using System.Windows;

namespace NeoClientVis
{
    public partial class EditConfigWindow : Window
    {
        public Neo4jConfig UpdatedConfig { get; private set; }

        public EditConfigWindow(Neo4jConfig currentConfig)
        {
            InitializeComponent();
            UpdatedConfig = null;

            // Заполняем поля текущими значениями
            UriTextBox.Text = currentConfig.Uri;
            UsernameTextBox.Text = currentConfig.Username;
            PasswordBox.Password = currentConfig.Password;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(UriTextBox.Text) ||
                string.IsNullOrWhiteSpace(UsernameTextBox.Text) ||
                string.IsNullOrWhiteSpace(PasswordBox.Password))
            {
                MessageBox.Show("Все поля обязательны для заполнения!");
                return;
            }

            UpdatedConfig = new Neo4jConfig
            {
                Uri = UriTextBox.Text.Trim(),
                Username = UsernameTextBox.Text.Trim(),
                Password = PasswordBox.Password.Trim()
            };

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}