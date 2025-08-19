using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
namespace NeoClientVis
{
    public partial class AddNodeWindow : Window
    {
        private readonly Dictionary<string, Control> _propertyInputs;
        private readonly Dictionary<string, Type> _propertyTypes;
        public Dictionary<string, object> Properties { get; private set; }
        public AddNodeWindow(Dictionary<string, Type> propertiesTemplate)
        {
            InitializeComponent();
            _propertyInputs = new Dictionary<string, Control>();
            _propertyTypes = propertiesTemplate;
            Properties = null;
            foreach (var property in propertiesTemplate)
            {
                var label = new Label { Content = $"{property.Key}:" };
                Control inputControl;
                if (property.Key == "Путь_к_файлу")
                {
                    // Создаём TextBox для пути к файлу
                    var textBox = new TextBox
                    {
                        Width = 150,
                        Margin = new Thickness(0, 0, 5, 5)
                    };
                    // Создаём кнопку для вызова проводника
                    var button = new Button
                    {
                        Content = "...",
                        Width = 30
                    };
                    // Обработчик события нажатия кнопки
                    button.Click += (s, e) =>
                    {
                        var dialog = new Microsoft.Win32.OpenFileDialog
                        {
                            Title = "Выберите файл",
                            Filter = "Все файлы (*.*)|*.*" // Можно настроить фильтр под нужные типы файлов
                        };
                        if (dialog.ShowDialog() == true)
                        {
                            textBox.Text = dialog.FileName;
                        }
                    };
                    // Помещаем TextBox и кнопку в горизонтальный StackPanel
                    var panel = new StackPanel
                    {
                        Orientation = Orientation.Horizontal
                    };
                    panel.Children.Add(textBox);
                    panel.Children.Add(button);
                    // Сохраняем TextBox как основной элемент управления для получения значения
                    inputControl = textBox;
                    // Добавляем метку и панель в интерфейс
                    PropertiesPanel.Children.Add(label);
                    PropertiesPanel.Children.Add(panel);
                }
                else if (property.Value == typeof(bool))
                {
                    inputControl = new CheckBox
                    {
                        IsChecked = true, // Значение по умолчанию
                        Margin = new Thickness(0, 0, 0, 5)
                    };
                    PropertiesPanel.Children.Add(label);
                    PropertiesPanel.Children.Add(inputControl);
                }
                else if (property.Value == typeof(Neo4j.Driver.LocalDate) || property.Value == typeof(DateTime)) // Добавлена проверка на DateTime
                {
                    inputControl = new DatePicker
                    {
                        Width = 200,
                        Margin = new Thickness(0, 0, 0, 5),
                        SelectedDate = DateTime.Now // Значение по умолчанию - текущая дата
                    };
                    PropertiesPanel.Children.Add(label);
                    PropertiesPanel.Children.Add(inputControl);
                }
                else
                {
                    inputControl = new TextBox
                    {
                        Width = 200,
                        Margin = new Thickness(0, 0, 0, 5)
                    };
                    PropertiesPanel.Children.Add(label);
                    PropertiesPanel.Children.Add(inputControl);
                }
                _propertyInputs[property.Key] = inputControl;
            }
        }
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            Properties = new Dictionary<string, object>();
            foreach (var kvp in _propertyInputs)
            {
                if (_propertyTypes[kvp.Key] == typeof(Neo4j.Driver.LocalDate) || _propertyTypes[kvp.Key] == typeof(DateTime)) // Добавлена проверка на DateTime
                {
                    var datePicker = kvp.Value as DatePicker;
                    if (datePicker?.SelectedDate.HasValue == true)
                    {
                        Properties[kvp.Key] = datePicker.SelectedDate.Value.ToString("yyyy-MM-dd"); // Преобразование в строку для БД
                    }
                    else
                    {
                        MessageBox.Show($"Пожалуйста, выберите дату для '{kvp.Key}'.");
                        return;
                    }
                }
                else if (_propertyTypes[kvp.Key] == typeof(bool))
                {
                    Properties[kvp.Key] = (kvp.Value as CheckBox)?.IsChecked ?? false;
                }
                else
                {
                    Properties[kvp.Key] = (kvp.Value as TextBox)?.Text ?? "";
                }
            }
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