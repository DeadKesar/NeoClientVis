using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace NeoClientVis
{
    public partial class EditNodeWindow : Window
    {
        private readonly Dictionary<string, TextBox> _propertyInputs;

        public Dictionary<string, string> Properties { get; private set; }

        public EditNodeWindow(Dictionary<string, object> currentProperties)
        {
            InitializeComponent();
            _propertyInputs = new Dictionary<string, TextBox>();
            Properties = null;

            foreach (var property in currentProperties)
            {
                var label = new Label { Content = $"{property.Key}:" };
                var textBox = new TextBox
                {
                    Width = 200,
                    Margin = new Thickness(0, 0, 0, 5),
                    Text = property.Value?.ToString() ?? ""
                };

                _propertyInputs[property.Key] = textBox;
                PropertiesPanel.Children.Add(label);
                PropertiesPanel.Children.Add(textBox);
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            Properties = new Dictionary<string, string>();
            foreach (var kvp in _propertyInputs)
            {
                Properties[kvp.Key] = kvp.Value.Text ?? "";
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