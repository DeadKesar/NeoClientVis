
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace NeoClientVis
{
    public partial class EditNodeWindow : Window
    {
        private readonly Dictionary<string, Control> _propertyInputs;
        private readonly Dictionary<string, Type> _propertyTypes;

        public Dictionary<string, object> Properties { get; private set; }

        public EditNodeWindow(Dictionary<string, object> currentProperties, Dictionary<string, Type> propertyTypes)
        {
            InitializeComponent();
            _propertyInputs = new Dictionary<string, Control>();
            _propertyTypes = propertyTypes;
            Properties = null;

            foreach (var property in currentProperties)
            {
                var label = new Label { Content = $"{property.Key}:" };
                Control inputControl;

                if (_propertyTypes[property.Key] == typeof(bool))
                {
                    inputControl = new CheckBox
                    {
                        IsChecked = property.Value is bool boolValue && boolValue,
                        Margin = new Thickness(0, 0, 0, 5)
                    };
                }
                else if (_propertyTypes[property.Key] == typeof(Neo4j.Driver.LocalDate))
                {
                    inputControl = new DatePicker
                    {
                        Width = 200,
                        Margin = new Thickness(0, 0, 0, 5)
                    };
                    if (property.Value is string dateStr && DateTime.TryParse(dateStr, out var date))
                    {
                        ((DatePicker)inputControl).SelectedDate = date;
                    }
                }
                else
                {
                    inputControl = new TextBox
                    {
                        Width = 200,
                        Margin = new Thickness(0, 0, 0, 5),
                        Text = property.Value?.ToString() ?? ""
                    };
                }

                _propertyInputs[property.Key] = inputControl;
                PropertiesPanel.Children.Add(label);
                PropertiesPanel.Children.Add(inputControl);
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            Properties = new Dictionary<string, object>();
            foreach (var kvp in _propertyInputs)
            {
                if (_propertyTypes[kvp.Key] == typeof(Neo4j.Driver.LocalDate))
                {
                    var datePicker = kvp.Value as DatePicker;
                    if (datePicker?.SelectedDate.HasValue == true)
                    {
                        Properties[kvp.Key] = datePicker.SelectedDate.Value.ToString("yyyy-MM-dd");
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