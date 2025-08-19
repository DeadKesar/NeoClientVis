using System;
using System.Windows;
using System.Windows.Controls;
using System.Text.RegularExpressions;
using System.Windows;

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
            string inputName = PropertyNameTextBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(inputName))
            {
                MessageBox.Show("Введите название свойства!");
                return;
            }

            // Санитизация: заменяем недопустимые символы на '_'
            string sanitizedName = Regex.Replace(inputName, @"[^a-zA-Z0-9_\u0400-\u04FF]", "_");

            // Если начинается с цифры, добавляем префикс
            if (!string.IsNullOrEmpty(sanitizedName) && char.IsDigit(sanitizedName[0]))
            {
                sanitizedName = "Prop_" + sanitizedName;
            }

            // Проверка на пустоту после санитизации
            if (string.IsNullOrWhiteSpace(sanitizedName))
            {
                MessageBox.Show("Название свойства после очистки стало пустым. Используйте буквы, цифры или '_'!");
                return;
            }

            // Если имя изменилось, показываем предупреждение
            if (sanitizedName != inputName)
            {
                var result = MessageBox.Show($"Название свойства будет изменено на '{sanitizedName}' для совместимости с БД. Продолжить?", "Предупреждение", MessageBoxButton.YesNo);
                if (result != MessageBoxResult.Yes)
                {
                    return;
                }
            }

            PropertyName = sanitizedName;

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