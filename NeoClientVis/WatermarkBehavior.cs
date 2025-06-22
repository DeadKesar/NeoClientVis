using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NeoClientVis
{
    public static class WatermarkBehavior
    {
        public static readonly DependencyProperty WatermarkProperty =
            DependencyProperty.RegisterAttached("Watermark", typeof(string), typeof(WatermarkBehavior),
            new PropertyMetadata(string.Empty, OnWatermarkChanged));

        public static string GetWatermark(DependencyObject obj) => (string)obj.GetValue(WatermarkProperty);
        public static void SetWatermark(DependencyObject obj, string value) => obj.SetValue(WatermarkProperty, value);

        private static void OnWatermarkChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TextBox textBox)
            {
                textBox.Loaded += (s, args) => SetWatermarkVisibility(textBox);
                textBox.GotFocus += (s, args) =>
                {
                    if (textBox.Text == GetWatermark(textBox))
                    {
                        textBox.Text = string.Empty;
                        textBox.Foreground = Brushes.Black;
                    }
                };
                textBox.LostFocus += (s, args) => SetWatermarkVisibility(textBox);
            }
        }

        private static void SetWatermarkVisibility(TextBox textBox)
        {
            if (string.IsNullOrEmpty(textBox.Text))
            {
                textBox.Text = GetWatermark(textBox);
                textBox.Foreground = Brushes.Gray;
            }
        }
    }
}