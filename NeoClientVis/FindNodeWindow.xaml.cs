using Neo4jClient;
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

namespace NeoClientVis
{
    public partial class FindNodeWindow : Window
    {
        private readonly GraphClient _client;
        private readonly NodeTypeCollection _nodeTypeCollection;
        private NodeData _selectedNode;

        public NodeData SelectedNode => _selectedNode;

        public FindNodeWindow(GraphClient client, NodeTypeCollection nodeTypeCollection)
        {
            InitializeComponent();
            _client = client;
            _nodeTypeCollection = nodeTypeCollection;

            // Инициализация комбобокса типа связи
            RelationshipTypeComboBox.SelectedIndex = 0;

            // Настройка водяного знака для текстового поля
            CustomRelationshipTextBox.GotFocus += (s, e) =>
            {
                if (CustomRelationshipTextBox.Text == "Или введите свой тип...")
                {
                    CustomRelationshipTextBox.Text = "";
                    CustomRelationshipTextBox.Foreground = Brushes.Black;
                }
            };

            CustomRelationshipTextBox.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(CustomRelationshipTextBox.Text))
                {
                    CustomRelationshipTextBox.Text = "Или введите свой тип...";
                    CustomRelationshipTextBox.Foreground = Brushes.Gray;
                }
            };

            CustomRelationshipTextBox.Foreground = Brushes.Gray;

            LoadNodeTypes();
            Loaded += Window_Loaded;
        }

        private void LoadNodeTypes()
        {
            try
            {
                SearchTypeComboBox.ItemsSource = _nodeTypeCollection.NodeTypes
                    .Select(nt => nt.Label.First().Key)
                    .ToList();

                if (SearchTypeComboBox.Items.Count > 0)
                    SearchTypeComboBox.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке типов узлов: {ex.Message}");
            }
        }

        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadNodesForSelectedType(SearchTextBox.Text);
        }


        private void LinkButton_Click(object sender, RoutedEventArgs e)
        {
            if (SearchResultsListBox.SelectedItem is NodeData selectedNode)
            {
                _selectedNode = selectedNode;
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("Выберите документ для связи!");
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Загружаем все объекты при открытии окна
            await LoadNodesForSelectedType();
        }

        private async Task LoadNodesForSelectedType(string searchText = null)
        {
            try
            {
                if (SearchTypeComboBox.SelectedItem == null) return;

                string selectedType = SearchTypeComboBox.SelectedItem.ToString();
                var nodeType = _nodeTypeCollection.NodeTypes
                    .FirstOrDefault(nt => nt.Label.ContainsKey(selectedType));

                if (nodeType == null) return;

                string label = nodeType.Label.Values.First();
                List<NodeData> nodes;

                if (string.IsNullOrWhiteSpace(searchText))
                {
                    nodes = await BDController.LoadNodesByType(_client, label);
                }
                else
                {
                    nodes = await BDController.SearchNodes(_client, label, searchText);
                }

                SearchResultsListBox.ItemsSource = nodes;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке узлов: {ex.Message}");
            }
        }
        public string RelationshipType
        {
            get
            {
                // Если выбран элемент в комбобоксе
                if (RelationshipTypeComboBox.SelectedItem is ComboBoxItem selectedItem)
                    return selectedItem.Content.ToString();

                // Если введен текст в поле
                if (!string.IsNullOrWhiteSpace(CustomRelationshipTextBox.Text))
                    return CustomRelationshipTextBox.Text;

                // Значение по умолчанию
                return "СВЯЗАН_С";
            }
        }
    }
}
