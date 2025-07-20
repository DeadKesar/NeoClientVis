
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace NeoClientVis
{
    public partial class AddLinkedNodeWindow : Window
    {
        private readonly NodeTypeCollection _nodeTypeCollection;
        private Dictionary<string, Control> _propertyInputs;
        private Dictionary<string, Type> _currentPropertyTypes;

        public string SelectedType { get; private set; }
        public Dictionary<string, object> Properties { get; private set; }
        public string RelationshipType { get; private set; }

        public AddLinkedNodeWindow(NodeTypeCollection nodeTypeCollection)
        {
            InitializeComponent();
            _nodeTypeCollection = nodeTypeCollection;
            _propertyInputs = new Dictionary<string, Control>();

            // Заполняем комбобокс типов узлов
            NodeTypeComboBox.ItemsSource = _nodeTypeCollection.NodeTypes.Select(nt => nt.Label.First().Key);
            if (NodeTypeComboBox.Items.Count > 0)
            {
                NodeTypeComboBox.SelectedIndex = 0;
            }

            // Устанавливаем тип связи по умолчанию
            RelationshipTypeComboBox.SelectedIndex = 0;
        }

        private void NodeTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (NodeTypeComboBox.SelectedItem is string selectedTypeKey)
            {
                SelectedType = selectedTypeKey;
                var selectedNodeType = _nodeTypeCollection.NodeTypes.FirstOrDefault(nt => nt.Label.ContainsKey(selectedTypeKey));
                if (selectedNodeType != null)
                {
                    _currentPropertyTypes = selectedNodeType.Properties;
                    CreatePropertyInputs(_currentPropertyTypes);
                }
            }
        }

        private void CreatePropertyInputs(Dictionary<string, Type> properties)
        {
            PropertiesPanel.Children.Clear();
            _propertyInputs.Clear();

            foreach (var property in properties)
            {
                var label = new Label { Content = $"{property.Key}:" };
                Control inputControl;

                if (property.Key == "Путь_к_файлу")
                {
                    var textBox = new TextBox { Width = 150, Margin = new Thickness(0, 0, 5, 5) };
                    var button = new Button { Content = "...", Width = 30 };
                    button.Click += (s, ev) =>
                    {
                        var dialog = new Microsoft.Win32.OpenFileDialog { Title = "Выберите файл", Filter = "Все файлы (*.*)|*.*" };
                        if (dialog.ShowDialog() == true)
                        {
                            textBox.Text = dialog.FileName;
                        }
                    };

                    var panel = new StackPanel { Orientation = Orientation.Horizontal };
                    panel.Children.Add(textBox);
                    panel.Children.Add(button);

                    inputControl = textBox;
                    PropertiesPanel.Children.Add(label);
                    PropertiesPanel.Children.Add(panel);
                }
                else if (property.Value == typeof(bool))
                {
                    inputControl = new CheckBox { IsChecked = true, Margin = new Thickness(0, 0, 0, 5) };
                    PropertiesPanel.Children.Add(label);
                    PropertiesPanel.Children.Add(inputControl);
                }
                else
                {
                    inputControl = new TextBox
                    {
                        Width = 200,
                        Margin = new Thickness(0, 0, 0, 5),
                        Text = property.Value == typeof(DateTime) || property.Value == typeof(Neo4j.Driver.LocalDate) ? DateTime.Now.ToString("yyyy-MM-dd") : ""
                    };
                    PropertiesPanel.Children.Add(label);
                    PropertiesPanel.Children.Add(inputControl);
                }

                _propertyInputs[property.Key] = inputControl;
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(SelectedType))
            {
                MessageBox.Show("Выберите тип объекта!");
                return;
            }

            Properties = new Dictionary<string, object>();
            foreach (var kvp in _propertyInputs)
            {
                if (_currentPropertyTypes[kvp.Key] == typeof(DateTime) || _currentPropertyTypes[kvp.Key] == typeof(Neo4j.Driver.LocalDate))
                {
                    var text = (kvp.Value as TextBox)?.Text;
                    if (!string.IsNullOrEmpty(text) && !DateTime.TryParse(text, out _))
                    {
                        MessageBox.Show($"Неверный формат даты для '{kvp.Key}'. Используйте 'yyyy-MM-dd'.");
                        return;
                    }
                    Properties[kvp.Key] = text ?? "";
                }
                else if (_currentPropertyTypes[kvp.Key] == typeof(bool))
                {
                    Properties[kvp.Key] = (kvp.Value as CheckBox)?.IsChecked ?? false;
                }
                else
                {
                    Properties[kvp.Key] = (kvp.Value as TextBox)?.Text ?? "";
                }
            }

            if (RelationshipTypeComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                RelationshipType = selectedItem.Content.ToString();
            }
            else
            {
                MessageBox.Show("Выберите тип связи!");
                return;
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