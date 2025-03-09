using System;
using System.Windows;
using System.Windows.Controls;

namespace NeoClientVis
{
    public partial class AddPropertyWindow : Window
    {
        public string PropertyName { get; private set; }
        public Type PropertyType { get; private set; }

        public AddPropertyWindow()
        {
            InitializeComponent();
            PropertyTypeComboBox.SelectedIndex = 0; // По умолчанию "Строка"
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            PropertyName = PropertyNameTextBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(PropertyName))
            {
                MessageBox.Show("Введите название свойства!");
                return;
            }

            var selectedItem = PropertyTypeComboBox.SelectedItem as ComboBoxItem;
            PropertyType = Type.GetType(selectedItem.Tag.ToString());
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