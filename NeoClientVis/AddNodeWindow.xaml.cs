using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace NeoClientVis
{
    public partial class AddNodeWindow : Window
    {
        private readonly Dictionary<string, TextBox> _propertyInputs;

        public Dictionary<string, string> Properties { get; private set; }

        public AddNodeWindow(Dictionary<string, string> propertiesTemplate)
        {
            InitializeComponent();
            _propertyInputs = new Dictionary<string, TextBox>();
            Properties = null;

            foreach (var property in propertiesTemplate.Keys)
            {
                var label = new Label { Content = $"{property}:" };
                var textBox = new TextBox { Width = 200, Margin = new Thickness(0, 0, 0, 5) };

                _propertyInputs[property] = textBox;
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